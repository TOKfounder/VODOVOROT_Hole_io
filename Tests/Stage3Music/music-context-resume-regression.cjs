'use strict';
// Device-free regression for Unity WebGL's pending-channel resume race.
// Executes the project's installed Unity Audio.js initialization + the real gate to
// obtain event orders, then compiles/runs the actual player suspension/volume
// methods against controllable AudioSource stubs. No Unity launch or playback.
// Run: node Tests/Stage3Music/music-context-resume-regression.cjs
const assert = require('node:assert/strict');
const fs = require('node:fs'), path = require('node:path'), os = require('node:os');
const vm = require('node:vm'), cp = require('node:child_process');
const project = path.resolve(__dirname, '../..');
const player = fs.readFileSync(path.join(project, 'Assets/Scripts/Stage3/BackgroundMusicPlayer.cs'), 'utf8');
const gate = fs.readFileSync(path.join(project, 'Assets/Plugins/WebGL/Stage3MusicGate.jslib'), 'utf8');
const projectVersion = fs.readFileSync(path.resolve(__dirname, '../../ProjectSettings/ProjectVersion.txt'), 'utf8')
  .match(/^m_EditorVersion: (.+)$/m)[1];
const nativePath = process.env.VODOVOROT_UNITY_AUDIO_JS || process.env.UNITY2022_AUDIO_JS ||
  `/Applications/Unity/Hub/Editor/${projectVersion}/PlaybackEngines/WebGLSupport/BuildTools/lib/Audio.js`;
const native = fs.readFileSync(nativePath, 'utf8');
const initMarker = 'JS_Sound_Init: ';
const initStart = native.indexOf(initMarker), initEnd = native.indexOf('\nJS_Sound_ReleaseInstance__proxy', initStart);
assert.ok(initStart >= 0 && initEnd > initStart, 'Installed Unity Audio.js initialization must be found');
const actualInit = native.slice(initStart + initMarker.length, initEnd).trim().replace(/,$/, '');

function nativeTrace(reverseOrder) {
  const events = [], contextListeners = [];
  let nativePlaying = true;
  class Context {
    constructor() { this.state = 'running'; this.currentTime = 0; }
    addEventListener(type, callback) { assert.equal(type, 'statechange'); contextListeners.push(callback); }
    removeEventListener() {}
    resume() { this.change('running'); return Promise.resolve(); }
    change(state) {
      this.state = state;
      const nativeNotify = () => this.onstatechange();
      const gateNotify = () => contextListeners.forEach(callback => callback());
      if (reverseOrder) { gateNotify(); nativeNotify(); }
      else { nativeNotify(); gateNotify(); }
    }
  }
  const sandbox = {
    window: { AudioContext: Context, addEventListener() {}, removeEventListener() {} },
    AudioContext: Context, document: { hidden: false, hasFocus: () => true, addEventListener() {}, removeEventListener() {} },
    Module: { setInterval: () => 1, clearInterval() {}, deinitializers: [] },
    _GetFakemodTimeInSeconds: () => 1, console: { log() {}, warn() {} }, alert() {}, Audio: class {},
    WEBAudio: { audioInstances: {}, audioCache: [], soundsPendingContextResume: [] },
    LibraryManager: { library: {} }, mergeInto: (target, value) => Object.assign(target, value), UTF8ToString: value => value,
    SendMessage(receiver, method, state) {
      assert.equal(receiver, 'Stage3BackgroundMusic'); assert.equal(method, 'OnBrowserAudioState');
      events.push('gate:' + state);
    }
  };
  vm.createContext(sandbox); vm.runInContext('(' + actualInit + ')()', sandbox);
  sandbox.WEBAudio.audioInstances[1] = {
    source: { mediaElement: {}, playbackPausedAtPosition: 0 },
    isPaused: () => !nativePlaying,
    pause() { nativePlaying = false; events.push('native.pause'); },
    resume() { nativePlaying = true; events.push('native.resume'); }
  };
  vm.runInContext(gate, sandbox);
  sandbox.VodovorotMusicGate = sandbox.LibraryManager.library.$VodovorotMusicGate;
  sandbox.VodovorotMusicGate.authorized = true;
  sandbox.VodovorotMusicGate.receiver = 'Stage3BackgroundMusic';
  assert.equal(sandbox.VodovorotMusicGate.state(), 15);
  sandbox.WEBAudio.audioContext.change('suspended');
  events.push('reason'); // An ad/pause/focus reason appears while context is suspended.
  sandbox.WEBAudio.audioContext.change('running');
  assert.ok(events.includes('native.resume'), 'Actual Unity initialization must reproduce its automatic pending-channel resume');
  assert.equal(nativePlaying, true);
  return events.join(',');
}

const traces = [nativeTrace(false), nativeTrace(true)];
assert.ok(traces[0].indexOf('native.resume') < traces[0].indexOf('gate:15'));
assert.ok(traces[1].indexOf('gate:15') < traces[1].indexOf('native.resume'));

function method(name) {
  const escaped = name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const value = player.match(new RegExp('(?:public|private) (?:static )?(?:void|float) ' + escaped + '\\([^)]*\\)\\s*\\{[\\s\\S]*?\\n        \\}'))?.[0];
  assert.ok(value, 'Actual player method missing: ' + name);
  return value;
}
const predicate = player.match(/private bool MustSuspend\(\) =>[\s\S]*?;/)?.[0];
const safeVolume = player.match(/private static float SafeVolume\([^;]+;/)?.[0];
const savedVolume = player.match(/private static float SavedMusicVolume =>[^;]+;/)?.[0];
assert.ok(predicate && safeVolume && savedVolume, 'Actual suspension and saved-volume predicates must exist');
assert.ok(!predicate.includes('Time.timeScale'), 'Results timeScale=0 must not be treated as a platform audio pause');

const sdkLines = cp.spawnSync('dotnet', ['--list-sdks'], { encoding: 'utf8' });
if (sdkLines.status !== 0) throw Error('A local .NET SDK is required; no installation is performed');
const sdk = sdkLines.stdout.trim().split('\n').at(-1).match(/^([^ ]+) \[([^\]]+)\]/);
assert.ok(sdk, 'No local .NET SDK');
const dotnetRoot = path.dirname(sdk[2]);
const versions = directory => fs.readdirSync(directory).sort((a, b) => b.localeCompare(a, undefined, { numeric: true }));
const packs = path.join(dotnetRoot, 'packs/Microsoft.NETCore.App.Ref');
const referenceBase = path.join(packs, versions(packs)[0], 'ref'), tfm = versions(referenceBase)[0];
const references = fs.readdirSync(path.join(referenceBase, tfm)).filter(file => file.endsWith('.dll'))
  .map(file => '-r:' + path.join(referenceBase, tfm, file));
const temporary = fs.mkdtempSync(path.join(os.tmpdir(), 'vodovorot-music-context-race-'));
function run(args) {
  const result = cp.spawnSync('dotnet', args, { encoding: 'utf8' });
  if (result.stdout) process.stdout.write(result.stdout);
  if (result.stderr) process.stderr.write(result.stderr);
  if (result.status !== 0) throw Error('Music context-resume regression failed: ' + result.status);
}
try {
  const input = path.join(temporary, 'ActualMusicSuspension.cs'), output = path.join(temporary, 'context-race.dll');
  fs.writeFileSync(input, `using System;
using UnityEngine;
using YG;
namespace UnityEngine {
 public static class AudioListener { public static bool pause; }
 public static class Time { public static float timeScale; }
 public static class Mathf {
  public static float Clamp01(float value) => Math.Clamp(value,0,1);
  public static float Max(float first,float second) => Math.Max(first,second);
  public static float Log10(float value) => (float)Math.Log10(value);
  public static bool Approximately(float first,float second) => Math.Abs(first-second)<.000001f;
 }
 public sealed class AudioSource {
  public object clip = new object(); public bool isPlaying=true, mute; public float volume;
  public int starts, pauses, resumes;
  public void Pause() { pauses++; isPlaying=false; }
  public void UnPause() { resumes++; isPlaying=true; }
  public void Play() { starts++; isPlaying=true; }
 }
 public sealed class AudioMixer { public void SetFloat(string name,float value) {} }
}
namespace YG {
 public static class YG2 {
  public sealed class Save { public float musicValue=.5f; } public static Save saves=new Save();
  public static bool isSDKEnabled=true, isFocusWindowGame=true, nowAdsShow, isPauseGame;
 }
}
public sealed class ActualMusicSuspension {
 private const float NominalGain=.28f;
 private readonly AudioSource[] voices={new AudioSource(),new AudioSource()};
 private readonly bool[] started={true,true}; private readonly float[] levels={1,.5f};
 private bool suspended, focused=true, applicationPaused, platformFocused=true, adOpen;
 private int browserState=15; private float duck=1, lastAppliedSetting=float.NaN;
 private object musicGroup; private AudioMixer settingsMixer;
 private static int checks;
 ${predicate}
 ${safeVolume}
 ${savedVolume}
 ${method('StartVoice')}
 ${method('SourceGain')}
 ${method('UpdateSuspension')}
 ${method('ApplyVolumes')}
 private static void Check(bool pass,string message) { checks++; if(!pass) throw new Exception(message); }
 private void SetReason(string reason,bool active) {
  switch(reason) {
   case "adOpen": adOpen=active; break;
   case "nowAdsShow": YG2.nowAdsShow=active; break;
   case "sdkPause": YG2.isPauseGame=active; break;
   case "appFocus": focused=!active; break;
   case "appPause": applicationPaused=active; break;
   case "platformFocus": platformFocused=!active; break;
   case "sdkFocus": YG2.isFocusWindowGame=!active; break;
   case "sdkNotReady": YG2.isSDKEnabled=!active; break;
   case "listenerPause": AudioListener.pause=active; break;
   default: throw new Exception("Unknown fixture reason: "+reason);
  }
 }
 private static void ResetPlatform() {
  YG2.isSDKEnabled=YG2.isFocusWindowGame=true;
  YG2.nowAdsShow=YG2.isPauseGame=AudioListener.pause=false;
  YG2.saves.musicValue=.5f; Time.timeScale=0;
 }
 private void CheckSilent(string context) {
  Check(suspended,context+": logical suspension must remain true");
  foreach(var voice in voices) Check(voice.mute,context+": physical mute must survive even a late native resume");
 }
 private static void Scenario(string trace,string reason) {
  ResetPlatform(); var fixture=new ActualMusicSuspension();
  fixture.ApplyVolumes();
  Check(!fixture.MustSuspend(),"Results timeScale0 should allow music when platform is ready");
  foreach(string step in trace.Split(',')) {
   if(step=="native.pause") foreach(var voice in fixture.voices) voice.isPlaying=false;
   else if(step=="native.resume") {
    foreach(var voice in fixture.voices) voice.isPlaying=true;
    fixture.CheckSilent(reason+" native autoresume, before another frame");
   } else if(step=="reason") fixture.SetReason(reason,true);
   else if(step.StartsWith("gate:")) {
    fixture.browserState=int.Parse(step.Substring(5)); fixture.UpdateSuspension();
    fixture.CheckSilent(reason+" gate notification");
   } else throw new Exception("Unknown actual Unity/gate trace event: "+step);
  }
  fixture.ApplyVolumes(); fixture.CheckSilent(reason+" ApplyVolumes must preserve mute");
  fixture.UpdateSuspension();
  foreach(var voice in fixture.voices) Check(!voice.isPlaying,reason+": next Update must reassert physical Pause");
  fixture.SetReason(reason,false); fixture.UpdateSuspension(); fixture.ApplyVolumes();
  Check(!fixture.suspended,reason+": clearing the final reason must release suspension");
  foreach(var voice in fixture.voices) {
   Check(voice.isPlaying&&!voice.mute,reason+": both existing crossfade voices must resume audibly");
   Check(voice.starts==0&&voice.resumes==1,reason+": resume must preserve existing voices without restarting clips");
  }
  fixture.musicGroup=new object(); fixture.settingsMixer=new AudioMixer(); fixture.ApplyVolumes();
  Check(Math.Abs(fixture.voices[0].volume-.28f)<.00001f,"Routed volume must apply saved gain only through mixer");
  fixture.musicGroup=null; fixture.ApplyVolumes();
  Check(Math.Abs(fixture.voices[0].volume-.14f)<.00001f,"Unrouted volume must apply saved gain exactly once");
  YG2.saves.musicValue=0; fixture.ApplyVolumes();
  foreach(var voice in fixture.voices) Check(voice.mute&&voice.volume==0,"Saved zero must stay physically silent");
 }
 public static void Main(string[] traces) {
  string[] reasons={"adOpen","nowAdsShow","sdkPause","appFocus","appPause","platformFocus","sdkFocus","sdkNotReady","listenerPause"};
  foreach(string trace in traces) foreach(string reason in reasons) Scenario(trace,reason);
  Console.WriteLine("PASS: "+checks+" actual player assertions, 9 suspension reasons x 2 native/gate event orders; max2 existing voices, mute hold, re-Pause, safe resume, timeScale0 and single-volume paths");
 }
}`);
  run([path.join(sdk[2], sdk[1], 'Roslyn/bincore/csc.dll'), '-nologo', '-nostdlib+', '-langversion:9.0', '-target:exe',
    '-out:' + output, ...references, input]);
  const runtime = versions(path.join(dotnetRoot, 'shared/Microsoft.NETCore.App'))[0];
  fs.writeFileSync(path.join(temporary, 'context-race.runtimeconfig.json'), JSON.stringify({ runtimeOptions: {
    tfm, framework: { name: 'Microsoft.NETCore.App', version: runtime }
  }}));
  console.log('Real Unity/gate traces: ' + JSON.stringify(traces));
  run([output, ...traces]);
} finally { fs.rmSync(temporary, { recursive: true, force: true }); }

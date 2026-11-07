// Exercises the actual Stop method with the Editor's pending-Play state. Does not launch Unity.
const fs = require('fs'), path = require('path'), os = require('os'), cp = require('child_process');
const project = path.resolve(__dirname, '../..');
const source = fs.readFileSync(path.join(project, 'Assets/Scripts/Stage3/RankSimulationContext.cs'), 'utf8');
const actualStop = source.match(/public static void Stop\(\)\s*\{[\s\S]*?\n        \}/)?.[0];
if (!actualStop) throw Error('Actual simulation Stop method not found');
const actualCleanup = source.match(/private static void ClearIdleTransitionGuard\(\)\s*\{[\s\S]*?\n        \}/)?.[0];
if (!actualCleanup) throw Error('Actual idle guard cleanup method not found');
const sdks = cp.spawnSync('dotnet', ['--list-sdks'], { encoding: 'utf8' }).stdout.trim().split('\n');
const sdk = sdks.at(-1).match(/^([^ ]+) \[([^\]]+)\]/);
if (!sdk) throw Error('No .NET SDK');
const versions = p => fs.readdirSync(p).sort((a, b) => b.localeCompare(a, undefined, { numeric: true }));
const root = path.dirname(sdk[2]), pack = path.join(root, 'packs/Microsoft.NETCore.App.Ref');
const refBase = path.join(pack, versions(pack)[0], 'ref'), tfm = versions(refBase)[0];
const refs = fs.readdirSync(path.join(refBase, tfm)).filter(p => p.endsWith('.dll')).map(p => '-r:' + path.join(refBase, tfm, p));
const out = fs.mkdtempSync(path.join(os.tmpdir(), 'vodovorot-stop-transition-'));
function run(args) {
  const result = cp.spawnSync('dotnet', args, { encoding: 'utf8' });
  if (result.stdout) process.stdout.write(result.stdout);
  if (result.stderr) process.stderr.write(result.stderr);
  if (result.status !== 0) throw Error('Stop transition smoke failed: ' + result.status);
}
try {
  const input = path.join(out, 'StopTransition.cs'), output = path.join(out, 'stop-transition.dll');
  fs.writeFileSync(input, `using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace UnityEngine { public static class Application { public static bool isPlaying; } }
namespace UnityEditor {
 public static class EditorApplication {
  public static bool pending, cancelRequested;
  public static bool isPlayingOrWillChangePlaymode => Application.isPlaying || pending;
  public static bool isPlaying { set { if (!value) { pending = false; cancelRequested = true; } } }
 }
 public static class SessionState {
  public static readonly Dictionary<string,bool> Values = new Dictionary<string,bool>();
  public static bool GetBool(string key, bool fallback) => Values.TryGetValue(key, out bool value) ? value : fallback;
  public static void SetBool(string key, bool value) { Values[key] = value; }
 }
}
public static class RankSimulationContext {
 private const string OwnsPlayKey = "own", GuardKey = "guard", ArmedKey = "armed";
 private static bool Active;
 private static bool cityPending, cityRunning;
 private static int restoreCount;
 private static void Restore(bool clearQueue) {
  if (!clearQueue) throw new Exception("Stop must clear the queued simulation");
  restoreCount++; SessionState.SetBool("armed", false);
 }
 ${actualStop}
 ${actualCleanup}
 private static void Setup(bool playing, bool pending, bool own, bool city) {
  Application.isPlaying = playing; EditorApplication.pending = pending;
  EditorApplication.cancelRequested = false; restoreCount = 0;
  cityPending = city; cityRunning = false; Active = false; SessionState.Values.Clear();
  SessionState.SetBool(OwnsPlayKey, own); SessionState.SetBool("armed", true);
 }
 private static void Check(bool passed, string message) { if (!passed) throw new Exception(message); }
 public static void Main() {
  Setup(false, true, true, false); Stop();
  Check(EditorApplication.cancelRequested && !EditorApplication.pending, "Stop must cancel queued auto-entry before Application.isPlaying becomes true");
  Check(SessionState.GetBool(GuardKey, false), "The save guard must survive cancellation until Edit Mode teardown");
  Check(restoreCount == 1 && !SessionState.GetBool("armed", true), "Cancelled entry must restore once and clear the queued action");
  ClearIdleTransitionGuard();
  Check(!SessionState.GetBool(GuardKey, true) && !SessionState.GetBool(OwnsPlayKey, true), "Idle cleanup must release both transition flags even without EnteredEditMode");
  Setup(true, false, true, false); Stop();
  Check(EditorApplication.cancelRequested && SessionState.GetBool(GuardKey, false), "An owned running Play session must still stop under the guard");
  ClearIdleTransitionGuard();
  Check(SessionState.GetBool(GuardKey, false), "A teardown with Application.isPlaying still true must keep its guard");
  Application.isPlaying = false; ClearIdleTransitionGuard();
  Check(!SessionState.GetBool(GuardKey, true), "The guard may release only after Play teardown becomes idle");
  Setup(true, false, false, true); Stop();
  Check(EditorApplication.cancelRequested && SessionState.GetBool(GuardKey, false), "A short City run must still stop under the guard");
  Setup(true, false, false, false); Stop();
  Check(!EditorApplication.cancelRequested && restoreCount == 1, "An immediate test inside existing user-owned Play must restore without cancelling that Play session");
  Setup(false, true, true, false); SessionState.SetBool(GuardKey, true); ClearIdleTransitionGuard();
  Check(SessionState.GetBool(GuardKey, false) && SessionState.GetBool(OwnsPlayKey, false), "Pending entry must keep both transition flags");
  EditorApplication.pending = false; ClearIdleTransitionGuard();
  Check(SessionState.GetBool(GuardKey, false), "An armed request must retain its guard even while temporarily idle during reload");
  SessionState.SetBool(ArmedKey, false); Active = true; ClearIdleTransitionGuard();
  Check(SessionState.GetBool(GuardKey, false), "An active profile must retain the guard while Editor flags are transiently idle");
  Active = false; ClearIdleTransitionGuard();
  Check(!SessionState.GetBool(GuardKey, true) && !SessionState.GetBool(OwnsPlayKey, true), "A completed reload/stop with no pending request or profile must release both flags");
  Console.WriteLine("PASS: 13 actual Stop pending-entry/cancellation/idle-cleanup/teardown/reload-guard contracts");
 }
}`);
  run([path.join(sdk[2], sdk[1], 'Roslyn/bincore/csc.dll'), '-nologo', '-nostdlib+', '-langversion:9.0', '-target:exe',
    '-out:' + output, ...refs, input]);
  const runtime = versions(path.join(root, 'shared/Microsoft.NETCore.App'))[0];
  fs.writeFileSync(path.join(out, 'stop-transition.runtimeconfig.json'), JSON.stringify({ runtimeOptions: {
    tfm, framework: { name: 'Microsoft.NETCore.App', version: runtime }
  }}));
  run([output]);
} finally { fs.rmSync(out, { recursive: true, force: true }); }

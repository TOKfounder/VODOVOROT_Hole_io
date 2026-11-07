'use strict';
// Type-check the real player against the project Unity API assemblies without launching Unity.
const fs = require('fs'), path = require('path'), os = require('os'), cp = require('child_process');
const sdk = cp.spawnSync('dotnet', ['--list-sdks'], { encoding: 'utf8' }).stdout.trim().split('\n').at(-1).match(/^([^ ]+) \[([^\]]+)\]/);
if (!sdk) throw Error('No .NET SDK');
const root = path.dirname(sdk[2]), versions = p => fs.readdirSync(p).sort((a, b) => b.localeCompare(a, undefined, { numeric: true }));
const pack = path.join(root, 'packs/Microsoft.NETCore.App.Ref'), refBase = path.join(pack, versions(pack)[0], 'ref');
const tfm = versions(refBase)[0], refs = fs.readdirSync(path.join(refBase, tfm)).filter(p => p.endsWith('.dll')).map(p => '-r:' + path.join(refBase, tfm, p));
const projectVersion = fs.readFileSync(path.resolve(__dirname, '../../ProjectSettings/ProjectVersion.txt'), 'utf8').match(/^m_EditorVersion: (.+)$/m)[1];
const editor = process.env.VODOVOROT_UNITY_EDITOR || `/Applications/Unity/Hub/Editor/${projectVersion}/Unity.app/Contents`;
const unity = ['Resources/Scripting/Managed/UnityEngine', 'Managed/UnityEngine']
  .map(relative => path.join(editor, relative)).find(candidate => fs.existsSync(path.join(candidate, 'UnityEngine.CoreModule.dll')));
if (!unity) throw Error('Unity API assemblies not found for ' + projectVersion);
const engine = ['UnityEngine.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.AudioModule.dll', 'UnityEngine.ScriptingModule.dll']
  .filter(file => fs.existsSync(path.join(unity, file))).map(file => '-r:' + path.join(unity, file));
const out = fs.mkdtempSync(path.join(os.tmpdir(), 'vodovorot-music-compile-'));
try {
  const stubs = path.join(out, 'ProjectReferences.cs');
  fs.writeFileSync(stubs, `using System; using UnityEngine; using UnityEngine.Audio;
public class AudioManager : MonoBehaviour { public AudioSource musicSource; public AudioMixer mixer; }
public class VodovorotGameManager : MonoBehaviour { public static VodovorotGameManager Instance; public AudioManager AudioManager; }
namespace YG { public static class YG2 {
public class Save { public float musicValue=.5f; } public static Save saves = new Save();
public static bool nowAdsShow, isPauseGame, isFocusWindowGame=true, isSDKEnabled=true;
public static Action onGetSDKData, onOpenAnyAdv, onCloseAnyAdv, onErrorAnyAdv;
public static Action<bool> onPauseGame, onFocusWindowGame;
} }
namespace Vodovorot.Stage3 { public static class RankSimulationContext { public static bool Active; public static string CurrentRoundId; } }
namespace Vodovorot.Stage2 { public static class Stage2Audio { public static double NextRankAccentAt; public static void PlayConfirmedRankImprovement(int before,int after,string id) {} } }`);
  const player = path.resolve(__dirname, '../../Assets/Scripts/Stage3/BackgroundMusicPlayer.cs');
  for (const defines of ['UNITY_EDITOR', 'UNITY_WEBGL', 'UNITY_WEBGL,DEVELOPMENT_BUILD']) {
    const additional = defines === 'UNITY_EDITOR'
      ? fs.readdirSync(unity).filter(p => /^Unity(?:Editor|Engine).*\.dll$/.test(p)).map(p => '-r:' + path.join(unity, p))
        .concat(path.resolve(__dirname, '../../Assets/Scripts/Stage3/Editor/Stage3MusicAudition.cs')) : [];
    const result = cp.spawnSync('dotnet', [path.join(sdk[2], sdk[1], 'Roslyn/bincore/csc.dll'), '-nologo', '-nostdlib+',
      '-target:library', '-langversion:9.0', '-define:' + defines, '-out:' + path.join(out, defines.replaceAll(',', '-') + '.dll'),
      ...refs, ...engine, ...additional, player, stubs], { encoding: 'utf8' });
    if (result.stdout) process.stdout.write(result.stdout); if (result.stderr) process.stderr.write(result.stderr);
    if (result.status !== 0) throw Error('Unity ' + projectVersion + ' API compilation failed: ' + defines);
  }
  console.log('PASS: real player + Editor audition compile against Unity ' + projectVersion + ' APIs; WebGL release/development player compiles');
} finally { fs.rmSync(out, { recursive: true, force: true }); }

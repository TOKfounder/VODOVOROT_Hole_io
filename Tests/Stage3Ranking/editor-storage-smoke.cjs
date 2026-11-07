const fs = require('fs'), path = require('path'), os = require('os'), cp = require('child_process');
function run(args) {
  const result = cp.spawnSync('dotnet', args, { encoding: 'utf8' });
  if (result.stdout) process.stdout.write(result.stdout);
  if (result.stderr) process.stderr.write(result.stderr);
  if (result.status !== 0) process.exit(result.status || 1);
  return result.stdout;
}
const sdks = cp.spawnSync('dotnet', ['--list-sdks'], { encoding: 'utf8' }).stdout.trim().split('\n');
const sdk = sdks.at(-1).match(/^([^ ]+) \[([^\]]+)\]/);
if (!sdk) throw Error('No .NET SDK');
const root = path.dirname(sdk[2]);
const versions = p => fs.readdirSync(p).sort((a, b) => b.localeCompare(a, undefined, { numeric: true }));
const pack = path.join(root, 'packs', 'Microsoft.NETCore.App.Ref');
const refBase = path.join(pack, versions(pack)[0], 'ref'), tfm = versions(refBase)[0];
const refs = fs.readdirSync(path.join(refBase, tfm)).filter(p => p.endsWith('.dll')).map(p => '-r:' + path.join(refBase, tfm, p));
const project = path.resolve(__dirname, '../..');
const out = fs.mkdtempSync(path.join(os.tmpdir(), 'vodovorot-stage3-editor-storage-smoke-'));
try {
  const output = path.join(out, 'editor-storage-checks.dll');
  const sources = ['Tests/Stage3Ranking/EditorStorageIsolationSmoke.cs', 'Assets/PluginYourGames/Modules/Storage/Scripts/Storage_yg.cs',
    'Assets/PluginYourGames/Modules/Storage/Scripts/SavesYG2.cs', 'Assets/Scripts/SavesYG.cs', 'Assets/Scripts/Stage2/LeaderboardSaves.cs'];
  run([path.join(sdk[2], sdk[1], 'Roslyn/bincore/csc.dll'), '-nologo', '-nostdlib+', '-langversion:9.0', '-target:exe',
    '-define:UNITY_EDITOR', '-out:' + output, ...refs, ...sources.map(p => path.join(project, p))]);
  const runtime = versions(path.join(root, 'shared', 'Microsoft.NETCore.App'))[0];
  fs.writeFileSync(path.join(out, 'editor-storage-checks.runtimeconfig.json'), JSON.stringify({ runtimeOptions: {
    tfm, framework: { name: 'Microsoft.NETCore.App', version: runtime }
  }}));
  run([output]);
} finally { fs.rmSync(out, { recursive: true, force: true }); }

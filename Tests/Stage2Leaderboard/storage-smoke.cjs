// Runs the real Storage_yg.cs control flow without launching or recompiling Unity.
const fs=require('fs'), path=require('path'), os=require('os'), cp=require('child_process');
function run(args) { const r=cp.spawnSync('dotnet',args,{encoding:'utf8'});if(r.stdout)process.stdout.write(r.stdout);if(r.stderr)process.stderr.write(r.stderr);if(r.status!==0)process.exit(r.status||1);return r.stdout; }
const sdks=cp.spawnSync('dotnet',['--list-sdks'],{encoding:'utf8'}).stdout.trim().split('\n');
const sdk=sdks[sdks.length-1].match(/^([^ ]+) \[([^\]]+)\]/);if(!sdk)throw Error('No .NET SDK');
const root=path.dirname(sdk[2]);
const versions=p=>fs.readdirSync(p).sort((a,b)=>b.localeCompare(a,undefined,{numeric:true}));
const pack=path.join(root,'packs','Microsoft.NETCore.App.Ref');
const refVersion=versions(pack)[0], refBase=path.join(pack,refVersion,'ref');
const tfm=versions(refBase)[0], refs=fs.readdirSync(path.join(refBase,tfm)).filter(x=>x.endsWith('.dll')).map(x=>'-r:'+path.join(refBase,tfm,x));
const project=path.resolve(__dirname,'../..'), out=fs.mkdtempSync(path.join(os.tmpdir(),'vodovorot-storage-smoke-'));
const output=path.join(out,'storage-checks.dll');
const sources=['Tests/Stage2Leaderboard/StorageGuardSmoke.cs','Assets/PluginYourGames/Modules/Storage/Scripts/Storage_yg.cs',
 'Assets/PluginYourGames/Modules/Storage/Scripts/SavesYG2.cs','Assets/Scripts/SavesYG.cs','Assets/Scripts/Stage2/LeaderboardSaves.cs',
 'Assets/Scripts/Stage2/LeaderboardProfileLoadRules.cs'].map(x=>path.join(project,x));
run([path.join(sdk[2],sdk[1],'Roslyn','bincore','csc.dll'),'-nologo','-nostdlib+','-langversion:9.0','-target:exe',
 '-define:YandexGamesPlatform_yg','-out:'+output,...refs,...sources]);
const runtime=versions(path.join(root,'shared','Microsoft.NETCore.App'))[0];
fs.writeFileSync(path.join(out,'storage-checks.runtimeconfig.json'),JSON.stringify({runtimeOptions:{tfm,framework:{name:'Microsoft.NETCore.App',version:runtime}}}));
run([output]);

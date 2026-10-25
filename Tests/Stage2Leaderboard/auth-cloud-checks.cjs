// Executes the actual shipped cloud CopyCode, with only SDK I/O stubbed.
const fs=require('fs');
const vm=require('vm');
const assert=require('assert/strict');
// The production plugin must distinguish explicit empty cloud from a failed read during our login only.
(async()=>{
 const source=fs.readFileSync('Assets/PluginYourGames/Modules/Storage/Scripts/Editor/CopyCode/CloudStorage_js.js','utf8');
 let checks=0;
 async function load(pending, fail, present) {
  let messages=[],writes=0;
  const root={__vodovorotStage2AuthPending:pending};
  const context={NO_DATA:'no data',window:root,ysdk:{},console:{error(){}},
   player:{getData:()=>fail?Promise.reject(Error('offline')):Promise.resolve(present?{saves:['profile']}:{}),setData:()=>writes++},
   YG2Instance:(method,data)=>messages.push(data),Promise,JSON};
  vm.runInNewContext(source,context);
  context.SaveCloud('guest',true);
  await context.LoadCloud();
  return {result:messages[0],writes};
 }
 const empty=await load(true,false,false);assert.equal(empty.result,'no data');assert.equal(empty.writes,0);checks++;
 const failed=await load(true,true,false);assert.equal(failed.result,'STAGE2_AUTH_CLOUD_ERROR');assert.equal(failed.writes,0);checks++;
 const old=await load(false,true,false);assert.equal(old.result,'no data');assert.equal(old.writes,1);checks++;
 const existing=await load(true,false,true);assert.equal(existing.result,'["profile"]');assert.equal(existing.writes,0);checks++;
 console.log(`PASS: ${checks} auth-only cloud empty/error/write-gate contract checks`);
})().catch(e=>{console.error(e);process.exitCode=1;});

(async()=>{
 const cloudSource=fs.readFileSync('Assets/PluginYourGames/Modules/Storage/Scripts/Editor/CopyCode/CloudStorage_js.js','utf8');
 const bridgeSource=fs.readFileSync('Assets/Scripts/Stage2/LeaderboardBridge.jslib','utf8');
 const pluginSource=fs.readFileSync('Assets/PluginYourGames/Modules/Storage/Plugins/CloudStorage.jslib','utf8');
 let library={},messages=[],writes=[],resolvers=[],checks=0;
 const root={};
 const context={NO_DATA:'no data',window:root,ysdk:{},console:{error(){}},Promise,JSON,
  player:{getData:()=>new Promise(resolve=>resolvers.push(resolve)),setData:data=>writes.push(data)},
  YG2Instance:(method,data)=>messages.push(data),LibraryManager:{library},mergeInto:(a,b)=>Object.assign(a,b),UTF8ToString:x=>x};
 vm.runInNewContext(cloudSource,context);vm.runInNewContext(bridgeSource,context);vm.runInNewContext(pluginSource,context);
 library.LeaderboardStage2_AuthLoad(1);
 const old=context.LoadCloud();
 context.SaveCloud('before-ready',true);library.SaveCloud_js('before-ready',true);
 assert.equal(writes.length,0);checks++;
 library.LeaderboardStage2_AuthLoad(-1);
 context.SaveCloud('after-failure',true);library.SaveCloud_js('after-failure',true);
 assert.equal(writes.length,0);checks++;
 library.LeaderboardStage2_AuthLoad(1);
 const retry=context.LoadCloud();
 resolvers[1]({saves:['current-account']});await retry;
 resolvers[0]({saves:['old-late-account']});await old;
 assert.deepEqual(messages,['["current-account"]']);assert.equal(context.cloudSaves,'["current-account"]');checks++;
 library.LeaderboardStage2_AuthLoad(0);
 context.SaveCloud('latest-guest-progress',true);
 assert.equal(writes.length,1);assert.equal(writes[0].saves[0],'latest-guest-progress');checks++;
 console.log(`PASS: ${checks} retry-generation/pending-failed-write-gate/recovery contract checks`);
})().catch(e=>{console.error(e);process.exitCode=1;});

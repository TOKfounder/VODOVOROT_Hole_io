// Executes the actual SDK auth CopyCode. The slow-dialog clock is virtual; no live SDK result is fabricated.
const fs=require('fs'),vm=require('vm'),assert=require('assert/strict');
const source=fs.readFileSync('Assets/PluginYourGames/Modules/Authorization/Scripts/Editor/CopyCode/Auth_js.js','utf8');
let checks=0;
function fixture(options={}) {
 let authorized=!!options.authorized,messages=[],dialogs=0,getters=0,resolveDialog,rejectDialog;
 let clock=0;
 const player={isAuthorized:()=>authorized,getUniqueID:()=>authorized?'account':'guest',getName:()=>'',getPhoto:()=>'',getPayingStatus:()=>''};
 const dialog=new Promise((resolve,reject)=>{resolveDialog=resolve;rejectDialog=reject;});
 const context={NO_DATA:'no data',JSON,Promise,console:{error(){}},LogStyledMessage(){},
  ysdk:options.noSdk?null:{getPlayer:async()=>{getters++;return player;},auth:{openAuthDialog:()=>{dialogs++;return dialog;}}},
  YG2Instance:(method,data)=>{messages.push({method,data,at:clock});if(method==='LoggedIn'&&options.earlyCloud)messages.push({method:'profile-already-loaded',at:clock});}};
 vm.runInNewContext(source,context);
 return {context,messages,start:()=>context.OpenAuthDialog(),time:t=>{clock=t;},dialogs:()=>dialogs,getters:()=>getters,
  complete:()=>{authorized=true;resolveDialog();},cancel:()=>rejectDialog({name:'AbortError'})};
}
const flush=async()=>{for(let i=0;i<8;i++)await Promise.resolve();};
(async()=>{
 const slow=fixture();const pending=slow.start();await flush();assert.equal(slow.dialogs(),1);
 slow.time(31000);await flush();
 assert.equal(slow.messages.filter(x=>x.method==='Stage2AuthDialogClosed').length,0,'31 seconds in the still-open dialog is not cancellation');
 slow.complete();await pending;
 const closed=slow.messages.filter(x=>x.method==='Stage2AuthDialogClosed');
 assert.equal(closed.length,1);assert.equal(closed[0].data,'true');assert.ok(closed[0].at>=25000);
 assert.equal(slow.messages.filter(x=>x.method==='LoggedIn').length,1);checks++;
 const cancel=fixture();const canceled=cancel.start();await flush();cancel.cancel();await canceled;
 assert.equal(cancel.messages.filter(x=>x.method==='LoggedIn').length,0);
 assert.deepEqual(cancel.messages.filter(x=>x.method==='Stage2AuthDialogClosed').map(x=>x.data),['false']);checks++;
 const already=fixture({authorized:true});await already.start();
 assert.equal(already.dialogs(),0);assert.equal(already.messages.filter(x=>x.method==='LoggedIn').length,1);
 assert.deepEqual(already.messages.filter(x=>x.method==='Stage2AuthDialogClosed').map(x=>x.data),['true']);checks++;
 const absent=fixture({noSdk:true});await absent.start();
 assert.equal(absent.getters(),0);assert.deepEqual(absent.messages.filter(x=>x.method==='Stage2AuthDialogClosed').map(x=>x.data),['false']);checks++;
 const early=fixture({authorized:true,earlyCloud:true});await early.start();
 assert.deepEqual(early.messages.slice(-3).map(x=>x.method),['LoggedIn','profile-already-loaded','Stage2AuthDialogClosed']);
 assert.equal(early.messages[early.messages.length-1].data,'true','Completion must arrive even when profile loading already finished');checks++;
 console.log(`PASS: ${checks} actual auth-dialog slow-await/cancel/already-auth/SDK-absent/early-profile contracts`);
})().catch(error=>{console.error(error);process.exitCode=1;});

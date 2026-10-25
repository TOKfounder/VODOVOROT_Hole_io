// Contract tests for the actual WebGL bridge, using deterministic SDK promises.
// These are not a claim of testing the live Yandex service.
const fs = require('fs');
const vm = require('vm');
const assert = require('assert/strict');
const bridge = fs.readFileSync('Assets/Scripts/Stage2/LeaderboardBridge.jslib', 'utf8');
let checks = 0;
async function run(options = {}) {
    let library = {}, messages = [], calls = [], ownReads = 0;
    const entry = (rank, score, id='a') => ({ rank, score, player: {uniqueID:id, publicName:'Visible name', scopePermissions:{public_name:'allow'}} });
    const sdk = {
        getPlayer: async () => ({isAuthorized: () => !options.guest, getUniqueID: () => options.identity || 'a'}),
        isAvailableMethod: async () => options.available !== false,
        leaderboards: {
            getDescription: async () => ({description:{invert_sort_order:!!options.ascending,score_format:{type:'numeric',options:{decimal_offset:0}}}}),
            getPlayerEntry: async () => {
                calls.push('read'); ownReads++;
                if (options.noEntry || options.firstEntry && ownReads === 1) throw {code:'LEADERBOARD_PLAYER_NOT_PRESENT'};
                return entry(ownReads===1 ? 100 : 90, options.stale ? 9 : ownReads ===1 ? 9 : 10, options.wrongEntry ? 'b' : 'a');
            },
            setScore: async (name, score) => { calls.push('write'); assert.equal(name,'BestPlayers');assert.equal(score,10); if(options.writeFails)throw Error('no'); },
            getEntries: async () => {calls.push('neighbors');if(options.neighborsFail)throw Error('offline');return {entries:[entry(89,11,'b'),entry(90,10),entry(91,9,'c')]};}
        }
    };
    const context = { LibraryManager:{library},mergeInto:(target,source)=>Object.assign(target,source),UTF8ToString:x=>x,
        window:{}, ysdk:options.offline ? null : sdk,navigator:{onLine:!options.offline},
        Module:{canvas:{width:780,getBoundingClientRect:()=>({width:390})}},
        SendMessage:(obj,method,json)=>messages.push(JSON.parse(json)),setTimeout,clearTimeout,Promise,Date,Number,Object,String };
    vm.runInNewContext(bridge,context);
    assert.equal(library.LeaderboardStage2_CssRatio(),2);
    library.LeaderboardStage2_Request('target',1,'a',10);
    for(let i=0;i<250 && !messages.length;i++)await new Promise(r=>setTimeout(r,5));
    assert.equal(messages.length,1,'Exactly one callback per transaction');
    return {reply:messages[0],calls,library,messages,context};
}
(async()=>{
 const success=await run();assert.equal(success.reply.status,'confirmed');assert.equal(success.reply.rank,90);assert.equal(success.reply.score,10);assert.equal(success.reply.submitted,true);assert.deepEqual(success.calls,['read','write','read','neighbors']);checks++;
 const failed=await run({writeFails:true});assert.equal(failed.reply.status,'unavailable');assert.deepEqual(failed.calls,['read','write']);checks++;
 const stale=await run({stale:true});assert.equal(stale.reply.status,'pending');assert.equal(stale.reply.rank,0);checks++;
 const guest=await run({guest:true});assert.equal(guest.reply.status,'guest');assert.equal(guest.calls.length,0);checks++;
 const offline=await run({offline:true});assert.equal(offline.reply.status,'offline');assert.equal(offline.calls.length,0);checks++;
 const wrong=await run({identity:'b'});assert.equal(wrong.reply.status,'profile_changed');assert.equal(wrong.calls.length,0);checks++;
 const invalidEntry=await run({wrongEntry:true});assert.equal(invalidEntry.reply.status,'profile_changed');assert.deepEqual(invalidEntry.calls,['read']);checks++;
 const missing=await run({noEntry:true});assert.equal(missing.reply.status,'no_entry');assert.equal(missing.reply.rank,0);checks++;
 const first=await run({firstEntry:true});assert.equal(first.reply.status,'confirmed');assert.equal(first.reply.beforeRank,0);checks++;
 const neighbors=await run({neighborsFail:true});assert.equal(neighbors.reply.status,'confirmed');assert.equal(neighbors.reply.neighborsAvailable,false);checks++;
 const config=await run({ascending:true});assert.equal(config.reply.status,'configuration');assert.equal(config.calls.length,0);checks++;
 const unavailable=await run({available:false});assert.equal(unavailable.reply.status,'unavailable');assert.equal(unavailable.calls.length,0);checks++;
 success.library.LeaderboardStage2_Request('target',2,'a',10);
 await new Promise(r=>setTimeout(r,10));assert.equal(success.messages[1].status,'rate_limited');checks++;
 console.log(`PASS: ${checks} awaited bridge/identity/availability/rate-limit contract checks`);
})().catch(e=>{console.error(e);process.exitCode=1;});

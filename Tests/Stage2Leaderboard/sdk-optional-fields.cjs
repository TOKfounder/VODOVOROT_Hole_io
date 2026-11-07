'use strict';
// Execute the real SDK bridge sources with the optional fields omitted by Yandex's dev adapter.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '../..');
const environmentSource = fs.readFileSync(path.join(root,
  'Assets/PluginYourGames/Modules/EnvirData/Scripts/Editor/CopyCode/EnvirData_js.js'), 'utf8');
const leaderboardSource = fs.readFileSync(path.join(root,
  'Assets/PluginYourGames/Modules/Leaderboards/Scripts/Editor/CopyCode/LB_js.js'), 'utf8');

(async () => {
  for (const [legacyBrowser, navigatorLanguage, expected] of [[undefined, 'en-US', 'en-US'],
    [{ lang: 'tr' }, 'en-US', 'tr'], [undefined, '', 'en']]) {
    const messages = [];
    const context = { NO_DATA: 'no data', console, LogStyledMessage() {},
      YG2Instance(method, value) { messages.push({ method, value }); },
      navigator: { userAgent: 'Chrome', platform: 'MacIntel', language: navigatorLanguage },
      ysdk: { environment: { i18n: { lang: 'ru', tld: 'ru' }, app: { id: '473763' }, browser: legacyBrowser },
        deviceInfo: { type: 'desktop', isMobile: () => false, isDesktop: () => true, isTablet: () => false, isTV: () => false } } };
    vm.createContext(context);
    vm.runInContext(environmentSource, context);
    const data = JSON.parse(await context.RequestingEnvironmentData());
    assert.equal(data.language, 'ru');
    assert.equal(data.browserLang, expected);
    assert.equal(data.appID, '473763');
    assert.equal(messages[0].method, 'SetEnvirData');
  }

  const context = { NO_DATA: 'no data', console };
  vm.createContext(context);
  vm.runInContext(leaderboardSource, context);
  const rows = [undefined, { public_name: 'deny' }, { public_name: 'allow' }].map((permissions, index) => ({
    rank: index + 1, score: 100 - index, extraData: '',
    player: { uniqueID: String(index), publicName: 'Visible', scopePermissions: permissions, getAvatarSrc: () => '' }
  }));
  const data = context.EntriesLB({ entries: rows }, 'small');
  assert.deepEqual(Array.from(data.names), ['anonymous', 'anonymous', 'Visible']);
  assert.deepEqual(Array.from(data.ranks), [1, 2, 3]);
  assert.equal(data.scores[0], 100);
  assert.equal(data.extraDataArray[0], 'no data');
  console.log('PASS: real environment bridge without legacy browser, and mixed leaderboard privacy permissions');
})().catch(error => { console.error(error); process.exitCode = 1; });

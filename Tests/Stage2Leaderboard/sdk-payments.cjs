'use strict';
// Exercise the shipped bridge against the current SDK's object argument contract.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.resolve(__dirname,
  '../../Assets/PluginYourGames/Modules/Payments/Scripts/Editor/CopyCode/Payments_js.js'), 'utf8');

(async () => {
  for (const cancelled of [false, true]) {
    const calls = [];
    const messages = [];
    const context = { NO_DATA: 'no data', console: { error() {} },
      LogStyledMessage() {}, FocusGame() {},
      YG2Instance(method, id) { messages.push([method, id]); } };
    vm.createContext(context);
    vm.runInContext(source, context);
    context.mockPayments = {
      async purchase(data) {
        assert.equal(typeof data, 'object');
        assert.equal(data.id, 'hand');
        calls.push('purchase');
        if (cancelled) throw new Error('Payment was canceled');
        return { productID: 'hand', purchaseToken: 'test-token' };
      },
      async getPurchases() { return [{ productID: 'hand', purchaseToken: 'test-token' }]; },
      async consumePurchase(token) { assert.equal(token, 'test-token'); calls.push('consume'); }
    };
    vm.runInContext('payments = mockPayments', context);
    context.BuyPayments('hand');
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(messages, [[cancelled ? 'OnPurchaseFailed' : 'OnPurchaseSuccess', 'hand']]);
    assert.deepEqual(calls, cancelled ? ['purchase'] : ['purchase', 'consume']);
  }
  console.log('PASS: actual payment bridge uses { id }, success consumes once, cancellation grants nothing');
})().catch(error => { console.error(error); process.exitCode = 1; });

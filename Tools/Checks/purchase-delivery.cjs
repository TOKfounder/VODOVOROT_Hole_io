const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const paymentSource = fs.readFileSync('Assets/PluginYourGames/Modules/Payments/Scripts/Editor/CopyCode/Payments_js.js', 'utf8');
const storageSource = fs.readFileSync('Assets/PluginYourGames/Modules/Storage/Scripts/Editor/CopyCode/CloudStorage_js.js', 'utf8');
let checks = 0;
async function scenario({saveFails = false, consumeFails = false, unknown = false} = {}) {
    const calls = [];
    const purchase = {productID: unknown ? 'vortex_no_ads' : 'hand', purchaseToken: 'fixture-token'};
    let writeResolve;
    const context = vm.createContext({
        NO_DATA: 'none', window: {}, console: {warn() {}, error() {}}, setTimeout, clearTimeout,
        FocusGame() {}, LogStyledMessage() {},
        player: {setData(data, flush) {
            calls.push('save-start'); assert.equal(flush, true); checks++;
            return new Promise((resolve, reject) => { writeResolve = () => {
                calls.push(saveFails ? 'save-failed' : 'save-committed');
                if (saveFails) reject(new Error('offline')); else resolve();
            }; });
        }},
        YG2Instance(method, data) {
            if (method !== 'OnPurchaseDelivery') return;
            calls.push('grant');
            vm.runInContext('SaveCloud("{fixture:1}", false); AcknowledgePurchaseDelivery("fixture-token", true)', context);
        }
    });
    vm.runInContext(paymentSource + '\n' + storageSource, context);
    context.mockPayments = {
        getPurchases: async () => [purchase],
        consumePurchase: async () => {calls.push('consume'); if (consumeFails) throw new Error('retry');}
    };
    vm.runInContext('payments = mockPayments', context);
    const delivery = vm.runInContext('ConsumePurchases(true)', context);
    await new Promise(resolve => setImmediate(resolve));
    if (unknown) {
        await delivery; assert.deepEqual(calls, []); checks++; return;
    }
    assert.deepEqual(calls, ['grant', 'save-start']); checks++;
    writeResolve();
    await delivery;
    assert.deepEqual(calls, ['grant', 'save-start', saveFails ? 'save-failed' : 'save-committed', ...(saveFails ? [] : ['consume'])]); checks++;
}
(async () => {
    await scenario(); await scenario({saveFails: true}); await scenario({consumeFails: true}); await scenario({unknown: true});
    const report = {passed: true, checks, covered: ['no consume before cloud commit', 'failed save stays pending', 'failed consume remains recoverable', 'unknown SKU neither delivered nor consumed']};
    fs.writeFileSync('Docs/QualityPass-20261108/Checks/purchase-delivery-js.json', JSON.stringify(report, null, 2));
    console.log(JSON.stringify(report));
})().catch(error => {console.error(error); process.exitCode = 1;});

var cloudSaves = NO_DATA;

function SaveCloud(jsonData, flush) {
    var stage2Root = typeof window !== 'undefined' ? window : globalThis;
    if (stage2Root.__vodovorotStage2AuthPending || stage2Root.__vodovorotStage2AuthBlocked) return;
    if (player == null) {
        console.error('CRASH Save Cloud: ', 'Didnt have time to load');
        return;
    }
    try {
        // Purchase grants require a confirmed cloud commit; ordinary saves keep
        // the configured flush policy. Rejections leave the platform order pending.
        var deliveryPending = typeof pendingPurchaseDelivery !== 'undefined' && pendingPurchaseDelivery;
        var write = Promise.resolve(player.setData({ saves: [jsonData] }, deliveryPending ? true : flush));
        if (deliveryPending) window.__vodovorotPurchaseCloudWrite = write;
        write.catch(() => console.warn('Cloud save failed; progress retained locally'));
    } catch (e) {
        console.error('CRASH Save Cloud: ', e.message);
    }
}

function LoadCloud() {
    var stage2RequestRoot = typeof window !== 'undefined' ? window : globalThis;
    var stage2AuthRequest = stage2RequestRoot.__vodovorotStage2AuthPending === true;
    var stage2AuthGeneration = stage2RequestRoot.__vodovorotStage2AuthGeneration || 0;
    return new Promise((resolve) => {
        if (ysdk == null) {
            Final(NO_DATA, true);
            return;
        }
        try {
            player.getData(["saves"]).then(data => {
                if (data.saves) {
                    Final(JSON.stringify(data.saves));
                } else {
                    Final(NO_DATA);
                }
            }).catch(() => {
                console.error('Load Cloud Error!');
                Final(NO_DATA, true);
            });
        }
        catch (e) {
            console.error('CRASH Load saves Cloud: ', e.message);
            Final(NO_DATA, true);
        }

        function Final(res, failed) {
            var stage2Root = typeof window !== 'undefined' ? window : globalThis;
            // A user retry supersedes the earlier slow request; its late result must not replace the newer profile.
            if (stage2AuthRequest && stage2AuthGeneration !== (stage2Root.__vodovorotStage2AuthGeneration || 0)) {
                resolve(res); return;
            }
            if (failed && stage2AuthRequest) res = 'STAGE2_AUTH_CLOUD_ERROR';
            cloudSaves = res;
            YG2Instance('SetLoadSaves', res);
            resolve(res);
        }
    });
}
var paymentsData = NO_DATA;
let payments = null;

function InitPayments(sendback) {
    return new Promise((resolve) => {
        try {
            if (ysdk == null) {
                Final(NO_DATA);
                return;
            }

            ysdk.getPayments().then(_payments => {
                payments = _payments;

                payments.getCatalog()
                    .then(products => {
                        let productID = [];
                        let title = [];
                        let description = [];
                        let imageURI = [];
                        let price = [];
                        let priceValue = [];
                        let priceCurrencyCode = [];
                        let currencyImageURL = [];
                        let consumed = [];

                        payments.getPurchases().then(purchases => {
                            for (let i = 0; i < products.length; i++) {
                                productID[i] = products[i].id;
                                title[i] = products[i].title;
                                description[i] = products[i].description;
                                imageURI[i] = products[i].imageURI;
                                price[i] = products[i].price;
                                priceValue[i] = products[i].priceValue;
                                priceCurrencyCode[i] = products[i].priceCurrencyCode;
                                currencyImageURL[i] = products[i].getPriceCurrencyImage("medium");

                                consumed[i] = true;
                                for (let i2 = 0; i2 < purchases.length; i2++) {
                                    if (purchases[i2].productID === productID[i]) {
                                        consumed[i] = false;
                                        break;
                                    }
                                }
                            }

                            let jsonPayments = {
                                "id": productID,
                                "title": title,
                                "description": description,
                                "imageURI": imageURI,
                                "price": price,
                                "priceValue": priceValue,
                                "priceCurrencyCode": priceCurrencyCode,
                                "currencyImageURL": currencyImageURL,
                                "consumed": consumed,
                                "language": ysdk.environment.i18n.lang
                            };

                            Final(JSON.stringify(jsonPayments));
                        });
                    });

            }).catch(e => {
                LogStyledMessage('Purchases are not available', e.message);
                Final(NO_DATA);
            })
        } catch (e) {
            console.error('CRASH Init Payments: ', e.message);
            Final(NO_DATA);
        }

        function Final(res) {
            paymentsData = res;
            YG2Instance('PaymentsEntries', res);
            resolve(res);
        }
    });
}

// Serialize deliveries so each acknowledgement refers to its own full-profile save.
let purchaseDeliveryQueue = Promise.resolve();
let pendingPurchaseDelivery = null;
const supportedPurchaseIDs = new Set(['hand', 'bag', 'box', 'chest', 'gold', 'scrag', 'lord']);

function BuyPayments(id) {
    if (!payments || !supportedPurchaseIDs.has(id)) {
        YG2Instance('OnPurchaseFailed', id);
        return;
    }
    payments.purchase({ id: id })
        .then(() => ConsumePurchase(id, true))
        .catch(() => YG2Instance('OnPurchaseFailed', id))
        .finally(() => FocusGame());
}

function QueuePurchaseDelivery(purchase) {
    // Other versions of this app have their own catalog. Never consume their orders.
    if (!supportedPurchaseIDs.has(purchase.productID)) return Promise.resolve();
    purchaseDeliveryQueue = purchaseDeliveryQueue.catch(() => {}).then(async () => {
        await new Promise((resolve, reject) => {
            const timeout = setTimeout(() => {
                pendingPurchaseDelivery = null;
                reject(new Error('Purchase delivery acknowledgement timed out'));
            }, 15000);
            pendingPurchaseDelivery = {
                token: purchase.purchaseToken,
                resolve: () => { clearTimeout(timeout); resolve(); },
                reject: () => { clearTimeout(timeout); reject(new Error('Purchase remains pending')); }
            };
            window.__vodovorotPurchaseCloudWrite = null;
            YG2Instance('OnPurchaseDelivery', JSON.stringify({
                productID: purchase.productID, purchaseToken: purchase.purchaseToken
            }));
        });
        await payments.consumePurchase(purchase.purchaseToken);
    });
    return purchaseDeliveryQueue;
}

function AcknowledgePurchaseDelivery(token, accepted) {
    const pending = pendingPurchaseDelivery;
    if (!pending || pending.token !== token) return;
    const write = window.__vodovorotPurchaseCloudWrite;
    if (!accepted || !write) {
        pendingPurchaseDelivery = null;
        pending.reject();
        return;
    }
    // A real, flushed cloud commit must succeed before acknowledging the SDK order.
    Promise.resolve(write).then(() => {
        if (pendingPurchaseDelivery === pending) pendingPurchaseDelivery = null;
        pending.resolve();
    }, () => {
        if (pendingPurchaseDelivery === pending) pendingPurchaseDelivery = null;
        pending.reject();
    });
}

function ConsumePurchase(id, onPurchaseSuccess) {
    if (!payments) return Promise.resolve();
    return payments.getPurchases().then(async purchases => {
        for (const purchase of purchases) {
            if (purchase.productID === id) await QueuePurchaseDelivery(purchase);
        }
    }).catch(() => console.warn('Purchase delivery pending; retry on next launch'));
}

function ConsumePurchases(onPurchaseSuccess) {
    if (!payments) return Promise.resolve();
    return payments.getPurchases().then(async purchases => {
        for (const purchase of purchases) await QueuePurchaseDelivery(purchase);
    }).catch(() => console.warn('Purchase restoration pending; retry on next launch'));
}

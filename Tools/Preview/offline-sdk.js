/* LOCAL QA ONLY. Serve as /sdk.js on loopback; never package with production. */
(function (root) {
    'use strict';
    var loc = root.location;
    var host = loc && String(loc.hostname).toLowerCase();
    if (!loc || !/^https?:$/.test(loc.protocol) ||
        ['localhost', '127.0.0.1', '::1', '[::1]'].indexOf(host) < 0) return;
    if (root.YaGames && typeof root.YaGames.init === 'function') return;

    var storageKey = 'vodovorot.stage2.local-preview.sdk.v1';
    var qaParams = new URLSearchParams(loc.search);
    var qaAds = qaParams.get('qaAds') === '1';
    var qaMobile = qaParams.get('qaMobile') === '1';
    var noteText = 'ТЕСТ: локальный SDK; без реальных платежей, рекламы и рейтинга' + (qaMobile ? ' · мобильный UI (эмуляция)' : '');
    var nav = root.navigator || {};
    var browserLang = nav.language || 'en';
    var language = String(browserLang).toLowerCase().split('-')[0] || 'en';
    var ua = nav.userAgent || '';
    var tablet = /iPad|Tablet|Android(?!.*Mobile)/i.test(ua) ||
        (/Macintosh/i.test(ua) && nav.maxTouchPoints > 1);
    var mobile = !tablet && /Mobi|iPhone|iPod|Android/i.test(ua);
    if (qaMobile) { tablet = false; mobile = true; }
    var type = tablet ? 'tablet' : mobile ? 'mobile' : 'desktop';
    var listeners = Object.create(null);

    function error(code) {
        var value = new Error('Local preview: Yandex SDK is disabled (' + code + ').');
        value.code = code;
        return value;
    }
    function rejected(code) {
        // Some legacy CopyCode writers do not await promises. Mark the same
        // rejection handled without turning it into a successful operation.
        var promise = Promise.reject(typeof code === 'string' ? error(code) : code);
        promise.catch(function () {});
        return promise;
    }
    function object(value) {
        return value && typeof value === 'object' && !Array.isArray(value);
    }
    function clone(value) { return JSON.parse(JSON.stringify(value)); }
    function read() {
        var raw = root.localStorage.getItem(storageKey);
        if (raw === null) return { data: {}, stats: {} };
        var saved = JSON.parse(raw);
        if (!object(saved) || !object(saved.data) || !object(saved.stats))
            throw error('LOCAL_PREVIEW_STORAGE_INVALID');
        return saved;
    }
    function getFields(bucket, keys) {
        try {
            var source = read()[bucket];
            var selected = Object.create(null);
            (Array.isArray(keys) ? keys : Object.keys(source)).forEach(function (key) {
                if (Object.prototype.hasOwnProperty.call(source, key)) selected[key] = source[key];
            });
            return Promise.resolve(clone(selected));
        } catch (cause) { return rejected(cause); }
    }
    function setFields(bucket, fields) {
        try {
            if (!object(fields)) throw error('LOCAL_PREVIEW_STORAGE_INVALID');
            var saved = read();
            // A null-prototype target preserves JSON fields without mutating a
            // JavaScript prototype. Only this adapter's isolated key is written.
            var merged = Object.assign(Object.create(null), saved[bucket], clone(fields));
            saved[bucket] = merged;
            root.localStorage.setItem(storageKey, JSON.stringify(saved));
            return Promise.resolve();
        } catch (cause) { return rejected(cause); }
    }
    function later(callback) {
        Promise.resolve().then(callback).catch(function (cause) {
            if (root.console) root.console.error('Local preview callback failed:', cause);
        });
    }
    function callback(options, name, value) {
        var callbacks = options && options.callbacks;
        if (callbacks && typeof callbacks[name] === 'function') callbacks[name](value);
    }
    function noBanner() {
        return Promise.resolve({ stickyAdvIsShowing: false, reason: 'LOCAL_PREVIEW_ADS_DISABLED' });
    }

    var player = {
        isAuthorized: function () { return false; },
        getUniqueID: function () { return 'local-preview-guest'; },
        getName: function () { return ''; },
        getPhoto: function () { return ''; },
        getPayingStatus: function () { return 'unknown'; },
        getData: function (keys) { return getFields('data', keys); },
        setData: function (fields) { return setFields('data', fields); },
        getStats: function (keys) { return getFields('stats', keys); },
        setStats: function (fields) { return setFields('stats', fields); }
    };
    var payments = {
        getCatalog: function () { return Promise.resolve([]); },
        getPurchases: function () { return Promise.resolve([]); },
        purchase: function () { return rejected('LOCAL_PREVIEW_PAYMENTS_DISABLED'); },
        consumePurchase: function () { return rejected('LOCAL_PREVIEW_PAYMENTS_DISABLED'); }
    };
    var gameplay = { start: function () {}, stop: function () {} };
    var sdk = {
        __localPreview: true,
        environment: {
            i18n: { lang: language, tld: 'ru' },
            app: { id: '473763' },
            browser: { lang: browserLang },
            payload: ''
        },
        deviceInfo: {
            type: type,
            isMobile: function () { return mobile; },
            isTablet: function () { return tablet; },
            isDesktop: function () { return !mobile && !tablet; },
            isTV: function () { return false; }
        },
        getPlayer: function () { return Promise.resolve(player); },
        auth: { openAuthDialog: function () { return rejected('LOCAL_PREVIEW_AUTH_DISABLED'); } },
        getPayments: function () { return Promise.resolve(payments); },
        getFlags: function () { return Promise.resolve({}); },
        isAvailableMethod: function () { return Promise.resolve(false); },
        leaderboards: {
            setScore: function () { return rejected('LOCAL_PREVIEW_LEADERBOARDS_DISABLED'); },
            getDescription: function () { return rejected('LOCAL_PREVIEW_LEADERBOARDS_DISABLED'); },
            getPlayerEntry: function () { return rejected('LEADERBOARD_PLAYER_NOT_PRESENT'); },
            getEntries: function () { return Promise.resolve({ entries: [], ranges: [] }); }
        },
        adv: {
            showFullscreenAdv: function (options) {
                if (qaAds) showTestAd(options, false);
                else later(function () { callback(options, 'onClose', false); });
            },
            showRewardedVideo: function (options) {
                if (qaAds) { showTestAd(options, true); return; }
                later(function () {
                    try { callback(options, 'onError', error('LOCAL_PREVIEW_ADS_DISABLED')); }
                    finally { callback(options, 'onClose'); }
                });
            },
            getBannerAdvStatus: noBanner,
            showBannerAdv: noBanner,
            hideBannerAdv: noBanner
        },
        features: { LoadingAPI: { ready: function () {} }, GameplayAPI: gameplay, GameAPI: gameplay },
        on: function (event, handler) {
            if (typeof handler !== 'function') return;
            (listeners[event] || (listeners[event] = new Set())).add(handler);
        },
        off: function (event, handler) {
            if (listeners[event]) listeners[event].delete(handler);
        }
    };

    var fullScreenRequests = 0;
    function showTestAd(options, rewarded) {
        var doc = root.document;
        if (doc.getElementById('vodovorot-test-ad')) return;
        callback(options, 'onOpen');
        if (!rewarded) fullScreenRequests++;
        var panel = doc.createElement('section');
        panel.id = 'vodovorot-test-ad';
        panel.setAttribute('role', 'dialog');
        panel.setAttribute('aria-label', rewarded ? 'Тест rewarded рекламы' : 'Тест межстраничной рекламы');
        panel.style.cssText = 'position:fixed;inset:0;z-index:2147483647;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:16px;background:#10212f;color:white;font:20px sans-serif;text-align:center;padding:20px';
        var label = doc.createElement('p');
        label.textContent = 'ЛОКАЛЬНЫЙ ТЕСТ. ' + (rewarded ? 'Награду рассчитывает настоящая игра.' : 'Естественная пауза. Показов в тесте: ' + fullScreenRequests);
        panel.appendChild(label);
        function finish(rewards, failed) {
            panel.remove();
            if (failed) callback(options, 'onError', error('QA_AD_ERROR'));
            for (var i = 0; i < rewards; i++) callback(options, 'onRewarded');
            callback(options, 'onClose', !rewarded);
        }
        function button(text, action) {
            var b = doc.createElement('button'); b.textContent = text;
            b.style.cssText = 'font:20px sans-serif;padding:14px 22px;min-width:240px';
            b.addEventListener('click', action); panel.appendChild(b);
        }
        if (rewarded) {
            button('Успешный просмотр', function () { finish(1, false); });
            button('Повтор callback (тест)', function () { finish(2, false); });
            button('Отмена', function () { finish(0, false); });
            button('Ошибка', function () { finish(0, true); });
        } else button('Закрыть тестовую рекламу', function () { finish(0, false); });
        doc.body.appendChild(panel);
    }

    function showNote() {
        var doc = root.document;
        if (!doc || !doc.body || doc.getElementById('vodovorot-local-preview-sdk-note')) return;
        var note = doc.createElement('div');
        note.id = 'vodovorot-local-preview-sdk-note';
        note.textContent = noteText;
        note.title = noteText;
        note.setAttribute('role', 'note');
        note.style.cssText = 'position:fixed;left:50%;bottom:2px;transform:translateX(-50%);' +
            'max-width:calc(100vw - 16px);box-sizing:border-box;padding:1px 5px;' +
            'font:10px/14px system-ui,sans-serif;color:#fff;background:rgba(23,29,42,.85);' +
            'border-radius:3px;z-index:2147483645;pointer-events:none;user-select:none;' +
            'white-space:nowrap;overflow:hidden;text-overflow:ellipsis;';
        doc.body.appendChild(note);
    }
    root.YaGames = { init: function () { showNote(); return Promise.resolve(sdk); } };
    root.__vodovorotLocalPreviewSDK = true;
    if (root.document && root.document.readyState === 'loading')
        root.document.addEventListener('DOMContentLoaded', showNote, { once: true });
    else showNote();
})(typeof window !== 'undefined' ? window : globalThis);

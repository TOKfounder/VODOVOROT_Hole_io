mergeInto(LibraryManager.library, {
    $VodovorotMusicGate__deps: ['$WEBAudio'],
    $VodovorotMusicGate: {
        receiver: null,
        authorized: false,
        context: null,
        removals: [],
        visible: function () { return typeof document === 'undefined' || !document.hidden; },
        focused: function () { return typeof document === 'undefined' || !document.hasFocus || document.hasFocus(); },
        state: function () {
            var context = typeof WEBAudio !== 'undefined' ? WEBAudio.audioContext : null;
            if (context !== this.context) {
                if (this.context && this.context.removeEventListener) this.context.removeEventListener('statechange', this.notify);
                this.context = context;
                if (context && context.addEventListener) context.addEventListener('statechange', this.notify);
            }
            return (this.authorized ? 1 : 0) | (context && context.state === 'running' ? 2 : 0) |
                (this.visible() ? 4 : 0) | (this.focused() ? 8 : 0);
        },
        notify: function () {
            var gate = VodovorotMusicGate;
            if (!gate.receiver) return;
            try { SendMessage(gate.receiver, 'OnBrowserAudioState', String(gate.state())); }
            catch (ignored) { /* Teardown can remove the Unity receiver before DOM listeners. */ }
        },
        tryResume: function () {
            var gate = VodovorotMusicGate;
            if (!gate.authorized || !gate.visible() || !gate.focused()) return;
            var context = typeof WEBAudio !== 'undefined' ? WEBAudio.audioContext : null;
            if (!context || context.state === 'closed') return;
            if (context.state !== 'running') {
                try {
                    var promise = context.resume();
                    if (promise && promise.then) promise.then(gate.notify, gate.notify);
                } catch (ignored) { gate.notify(); }
            }
            gate.notify();
        },
        gesture: function (event) {
            var gate = VodovorotMusicGate;
            if (!event || event.isTrusted !== true || !gate.visible() || !gate.focused()) return;
            // Match Unity 2022 Audio.js's Safari requirement: create compressed-audio elements
            // in the direct trusted gesture. Reuse Unity's pool, never create another context.
            if (typeof WEBAudio !== 'undefined' && WEBAudio.audioCache && typeof Audio !== 'undefined') {
                while (WEBAudio.audioCache.length < 2) {
                    var audio = new Audio();
                    audio.autoplay = false;
                    WEBAudio.audioCache.push(audio);
                }
            }
            gate.authorized = true;
            gate.tryResume();
        },
        install: function (receiver) {
            this.uninstall();
            this.receiver = receiver;
            var gate = this;
            function listen(target, event, action) {
                target.addEventListener(event, action, true);
                gate.removals.push(function () { target.removeEventListener(event, action, true); });
            }
            if (typeof window !== 'undefined') {
                listen(window, 'pointerdown', this.gesture);
                listen(window, 'mousedown', this.gesture);
                listen(window, 'touchstart', this.gesture);
                listen(window, 'keydown', this.gesture);
                listen(window, 'blur', this.notify);
                listen(window, 'focus', this.tryResume);
                listen(window, 'pagehide', this.notify);
                listen(window, 'pageshow', this.tryResume);
            }
            if (typeof document !== 'undefined')
                listen(document, 'visibilitychange', function () { gate.notify(); gate.tryResume(); });
            this.notify();
        },
        uninstall: function () {
            this.receiver = null;
            while (this.removals.length) this.removals.pop()();
            if (this.context && this.context.removeEventListener) this.context.removeEventListener('statechange', this.notify);
            this.context = null;
            this.authorized = false;
        }
    },
    MusicStage3_Install__deps: ['$VodovorotMusicGate'],
    MusicStage3_Install: function (receiver) { VodovorotMusicGate.install(UTF8ToString(receiver)); },
    MusicStage3_GetState__deps: ['$VodovorotMusicGate'],
    MusicStage3_GetState: function () { return VodovorotMusicGate.state(); },
    MusicStage3_Uninstall__deps: ['$VodovorotMusicGate'],
    MusicStage3_Uninstall: function () { VodovorotMusicGate.uninstall(); }
});

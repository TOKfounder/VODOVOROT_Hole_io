mergeInto(LibraryManager.library, {
    LeaderboardStage2_AuthLoad: function (state) {
        var root = typeof window !== 'undefined' ? window : globalThis;
        if (state === 1) root.__vodovorotStage2AuthGeneration = (root.__vodovorotStage2AuthGeneration || 0) + 1;
        root.__vodovorotStage2AuthPending = state !== 0;
        root.__vodovorotStage2AuthBlocked = state === -1;
    },
    LeaderboardStage2_CssRatio: function () {
        var canvas = Module['canvas'];
        if (!canvas || !canvas.getBoundingClientRect) return 1;
        var width = canvas.getBoundingClientRect().width;
        return width > 0 ? canvas.width / width : 1;
    },
    LeaderboardStage2_SafeInset: function (edge) {
        if (typeof document === 'undefined' || !document.body) return 0;
        var root = typeof window !== 'undefined' ? window : globalThis;
        var key = String(root.innerWidth) + ':' + root.innerHeight;
        var cached = root.__vodovorotStage2Safe;
        if (!cached || cached.key !== key) {
            var probe = document.createElement('div');
            probe.style.cssText = 'position:fixed;visibility:hidden;pointer-events:none;padding-left:env(safe-area-inset-left,0px);padding-right:env(safe-area-inset-right,0px);padding-top:env(safe-area-inset-top,0px);padding-bottom:env(safe-area-inset-bottom,0px);';
            document.body.appendChild(probe);
            var style = root.getComputedStyle(probe);
            cached = { key: key, values: [parseFloat(style.paddingLeft) || 0, parseFloat(style.paddingRight) || 0,
                parseFloat(style.paddingTop) || 0, parseFloat(style.paddingBottom) || 0] };
            probe.parentNode.removeChild(probe);
            root.__vodovorotStage2Safe = cached;
        }
        return cached.values[edge] || 0;
    },
    LeaderboardStage2_Request: function (objectPtr, requestId, playerPtr, xp) {
        var objectName = UTF8ToString(objectPtr);
        var expectedPlayer = UTF8ToString(playerPtr);
        var root = typeof window !== 'undefined' ? window : globalThis;
        var state = root.__vodovorotStage2LB || (root.__vodovorotStage2LB = {
            active: false, lastRequest: 0, lastSet: 0, reads: [], description: null, descriptionAt: 0,
            available: null, availabilityAt: 0
        });
        var answered = false;
        function reply(status, fields) {
            if (answered) return;
            answered = true;
            var data = Object.assign({ requestId: requestId, status: status, playerId: expectedPlayer,
                rank: 0, score: 0, submitted: false, entries: [] }, fields || {});
            SendMessage(objectName, 'OnLeaderboardResponse', JSON.stringify(data));
        }
        function wait(ms) { return new Promise(function (resolve) { setTimeout(resolve, ms); }); }
        function sdkTimeout(promise) {
            return new Promise(function (resolve, reject) {
                var timer = setTimeout(function () { reject({ code: 'STAGE2_TIMEOUT' }); }, 9000);
                Promise.resolve(promise).then(function (value) { clearTimeout(timer); resolve(value); },
                    function (error) { clearTimeout(timer); reject(error); });
            });
        }
        function noEntry(error) { return error && error.code === 'LEADERBOARD_PLAYER_NOT_PRESENT'; }
        function readEntry(sdk) {
            var now = Date.now();
            state.reads = state.reads.filter(function (at) { return now - at < 300001; });
            if (state.reads.length >= 60) return Promise.reject({ code: 'STAGE2_RATE_LIMIT' });
            state.reads.push(now);
            return sdkTimeout(sdk.leaderboards.getPlayerEntry('BestPlayers'));
        }
        function validEntry(entry) {
            return entry && Number.isSafeInteger(entry.rank) && entry.rank > 0 && entry.rank <= 2147483647 &&
                Number.isSafeInteger(entry.score) && entry.score >= 0 && entry.score <= 2147483647 &&
                entry.player && entry.player.uniqueID === expectedPlayer;
        }
        (async function () {
            var acquired = false;
            try {
                if (typeof ysdk === 'undefined' || !ysdk || !ysdk.leaderboards ||
                    typeof navigator !== 'undefined' && navigator.onLine === false) return reply('offline');
                if (state.active) return reply('busy');
                var now = Date.now();
                // At most nineteen own transactions in any five-minute interval.
                // Each uses <=3 own-entry reads and one surrounding-entries request.
                if (now - state.lastRequest < 16000) return reply('rate_limited');
                state.active = true; acquired = true; state.lastRequest = now;
                var sdk = ysdk;
                var player = await sdkTimeout(sdk.getPlayer());
                if (!player || !player.isAuthorized()) return reply('guest');
                if (player.getUniqueID() !== expectedPlayer) return reply('profile_changed');
                if (state.available === null || now - state.availabilityAt > 300000) {
                    state.available = await sdkTimeout(sdk.isAvailableMethod('leaderboards.setScore')) &&
                        await sdkTimeout(sdk.isAvailableMethod('leaderboards.getPlayerEntry'));
                    state.availabilityAt = Date.now();
                }
                if (!state.available) return reply('unavailable');
                if (!state.description || now - state.descriptionAt > 300000) {
                    state.description = await sdkTimeout(sdk.leaderboards.getDescription('BestPlayers'));
                    state.descriptionAt = Date.now();
                }
                var desc = state.description && state.description.description;
                if (!desc || desc.invert_sort_order !== false || !desc.score_format ||
                    desc.score_format.type !== 'numeric' || desc.score_format.options.decimal_offset !== 0)
                    return reply('configuration');
                var before = null;
                try { before = await readEntry(sdk); } catch (error) { if (!noEntry(error)) throw error; }
                if (before && !validEntry(before)) return reply('profile_changed');
                var delay = 1100 - (Date.now() - state.lastSet);
                if (delay > 0) await wait(delay);
                state.lastSet = Date.now();
                await sdkTimeout(sdk.leaderboards.setScore('BestPlayers', xp));
                var own = null;
                try { own = await readEntry(sdk); } catch (error) { if (!noEntry(error)) throw error; }
                if (!own || own.score < xp) {
                    await wait(750);
                    try { own = await readEntry(sdk); } catch (error) { if (!noEntry(error)) throw error; }
                }
                if (!own) return reply('no_entry', { submitted: true });
                if (!validEntry(own)) return reply('profile_changed');
                if (own.score < xp) return reply('pending', { submitted: true });
                var rows = [], neighborsAvailable = true;
                try {
                    var around = await sdkTimeout(sdk.leaderboards.getEntries('BestPlayers', {
                        quantityTop: 1, quantityAround: 2, includeUser: true
                    }));
                    (around.entries || []).forEach(function (entry) {
                        if (!entry || !entry.player || !Number.isSafeInteger(entry.rank) || entry.rank <= 0 ||
                            entry.rank > 2147483647 || !Number.isSafeInteger(entry.score) || entry.score < 0 ||
                            entry.score > 2147483647) return;
                        var allowName = entry.player.scopePermissions && entry.player.scopePermissions.public_name === 'allow';
                        rows.push({ rank: entry.rank, score: entry.score,
                            name: allowName && entry.player.publicName ? String(entry.player.publicName).slice(0, 32) : '',
                            isSelf: entry.player.uniqueID === expectedPlayer });
                    });
                } catch (_) { neighborsAvailable = false; }
                // Reading the own server entry, not a locally estimated table, confirms the rank.
                reply('confirmed', { submitted: true, rank: own.rank, score: own.score,
                    beforeRank: before ? before.rank : 0, beforeScore: before ? before.score : 0,
                    neighborsAvailable: neighborsAvailable, entries: rows });
            } catch (error) {
                var code = error && error.code;
                reply(code === 'STAGE2_RATE_LIMIT' ? 'rate_limited' : code === 'STAGE2_TIMEOUT' ? 'timeout' : 'unavailable');
            } finally {
                if (acquired) state.active = false;
            }
        })();
    }
});

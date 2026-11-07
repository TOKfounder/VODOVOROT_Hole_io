'use strict';
// Executes the actual .jslib with DOM/Unity-audio mocks. No browser rendering or audio claim.
const assert = require('node:assert/strict'), fs = require('fs'), path = require('path'), vm = require('vm');
const source = fs.readFileSync(path.resolve(__dirname, '../../Assets/Plugins/WebGL/Stage3MusicGate.jslib'), 'utf8');
let checks = 0;
function check(value, message) { assert.ok(value, message); checks++; }
function surface() {
  const listeners = new Map();
  return {
    listeners,
    addEventListener(type, callback) { if (!listeners.has(type)) listeners.set(type, new Set()); listeners.get(type).add(callback); },
    removeEventListener(type, callback) { listeners.get(type)?.delete(callback); },
    fire(type, event = {}) { for (const callback of [...(listeners.get(type) || [])]) callback(event); },
    count() { return [...listeners.values()].reduce((sum, entries) => sum + entries.size, 0); }
  };
}
function audioContext() {
  return Object.assign(surface(), {
    state: 'suspended', attempts: 0, deny: false,
    resume() {
      this.attempts++;
      if (this.deny) return Promise.reject(new Error('Permission unavailable'));
      this.state = 'running'; this.fire('statechange'); return Promise.resolve();
    }
  });
}
async function run() {
  const window = surface(), document = Object.assign(surface(), { hidden: false, focused: true, hasFocus() { return this.focused; } });
  const notices = [], library = {}, ctx = audioContext(); let elements = 0;
  const sandbox = { window, document, WEBAudio: { audioContext: ctx, audioCache: [] },
    LibraryManager: { library }, mergeInto: (target, values) => Object.assign(target, values), UTF8ToString: value => value,
    SendMessage: (receiver, method, state) => notices.push({ receiver, method, state: Number(state) }),
    Audio: class { constructor() { elements++; } }, console };
  vm.createContext(sandbox); vm.runInContext(source, sandbox);
  sandbox.VodovorotMusicGate = library.$VodovorotMusicGate;
  library.MusicStage3_Install('Stage3BackgroundMusic');
  check(library.MusicStage3_GetState() === 12, 'Auto-install must stay locked before a trusted gesture even in a focused visible tab.');
  check(ctx.attempts === 0 && elements === 0, 'Auto-install must not resume/create audible browser playback.');
  window.fire('pointerdown', { isTrusted: false });
  check(ctx.attempts === 0 && library.MusicStage3_GetState() === 12, 'Synthetic/untrusted input cannot unlock music.');
  window.fire('pointerdown', { isTrusted: true }); await Promise.resolve();
  check(ctx.attempts === 1 && library.MusicStage3_GetState() === 15, 'A trusted permitted gesture unlocks the existing Unity context.');
  check(elements === 2 && sandbox.WEBAudio.audioCache.length === 2, 'Safari-compatible compressed audio elements are created in the gesture and reuse the Unity cache.');
  check(notices.at(-1).receiver === 'Stage3BackgroundMusic' && notices.at(-1).method === 'OnBrowserAudioState', 'Notifications target only the existing soundtrack object.');
  document.focused = false; window.fire('blur');
  check(notices.at(-1).state === 7, 'Blur immediately notifies Unity to suspend without waiting for a frame.');
  document.hidden = true; document.fire('visibilitychange');
  check(notices.at(-1).state === 3 && ctx.attempts === 1, 'A hidden/unfocused tab cannot resume the context.');
  document.hidden = false; document.fire('visibilitychange');
  check(library.MusicStage3_GetState() === 7, 'Visibility alone cannot unlock a still-unfocused tab.');
  document.focused = true; ctx.state = 'suspended'; window.fire('focus'); await Promise.resolve();
  check(ctx.attempts === 2 && library.MusicStage3_GetState() === 15, 'Returning focus can resume a previously permitted context.');
  ctx.state = 'suspended'; ctx.deny = true; window.fire('keydown', { isTrusted: true }); await Promise.resolve();
  check(library.MusicStage3_GetState() === 13, 'A rejected browser resume must remain suspended without treating authorization as audible readiness.');
  ctx.deny = false; window.fire('touchstart', { isTrusted: true }); await Promise.resolve();
  check(library.MusicStage3_GetState() === 15 && elements === 2, 'The next permitted touch retries without duplicate sources/contexts.');
  const replacement = audioContext(); sandbox.WEBAudio.audioContext = replacement;
  check(library.MusicStage3_GetState() === 13 && ctx.count() === 0 && replacement.count() === 1, 'Device/context changes replace only the state listener; they retain the readiness gate.');
  const before = notices.length;
  library.MusicStage3_Uninstall();
  check(window.count() === 0 && document.count() === 0 && replacement.count() === 0, 'Teardown removes every DOM/context listener.');
  window.fire('pointerdown', { isTrusted: true });
  check(notices.length === before, 'Teardown cannot notify or restart a destroyed soundtrack player.');
  library.MusicStage3_Install('Stage3BackgroundMusic');
  const expectedWindowListeners = window.count();
  library.MusicStage3_Install('Stage3BackgroundMusic');
  check(window.count() === expectedWindowListeners && document.count() === 1, 'Repeated installation does not duplicate listeners.');
  check(library.MusicStage3_GetState() === 12, 'Reinstallation starts with explicit gesture permission cleared.');
  library.MusicStage3_Uninstall();
  console.log(`PASS: ${checks} actual browser music gate gesture/focus/visibility/lifecycle contracts`);
}
run().catch(error => { console.error(error); process.exitCode = 1; });

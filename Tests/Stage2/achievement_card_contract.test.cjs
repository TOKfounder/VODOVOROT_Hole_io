'use strict';
// Contract tests execute the actual Unity .jslib against SDK-free browser mocks.
// They do not replace the implementation and do not claim visual/browser rendering coverage.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const implementation = fs.readFileSync(path.join(__dirname, '../../Assets/Plugins/WebGL/VodovorotAchievement.jslib'), 'utf8');

function fixture(options = {}) {
  const state = { activation: false, shares: [], copies: [], downloads: [], created: [], revoked: [], blobs: [], timers: [], text: [], images: [] };
  const context2d = Object.fromEntries(['fillRect', 'beginPath', 'moveTo', 'arcTo', 'closePath', 'fill', 'ellipse'].map(k => [k, () => {}]));
  context2d.fillText = (...args) => state.text.push(String(args[0]));
  context2d.drawImage = image => state.images.push(image);
  let document;
  class Element {
    constructor(tag) {
      this.tagName = tag.toUpperCase(); this.children = []; this.parentNode = null;
      this.style = { cssText: '' }; this.attributes = {}; this.handlers = {}; this.disabled = false;
      this.textContent = ''; this.id = '';
    }
    setAttribute(name, value) { this.attributes[name] = String(value); }
    appendChild(child) { if (child.parentNode) child.remove(); child.parentNode = this; this.children.push(child); return child; }
    removeChild(child) { this.children.splice(this.children.indexOf(child), 1); child.parentNode = null; return child; }
    remove() { if (this.parentNode) this.parentNode.removeChild(this); }
    addEventListener(name, fn) { (this.handlers[name] ||= []).push(fn); }
    focus() { document.activeElement = this; }
    select() { this.selected = true; }
    descendants() { return this.children.flatMap(child => [child, ...child.descendants()]); }
    querySelectorAll(selector) {
      if (selector === 'button:not(:disabled),input') return this.descendants().filter(e => e.tagName === 'INPUT' || e.tagName === 'BUTTON' && !e.disabled);
      return this.descendants().filter(e => e.tagName.toLowerCase() === selector.toLowerCase());
    }
    click() {
      if (this.disabled) return;
      if (this.tagName === 'A') { state.downloads.push({ href: this.href, name: this.download, activation: state.activation }); return; }
      state.activation = true;
      try { for (const fn of this.handlers.click || []) fn({ type: 'click', target: this }); }
      finally { state.activation = false; }
    }
    getContext(type) { assert.equal(type, '2d'); return context2d; }
    toBlob(callback, type) { assert.equal(type, 'image/png'); state.blobs.push(() => callback(options.blobFailure ? null : { type: 'image/png', size: 1200 })); }
  }
  const handlers = new Map();
  document = {
    activeElement: null,
    createElement: tag => new Element(tag),
    getElementById: id => [document.body, ...document.body.descendants()].find(e => e.id === id) || null,
    addEventListener(name, fn) { if (!handlers.has(name)) handlers.set(name, new Set()); handlers.get(name).add(fn); },
    removeEventListener(name, fn) { handlers.get(name)?.delete(fn); },
    execCommand(command) { assert.equal(command, 'copy'); return options.legacyCopy === true; }
  };
  document.body = new Element('body');
  const previous = new Element('canvas'); document.body.appendChild(previous); previous.focus();
  const navigator = {};
  if (options.share !== false) {
    navigator.canShare = payload => options.fileShare !== false && Array.isArray(payload.files) && payload.files.length === 1;
    navigator.share = payload => {
      state.shares.push({ payload, activation: state.activation });
      if (options.shareError) return Promise.reject(Object.assign(new Error('share rejected'), { name: options.shareError }));
      return Promise.resolve();
    };
  }
  if (options.clipboard !== false) navigator.clipboard = { writeText: text => {
    state.copies.push(text); return options.clipboardDenied ? Promise.reject(new Error('denied')) : Promise.resolve();
  } };
  class ImageMock {
    set src(value) { this.value = value; if (options.imageError) this.onerror?.(); else this.onload?.(); }
  }
  class FileMock { constructor(parts, name, props) { this.parts = parts; this.name = name; this.type = props.type; } }
  const library = {};
  const sandbox = {
    LibraryManager: { library }, mergeInto: (target, source) => Object.assign(target, source), UTF8ToString: value => value,
    document, navigator, Image: ImageMock, File: FileMock,
    URL: { createObjectURL(blob) { const url = 'blob:fixture-' + (state.created.length + 1); state.created.push({ url, blob }); return url; },
      revokeObjectURL(url) { state.revoked.push(url); } },
    setTimeout: (fn, delay) => { state.timers.push({ fn, delay }); return state.timers.length; }
  };
  vm.runInNewContext(implementation, sandbox, { filename: 'VodovorotAchievement.jslib' });
  const defaults = { ru: false, percent: '63.2%', map: 'Garden', mode: 'Total Cleaning', skin: 'Golden Toilet', url: 'https://yandex.ru/games/app/473763', rank: 7, xp: 123 };
  return {
    state, document, previous, handlers,
    open(overrides = {}, portrait = '') { library.VodovorotAchievementOpen(JSON.stringify({ ...defaults, ...overrides }), portrait); },
    rawOpen(json) { library.VodovorotAchievementOpen(json, ''); },
    ready() { for (const callback of state.blobs.splice(0)) callback(); },
    timers() { for (const timer of state.timers.splice(0)) timer.fn(); },
    overlay() { return document.getElementById('vodovorot-achievement'); },
    button(label) { const found = this.overlay().querySelectorAll('button').find(b => b.textContent === label); assert.ok(found, 'Missing ' + label); return found; },
    key(key, shiftKey = false) { const event = { key, shiftKey, prevented: false, stopped: false, preventDefault() { this.prevented = true; }, stopPropagation() { this.stopped = true; } };
      for (const fn of handlers.get('keydown') || []) fn(event); return event; }
  };
}
const flush = async () => { await Promise.resolve(); await Promise.resolve(); await Promise.resolve(); };
const checks = [];
async function test(name, callback) {
  try { await callback(); checks.push({ name, passed: true }); }
  catch (error) { checks.push({ name, passed: false, error: error.message }); }
}

(async () => {
  await test('native_file_share_runs_in_real_click_activation_with_png_and_public_url', async () => {
    const f = fixture(); f.open({}, 'originalPortraitPng');
    assert.equal(f.button('Share').disabled, true); assert.equal(f.button('Save image').disabled, true);
    assert.equal(f.state.images.length, 1);
    f.ready(); f.button('Share').click(); await flush();
    assert.equal(f.state.shares.length, 1); const { payload, activation } = f.state.shares[0];
    assert.equal(activation, true); assert.equal(payload.files.length, 1);
    assert.equal(payload.files[0].type, 'image/png'); assert.equal(payload.files[0].name, 'vodovorot-achievement.png');
    assert.match(payload.text, /63\.2%/); assert.match(payload.text, /rank 7/); assert.match(payload.text, /https:\/\/yandex\.ru\/games\/app\/473763/);
    assert.equal(payload.text.includes('xp'), false); assert.equal(payload.text.includes('123'), false);
  });
  await test('link_share_when_file_share_unsupported_and_no_invented_unknown_rank', async () => {
    const f = fixture({ fileShare: false }); f.open({ rank: 0 }); f.ready(); f.button('Share').click(); await flush();
    const payload = f.state.shares[0].payload;
    assert.equal(payload.files, undefined); assert.equal(payload.url, 'https://yandex.ru/games/app/473763');
    assert.equal(payload.text.includes('rank'), false); assert.ok(f.state.text.includes('Growing with every game'));
  });
  await test('unsupported_web_share_keeps_real_download_and_copy_fallbacks', async () => {
    const f = fixture({ share: false }); f.open(); f.ready(); f.button('Share').click();
    assert.equal(f.state.shares.length, 0); f.button('Save image').click();
    assert.equal(f.state.downloads.length, 1); assert.equal(f.state.downloads[0].activation, true);
    assert.equal(f.state.downloads[0].name, 'vodovorot-achievement.png');
    f.button('Copy link').click(); await flush(); assert.equal(f.state.copies[0], 'https://yandex.ru/games/app/473763');
  });
  await test('clipboard_denial_has_one_reusable_selectable_manual_link', async () => {
    const f = fixture({ clipboardDenied: true }); f.open(); f.ready();
    for (let i = 0; i < 3; i++) { f.button('Copy link').click(); await flush(); }
    const fields = f.overlay().querySelectorAll('input');
    assert.equal(fields.length, 1, 'Repeated clipboard denial must reuse one manual-copy field');
    assert.equal(fields[0].value, 'https://yandex.ru/games/app/473763'); assert.equal(fields[0].selected, true);
  });
  await test('legacy_copy_success_removes_temporary_field', async () => {
    const f = fixture({ clipboard: false, legacyCopy: true }); f.open(); f.button('Copy link').click();
    assert.equal(f.overlay().querySelectorAll('input').length, 0);
  });
  await test('close_revokes_blob_removes_keyboard_listener_and_restores_focus', async () => {
    const f = fixture(); f.open(); f.ready();
    const event = f.key('Escape'); assert.equal(event.prevented, true); assert.equal(event.stopped, true);
    assert.equal(f.overlay(), null); assert.equal(f.handlers.get('keydown').size, 0); assert.equal(f.document.activeElement, f.previous);
    assert.equal(f.state.revoked.length, 0); f.timers(); assert.deepEqual(f.state.revoked, ['blob:fixture-1']);
  });
  await test('closing_before_async_blob_does_not_create_or_leak_object_url', async () => {
    const f = fixture(); f.open(); f.button('Close').click(); f.ready(); f.timers();
    assert.equal(f.state.created.length, 0); assert.equal(f.state.revoked.length, 0);
  });
  await test('reopening_replaces_dialog_and_old_url_only', async () => {
    const f = fixture(); f.open(); f.ready(); const old = f.overlay();
    f.open({ rank: 5 }); f.ready(); assert.notEqual(f.overlay(), old);
    assert.equal(f.document.body.descendants().filter(e => e.id === 'vodovorot-achievement').length, 1);
    assert.equal(f.handlers.get('keydown').size, 1); f.timers(); assert.deepEqual(f.state.revoked, ['blob:fixture-1']);
    assert.equal(f.state.created.length, 2);
  });
  await test('tab_focus_trap_and_mobile_css_bounds_are_explicit', async () => {
    const f = fixture(); f.open(); f.ready();
    f.button('Copy link').focus(); assert.equal(f.key('Tab').prevented, true); assert.equal(f.document.activeElement, f.button('Close'));
    assert.equal(f.key('Tab', true).prevented, true); assert.equal(f.document.activeElement, f.button('Copy link'));
    assert.match(f.overlay().style.cssText, /overflow:auto/); assert.match(f.overlay().style.cssText, /safe-area-inset-bottom/);
    assert.match(f.overlay().querySelectorAll('canvas')[0].style.cssText, /max-width:min\(100%,560px\)/);
    assert.match(f.overlay().querySelectorAll('canvas')[0].style.cssText, /100dvh/);
    assert.match(f.button('Share').style.cssText, /min-height:48px/);
  });
  await test('invalid_or_private_url_cannot_be_shared', async () => {
    const f = fixture(); f.open({ url: 'https://example.com/private?token=secret' }); f.ready(); f.button('Share').click(); await flush();
    assert.equal(f.state.shares[0].payload.text.includes('secret'), false);
    assert.match(f.state.shares[0].payload.text, /https:\/\/yandex\.ru\/games\/app\/473763/);
    f.button('Copy link').click(); await flush(); assert.equal(f.state.copies[0], 'https://yandex.ru/games/app/473763');
  });
  await test('share_cancellation_and_image_encoding_failure_do_not_claim_success', async () => {
    const canceled = fixture({ shareError: 'AbortError' }); canceled.open(); canceled.ready(); canceled.button('Share').click(); await flush();
    assert.ok(canceled.overlay().descendants().some(e => e.textContent === 'Sharing cancelled.'));
    const failed = fixture({ blobFailure: true }); failed.open(); failed.ready();
    assert.equal(failed.button('Share').disabled, true); assert.equal(failed.button('Save image').disabled, true);
    assert.equal(failed.state.created.length, 0); assert.equal(failed.button('Copy link').disabled, false);
  });
  const passed = checks.every(check => check.passed);
  process.stdout.write(JSON.stringify({ passed, implementation: 'Assets/Plugins/WebGL/VodovorotAchievement.jslib', checks }, null, 2) + '\n');
  process.exitCode = passed ? 0 : 1;
})();

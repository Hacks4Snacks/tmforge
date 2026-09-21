import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { test } from 'node:test';
import { studioHtml } from '../preview';

test('Studio replaces preview commands and is the sole default editor for both formats', () => {
  const manifest = JSON.parse(readFileSync(resolve(__dirname, '../../package.json'), 'utf8')) as {
    contributes: { customEditors: { viewType: string; priority: string; selector: { filenamePattern: string }[] }[] };
  };
  assert.equal(manifest.contributes.customEditors.length, 1);
  const editor = manifest.contributes.customEditors[0];
  assert.equal(editor.viewType, 'tmforge.studio');
  assert.equal(editor.priority, 'default');
  assert.deepEqual(editor.selector.map(item => item.filenamePattern), ['*.tm7', '*.tmforge.json']);
  assert.ok(!JSON.stringify(manifest.contributes).includes('Preview'));
});

test('Studio allows local UI assets and dynamic styles but no network or arbitrary scripts', () => {
  const html = studioHtml('vscode-webview:', 'https://local/studio.js?x="bad"', 'https://local/studio.css', 'studioNonce');
  assert.match(html, /default-src 'none'/);
  assert.match(html, /connect-src 'none'/);
  assert.match(html, /script-src 'nonce-studioNonce'/);
  assert.ok(!html.includes('unsafe-eval'));
  assert.match(html, /style-src vscode-webview: 'unsafe-inline'/);
  assert.match(html, /x=&quot;bad&quot;/);
});

import assert from 'node:assert/strict';
import { test } from 'node:test';
import { previewHtml, studioHtml } from '../preview';

test('webview has no network or workspace capability and escapes resource attributes', () => {
  const html = previewHtml('vscode-webview:', 'https://local/script.js?x="bad"', 'https://local/style.css', 'testNonce');
  assert.match(html, /default-src 'none'/);
  assert.match(html, /connect-src 'none'/);
  assert.match(html, /img-src blob:/);
  assert.match(html, /script-src 'nonce-testNonce'/);
  assert.ok(!html.includes('unsafe-eval') && !html.includes('unsafe-inline'));
  assert.match(html, /x=&quot;bad&quot;/);
  assert.ok(!html.includes('acquireVsCodeApi()'));
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

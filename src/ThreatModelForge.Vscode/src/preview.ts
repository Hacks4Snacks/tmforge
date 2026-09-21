const attribute = (value: string) => value.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');

export function studioHtml(cspSource: string, script: string, style: string, nonce: string): string {
  return `<!doctype html>
<html lang="en"><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src ${attribute(cspSource)} data: blob:; style-src ${attribute(cspSource)} 'unsafe-inline'; font-src ${attribute(cspSource)}; script-src 'nonce-${attribute(nonce)}'; connect-src 'none'; base-uri 'none'; form-action 'none';">
<link rel="stylesheet" href="${attribute(style)}"><title>Threat Model Forge Studio</title></head>
<body><div id="root"></div><script nonce="${attribute(nonce)}" type="module" src="${attribute(script)}"></script></body></html>`;
}

export function previewHtml(cspSource: string, script: string, style: string, nonce: string): string {
  return `<!doctype html>
<html lang="en"><head><meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src blob:; style-src ${attribute(cspSource)}; script-src 'nonce-${attribute(nonce)}'; connect-src 'none'; base-uri 'none'; form-action 'none';">
<link rel="stylesheet" href="${attribute(style)}"><title>Threat Model Forge Preview</title></head>
<body>
<header><strong id="title">Threat Model Forge</strong><span class="spacer"></span>
<button id="reanalyze" type="button">Reanalyze</button><button id="source" type="button">Open source</button><button id="problems" type="button">Problems</button></header>
<p id="status" role="status">Loading local engine...</p>
<section aria-label="Diagram preview"><div class="diagram-tools"><label>Diagram <select id="page" disabled></select></label>
<label>Zoom <input id="zoom" type="range" min="50" max="200" step="10" value="100"></label><button id="fit" type="button">Fit</button></div>
<div id="diagram"><img id="image" alt="Threat model diagram" hidden></div></section>
<section aria-label="Analysis findings"><div class="finding-tools"><h1 id="summary">Findings</h1><span class="spacer"></span>
<label>Severity <select id="severity"><option value="all">All</option><option value="error">Errors</option><option value="warning">Warnings</option><option value="info">Information</option></select></label></div>
<ul id="input-diagnostics"></ul><table><thead><tr><th>Severity</th><th>Rule</th><th>Finding</th></tr></thead><tbody id="findings"></tbody></table></section>
<script nonce="${attribute(nonce)}" src="${attribute(script)}"></script></body></html>`;
}

const vscode = acquireVsCodeApi();
const controls = Object.fromEntries(['title', 'status', 'page', 'zoom', 'fit', 'image', 'summary', 'severity', 'findings', 'input-diagnostics'].map(id => [id, document.getElementById(id)]));
let result;
let imageUrl;
const saved = vscode.getState() ?? {};
controls.zoom.value = String(Math.max(50, Math.min(200, Number(saved.zoom) || 100)));
controls.severity.value = ['all', 'error', 'warning', 'info'].includes(saved.severity) ? saved.severity : 'all';

function persist() {
  vscode.setState({ page: controls.page.value, zoom: controls.zoom.value, severity: controls.severity.value });
}

function sizeImage() {
  if (!controls.image.naturalWidth || !controls.image.naturalHeight) return;
  const width = Math.max(1, document.getElementById('diagram').clientWidth - 28);
  const height = Math.max(120, window.innerHeight * 0.65 - 28);
  const fitted = Math.min(width, controls.image.naturalWidth, height * controls.image.naturalWidth / controls.image.naturalHeight);
  controls.image.width = Math.max(1, Math.round(fitted * Number(controls.zoom.value) / 100));
  persist();
}

function showPage() {
  if (imageUrl) URL.revokeObjectURL(imageUrl);
  imageUrl = undefined;
  const page = result?.pages?.find(page => page.id === controls.page.value);
  controls.image.hidden = !page;
  if (!page) { controls.image.removeAttribute('src'); return; }
  imageUrl = URL.createObjectURL(new Blob([page.svg], { type: 'image/svg+xml' }));
  controls.image.src = imageUrl;
  controls.image.alt = page.name;
  persist();
}

function showFindings() {
  controls.findings.replaceChildren();
  const findings = result?.analysis?.findings ?? [];
  const visible = findings.filter(finding => controls.severity.value === 'all' || finding.severity === controls.severity.value);
  controls.summary.textContent = result?.analysis ? `Findings (${visible.length} of ${findings.length})` : 'Analysis unavailable';
  for (const finding of visible) {
    const row = document.createElement('tr');
    for (const [field, value] of [['severity', finding.severity], ['rule', finding.ruleId ?? finding.id], ['message', finding.message]]) {
      const cell = document.createElement('td');
      cell.className = field;
      cell.textContent = value;
      if (field === 'message' && finding.elementIds?.length) {
        const ids = document.createElement('small');
        ids.textContent = `Elements: ${finding.elementIds.join(', ')}`;
        cell.append(ids);
      }
      row.append(cell);
    }
    controls.findings.append(row);
  }
  persist();
}

window.addEventListener('message', event => {
  const message = event.data;
  if (message?.type !== 'update') return;
  controls.title.textContent = message.title;
  controls.status.textContent = message.status ?? '';
  controls.status.hidden = !message.status;
  result = message.result;
  const selected = controls.page.value || saved.page;
  controls.page.replaceChildren();
  for (const page of result?.pages ?? []) {
    const option = document.createElement('option');
    option.value = page.id;
    option.textContent = page.name;
    controls.page.append(option);
  }
  controls.page.disabled = !(result?.pages?.length);
  if (result?.pages?.some(page => page.id === selected)) controls.page.value = selected;
  controls['input-diagnostics'].replaceChildren();
  for (const diagnostic of [...(result?.diagnostics ?? []), ...(result?.analysis?.diagnostics ?? []).map(message => ({ severity: 'error', code: 'analysis.incomplete', message }))]) {
    const item = document.createElement('li');
    item.textContent = `${diagnostic.severity} ${diagnostic.code}${diagnostic.path ? ' ' + diagnostic.path : ''}: ${diagnostic.message}`;
    controls['input-diagnostics'].append(item);
  }
  showPage();
  showFindings();
});
controls.page.addEventListener('change', showPage);
controls.image.addEventListener('load', sizeImage);
controls.zoom.addEventListener('input', sizeImage);
controls.fit.addEventListener('click', () => { controls.zoom.value = '100'; sizeImage(); });
controls.severity.addEventListener('change', showFindings);
window.addEventListener('resize', sizeImage);
for (const type of ['reanalyze', 'source', 'problems']) document.getElementById(type).addEventListener('click', () => vscode.postMessage({ type }));
vscode.postMessage({ type: 'ready' });

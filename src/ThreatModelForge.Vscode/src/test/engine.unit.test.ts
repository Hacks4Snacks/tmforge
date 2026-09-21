import assert from 'node:assert/strict';
import { readFile, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { test } from 'node:test';
import { EngineWorker, MAX_DOCUMENT_BYTES, MAX_REQUEST_BYTES } from '../engine';

const runtime = resolve(__dirname, '../../engine/_framework');
const sourceWorker = resolve(__dirname, '../engine-worker.mjs');

test('bundled WASM inspects the real webshop deterministically without a .NET process', async () => {
  const worker = new EngineWorker(runtime, 30000, sourceWorker);
  try {
    const bytes = await readFile(resolve(__dirname, '../../../../examples/webshop.tm7'));
    const before = Buffer.from(bytes);
    const first = await worker.inspect(bytes, 'tm7');
    const second = await worker.inspect(bytes, 'tm7');
    assert.deepEqual(first, second);
    assert.equal(first.pages.length, 1);
    assert.match(first.pages[0].svg, /<svg/);
    assert.match(first.pages[0].svg, /Orders API/);
    assert.ok(first.analysis?.findings.some(finding => finding.ruleId === 'TM1029'));
    assert.ok(first.analysis?.findings.some(finding => finding.elementIds.length));
    assert.deepEqual(bytes, before);
  } finally { worker.dispose(); }
});

test('invalid model input reports errors, then the same worker can inspect another model', async () => {
  const worker = new EngineWorker(runtime, 30000, sourceWorker);
  try {
    const invalid = await worker.inspect(Buffer.from('{"schema":"tmforge-json","elements":[],"flows":[{"id":"broken","source":"a","target":"b"}]}'), 'tmforge-json');
    assert.equal(invalid.pages.length, 0);
    assert.equal(invalid.analysis, null);
    assert.ok(invalid.diagnostics.some(diagnostic => diagnostic.severity === 'error'));
    const valid = await worker.inspect(Buffer.from('{"schema":"tmforge-json","elements":[{"id":"api","kind":"process","name":"API","x":10,"y":20}],"flows":[]}'), 'tmforge-json');
    assert.equal(valid.pages.length, 1);
    assert.ok(valid.analysis?.findings.length);
    assert.ok(!valid.analysis?.findings.some(finding => finding.id === 'engine-error'));
  } finally { worker.dispose(); }
});

test('document limits and disposal fail explicitly without starting an engine', async () => {
  const worker = new EngineWorker('/not/a/runtime', 100, sourceWorker);
  await assert.rejects(worker.inspect(new Uint8Array(MAX_DOCUMENT_BYTES + 1)), /8 MiB/);
  await assert.rejects(worker.invoke('constructor'), /Unsupported/);
  await assert.rejects(worker.invoke('ReadFile', []), /Unsupported/);
  await assert.rejects(worker.invoke('Analyze', ['x'.repeat(MAX_REQUEST_BYTES + 1)]), /16 MiB/);
  worker.dispose();
  await assert.rejects(worker.inspect(new Uint8Array()), /closed/);
  await assert.rejects(worker.invoke('Formats'), /closed/);
});

test('bundled worker supports Studio catalogs, analysis and native export', async () => {
  const worker = new EngineWorker(runtime, 30000, sourceWorker);
  try {
    const formats = JSON.parse(await worker.invoke('Formats')) as { id: string }[];
    assert.ok(formats.some(format => format.id === 'tmforge-json'));
    assert.ok((JSON.parse(await worker.invoke('Stencils')) as unknown[]).length > 0);
    assert.equal(await worker.invoke('Detect', [Buffer.from('not a model').toString('base64')]), '');
    const bytes = await readFile(resolve(__dirname, '../../../../examples/webshop.tm7'));
    const model = await worker.invoke('ReadFile', [bytes.toString('base64'), 'tm7']);
    const analysis = JSON.parse(await worker.invoke('Analysis', [model])) as { findings: unknown[] };
    assert.ok(analysis.findings.length > 0);
    const exported = Buffer.from(await worker.invoke('ExportTm7', [model]), 'base64');
    assert.equal((await worker.inspect(exported, 'tm7')).pages.length, 1);
  } finally { worker.dispose(); }
});

test('native saves retain source bytes, opaque XML and previous native edits', async () => {
  const worker = new EngineWorker(runtime, 30000, sourceWorker);
  try {
    const source = await readFile(resolve(__dirname, '../../../../examples/webshop.tm7'));
    const original = Buffer.from(source.toString().replace('</ThreatModel>', '<Extension xmlns="urn:tmforge:test">kept</Extension></ThreatModel>'));
    const content = original.toString('base64');
    const baseline = await worker.invoke('ReadFile', [content, 'tm7']);
    assert.deepEqual(Buffer.from(await worker.invoke('SaveTm7', [content, baseline]), 'base64'), original);
    for (const xml of [
      '<WrongRoot xmlns="http://schemas.datacontract.org/2004/07/ThreatModeling.Model"/>',
      '<ThreatModel xmlns="urn:untrusted"/>',
      '<!DOCTYPE ThreatModel [<!ENTITY data SYSTEM "file:///not-read">]><ThreatModel>&data;</ThreatModel>',
    ]) {
      const invalid = Buffer.from(xml).toString('base64');
      await assert.rejects(worker.invoke('SaveTm7', [invalid, baseline]), /native TM7 XML is invalid/);
      await assert.rejects(worker.invoke('SaveTm7WithPrevious', [content, baseline, invalid]), /native TM7 XML is invalid/);
    }
    const edited = JSON.parse(baseline);
    const page = edited.diagrams[0];
    page.elements.find((element: { name: string }) => element.name === 'Orders API').name = 'Native VS Code API';
    edited.elements = page.elements;
    const first = await worker.invoke('SaveTm7', [content, JSON.stringify(edited)]);
    assert.match(Buffer.from(first, 'base64').toString(), /<Extension xmlns="urn:tmforge:test">kept<\/Extension>/);
    const next = JSON.parse(await worker.invoke('ReadFile', [first, 'tm7']));
    next.diagrams[0].name = 'Edited page';
    const second = await worker.invoke('SaveTm7WithPrevious', [content, JSON.stringify(next), first]);
    const restored = JSON.parse(await worker.invoke('ReadFile', [second, 'tm7']));
    assert.equal(restored.diagrams[0].name, 'Edited page');
    assert.ok(restored.elements.some((element: { name: string }) => element.name === 'Native VS Code API'));
    assert.match(Buffer.from(second, 'base64').toString(), /<Extension xmlns="urn:tmforge:test">kept<\/Extension>/);
  } finally { worker.dispose(); }
});

test('timeouts stop a stalled worker and reject all queued requests', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'tmforge-worker-'));
  const workerPath = join(directory, 'stalled.mjs');
  await writeFile(workerPath, "import { parentPort } from 'node:worker_threads'; parentPort.on('message', () => {});");
  const worker = new EngineWorker(runtime, 100, workerPath);
  try {
    const results = await Promise.allSettled([worker.inspect(new Uint8Array()), worker.inspect(new Uint8Array())]);
    assert.ok(results.every(result => result.status === 'rejected' && /timed out/.test(result.reason.message)));
    await assert.rejects(worker.inspect(new Uint8Array()), /timed out/);
  } finally {
    worker.dispose();
    await rm(directory, { recursive: true, force: true });
  }
});

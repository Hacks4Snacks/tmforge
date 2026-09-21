import { access, readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const manifest = JSON.parse(await readFile(resolve(root, 'package.json'), 'utf8'));
for (const path of [manifest.main, 'out/engine-worker.mjs', 'engine/package.json', 'engine/_framework/dotnet.js', 'engine/_framework/dotnet.native.wasm', 'engine/_framework/ThreatModelForge.Wasm.wasm', 'engine/_framework/ThreatModelForge.Engine.wasm', 'media/studio/studio.js', 'media/studio/studio.css', 'schemas/tmforge-rules-v2.schema.json', 'LICENSE.md', 'NOTICE', ...manifest.contributes.jsonValidation.map(entry => entry.url)]) {
  await access(resolve(root, path));
}
if (manifest.dependencies && Object.keys(manifest.dependencies).length) throw new Error('Runtime dependencies require explicit VSIX packaging support.');
if (/process\.env/.test(await readFile(resolve(root, 'media/studio/studio.js'), 'utf8'))) throw new Error('Studio must be bundled for the browser, without Node globals.');
console.log('VSIX inputs verified; bundled WASM runtime and Studio are present.');

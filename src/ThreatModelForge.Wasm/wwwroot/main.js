// Standalone smoke page for the WASM engine host. The Studio consumes the engine via its
// WasmEngineClient; this page proves the FULL engine surface works in-browser through the shared
// EngineService — catalogs, validation, format read/write, conversion, and reports.
import { dotnet } from './_framework/dotnet.js'

const { getAssemblyExports, getConfig } = await dotnet.create();
const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const engine = exports.ThreatModelForge.Wasm.Engine;

const out = document.getElementById('out');
const result = document.getElementById('result');
const log = (msg) => { out.textContent += msg + '\n'; };
let pass = true;
const check = (label, ok, detail) => {
  pass = pass && ok;
  log(`${ok ? 'PASS' : 'FAIL'}  ${label}${detail ? ' — ' + detail : ''}`);
};

// A minimal canonical model — no external fixture needed for the smoke page.
const model = {
  schema: 'tmforge-json',
  version: '0.1',
  elements: [
    { id: 'p1', kind: 'process', name: 'API', x: 0, y: 0, properties: {} },
    { id: 'd1', kind: 'datastore', name: 'DB', x: 200, y: 0, properties: {} },
  ],
  flows: [
    { id: 'f1', source: 'p1', target: 'd1', name: 'query', properties: {} },
  ],
};

try {
  log(engine.Ping());
  const json = JSON.stringify(model);

  // Catalogs
  const formats = JSON.parse(engine.Formats());
  check('Formats', Array.isArray(formats) && formats.length > 0, `${formats.length} formats`);
  check('Stencils', JSON.parse(engine.Stencils()).length > 0);
  check('Rules', JSON.parse(engine.Rules()).length > 0);
  check('RulePacks', JSON.parse(engine.RulePacks()).length > 0);
  check('PropertySchema', JSON.parse(engine.PropertySchema()).length > 0);

  // Validation (real reflection-loaded rule engine)
  const findings = JSON.parse(engine.Validate(json));
  check('Validate', !findings.find((f) => f.id === 'engine-error'), `${findings.length} findings`);

  // Export .tm7 -> Detect -> ReadFile round-trip
  const tm7 = engine.ExportTm7(json);
  const detected = JSON.parse(engine.Detect(tm7) || 'null');
  check('Detect(.tm7)', detected && detected.id === 'tm7', detected ? detected.id : 'none');
  const back = JSON.parse(engine.ReadFile(tm7, 'tm7'));
  check('ExportTm7 -> ReadFile',
    back.elements.length === model.elements.length && back.flows.length === model.flows.length,
    `${back.elements.length}/${back.flows.length} elements/flows`);

  // Conversion + reports
  check('Convert(drawio)', engine.ConvertModel(json, 'drawio').length > 0);
  check('Report(html)', engine.Report(json, 'html').length > 0);
  check('Report(svg)', engine.Report(json, 'svg').length > 0);
} catch (e) {
  pass = false;
  log('EXCEPTION: ' + (e && e.stack ? e.stack : e));
}

result.textContent = pass ? 'ALL PASS' : 'FAIL';
result.style.color = pass ? 'green' : 'red';

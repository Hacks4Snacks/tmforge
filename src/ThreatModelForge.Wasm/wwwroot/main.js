import { dotnet } from './_framework/dotnet.js'

const { getAssemblyExports, getConfig } = await dotnet.create();
const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const engine = exports.ThreatModelForge.Wasm.Engine;

const out = document.getElementById('out');
const result = document.getElementById('result');
const log = (msg) => { out.textContent += msg + '\n'; };

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

let pass = true;
try {
  log(engine.Ping());
  const json = JSON.stringify(model);

  // Validate via the real reflection-loaded rule engine.
  const findings = JSON.parse(engine.Validate(json));
  const engineError = findings.find((f) => f.id === 'engine-error');
  const validateOk = !engineError;
  pass = pass && validateOk;
  log(`Validate: ${findings.length} findings${engineError ? ' - ' + engineError.message : ''}: ${validateOk ? 'PASS' : 'FAIL'}`);

  // tmforge-json -> .tm7 (DataContractSerializer) -> tmforge-json, preserving the graph.
  const tm7 = engine.WriteTm7(json);
  const back = JSON.parse(engine.ReadTm7(tm7));
  const roundTripOk = tm7.length > 0 && back.elements.length === model.elements.length && back.flows.length === model.flows.length;
  pass = pass && roundTripOk;
  log(`WriteTm7 -> ReadTm7 preserves ${back.elements.length}/${back.flows.length} elements/flows: ${roundTripOk ? 'PASS' : 'FAIL'}`);
} catch (e) {
  pass = false;
  log('EXCEPTION: ' + (e && e.stack ? e.stack : e));
}

result.textContent = pass ? 'ALL PASS' : 'FAIL';
result.style.color = pass ? 'green' : 'red';

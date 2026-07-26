# MTMT interaction-scope differentials

These fixtures lock down Microsoft Threat Modeling Tool (MTMT) behavior that cannot be inferred from
the `.tb7` grammar alone. No Microsoft binaries or templates are stored in this directory.

## Endpoint-only filters

The per-interaction contract is evidenced by Microsoft
[`tm-file-parser`](https://github.com/microsoft/tm-file-parser) fixture `sample1.tm7` at commit
`654c759b3ef2538220508ca390bf0e94c12e4d24` (file SHA-256
`52281d0f7b3368cdd363f82a7222efb9fb98adae79fd82f83682311c431ce739`). It persists two `TH53`
threats for the same source and target:

- source `d19592b5-fb5d-47a8-ab2f-45f6865332d5`
- target `72dd0225-194c-4817-8512-36bb028fa529`
- flow `4bc02f7f-8c06-448b-a9ae-a21dbc2f15b1`
- flow `7b771d6c-ba83-43cb-8888-cad14a06e895`

`DeclarativeRuleProviderTests.EndpointOnlyMtmtFilterEvaluatesOncePerMatchingFlow` reproduces that
scope: endpoint-only expressions yield one finding per matching connector, not one per endpoint.

## ROOT scope

`root-scope.tmforge.json` contains one connector on Diagram A and two parallel connectors on Diagram
B. That asymmetry distinguishes once-per-diagram from once-per-interaction generation. The Windows-only
`capture-mtmt-root-scope.ps1` script:

1. downloads MTMT `7.3.51110.1` from Microsoft's versioned ClickOnce distribution;
2. verifies the application manifest SHA-256 and every downloaded payload against the manifest;
3. embeds that release's `KnowledgeBase/Default.tb7` in the fixture through tmforge;
4. loads the model through MTMT's public `ObjectModel(StorageFile, bool)` API;
5. calls public `ConfigureThreatGeneration(true)` and `GenerateThreats()`; and
6. writes normalized JSON containing version and fixture provenance plus generated threat counts,
   titles, keys, diagrams, and interaction scope.

Run from the repository root on Windows with .NET Framework 4.8 and the .NET 10 SDK:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  test/ThreatModelForge.Analysis.Tests/Fixtures/MtmtDifferential/capture-mtmt-root-scope.ps1
```

The script relaunches itself under 32-bit Windows PowerShell in STA mode because the signed MTMT model
assembly is x86 and derives model types from WPF. By default, the downloaded payload remains in a
fresh random directory under the system temp directory and is never added to the repository. A
caller-supplied `-WorkingDirectory` must not already exist.

Manifest path confinement and payload digest verification can be exercised without loading MTMT or
requiring Windows:

```powershell
pwsh -NoProfile -File `
  test/ThreatModelForge.Analysis.Tests/Fixtures/MtmtDifferential/capture-mtmt-root-scope.ps1 `
  -Preflight
```

Interpret the normalized `rootThreatCount` as follows:

- `12`, exactly one each of `SU`, `TU`, `RU`, `IU`, `DU`, and `EU` on both diagrams: per-diagram ROOT
  scope;
- `18`, one of each ROOT type on Diagram A and two of each on Diagram B: per-interaction scope;
- `6`, exactly one of each ROOT type: model-wide ROOT scope;
- `0`: the migrated ROOT definitions are not generated; or
- any other result: an unexpected contract requiring investigation.

## Captured result

`root-scope.mtmt.json` is the committed capture from MTMT `7.3.51110.1`. It records
`rootThreatCount: 0` and `interpretation: "not-generated"`, alongside the six ROOT threat types the
knowledge base declares with the filter `source is 'ROOT'`. The types were available to the run and
produced nothing.

That is the tool's own behavior, not a defect in either product. `KnowledgeBaseModel.GetElementTypeChain`
walks an element's parents and stops before appending the virtual `ROOT` type, `PopulateNamespace`
publishes that chain as `SOURCE.OBJTYPE`, and the script host's `IS` operator tests it by equality or
`:`-delimited containment. `source is 'ROOT'` therefore cannot hold for any element, which leaves the
six types inert. Their own descriptions record them as migrated from version 3.

The capture also shows the tool generating strictly once per interaction: Diagram A holds one
connector and yields 21 threats, Diagram B holds two and yields 42. There is no per-diagram or
model-wide generation path in the tool at all — `GenerateThreatsForDrawingSurface` iterates the
connectors on a surface and evaluates every threat type against each one.

**Threat Model Forge deliberately diverges.** It keeps the six ROOT rules live and evaluates each once
per diagram, so this fixture yields 12 findings where the tool yields none. This is a decision to keep
STRIDE-per-diagram coverage the tool lost, not a parity claim; the imported ROOT rules are Threat Model
Forge behavior. `MtmtRootDifferentialTests` locks both halves — what the capture recorded and what the
engine does — so neither side can drift without restating the decision.

Because a ROOT rule cannot fire in the tool, the six rules never contribute threats to an exported
`.tm7`, so the divergence does not affect what the tool reads back.

# `src/`: Threat Model Forge projects

This directory holds the shipping libraries, the `tmforge` CLI, the engine API, and the
Studio front end, plus the VS Code extension. The .NET build is driven by [`dirs.proj`](dirs.proj)
(`Microsoft.Build.Traversal`); every project is also listed in
[`ThreatModelForge.slnx`](../ThreatModelForge.slnx) for IDE users.

The canonical in-memory model is the `.tm7`-shaped object graph in `ThreatModelForge.Core`.
Everything else layers on top of it: formats read/write it, analysis inspects it, reporting
renders it, editing mutates it, and the CLI and API expose it.

## Libraries

| Project | Role |
| --- | --- |
| [`ThreatModelForge.Core`](ThreatModelForge.Core) | The `ThreatModel` object graph and its byte-for-byte-lossless `.tm7`/`.tb7` IO (`DataContractSerializer`), the knowledge-base types, and shared serialization abstractions. |
| [`ThreatModelForge.Formats`](ThreatModelForge.Formats) | Pluggable format layer (`IThreatModelFormat` + `ThreatModelFormatRegistry`). `.tm7` is the first provider; draw.io, Visio (`.vsdx`), and `tmforge-json` map to and from the canonical model. |
| [`ThreatModelForge.Analysis`](ThreatModelForge.Analysis) | The analysis object model: the base rule types that rule sets derive from, plus the machinery `tmforge analyze` uses to evaluate a model. |
| [`ThreatModelForge.Analysis.Rules`](ThreatModelForge.Analysis.Rules) | The built-in rule set: completeness/hygiene checks and security-property checks. |
| [`ThreatModelForge.Analysis.Reporting`](ThreatModelForge.Analysis.Reporting) | Report writers for analysis *findings*: SARIF (for CI/code-scanning) and self-contained HTML. |
| [`ThreatModelForge.Reporting`](ThreatModelForge.Reporting) | Renders the threat *model itself* to a self-contained HTML report with inline SVG diagrams (no native graphics libraries). |
| [`ThreatModelForge.Editing`](ThreatModelForge.Editing) | UI-agnostic editing operations (add/move/rename/delete, connectors, layout) with snapshot undo/redo, so a web UI, the CLI, or tests can drive edits identically. |

## Applications

| Project | Role |
| --- | --- |
| [`ThreatModelForge.Cli`](ThreatModelForge.Cli) | The `tmforge` command-line tool: inspect, author, lint, report, and convert threat models, with `--json` for machine-readable output. |
| [`ThreatModelForge.Api`](ThreatModelForge.Api) | The engine API host: a versioned `/v1` HTTP surface over the engine, and the host that serves the Studio SPA from `wwwroot`. |
| [`ThreatModelForge.Studio`](ThreatModelForge.Studio) | The front end: a React + TypeScript single-page app whose DFD canvas is built on React Flow. It talks to the engine only through the generated `/v1` client. |
| [`ThreatModelForge.Vscode`](ThreatModelForge.Vscode) | The desktop VS Code extension: Studio editing for canonical JSON, native read-only previews, open/save findings, and JSON schema hints, using a bundled WebAssembly engine. |

## Tests

Tests live in [`../test`](../test), one `*.Tests` project per shipping library and
application. Build and run everything from the repo root:

```bash
dotnet build ../dirs.proj
dotnet test  ../dirs.proj --no-build
```

## VS Code Extension Development

The extension's [README](ThreatModelForge.Vscode/README.md) covers installation and usage.
Contributors need Node.js 22+, the repository's .NET SDK, and `wasm-tools` / `wasm-experimental`.
From the repository root:

```bash
dotnet publish src/ThreatModelForge.Wasm/ThreatModelForge.Wasm.csproj -c Release -o artifacts/vscode-wasm
npm --prefix src/ThreatModelForge.Studio ci
npm --prefix src/ThreatModelForge.Vscode ci
node src/ThreatModelForge.Vscode/scripts/stage-engine.mjs
npm --prefix src/ThreatModelForge.Vscode test
npm --prefix src/ThreatModelForge.Vscode run test:extension
npm --prefix src/ThreatModelForge.Vscode run package
```

`stage-engine.mjs` optionally accepts the path to an already-published `_framework` directory.
Generated runtime assets and VSIX files are ignored by Git. Packaging refuses to proceed if required
runtime assets or schemas are missing. Tests use an isolated VS Code profile; set
`VSCODE_EXECUTABLE_PATH` to reuse a specific installation, otherwise the test runner can download
VS Code. On Linux, run extension-host tests under `xvfb-run -a` when no display is available.

CI builds a VSIX artifact; Marketplace publication is a separate, deliberate release step.

Packaging builds the shared Studio UI through `npm run build:extension` into the extension's ignored
`media/studio/` directory. `EditorHost` supplies the document, engine, and host actions; the standalone
browser path remains unchanged. `WasmEngineClient` shares result decoding across local and asynchronous
worker transports.

Canonical JSON uses `CustomTextEditorProvider`: versioned canvas deltas become `WorkspaceEdit`s,
preserving unrepresented JSON fields. VS Code owns save, dirty state, undo/redo, and recovery.
Each open Studio document has an isolated engine/rule session shared by its views. The webview loads
only packaged assets; CSP blocks network access and arbitrary scripts. Inline styles are needed by
React Flow. File reads/writes use VS Code dialogs and workspace APIs, not arbitrary webview paths.

Native `.tm7` previews receive SVG images and inert finding text only. Raw-file inspection retains
line boundaries and geometry without round-tripping through the Studio projection. Native imports
create new JSON documents; exports never overwrite the currently edited document.

Inspection limits are 8 MiB input, 32 pages, 1,024 elements, 2,048 lines, one million element/line pairs,
16 MiB results, and 30 seconds per queued inspection. A timeout stops the worker; retry restarts it.
Native WebAssembly memory is not covered by Node's JavaScript heap limit.

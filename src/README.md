# `src/`: Threat Model Forge projects

This directory holds the shipping libraries, the `tmforge` CLI, the engine API, and the
Studio front end. The build is driven by [`dirs.proj`](dirs.proj)
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

## Tests

Tests live in [`../test`](../test), one `*.Tests` project per shipping library and
application. Build and run everything from the repo root:

```bash
dotnet build ../dirs.proj
dotnet test  ../dirs.proj --no-build
```

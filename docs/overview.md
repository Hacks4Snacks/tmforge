# Overview & features

Threat Model Forge (`tmforge`) is a toolkit for **authoring, validating, and reporting on
threat models** across platforms. It reads and writes Microsoft Threat Modeling Tool `.tm7`
files losslessly, adds a browser authoring experience and a headless CLI, and validates models
against a built-in rule set you can gate a CI build on.

## Why Threat Model Forge

The Microsoft Threat Modeling Tool (MTMT) is Windows-only and GUI-only. Threat Model Forge keeps
its file format and fidelity while removing those constraints:

- **Cross-platform.** Native binaries and container images for Linux, macOS, and Windows (x64 and
  arm64). No Windows dependency.
- **Automatable.** A headless CLI and an HTTP API let agents and CI pipelines author and validate
  models without a GUI ("threat-model-as-code").
- **Lossless `.tm7`.** Reads and writes `.tm7` **byte-for-byte** identically to MTMT, so models
  move between tools without drift.
- **Pluggable formats.** Beyond `.tm7`, import/export draw.io and Visio for interoperability, plus
  a canonical JSON wire format.
- **CI-grade validation.** A built-in rule set flags completeness and security-hygiene issues, with
  SARIF and HTML reports and meaningful exit codes.

## The three surfaces

Everything runs on one .NET engine over a single canonical, `.tm7`-shaped in-memory model.

### 1. CLI (`tmforge`)

The headless, scriptable face. Inspect, author, validate, report on, and convert models from a
shell or CI pipeline. Every command supports `--json` for machine-readable output and stable exit
codes so agents and pipelines can drive it deterministically. See the
[CLI reference](cli-reference.md).

### 2. Studio (browser authoring)

A React single-page app whose data-flow-diagram canvas is built on React Flow. Drag stencils onto
a canvas, draw data flows, rename and resize elements, and validate against the live engine with
findings overlaid on the offending nodes and edges. See the [Studio guide](studio-guide.md).

### 3. Engine API (`/v1`)

A small, versioned HTTP surface over the engine that also serves Studio from its root, so the API
and UI ship as one hosted artifact. See the [API reference](api-reference.md).

## Core concepts

A threat model in Threat Model Forge is a **data-flow diagram (DFD)** made of a few primitives:

| Primitive | DFD meaning | Example |
| --- | --- | --- |
| **Process** | Code that transforms or acts on data | An API, a service, a function |
| **Data store** | Where data rests | A database, a queue, a bucket, a cache |
| **External entity** | An actor or system outside your control | A user, a browser, a third-party API |
| **Trust boundary** | A region where the trust level changes | A VNet, a DMZ, a subnet, a process boundary |
| **Data flow** | A directed connection carrying data | An HTTPS request, a SQL query |

Elements and flows carry **custom properties** (for example `Protocol`, `Port`, `DataType`,
`AuthenticationScheme`) that the validation rules inspect. Properties round-trip through `.tm7`, so
what you set in Studio, the CLI, or MTMT stays consistent.

## Features at a glance

### Authoring

- **Browser authoring** (Studio): four DFD stencils, drag-to-connect data flows, double-click
  rename, resizable trust boundaries, pan/zoom/minimap/fit, undo/redo, and an inspector to edit
  flow properties.
- **Headless authoring** (CLI): `new`, `add`, `connect`, `remove`, `rename`, and `set` verbs
  mutate models in place with atomic writes and deterministic auto-layout. No GUI or server
  required.
- **Stencils.** Type elements from a built-in stencil catalog (`tmforge stencils`,
  `add --stencil <id>`) so authored models carry real stencil metadata.

### Inspection

- **Summaries and listings** (`open`, `list`): counts and enumerations of components, flows,
  boundaries, threats, and diagrams.
- **Element and schema discovery** (`show`, `stencils`, `properties`): inspect a single
  element/flow's properties, and list the stencil catalog and the typed property schema the rules
  read.
- **Terminal rendering** (`render`): draw the diagram directly in your terminal (Unicode/ANSI, or
  `--plain` ASCII).

### Validation

- **A built-in rule set** organized into rule packs (Core Hygiene, STRIDE Completeness,
  Input Validation, Data Protection, Transport Security, and Identity & Access), covering
  connectivity, naming, trust-boundary modeling, and declared security properties.
- **CI integration**: SARIF + HTML findings reports, suppression files, rule-set overrides, and
  a distinct exit code for "found issues" vs. "tool error." See
  [Validation rules & CI](validation-rules.md).

### Reporting

- **Self-contained HTML reports** (`report`) with inline SVG diagrams. A single file you can
  attach to a review or a pull request.

### Interoperability

- **Formats**: `.tm7` (lossless), `tmforge-json` (canonical wire model), `.drawio`
  (draw.io / diagrams.net), and `.vsdx` (Microsoft Visio). See [Formats](formats.md).

## What's in v0.1 (and what isn't)

**In scope:** `.tm7` read / write / validate / report, browser and CLI authoring, multi-format
import/export, and CI validation.

**Deferred:** in-product **STRIDE threat generation** and knowledge-base compilation are not in
v0.1. You author and validate models; automated threat suggestion is planned as a later, optional
generation pack. (The `.tm7` threat *data model* is fully preserved: threats authored in MTMT
round-trip and are listed by `tmforge list threats`.)

## Next steps

- [Quick start](quickstart.md): build your first model end to end.
- [Installation](installation.md): get the tools onto your machine or into CI.

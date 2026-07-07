# Threat Model Forge Studio

The Threat Model Forge front end: a React + TypeScript single‑page app whose DFD canvas is
built on [React Flow](https://reactflow.dev) (`@xyflow/react`, MIT). The UI depends only on an
`IEngineClient` interface; the real .NET engine sits behind it via the versioned `/v1` HTTP API
(the TypeScript client is generated from the engine's OpenAPI document).

The .NET build produces the SPA and serves it from `ThreatModelForge.Api` (`wwwroot`), so
`dotnet build dirs.proj` builds the API **and** this UI into one hosted artifact.

## Run

```bash
# 1. Dev server (hot reload): talks to the API on :5205 (start the API separately).
npm install
npm run dev          # http://localhost:5199

# 2. Hosted: build, then run the API, which serves this SPA at its root.
dotnet run --project ../ThreatModelForge.Api   # http://localhost:5205/
```

## Regenerate the API client

After changing the `/v1` contract, refresh the typed client from the engine's OpenAPI doc:

```bash
npm run gen:api      # openapi-typescript ../ThreatModelForge.Api/openapi/v1.json -> src/dfd/engine/schema.d.ts
```

## What it exercises

- **Four DFD stencils** (Process, Data Store, External Entity, Trust Boundary) dragged
  from the palette onto the canvas.
- **Connectors**: drag from any port (hover a node) to any other; `ConnectionMode.Loose`
  lets a flow start/end on any side.
- **Editing feel**: double‑click a node or flow to rename; `Delete` removes selection;
  the Trust Boundary is a resizable region; pan / zoom / minimap / fit.
- **The engine seam**: `Validate` calls the real `/v1` engine when it's online (falling back
  to an offline stub), returns findings, and overlays them on the offending nodes/edges. The
  **inspector** (right panel) edits a flow's protocol / data classification to clear them, and
  **undo/redo** (Cmd+Z / Shift+Cmd+Z) covers every edit.
- **Canonical model**: `Export tmforge-json` / `Import JSON` round‑trips the diagram
  through the `tmforge-json` shape the real editor + API would speak.

## Where the seam is

- [`src/dfd/engineClient.ts`](src/dfd/engineClient.ts): `IEngineClient` + `StubEngineClient`.
  Swap the stub for an `HttpEngineClient` (calls the ASP.NET Core `/v1` API, client generated
  from OpenAPI) or a WASM‑backed client. The UI imports only the interface + `engine`.
- [`src/dfd/mapping.ts`](src/dfd/mapping.ts): maps React Flow nodes/edges to and from `tmforge-json`.

## Stack

Vite + React 18 + TypeScript + `@xyflow/react` v12 (MIT). No UI kit; plain CSS.

## Out of scope (in the engine, not here)

Real `.tm7` / format parsing, threat generation, persistence, and auth all live in the .NET
engine behind `/v1`. The canvas never parses formats.

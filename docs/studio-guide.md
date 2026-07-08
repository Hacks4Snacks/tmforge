# Studio guide

**Studio** is the Threat Model Forge browser front end: a React single-page app whose data-flow-diagram
(DFD) canvas is built on [React Flow](https://reactflow.dev). It talks to the real .NET engine over
the versioned [`/v1` API](api-reference.md), and the API serves Studio from its root, so the UI and
engine ship as one artifact.

## Launch Studio

The fastest way to try Studio is the hosted, no-install
**[browser demo](https://hacks4snacks.github.io/tmforge/)**, where the .NET engine runs entirely in
your browser via WebAssembly.

To run it locally, the engine API hosts Studio. The quickest way is the container image:

```bash
docker run --rm -p 8080:8080 tmforge      # then open http://localhost:8080/
```

Or run the API from source, which serves the built SPA at its root:

```bash
dotnet run --project src/ThreatModelForge.Api    # http://localhost:5205/
```

See [Deployment](deployment.md) for hosting options.

## The canvas

Studio gives you a DFD canvas with a stencil palette, an inspector, and engine-backed validation.

### Stencils

Drag any of the four DFD stencils from the palette onto the canvas:

| Stencil | Represents |
| --- | --- |
| **Process** | Code that acts on data (a service, API, function). |
| **Data Store** | Where data rests (database, queue, cache, bucket). |
| **External Entity** | An actor or system outside your control (user, browser, third party). |
| **Trust Boundary** | A resizable region where the trust level changes (VNet, DMZ, subnet). |

### Drawing data flows

Hover a node to reveal its ports, then drag from any port to another node. Connections use React
Flow's **loose** mode, so a flow can start or end on any side of a node. You focus on the logical
connection, not the geometry.

### Editing

| Action | How |
| --- | --- |
| Rename a node or flow | Double-click it and type. |
| Delete the selection | `Delete` key. |
| Resize a trust boundary | Drag its handles (it's a resizable region). |
| Pan / zoom | Drag the canvas / scroll; use the minimap and **fit** control to navigate. |
| Undo / redo | `Cmd+Z` / `Shift+Cmd+Z` (covers every edit). |

### Pages

A model can hold several diagrams. The **page tab strip** below the canvas lets you work across them:

| Action | How |
| --- | --- |
| Switch pages | Click a tab. |
| Add a page | Click **+**. |
| Rename a page | Double-click the tab (or press `F2`) and type. |
| Reorder pages | Drag a tab. |
| Delete a page | Click the tab's **×** (the last page can't be deleted). |

Each page is an independent canvas with its own undo history; the active page and every page's
contents persist across reloads. Opening a multi-page `.tm7`, `.drawio`, or Visio model (imported via
the CLI or API into `tmforge-json`) shows each source diagram on its own page.

### The inspector

The right-hand panel edits the selected element or flow. It is **schema-driven**: it lists a typed
control for every property the engine declares for that primitive, so every property an analysis
rule can read is reachable, and you can clear any finding without leaving the canvas. A data flow,
for example, exposes **Protocol**, **Port**, **Channel**, **DataType**, **Algorithm**, **Identity**,
and more; a process exposes **AuthenticationScheme**, **Isolation**, **SanitizesInput**, and so on.
Enum and boolean properties render as dropdowns of canonical values (so the value always matches what
the rules expect); free-form properties render as text fields. You can also add arbitrary custom
properties below the typed ones.

## Validating against the engine

Click **Analyze** to send the whole model (every page) to the live `/v1` engine. Findings come back
and are **overlaid on the offending nodes and edges**, so you can see exactly what to fix. In a
multi-page model, tabs that carry findings are badged, and clicking a finding jumps to its page. Open
the inspector, set the missing property (e.g. a flow's protocol), and re-analyze.

If the engine is offline, Studio falls back to an offline stub so the canvas keeps working; connect
it to a running API to get the real rule set. See [Analysis rules & CI](analysis-rules.md) for
what the rules check.

## Importing and exporting

Studio round-trips through the canonical **`tmforge-json`** wire model:

- **Export tmforge-json**: save the diagram as `.tmforge.json`, which the CLI and API speak
  natively.
- **Import JSON**: load a `.tmforge.json` document back onto the canvas.

This is the bridge between visual authoring and the [CLI](cli-reference.md): export from Studio,
then `tmforge analyze` / `tmforge report` / `tmforge convert` in a pipeline, or vice versa.

> **Format parsing lives in the engine, not the browser.** The canvas never parses `.tm7`, `.vsdx`,
> or `.drawio` itself. Use the API's `convert` / `read` endpoints or the CLI for those. Studio
> speaks `tmforge-json`; the engine handles every other format behind `/v1`.

## Merging edits from two branches

When two people edit the same model on different branches, click **Merge** in the toolbar to
reconcile them visually, the canvas equivalent of the [`tmforge merge`](cli-reference.md#merge) git
driver. The modal takes:

- **Ours**: your version.
- **Theirs**: the incoming version.
- **Base** *(optional)*: the common ancestor both edits started from, when you have it.

**With a base**, Studio runs the same identity-keyed three-way merge as the CLI: non-overlapping
changes (a rename on one side, a new data store on the other) combine automatically, and only genuine
**conflicts** (where both sides changed the same property) are listed. **Without a base** (often the
original isn't at hand), it falls back to a two-way merge: elements unique to either side are unioned,
and every element both versions changed differently is listed as a conflict for you to resolve; the
modal shows a notice to that effect.

Pick **Ours** or **Theirs** for each conflict (the default keeps yours), then **Load into editor** to
drop the resolved model onto the canvas, or **Download .tm7** to save it. Structural conflicts (an
element deleted on one side and edited on the other, or a data flow left dangling) keep your version
and are flagged for you to fix after loading.

> The merge matches elements by their stable id, so it is most reliable on real `.tm7` files (and on
> `.tmforge.json` exported by this build, which preserves ids).

## Local development

To hack on Studio itself with hot reload, run the Vite dev server against a locally running API:

```bash
# Terminal 1: the engine API (Studio calls it on :5205)
dotnet run --project src/ThreatModelForge.Api

# Terminal 2: the Studio dev server with hot reload
cd src/ThreatModelForge.Studio
npm install
npm run dev        # http://localhost:5199
```

During development the API allows CORS from the Vite dev server at `http://localhost:5199`.

### Regenerating the API client

Studio's typed client is generated from the engine's OpenAPI document, the single source of truth.
After the `/v1` contract changes, refresh it:

```bash
npm run gen:api    # openapi-typescript ../ThreatModelForge.Api/openapi/v1.json -> src/dfd/engine/schema.d.ts
```

### Stack

Vite + React 18 + TypeScript + `@xyflow/react` v12 (React Flow, MIT). No UI kit; plain CSS.

## See also

- [Engine API reference](api-reference.md): the `/v1` surface Studio depends on.
- [Formats & interoperability](formats.md): `tmforge-json` and the other formats.
- [CLI reference](cli-reference.md): drive the same engine headlessly.

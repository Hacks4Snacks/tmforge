# Formats & interoperability

Threat Model Forge keeps one **canonical, `.tm7`-shaped in-memory model** and maps every file format
to and from it through a pluggable format layer. New formats plug in without changing the tools that
consume them, and each provider declares how faithfully it round-trips.

## Supported formats

| Format id | Extension | Display name | Read | Write | Round-trips |
| --- | --- | --- | :---: | :---: | :---: |
| `tm7` | `.tm7` | Microsoft Threat Modeling Tool | Yes | Yes | Lossless |
| `tmforge-json` | `.tmforge.json` | Threat Model Forge JSON (canvas wire model) | Yes | Yes | Structural |
| `drawio` | `.drawio` | draw.io / diagrams.net | Yes | Yes | Structural |
| `vsdx` | `.vsdx` | Microsoft Visio | Yes | Yes | Structural |

List these at runtime with `tmforge` (via conversion targets) or the API's `GET /v1/formats`, which
returns each provider's capabilities and fidelity note.

## Fidelity

"Round-trips" means reading a file and writing it back reproduces the source **without loss**. Only
`.tm7` does this.

### `tm7` (lossless)

Byte-stable round-trip via .NET's `DataContractSerializer` over the full model graph. Because
Threat Model Forge reuses the exact serializer type graph MTMT produces, `.tm7` files move between the
two tools **byte-for-byte identically**, including the rich threat data model. This is the canonical
format; when you mutate a `.tm7` with the CLI, it's written back through this byte-stable writer.

### `tm7` and the Microsoft Threat Modeling Tool

For a file to open in MTMT it must carry a **knowledge base** (the tool dereferences it without a null
check). So when you export a model that doesn't already have one, Threat Model Forge embeds its own
clean-room default — **“Threat Model Forge Core”**: the generic DFD element types, the six STRIDE
categories, a threat type per built-in rule, and a **standard element type for every authoring
stencil** so the tool's palette mirrors Threat Model Forge's. This happens on **every** `.tm7` write
(the CLI authoring verbs, `convert --to tm7`, and Studio/API export), so a `.tm7` is MTMT-ready at
every step.

- **Typed properties.** Schema properties (`Protocol`, `Encrypted`, `DataType`, …) are written as
  first-class **typed tool properties** — the drop-downs MTMT shows in its properties pane — rather
  than free-form custom attributes. Free-text properties (like `Port`) and any value outside a
  property's options are kept as custom attributes, so nothing is dropped.
- **Stencils as element types.** An element placed from a stencil (`azure-sql`, `entra-id`, …) is
  exported as the matching standard element type, so it appears in MTMT as that stencil rather than a
  bare generic.
- **Existing knowledge bases are preserved.** A model that already carries a knowledge base — a file
  authored in MTMT, or one you supply with `tmforge convert --knowledge-base <file.tb7>` — is written
  back untouched.

Analysis reads typed and custom properties identically, so an exported or tool-authored `.tm7` is
validated the same as one authored with custom attributes.

### `tmforge-json` (canonical wire model)

The shape Studio and the API speak: elements, flows, trust boundaries, names, and geometry, plus an
optional analysis selection (disabled packs/rules) and a risk-acceptance triage overlay. Multi-page
models carry a `diagrams` array (one entry per page, with its name); single-page models keep the flat
`elements`/`flows` shape, so older readers keep working. Knowledge-base attributes and the full
generated-threat register are not represented — only risk-acceptance triage round-trips — so it's
structural rather than lossless. Use it to bridge Studio and the CLI.

### `drawio` (draw.io / diagrams.net)

A structural mapping to and from mxGraph: nodes, flows, trust boundaries, names, and geometry, with
each draw.io page mapped to a diagram. Import recognizes the shapes this provider writes and its
documented style convention. Knowledge-base attributes and generated threats are not represented.

### `vsdx` (Microsoft Visio)

Editable Visio via template injection. **Every diagram (page) is exported as its own Visio page and
re-imported**, so multi-page models keep their pages. Structure (nodes, flows, trust boundaries,
names, geometry) is preserved; element custom properties and associated threats are written as
per-shape **Visio Shape Data** (visible in Visio's Shape Data pane) and re-imported as custom
properties. The rich threat model itself is not reconstructed, so the mapping is structural. Import
recognizes packages this provider wrote and the documented master/shape convention.

## Converting

### CLI

```bash
tmforge convert <input> --to <format> --out <path>
```

The target is chosen by `--to`, or inferred from the `--out` extension.

```bash
tmforge convert model.tm7 --to drawio --out model.drawio
tmforge convert model.tm7 --to vsdx   --out model.vsdx
tmforge convert model.drawio --to tm7 --out model.tm7
tmforge convert model.tm7 --to tmforge-json --out model.tmforge.json
```

See the [CLI reference](cli-reference.md#convert).

### API

```
POST /v1/model/convert?to=<format>     # convert to any format
POST /v1/model/export/tm7              # export a .tm7 specifically
POST /v1/detect                        # sniff a file's format from its bytes
```

See the [API reference](api-reference.md).

### Studio

Studio round-trips through `tmforge-json` (**Export tmforge-json** / **Import JSON**). For `.tm7`,
`.drawio`, or `.vsdx`, convert with the CLI or the API. The browser canvas itself doesn't parse file
formats. See the [Studio guide](studio-guide.md#importing-and-exporting).

## Choosing a format

| Goal | Use |
| --- | --- |
| Exchange with the Microsoft Threat Modeling Tool, or store the source of truth | `tm7` |
| Move a diagram between Studio and the CLI/API | `tmforge-json` |
| Share an editable diagram with draw.io / diagrams.net users | `drawio` |
| Share an editable diagram with Visio users | `vsdx` |

> **Tip:** keep `.tm7` as your canonical, version-controlled source (it's lossless), and generate
> `.drawio` / `.vsdx` on demand for sharing. Converting *from* a structural format *to* `.tm7` only
> reconstructs the structure the source captured.

## Extending

Formats are pluggable: a provider implements the format contract (`Id`, extensions, capabilities,
read/write, and content sniffing) and registers with the format registry. The tools depend on the
registry rather than any single format, so adding one doesn't touch the CLI, API, or Studio.

## See also

- [CLI reference: `convert`](cli-reference.md#convert)
- [Engine API reference](api-reference.md)

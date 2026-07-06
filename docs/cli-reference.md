# CLI reference

The `tmforge` command-line tool inspects, authors, validates, reports on, and converts
`.tm7`-compatible threat models from a shell or CI pipeline. It is the headless face of the same
engine Studio uses, so agents and pipelines can drive threat models without a GUI.

```text
tmforge <command> [options] <file>
```

## Conventions

- **Options are GNU-style.** Every option accepts either `--name value` or `--name=value`.
- **`--json` everywhere.** Add `--json` to any command for a single machine-readable document on
  stdout (see [JSON output](#json-output)). Human text and diagnostics go to stderr, so
  `tmforge ... --json | jq` stays clean.
- **Help.** `tmforge --help` lists commands; `tmforge <command> --help` (also `-h`, `-?`) shows
  command-specific options.
- **Version.** `tmforge --version` prints the released version.
- **Elements are addressed by GUID.** Discover GUIDs with `tmforge list` or the output of `add`.

## Command summary

| Command | Kind | Purpose |
| --- | --- | --- |
| [`open`](#open) | Inspect | Summarize a model (element / flow / threat counts). |
| [`list`](#list) | Inspect | List components, flows, boundaries, threats, or diagrams. |
| [`show`](#show) | Inspect | Show one element/flow's name, type, and properties. |
| [`stencils`](#stencils) | Inspect | List the built-in authoring stencils. |
| [`properties`](#properties) | Inspect | List the typed custom-property schema. |
| [`render`](#render) | Inspect | Draw the diagram in the terminal. |
| [`new`](#new) | Author | Create a new model (empty or from a template). |
| [`add`](#add) | Author | Add a process, store, external entity, or boundary. |
| [`connect`](#connect) | Author | Add a data flow between two elements. |
| [`remove`](#remove) | Author | Remove an element (and its connected flows). |
| [`rename`](#rename) | Author | Rename an element. |
| [`set`](#set) | Author | Set an element/flow's name or properties. |
| [`lint`](#lint) | Validate | Evaluate the rule set against a model. |
| [`report`](#report) | Report | Generate a self-contained HTML report. |
| [`convert`](#convert) | Convert | Convert a model between file formats. |

---

## Inspection commands

### `open`

Summarize a model: counts of elements, flows, and threats.

```text
tmforge open [--json] <input>
```

```bash
tmforge open payments.tm7
tmforge open payments.tm7 --json
```

### `list`

List entities of a chosen kind.

```text
tmforge list <components|flows|boundaries|threats|diagrams> [--json] <input>
```

| Noun | Lists |
| --- | --- |
| `components` | Processes, data stores, and external entities (with GUIDs). |
| `flows` | Data flows and their endpoints. |
| `boundaries` | Trust boundaries. |
| `threats` | Threats stored in the model (e.g. authored in MTMT). |
| `diagrams` | Diagrams in the model. |

```bash
tmforge list components payments.tm7
tmforge list flows payments.tm7 --json
```

### `show`

Show a single element or flow by GUID: its name, type, and custom properties. Use it to inspect the
values a rule reads (for example a flow's `Protocol` / `Port`) before fixing a finding with
[`set`](#set).

```text
tmforge show --id <guid> [--json] <input>
```

```bash
tmforge show payments.tm7 --id <guid>
tmforge show payments.tm7 --id <guid> --json
```

### `stencils`

List the built-in authoring stencils and the ids you pass to `add --stencil`.

```text
tmforge stencils [--pack <id>] [--json]
```

```bash
tmforge stencils
tmforge stencils --pack azure --json
```

### `properties`

List the built-in **typed property schema** — the custom properties the linter reads and Studio
edits, with each property's value kind, allowed values, and default. This is the same schema the API
serves at `GET /v1/property-schema`; use it to discover closed enums (for example `Channel`,
`Encrypted`, `AccessControl`, or the approved cipher list) without running `lint` first.

```text
tmforge properties [--base <process|datastore|external|flow>] [--json]
```

```bash
tmforge properties
tmforge properties --base flow
tmforge properties --base datastore --json | jq '.data.properties'
```

### `render`

Draw the first diagram in the terminal. Defaults to Unicode/ANSI; `--plain` uses ASCII only.
Boxes are clamped to the canvas — and to their enclosing trust boundary — so labels aren't clipped
or drawn over a boundary wall.

```text
tmforge render [--plain] [--width <n>] [--height <n>] <file>
```

| Option | Default | Range | Meaning |
| --- | --- | --- | --- |
| `--width <n>` | 100 | 20–400 | Canvas width in columns. |
| `--height <n>` | 30 | 10–200 | Canvas height in rows. |
| `--plain` | off | — | ASCII output (no Unicode/ANSI). |

```bash
tmforge render payments.tm7
tmforge render payments.tm7 --plain --width 120
```

---

## Authoring commands

Authoring verbs mutate the model **in place** and write back through the source format's writer
(byte-stable for `.tm7`). Writes are **atomic** — the tool writes to a temp file and renames on
success, so a failed run never corrupts the source. Elements without explicit coordinates get a
**deterministic** auto-layout.

### `new`

Create a new model, empty or from a template.

```text
tmforge new [--name <title>] [--template <file>] [--format <id>] [--json] <file>
```

| Option | Meaning |
| --- | --- |
| `--name <title>` | Model title. |
| `--template <file>` | Seed from a template file (thin copy + metadata reset). |
| `--format <id>` | Output format id (`tm7`, `tmforge-json`, `drawio`, `vsdx`). Inferred from the extension when omitted. |

```bash
tmforge new payments.tm7 --name "Payments"
tmforge new payments.tmforge.json --format tmforge-json
```

### `add`

Add an element to the first diagram. Use a **positional kind** for a generic element, **or**
`--stencil <id>` for a typed one (the two are mutually exclusive).

```text
tmforge add <process|store|external|boundary> [options] <file>
tmforge add --stencil <id> [options] <file>
```

| Option | Meaning |
| --- | --- |
| `--name <name>` | Element name (defaults to the stencil label when using `--stencil`). |
| `--stencil <id>` | Concrete stencil from the catalog; stamps `StencilType=<id>` plus preset defaults. |
| `--left <n>` / `--top <n>` | Explicit coordinates (otherwise auto-laid-out). |
| `--width <n>` / `--height <n>` | Size (boundaries default to 260×180 so they enclose in `render`). |
| `--property KEY=VALUE` | Repeatable. Sets a rule-checked custom property. |

```bash
tmforge add process  payments.tm7 --name "Checkout API"
tmforge add store    payments.tm7 --name "Orders DB" --property Encrypted=At-rest
tmforge add boundary payments.tm7 --name "Azure VNet"
tmforge add payments.tm7 --stencil azure-app-service --name "Checkout API"
```

### `connect`

Add a directed data flow between two elements, addressed by GUID.

```text
tmforge connect --source <guid> --target <guid> [--name <name>] [--property KEY=VALUE ...] [--json] <file>
```

| Option | Meaning |
| --- | --- |
| `--source <guid>` | Source element GUID (required). |
| `--target <guid>` | Target element GUID (required). |
| `--name <name>` | Flow label. |
| `--property KEY=VALUE` | Repeatable. Sets flow custom properties (`Protocol`, `Port`, `DataType`, `Channel`, …). |

Mark a non-network flow with `--property Channel=In-Process` (also `Local-file`, `Unix-socket`, or
`Loopback`) to skip the protocol, port, and cleartext-crossing checks. Run
[`tmforge properties --base flow`](#properties) to see every flow property and its allowed values.

```bash
tmforge connect payments.tm7 \
  --source 1111... --target 2222... \
  --name "Place order" --property Protocol=HTTPS --property Port=443
```

### `remove`

Remove an element and its connected flows.

```text
tmforge remove --id <guid> [--json] <file>
```

### `rename`

Rename an element.

```text
tmforge rename --id <guid> --name <name> [--json] <file>
```

### `set`

Set the name and/or properties of an existing element or flow by GUID. Use this to resolve linter
findings (e.g. add a missing `Protocol`) without recreating the element. List every settable property
and its allowed values with [`tmforge properties`](#properties).

```text
tmforge set --id <guid> [--name <name>] [--property KEY=VALUE ...] [--json] <file>
```

```bash
tmforge set payments.tm7 --id 3333... --property Protocol=HTTPS --property Port=443
tmforge set payments.tm7 --id 3333... --name "Authenticated request" --property AuthenticationScheme=OAuth
```

---

## Validation, reporting & conversion

### `lint`

Evaluate a rule set against the model. See [Validation rules & CI](validation-rules.md) for the
full rule catalog, packs, and suppressions.

```text
tmforge lint [--ruleset <path>] [--suppressionFile <path>] [--reportFolder <dir>] [--define name=value ...] [--max-severity <level>] [--json] <model>
```

| Option | Meaning |
| --- | --- |
| `--ruleset <path>` | Use a custom rule set instead of the built-in default. |
| `--suppressionFile <path>` | Apply a suppression document to filter findings. |
| `--reportFolder <dir>` | Also write SARIF + HTML findings reports (and a JSON listing) to `<dir>`. |
| `--define name=value` | Repeatable. Supplies a rule variable. |
| `--max-severity <level>` | Gate the exit code on findings at or above `<level>` (`error`, `warning`, or `info`). Default: `error`. |

**Exit codes:**

| Code | Meaning |
| --- | --- |
| `0` | Clean — no findings at or above `--max-severity`. |
| `1` | Tool error (bad arguments, load failure). |
| `2` | The model was analyzed and has findings at or above `--max-severity` (default: `error`). |

The distinct `2` lets CI fail on findings while separating them from a broken invocation. Lower the
gate with `--max-severity warning` (or `info`) to also fail the build on those severities.

```bash
tmforge lint payments.tm7
tmforge lint payments.tm7 --reportFolder ./findings
tmforge lint payments.tm7 --suppressionFile suppressions.json --json
```

> When a model is loaded from the native `tmforge-json` format, its embedded validation selection
> (disabled packs/rules) is honored automatically. Other formats use the full rule set or an
> explicit `--ruleset`.

### `report`

Generate a self-contained HTML report with an inline SVG diagram.

```text
tmforge report [--out <path.html>] [--json] <model.tm7>
```

```bash
tmforge report payments.tm7 --out payments.html
```

### `convert`

Convert between formats. The target is chosen by `--to` or inferred from the `--out` extension.

```text
tmforge convert [--to <format>] [--out <path>] [--json] <input>
```

| Format id | Extension | Notes |
| --- | --- | --- |
| `tm7` | `.tm7` | Lossless, byte-stable. |
| `tmforge-json` | `.tmforge.json` | Canonical wire model. |
| `drawio` | `.drawio` | draw.io / diagrams.net (structural). |
| `vsdx` | `.vsdx` | Microsoft Visio (structural). |

See [Formats & interoperability](formats.md) for fidelity details.

```bash
tmforge convert payments.tm7 --to drawio --out payments.drawio
tmforge convert payments.drawio --to tm7 --out payments.tm7
```

---

## JSON output

With `--json`, every command emits a single **versioned envelope** to stdout:

```json
{
  "schemaVersion": 1,
  "command": "open",
  "data": { }
}
```

- `schemaVersion` — pin this; the shape evolves under SemVer.
- `command` — the verb that produced the payload.
- `data` — the command-specific payload (camelCase properties, enums as strings).

Diagnostics never mix into this stream — they go to stderr — so piping to `jq` is safe:

```bash
tmforge list components payments.tm7 --json | jq '.data'
```

## See also

- [Quick start](quickstart.md) — the commands in a worked example.
- [Validation rules & CI](validation-rules.md) — the rule catalog and gating.
- [Formats & interoperability](formats.md) — conversion fidelity.

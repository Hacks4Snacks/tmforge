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
- **Elements are addressed by GUID, alias, or unique name.** `add --alias <name>` gives an element a
  stable, citeable id plus a handle that `connect`/`set`/`remove`/`rename`/`show` resolve (they also
  accept a unique element name). Discover ids with `tmforge list` or the output of `add`.

## Command summary

| Command | Kind | Purpose |
| --- | --- | --- |
| [`open`](#open) | Inspect | Summarize a model (element / flow / threat counts). |
| [`list`](#list) | Inspect | List components, flows, boundaries, threats, or diagrams. |
| [`show`](#show) | Inspect | Show one element/flow's name, type, and properties. |
| [`stencils`](#stencils) | Inspect | List the built-in authoring stencils. |
| [`properties`](#properties) | Inspect | List the typed custom-property schema. |
| [`schema`](#schema) | Inspect | Describe the `--json` envelope and per-command output shapes. |
| [`render`](#render) | Inspect | Draw the diagram in the terminal. |
| [`diff`](#diff) | Inspect | Structurally compare two models (or emit a git textconv). |
| [`merge`](#merge) | Merge | Three-way merge two models against a common ancestor (git driver). |
| [`git-setup`](#git-setup) | Git | Wire git to use tmforge for .tm7 diff/merge (no committed .gitattributes needed). |
| [`new`](#new) | Author | Create a new model (empty or from a template). |
| [`add`](#add) | Author | Add a process, store, external entity, or boundary. |
| [`connect`](#connect) | Author | Add a data flow between two elements. |
| [`remove`](#remove) | Author | Remove an element (and its connected flows). |
| [`rename`](#rename) | Author | Rename an element. |
| [`set`](#set) | Author | Set an element/flow's name or properties. |
| [`page`](#page) | Author | List, add, rename, reorder, or remove pages (diagrams). |
| [`layout`](#layout) | Author | Auto-lay-out the diagram (layered; no hand-placed coordinates). |
| [`lint`](#lint) | Validate | Evaluate the rule set against a model. |
| [`report`](#report) | Report | Generate a self-contained HTML report. |
| [`convert`](#convert) | Convert | Convert a model between file formats. |
| [`apply`](#apply) | Author | Build a model from a declarative JSON manifest (all-or-nothing). |
| [`export`](#export) | Author | Export a model as a declarative JSON manifest. |

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

List the built-in **typed property schema**: the custom properties the linter reads and Studio
edits, with each property's value kind, allowed values, and default. This is the same schema the API
serves at `GET /v1/property-schema`; use it to discover closed enums (for example `Channel`,
`Encrypted`, `AccessControl`, or the approved cipher list) without running `lint` first.

```text
tmforge properties [--base <process|datastore|external|flow>] [--explain] [--json]
```

```bash
tmforge properties
tmforge properties --base flow
tmforge properties --base datastore --json | jq '.data.properties'
```

Add `--explain` to map each property **value** to the rule id and severity it triggers, so you can
predict lint behavior before running [`lint`](#lint). A value shown as `(unset/condition)` means the
rule fires when the property is absent or by a computed condition.

```bash
tmforge properties --base flow --explain
tmforge properties --base external --explain --json | jq '.data.explain'
```

### `schema`

Describe the machine-readable [`--json`](#json-output) envelope and the `data` payload shape of every
command, so automation can be written against a documented contract instead of shapes discovered by
probing output.

```text
tmforge schema [--json]
```

```bash
tmforge schema
tmforge schema --json | jq '.data.commands'
```

### `render`

Draw the first diagram in the terminal. Defaults to Unicode/ANSI; `--plain` uses ASCII only.
Boxes are clamped to the canvas (and to their enclosing trust boundary), so labels aren't clipped
or drawn over a boundary wall.

```text
tmforge render [--plain] [--width <n>] [--height <n>] <file>
```

| Option | Default | Range | Meaning |
| --- | --- | --- | --- |
| `--width <n>` | 100 | 20-400 | Canvas width in columns. |
| `--height <n>` | 30 | 10-200 | Canvas height in rows. |
| `--plain` | off | n/a | ASCII output (no Unicode/ANSI). |

```bash
tmforge render payments.tm7
tmforge render payments.tm7 --plain --width 120
```

### `diff`

Structurally compare two models, matched by each element's **stable id** — so re-layout or
re-serialization produces no diff, and a rename shows as a single modification rather than a
delete-plus-add. Reports added, removed, and modified elements, with per-property changes for
modifications. Geometry (position and size) is ignored.

```text
tmforge diff [--json] <base> <revised>
tmforge diff --textconv <model>
```

```bash
tmforge diff payments.v1.tm7 payments.v2.tm7
tmforge diff payments.v1.tm7 payments.v2.tm7 --json
```

Identity is preserved in `.tm7`; other formats do not round-trip element ids, so `diff` is most
useful on `.tm7`.

#### Readable `.tm7` diffs in git

`--textconv` prints a canonical, deterministic outline of a **single** model. Wired as a git
[textconv](https://git-scm.com/docs/gitattributes#_generating_diff_text_via_textconv) it makes
`git diff`, `git log -p`, and pull requests render `.tm7` changes as readable structure instead of
opaque XML. Enable it once per clone:

```gitattributes
# .gitattributes (shipped in this repo)
*.tm7 diff=tmforge
```

```bash
git config diff.tmforge.textconv "tmforge diff --textconv"
```

Afterwards, `git diff` on a `.tm7` shows lines such as `process "API Gateway"  <id>` and
`Protocol=HTTPS`, so a reviewer sees exactly what changed.

> The committed `.gitattributes` is **optional** — you don't need this repository's source. Run
> [`tmforge git-setup`](#git-setup) to apply the config and a local (or global) mapping for you.

### `merge`

Three-way merge two edited models against their common ancestor, matched by element id.
Non-overlapping edits from both sides combine automatically; genuine conflicts (both sides changed
the same attribute, or one side deleted what the other modified) keep the `ours` value, are reported,
and are written to `<pathname>.conflicts.json`. The merged model is always valid — it never contains
textual conflict markers. The exit code is `0` on a clean merge and `1` when conflicts remain.

```text
tmforge merge <base> <ours> <theirs> [<pathname>] [--output <path>] [--json]
```

```bash
tmforge merge base.tm7 ours.tm7 theirs.tm7 --output merged.tm7
```

By default the result is written back to `<ours>`. Resolve any reported conflicts with
`tmforge set --id <guid> ...`.

#### Use as a git merge driver

Wire it up so `git merge`, `rebase`, and `cherry-pick` deconflict `.tm7` automatically:

```gitattributes
# .gitattributes (shipped in this repo)
*.tm7 merge=tmforge
```

```bash
git config merge.tmforge.name   "Threat Model Forge semantic merge"
git config merge.tmforge.driver "tmforge merge %O %A %B %P"
```

Git invokes the driver with the ancestor (`%O`), our version (`%A`, also where the result is
written), their version (`%B`), and the path (`%P`). A clean merge is applied silently; on conflict
the file keeps `ours` and the conflicts are listed on the console and in the sidecar.

### `git-setup`

Wire git to use tmforge for `.tm7` diffs and merges — **without** a committed `.gitattributes` and
**without** access to any repository's source. It registers the diff textconv and merge driver in
git config and maps `*.tm7` to them via `.git/info/attributes` (this repo) or your global attributes
file (`--global`).

```text
tmforge git-setup [--global] [--print]
```

```bash
tmforge git-setup            # configure the current repository (local; nothing is committed)
tmforge git-setup --global   # configure every repository for this user
tmforge git-setup --print    # print the exact commands instead of applying them
```

`tmforge diff` and `tmforge merge` also work **standalone** with no git configuration at all — the
setup above only enables the automatic `git diff` / `git merge` behavior. (Fully zero-config
auto-invocation isn't possible: git requires drivers to be configured explicitly, by design.)

---

## Authoring commands

Authoring verbs mutate the model **in place** and write back through the source format's writer
(byte-stable for `.tm7`). Writes are **atomic**: the tool writes to a temp file and renames on
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

Add an element to a page (the first diagram by default; target another with `--page`). Use a **positional kind** for a generic element, **or**
`--stencil <id>` for a typed one (the two are mutually exclusive).

```text
tmforge add <process|store|external|boundary> [options] <file>
tmforge add --stencil <id> [options] <file>
```

| Option | Meaning |
| --- | --- |
| `--name <name>` | Element name (defaults to the stencil label when using `--stencil`). |
| `--alias <name>` | Stable authoring handle. Resolvable by `connect`/`set`/`remove`/`rename`/`show`, and gives the element a **deterministic** id (the same alias yields the same id across rebuilds) so reports and docs can cite it. |
| `--boundary <ref>` | Place the element inside this trust boundary (by alias, name, or GUID) and record membership, so `export` and boundary-aware rules see it. |
| `--stencil <id>` | Concrete stencil from the catalog; stamps `StencilType=<id>` plus preset defaults. |
| `--left <n>` / `--top <n>` | Explicit coordinates (otherwise auto-laid-out). |
| `--width <n>` / `--height <n>` | Size (boundaries default to 260×180 so they enclose in `render`). |
| `--page <name\|index>` | Target page: a 1-based index or a page name (default: the first page; one is created if the model has none). |
| `--property KEY=VALUE` | Repeatable. Sets a rule-checked custom property. |

```bash
tmforge add process  payments.tm7 --name "Checkout API"
tmforge add store    payments.tm7 --name "Orders DB" --property Encrypted=At-rest
tmforge add boundary payments.tm7 --name "Azure VNet"
tmforge add payments.tm7 --stencil azure-app-service --name "Checkout API"
```

`--alias` gives an element a durable handle and a deterministic id: authoring the same alias in a
rebuilt model yields the same GUID, so companion docs and reports can cite it. Reference it later by
alias (or unique name) instead of GUID:

```bash
tmforge add process payments.tm7 --alias P1 --name "Checkout API"
tmforge add store   payments.tm7 --alias ORD --name "Orders DB"
tmforge connect payments.tm7 --source P1 --target ORD --name "place order"
tmforge set payments.tm7 --id P1 --property AuthenticationScheme=OAuth
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
| `--page <name\|index>` | Page to connect within (default: the first page). Both endpoints must be on it. |
| `--property KEY=VALUE` | Repeatable. Sets flow custom properties (`Protocol`, `Port`, `DataType`, `Channel`, ...). |

Mark a non-network flow with `--property Channel=In-Process` (also `Local-file`, `Unix-socket`, or
`Loopback`) to skip the protocol, port, and cleartext-crossing checks. Run
[`tmforge properties --base flow`](#properties) to see every flow property and its allowed values.

```bash
tmforge connect payments.tm7 \
  --source 1111... --target 2222... \
  --name "Place order" --property Protocol=HTTPS --property Port=443
```

### `remove`

Remove an element and its connected flows. The element is found on any page by default; `--page`
scopes the search to one page.

```text
tmforge remove --id <guid> [--page <name|index>] [--json] <file>
```

### `rename`

Rename an element. The element is found on any page by default; `--page` scopes the search.

```text
tmforge rename --id <ref> --name <name> [--page <name|index>] [--json] <file>
```

### `set`

Set the name and/or properties of an existing element or flow by GUID. Use this to resolve linter
findings (e.g. add a missing `Protocol`) without recreating the element. List every settable property
and its allowed values with [`tmforge properties`](#properties).

```text
tmforge set --id <ref> [--name <name>] [--page <name|index>] [--property KEY=VALUE ...] [--json] <file>
```

```bash
tmforge set payments.tm7 --id 3333... --property Protocol=HTTPS --property Port=443
tmforge set payments.tm7 --id 3333... --name "Authenticated request" --property AuthenticationScheme=OAuth
```

### `page`

List and manage the **pages** (diagrams) of a model. A `.tm7` model can hold several diagrams (for
example a context diagram plus per-service DFDs); the authoring verbs target one page at a time with
`--page`, and read-only verbs (`list`, `show`, `render`) already report every page.

```text
tmforge page ls [--json] <file>
tmforge page add [--name <name>] [--json] <file>
tmforge page rename --page <name|index> --name <newname> [--json] <file>
tmforge page rm --page <name|index> [--json] <file>
tmforge page reorder --page <name|index> --to <index> [--json] <file>
```

| Subcommand | Purpose |
| --- | --- |
| `ls` | List pages with their 1-based index, name, and element / flow / boundary counts. |
| `add` | Append a page (named `--name`, else `Diagram N`). |
| `rename` | Rename the selected page. |
| `rm` | Delete the selected page (the last page cannot be removed). |
| `reorder` | Move the selected page to the 1-based position `--to`. |

`--page` accepts a **1-based index** or a **page name** (case-insensitive; an ambiguous name is
rejected — use the index).

```bash
tmforge page ls payments.tm7
tmforge page add payments.tm7 --name "Payments service"
tmforge add process payments.tm7 --name "Ledger" --page "Payments service"
tmforge page reorder payments.tm7 --page "Payments service" --to 1
```

### `layout`

Apply a deterministic **layered auto-layout** so you never hand-place coordinates: components are
arranged left-to-right by their data flows and connectors are re-routed. Trust boundaries are left in
place, so run this to tidy a graph (it arranges the data-flow graph rather than preserving boundary
placement).

```text
tmforge layout [--page <name|index>] [--node-spacing <n>] [--layer-spacing <n>] [--json] <model>
```

```bash
tmforge layout payments.tm7
tmforge layout payments.tm7 --node-spacing 60 --layer-spacing 120
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
| `0` | Clean, no findings at or above `--max-severity`. |
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

Generate a self-contained HTML report with an inline SVG diagram per page, or export just the
diagram as a standalone SVG for review artifacts.

```text
tmforge report [--format <html|svg>] [--out <path>] [--json] <model.tm7>
```

- `--format html` (default) writes a self-contained HTML report (findings table + inline SVG per page).
- `--format svg` writes just the diagram as a standalone SVG (every page stacked), suitable for
  attaching to a pull request or embedding in docs.

```bash
tmforge report payments.tm7 --out payments.html
tmforge report payments.tm7 --format svg --out payments.svg
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

## Declarative manifest

A manifest is a small, review-friendly JSON document that describes a whole model — boundaries,
elements, and flows — by **alias** instead of GUID, so it diffs cleanly in a pull request and is the
source of truth for the `.tm7`. `apply` materializes it; `export` emits it from any model.

```json
{
  "name": "Checkout Service",
  "boundaries": [
    { "alias": "TB1", "name": "Payments VNet" }
  ],
  "elements": [
    { "alias": "API", "kind": "process", "name": "Checkout API", "boundary": "TB1",
      "props": { "RunningAs": "Service Account", "AuthenticationScheme": "OAuth" } },
    { "alias": "DB", "kind": "store", "stencil": "azure-sql", "name": "Orders DB", "boundary": "TB1" }
  ],
  "flows": [
    { "from": "API", "to": "DB", "name": "store order",
      "props": { "DataType": "Customer Content", "Protocol": "SQL", "Port": "1433" } }
  ]
}
```

- `elements[].boundary` names the trust boundary an element belongs to; `apply` places the element
  inside it so trust-boundary crossings are computed and membership round-trips through `export`.
- `elements[].alias` gives each element a **deterministic** id (stable across rebuilds), and flows
  reference elements by that alias (or by unique name).
- Either `kind` or `stencil` identifies an element; a stencil's base primitive sets the kind.

### `apply`

Build a model from a manifest. The whole model is built in memory and written **atomically**, so a
bad manifest never leaves a half-built model; re-running regenerates the model idempotently.

```text
tmforge apply <manifest.json> [--out <model>] [--format <id>] [--force] [--dry-run] [--json]
```

| Option | Meaning |
| --- | --- |
| `--out <model>` | Output path (default: the manifest path with a `.tm7` extension). |
| `--format <id>` | Output format id (`tm7`, `tmforge-json`, `drawio`, `vsdx`); inferred from `--out` otherwise. |
| `--dry-run` | Validate the manifest and report counts without writing. |
| `--force` | Store unknown/invalid property values instead of rejecting them. |

```bash
tmforge apply model.json --out model.tm7
tmforge apply model.json --dry-run
```

### `export`

Emit a manifest from an existing model (round-trips with `apply`). Geometry is intentionally dropped
so the manifest stays a stable, diffable source.

```text
tmforge export [--out <manifest.json>] [--json] <model>
```

```bash
tmforge export payments.tm7 --out payments.json
tmforge export payments.tm7 | jq '.elements'
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

- `schemaVersion`: pin this; the shape evolves under SemVer.
- `command`: the verb that produced the payload.
- `data`: the command-specific payload (camelCase properties, enums as strings).

Diagnostics never mix into this stream (they go to stderr), so piping to `jq` is safe:

```bash
tmforge list components payments.tm7 --json | jq '.data'
```

For the `data` shape of each command, run [`tmforge schema`](#schema) (add `--json` for a
machine-readable catalog). For example, `add` returns the new element id at `data.id`, and `connect`
returns `data.id` / `data.source` / `data.target`.

## See also

- [Quick start](quickstart.md): the commands in a worked example.
- [Validation rules & CI](validation-rules.md): the rule catalog and gating.
- [Formats & interoperability](formats.md): conversion fidelity.

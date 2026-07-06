# ThreatModelForge.Cli (`tmforge`)

The `tmforge` command-line tool: inspect, author, validate, report on, and convert
`.tm7`-compatible threat models from a shell or CI pipeline. It is the headless, scriptable
face of the same engine the Studio UI uses, so agents and pipelines can drive threat models
without a GUI.

Every command accepts `--json` for machine-readable output, and options take either
`--name value` or `--name=value`. Run `tmforge <command> --help` for command-specific options.

## Commands

### Inspect (read-only)

| Command | Purpose |
| --- | --- |
| `tmforge open [--json] <input>` | Summarize a model: counts of elements, flows, and threats. |
| `tmforge list <components\|flows\|boundaries\|threats\|diagrams> [--json] <input>` | List entities of the chosen kind. |
| `tmforge show --id <guid> [--json] <input>` | Show one element/flow: name, type, and custom properties. |
| `tmforge render [--plain] [--width <n>] [--height <n>] <file>` | Draw the diagram in the terminal (Unicode/ANSI; `--plain` for ASCII). |

### Discover (authoring aids)

| Command | Purpose |
| --- | --- |
| `tmforge stencils [--pack <id>] [--json]` | List the built-in stencils (ids for `add --stencil`). |
| `tmforge properties [--base <id>] [--json]` | List the typed custom-property schema the linter reads. |

### Author (mutating)

| Command | Purpose |
| --- | --- |
| `tmforge new [--name <title>] [--template <file>] [--format <id>] [--json] <file>` | Create a new model (empty or from a template). |
| `tmforge add <process\|store\|external\|boundary> [--name <name>] [--stencil <id>] [--left <n>] [--top <n>] [--width <n>] [--height <n>] [--property KEY=VALUE ...] [--json] <file>` | Add an element to the first diagram (generic kind or a typed `--stencil`). |
| `tmforge connect --source <guid> --target <guid> [--name <name>] [--property KEY=VALUE ...] [--json] <file>` | Add a data flow between two elements. |
| `tmforge set --id <guid> [--name <name>] [--property KEY=VALUE ...] [--json] <file>` | Set an element/flow's name and/or custom properties. |
| `tmforge remove --id <guid> [--json] <file>` | Remove an element (and its connected flows). |
| `tmforge rename --id <guid> --name <name> [--json] <file>` | Rename an element. |

### Validate, report & convert

| Command | Purpose |
| --- | --- |
| `tmforge lint [--ruleset <path>] [--suppressionFile <path>] [--reportFolder <dir>] [--define name=value ...] [--json] <model>` | Evaluate a rule set against the model. `--reportFolder` also emits SARIF + HTML findings reports. |
| `tmforge report [--out <path.html>] [--json] <model.tm7>` | Generate a self-contained HTML report of the model. |
| `tmforge convert [--to <format>] [--out <path>] [--json] <input>` | Convert between formats (`tm7`, `tmforge-json`, `drawio`, `vsdx`). |

`lint` exit codes: `0` = clean, `1` = error (bad arguments or load failure), `2` = findings
reported. This lets CI fail a build on findings while distinguishing them from tool errors.

## Run

```bash
# From source
dotnet run --project src/ThreatModelForge.Cli -- lint model.tm7 --json

# From the published container image (pulls on first run)
docker run --rm -v "$PWD:/work" ghcr.io/hacks4snacks/tmforge-cli tmforge lint model.tm7

# ...or build the image from source (see build/Dockerfile)
docker build -f build/Dockerfile -t tmforge-cli .
docker run --rm -v "$PWD:/work" tmforge-cli tmforge lint model.tm7
```

## Examples

```bash
tmforge new payments.tm7 --name "Payments"
tmforge add process payments.tm7 --name "Checkout API"
tmforge stencils
tmforge list components payments.tm7 --json
tmforge show payments.tm7 --id <guid>
tmforge set payments.tm7 --id <flow-guid> --property Protocol=HTTPS --property Port=443
tmforge render payments.tm7 --plain
tmforge report payments.tm7 --out payments.html
tmforge convert payments.tm7 --to drawio --out payments.drawio
```

Prebuilt, self-contained `tmforge` binaries (no .NET runtime required) are attached to each
GitHub Release for six platforms; container images and the RID-agnostic global tool are also
available. See the [installation guide](../../docs/installation.md).

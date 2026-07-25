# Engine API reference

The Threat Model Forge **engine API** exposes a small, versioned `/v1` HTTP surface over the real
.NET engine, and serves the [Studio](studio-guide.md) single-page app from its root, so the API and
UI ship as one hosted artifact.

The API contract is the single source of truth: the OpenAPI document at `/openapi/v1.json` is what
Studio's typed client is generated from. A checked-in copy lives at
[`src/ThreatModelForge.Api/openapi/v1.json`](../src/ThreatModelForge.Api/openapi/v1.json).

## Run

```bash
# From source: serves the built Studio SPA at the root.
dotnet run --project src/ThreatModelForge.Api        # http://localhost:5205/

# Container: the published engine API + Studio image (pulls on first run).
docker run --rm -p 8080:8080 ghcr.io/hacks4snacks/tmforge   # http://localhost:8080/
```

Any non-API path falls back to Studio's `index.html` (so client-side routes resolve), while `/v1`
and `/openapi` are matched first.

## Endpoints

| Method & path | Tag | Purpose |
| --- | --- | --- |
| `GET /v1/health` | System | Liveness probe. |
| `GET /v1/formats` | Formats | List supported formats and their capabilities. |
| `POST /v1/detect` | Formats | Detect a file's format from its bytes. |
| `GET /v1/stencils` | Catalog | List element stencils. |
| `GET /v1/stencil-packs` | Catalog | List stencil packs. |
| `GET /v1/rules` | Catalog | List analysis rules. |
| `GET /v1/rule-packs` | Catalog | List rule packs. |
| `GET /v1/rule-bundle` | Catalog | Report which custom rule packs this host loaded, and any load diagnostics. |
| `GET /v1/property-schema` | Catalog | List the typed custom-property schema the rules read. |
| `POST /v1/model/analyze` | Model | Analyze a model and return findings. |
| `POST /v1/model/analysis` | Model | One analysis action: findings **and** threats from a single rule evaluation, plus the effective rule packs and diagnostics. |
| `POST /v1/model/threats` | Model | Generate the STRIDE threat register (rule threats plus the model's author overlay). |
| `POST /v1/model/read` | Model | Parse uploaded bytes (base64) into the canonical model. |
| `POST /v1/model/convert?to=<format>` | Model | Convert a model to another format. |
| `POST /v1/model/export/tm7` | Model | Export a model as a `.tm7` file. |
| `POST /v1/model/report?format=<html\|svg>` | Report | Render a model to an HTML or SVG report. |
| `POST /v1/model/analysis-report?format=<sarif\|html\|json>` | Report | Render the analysis findings as SARIF, HTML, or JSON. |
| `GET /openapi/v1.json` | n/a | The OpenAPI document. |

`<format>` is one of `tm7`, `tmforge-json`, `drawio`, or `vsdx`. See
[Formats & interoperability](formats.md).

## One analysis action

Findings and threats are the same detection: a threat is a finding from a rule that declares a threat
category, kept for its lifecycle (open → mitigated → accepted). Asking for them separately makes the
engine evaluate every enabled rule twice for one user action, so a UI that shows both should call
`POST /v1/model/analysis`, which evaluates once and projects both:

```bash
curl -s -X POST http://localhost:8080/v1/model/analysis \
  -H 'Content-Type: application/json' --data @model.tmforge.json
# { "findings": […], "threats": […], "rulePacks": […], "diagnostics": [] }
```

`POST /v1/model/analyze` and `POST /v1/model/threats` remain available and return exactly what the
combined action returns for their half; they exist for callers that genuinely need only one
projection, and they do not materialize the other.

## Custom rule packs

Custom rules are deployment configuration, not request input: this host never loads rules from a
request body, so a caller cannot inject detection logic. Name the packs (files or directories) with
the `TmForge:Rules` setting and they are read once at startup, then applied to every rule-reading
endpoint — catalogs, analysis, threats, reports, and `.tm7` export — as one effective bundle:

```bash
# a single pack, or a ';'-separated list
TmForge__Rules='/etc/tmforge/corporate.tmrules.json' dotnet ThreatModelForge.Api.dll
```

Confirm what actually loaded before trusting a clean run:

```bash
curl http://localhost:8080/v1/rule-bundle
# { "rulePacks": [ { "id": "corporate", "version": "2.1", "fingerprint": "sha256:…", "ruleCount": 12 } ],
#   "diagnostics": [] }
```

A model may pin the packs it was reviewed with; a missing or changed pack becomes an `error` finding
with rule id `rule-pack-mismatch`. See
[Custom rules on every surface](analysis-rules.md#custom-rules-on-every-surface).

## Usage examples

### Liveness

```bash
curl http://localhost:8080/v1/health
```

### Discover capabilities

```bash
curl http://localhost:8080/v1/formats          # what can be read/written and how faithfully
curl http://localhost:8080/v1/rules            # the analysis rules
curl http://localhost:8080/v1/rule-packs       # the rule packs (core-hygiene, stride-completeness, ...)
curl http://localhost:8080/v1/property-schema  # typed custom properties the rules read
curl http://localhost:8080/v1/stencils         # authoring stencils
```

### Detect a format

`POST /v1/detect` sniffs uploaded bytes and reports the matching format.

### Analyze a model

`POST /v1/model/analyze` returns findings for a supplied model, the same rule engine `tmforge analyze`
uses. This is how Studio's **Analyze** button overlays findings on the canvas.

Each finding's `id` is stable: `{ruleId}:{diagram}:{target}:{occurrence}`, where the diagram and
target segments are the element ids from the request model (they read `model` when the finding is
about the model or a whole diagram). The same model analyzed twice produces the same ids, and
enabling or disabling an unrelated rule leaves the other ids alone — so a caller can reconcile a
finding against a previous run. See
[finding identity](analysis-rules.md#finding-identity) for the details and the one caveat.

### Generate threats

`POST /v1/model/threats` returns the model's **STRIDE threat register** the same rule findings as
`analyze`, framed as threats and overlaid with the model's author-owned state. The request model's
`threats` overlay carries risk acceptance, per-threat edits (state, priority, mitigation, description),
and **manually-authored threats** (`manual: true`, keyed `manual:<guid>`, scoped to element ids or
model-wide). Those edits and manual threats round-trip into the exported `.tm7` register, so a threat
accepted or authored in Studio opens in the Microsoft Threat Modeling Tool. This powers Studio's threat
panel and the `tmforge threats` verb.

### Convert / export

```bash
# Convert (target chosen by the `to` query parameter):
#   POST /v1/model/convert?to=drawio
# Export a .tm7 specifically:
#   POST /v1/model/export/tm7
```

Both `.tm7` paths embed the Threat Model Forge knowledge base and write typed properties, so the
exported file opens in the Microsoft Threat Modeling Tool. See
[Formats](formats.md#tm7-and-the-microsoft-threat-modeling-tool).

### Report

`POST /v1/model/report?format=html` (or `format=svg`) renders a report from a model, the hosted
equivalent of `tmforge report`. Multi-page models render every diagram: the HTML report has one
section per page, and the SVG stacks the pages. HTML reports generate the enabled rule-backed threats
on demand, overlay the model's manual threats and triage, and show rule id, STRIDE category, scope,
priority, mitigation, references, and decision note. SVG output renders diagrams only.

`POST /v1/model/analysis-report?format=sarif` (or `html` / `json`) renders the *analysis* artifacts
instead — the findings evidence, not the threat-model document. These are the same artifacts
`tmforge analyze --reportFolder` writes, so a report served here and a file written in CI are the
same document. An unrecognized format falls back to the readable findings HTML.

```bash
curl -s -X POST 'http://localhost:8080/v1/model/analysis-report?format=sarif' \
  -H 'Content-Type: application/json' --data @model.tmforge.json -o findings.sarif
```

Both report endpoints run the host's configured rule packs and honor the model's own disabled
selection, so a report always matches the analysis it claims to describe.

## OpenAPI & client generation

The document is produced by the ASP.NET Core OpenAPI integration and served at `/openapi/v1.json`.
After changing the `/v1` contract, refresh the checked-in copy so Studio's generated client stays in
sync (Studio's `npm run gen:api` reads that file). See the
[Studio guide](studio-guide.md#regenerating-the-api-client).

## Notes for hosting

- The container listens on port **8080**; from source it listens on **5205**.
- In development the API permits CORS from the Studio dev server at `http://localhost:5199`.
- The API is stateless: it operates on the model bytes you send it, so it scales horizontally.
  See [Deployment](deployment.md).

## See also

- [Studio guide](studio-guide.md): the browser client of this API.
- [Deployment](deployment.md): running the API in containers and Kubernetes.
- [Formats & interoperability](formats.md): the formats the model endpoints speak.

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
| `GET /v1/property-schema` | Catalog | List the typed custom-property schema the rules read. |
| `POST /v1/model/validate` | Model | Validate a model and return findings. |
| `POST /v1/model/read` | Model | Parse uploaded bytes (base64) into the canonical model. |
| `POST /v1/model/convert?to=<format>` | Model | Convert a model to another format. |
| `POST /v1/model/export/tm7` | Model | Export a model as a `.tm7` file. |
| `POST /v1/model/report?format=<html\|svg>` | Report | Render a model to an HTML or SVG report. |
| `GET /openapi/v1.json` | n/a | The OpenAPI document. |

`<format>` is one of `tm7`, `tmforge-json`, `drawio`, or `vsdx`. See
[Formats & interoperability](formats.md).

## Usage examples

### Liveness

```bash
curl http://localhost:8080/v1/health
```

### Discover capabilities

```bash
curl http://localhost:8080/v1/formats          # what can be read/written and how faithfully
curl http://localhost:8080/v1/rules            # the validation rules
curl http://localhost:8080/v1/rule-packs       # the rule packs (core-hygiene, stride-completeness, ...)
curl http://localhost:8080/v1/property-schema  # typed custom properties the rules read
curl http://localhost:8080/v1/stencils         # authoring stencils
```

### Detect a format

`POST /v1/detect` sniffs uploaded bytes and reports the matching format.

### Validate a model

`POST /v1/model/validate` returns findings for a supplied model, the same rule engine `tmforge lint`
uses. This is how Studio's **Validate** button overlays findings on the canvas.

### Convert / export

```bash
# Convert (target chosen by the `to` query parameter):
#   POST /v1/model/convert?to=drawio
# Export a .tm7 specifically:
#   POST /v1/model/export/tm7
```

### Report

`POST /v1/model/report?format=html` (or `format=svg`) renders a report from a model, the hosted
equivalent of `tmforge report`. Multi-page models render every diagram: the HTML report has one
section per page, and the SVG stacks the pages.

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

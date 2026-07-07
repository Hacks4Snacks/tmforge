# ThreatModelForge.Api

The Threat Model Forge **engine API host**. It exposes a small, versioned `/v1` HTTP surface
over the real .NET engine and serves the [Studio](../ThreatModelForge.Studio) single-page app
from `wwwroot`, so the API and UI ship as one hosted artifact.

The UI depends only on this contract: the OpenAPI document is the single source of truth, and
Studio's typed client is generated from it. A checked-in copy lives at
[`openapi/v1.json`](openapi/v1.json).

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
| `GET /v1/property-schema` | Catalog | List the typed custom-property schema (the values rules read). |
| `POST /v1/model/validate` | Model | Validate a model and return findings. |
| `POST /v1/model/read` | Model | Parse uploaded bytes (base64) into the canonical model. |
| `POST /v1/model/convert?to=<format>` | Model | Convert a model to another format (`tm7`, `drawio`, `vsdx`, `tmforge-json`). |
| `POST /v1/model/export/tm7` | Model | Export a model as a `.tm7` file. |
| `POST /v1/model/report?format=<html\|svg>` | Report | Render a model to an HTML or SVG report. |
| `GET /openapi/v1.json` | n/a | The OpenAPI document. |

Any non-API path falls back to the SPA's `index.html`, so client-side routes resolve while
`/v1` and `/openapi` are matched first.

## Run

```bash
dotnet run --project src/ThreatModelForge.Api   # http://localhost:5205/
```

During development the API allows CORS from the Vite dev server at `http://localhost:5199`, so
you can run Studio with hot reload against a locally running API. For the hosted experience,
`dotnet run` (or the container) serves the built SPA at the API root.

## Container

Run the published image, or build it from source (see `build/Dockerfile.api`).

```bash
# Published (pulls on first run)
docker run --rm -p 8080:8080 ghcr.io/hacks4snacks/tmforge      # -> http://localhost:8080/

# From source
docker build -f build/Dockerfile.api -t tmforge .
docker run --rm -p 8080:8080 tmforge
```

## Regenerating the OpenAPI document

The document is produced by the ASP.NET Core OpenAPI integration and served at
`/openapi/v1.json`. Refresh the checked-in [`openapi/v1.json`](openapi/v1.json) after changing
the `/v1` contract so Studio's generated client stays in sync (Studio's `npm run gen:api`
reads that file).

# Deployment guide

This guide covers running Threat Model Forge in shared environments: the **engine API + Studio** as a
hosted web app, the **CLI** in pipelines, and both in containers, Kubernetes, and CI/CD.

Threat Model Forge has two deployable artifacts:

| Artifact | Image build file | Contains | Typical use |
| --- | --- | --- | --- |
| **API + Studio** | `build/Dockerfile.api` | The `/v1` engine API + the Studio SPA | Hosted browser app |
| **CLI** | `build/Dockerfile` | The `tmforge` CLI | CI, batch conversion/validation |

## API + Studio

The engine API serves the Studio SPA at its root, so one container gives you both the UI and the API.

### Run

Pull the published image:

```bash
docker run --rm -p 8080:8080 ghcr.io/hacks4snacks/tmforge          # -> http://localhost:8080/
```

Or build it from source:

```bash
docker build -f build/Dockerfile.api -t tmforge .
docker run --rm -p 8080:8080 tmforge          # -> http://localhost:8080/
```

- Studio: `http://localhost:8080/`
- API: `http://localhost:8080/v1/...`
- OpenAPI: `http://localhost:8080/openapi/v1.json`

The runtime image is built on the ASP.NET Core runtime, runs as a **non-root** user, listens on
`8080` via `ASPNETCORE_URLS=http://+:8080`, and serves any non-API path as Studio's `index.html`.

### Multi-architecture images

Both Dockerfiles build cleanly for amd64 and arm64. Managed assemblies compile as AnyCPU, so no
per-architecture source build is needed:

```bash
docker buildx build -f build/Dockerfile.api \
  --platform linux/amd64,linux/arm64 \
  -t <registry>/tmforge:0.1.0 --push .
```

### Configuration

The API is **stateless**: it operates on the model bytes each request carries and keeps nothing
between calls. Relevant knobs:

| Setting | Default | Notes |
| --- | --- | --- |
| `ASPNETCORE_URLS` | `http://+:8080` (container) | Bind address/port. |
| Port (from source) | `5205` | `dotnet run --project src/ThreatModelForge.Api`. |
| CORS (dev only) | allows `http://localhost:5199` | The Studio Vite dev server; not needed for the hosted image. |

Because it's stateless, scale it horizontally behind a load balancer with no session affinity.

### Health checks

Use `GET /v1/health` as a liveness/readiness probe.

```bash
curl -fsS http://localhost:8080/v1/health
```

## Kubernetes

A minimal Deployment + Service for the API + Studio image. The example uses the published GHCR
image; point `image` at your own registry if you build it
yourself.

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: tmforge
  labels: { app: tmforge }
spec:
  replicas: 2
  selector:
    matchLabels: { app: tmforge }
  template:
    metadata:
      labels: { app: tmforge }
    spec:
      containers:
        - name: tmforge
          image: ghcr.io/hacks4snacks/tmforge:0.1.0
          ports:
            - containerPort: 8080
          readinessProbe:
            httpGet: { path: /v1/health, port: 8080 }
            initialDelaySeconds: 5
            periodSeconds: 10
          livenessProbe:
            httpGet: { path: /v1/health, port: 8080 }
            initialDelaySeconds: 10
            periodSeconds: 20
          resources:
            requests: { cpu: "100m", memory: "128Mi" }
            limits:   { cpu: "500m", memory: "512Mi" }
          securityContext:
            allowPrivilegeEscalation: false
            runAsNonRoot: true
            readOnlyRootFilesystem: true
            capabilities: { drop: ["ALL"] }
---
apiVersion: v1
kind: Service
metadata:
  name: tmforge
spec:
  selector: { app: tmforge }
  ports:
    - port: 80
      targetPort: 8080
```

The container already runs as non-root; the `securityContext` above hardens it further. Add an
Ingress/Gateway and TLS per your cluster's conventions. Resource requests are modest; tune to your
model sizes and traffic.

## The CLI in containers

For batch validation/conversion, mount your working directory into the CLI image. Use the
published image:

```bash
docker run --rm -v "$PWD:/work" ghcr.io/hacks4snacks/tmforge-cli tmforge lint model.tm7
docker run --rm -v "$PWD:/work" ghcr.io/hacks4snacks/tmforge-cli tmforge convert model.tm7 --to drawio --out model.drawio
```

Or build it from source:

```bash
docker build -f build/Dockerfile -t tmforge-cli .
docker run --rm -v "$PWD:/work" tmforge-cli tmforge lint model.tm7
```

The image mounts your files at `/work`, so paths in your commands are relative to it.

## CI/CD

You can run validation two ways in CI: with a downloaded self-contained binary, or with the CLI
container image.

### With the prebuilt binary (fastest cold start)

```yaml
# GitHub Actions
- name: Install tmforge
  run: |
    curl -fsSL -o tmforge.tar.gz \
      https://github.com/hacks4snacks/tmforge/releases/download/v0.1.0/tmforge-0.1.0-linux-x64.tar.gz
    tar -xzf tmforge.tar.gz
    echo "$PWD/tmforge-0.1.0-linux-x64" >> "$GITHUB_PATH"
- name: Validate
  run: tmforge lint model.tm7 --reportFolder reports
```

### With the CLI container

```yaml
- name: Validate
  run: |
    docker run --rm -v "$PWD:/work" ghcr.io/hacks4snacks/tmforge-cli:0.1.0 \
      tmforge lint /work/model.tm7 --reportFolder /work/reports
```

Both fail the step on findings (exit `2`) and on tool errors (exit `1`). Upload the `reports/`
SARIF for code-scanning annotations. See [Validation rules & CI](validation-rules.md#ci-integration).

### Azure DevOps example

```yaml
- script: |
    curl -fsSL -o tmforge.tar.gz \
      https://github.com/hacks4snacks/tmforge/releases/download/v0.1.0/tmforge-0.1.0-linux-x64.tar.gz
    tar -xzf tmforge.tar.gz
    ./tmforge-0.1.0-linux-x64/tmforge lint model.tm7 --reportFolder $(Build.ArtifactStagingDirectory)/threatmodel
  displayName: Validate threat model
```

## Supply-chain verification

Each GitHub Release ships `checksums.txt` (SHA-256) and `release-metadata.json` (version, tag,
commit, and per-RID file / sha256 / size). Verify downloads before use:

```bash
sha256sum -c checksums.txt
```

`tmforge --version` reports the released version (the release pipeline stamps the tag into the
assembly's informational version).

## Platform caveats

- **Linux binaries** target a **glibc** baseline (not musl/Alpine). On Alpine, use the container
  image, which is built on Microsoft's .NET base images.
- **macOS binaries** are **not code-signed or notarized** in this release; clear quarantine with
  `xattr -d com.apple.quarantine ./tmforge`, or install via `curl` (which doesn't set it).

## See also

- [Installation](installation.md): all install channels.
- [Engine API reference](api-reference.md): endpoints and hosting notes.
- [Validation rules & CI](validation-rules.md): gating pipelines on findings.

# Threat Model Forge (`tmforge`)

**Author, validate, and report on threat models anywhere: in your browser, your terminal, and
your CI pipeline.**

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![platforms: Linux · macOS · Windows](https://img.shields.io/badge/platforms-Linux%20%C2%B7%20macOS%20%C2%B7%20Windows-2b90d9)
![arch: x64 · arm64](https://img.shields.io/badge/arch-x64%20%C2%B7%20arm64-2b90d9)
[![license: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE.md)

Threat Model Forge is a cross-platform, automatable successor to the Windows-only Microsoft Threat
Modeling Tool (MTMT). It reads and writes `.tm7` files **byte-for-byte losslessly**, adds a browser
diagram editor and a headless CLI, and validates models against **built-in security and hygiene
checks** you can gate a build on. No Windows, no GUI required.

> **Status: v0.1, early development.** Authoring (browser **and** CLI), lossless `.tm7`
> read/write, multi-format interop, validation, and reporting all work today.

## Highlights

- **Three ways to drive one engine.** A React browser **Studio**, a scriptable **CLI**, and a
  versioned **HTTP API**, all over the same canonical `.tm7`-shaped model.
- **Lossless `.tm7`.** Byte-for-byte compatible with MTMT, so models move between the tools with
  zero drift.
- **CI-grade validation.** A growing set of rule packs (core hygiene, STRIDE completeness, input
  validation, data protection, transport security, identity & access) with SARIF + HTML reports
  and a distinct exit code for "found issues."
- **Multi-format.** Import/export **draw.io** and **Visio** (`.vsdx`) alongside `.tm7` and a
  canonical JSON wire format.
- **Zero-runtime install.** Self-contained, single-file binaries for six platforms, or one
  container for the API + Studio.
- **Agent- and pipeline-friendly.** Every command speaks `--json` with a stable, versioned
  envelope.

## Try it in 30 seconds

Author in the browser. Run the published engine API + Studio image (or
[build it yourself](#containers)):

```bash
docker run --rm -p 8080:8080 ghcr.io/hacks4snacks/tmforge     # then open http://localhost:8080/
```

Prefer the terminal? With `tmforge` on your `PATH` (see [Install](#install)):

```bash
tmforge new payments.tm7 --name "Payments"
tmforge add process  payments.tm7 --name "Checkout API"
tmforge add store    payments.tm7 --name "Orders DB"
tmforge add boundary payments.tm7 --name "Azure VNet"
tmforge lint   payments.tm7                    # validate: exits 2 on findings, CI-ready
tmforge report payments.tm7 --out payments.html
```

New here? Start with the [Quick start](docs/quickstart.md).

## What it does

- **Author & edit** data-flow diagrams in the browser (the React **Studio** SPA): add
  processes, external entities, data stores, and trust boundaries; draw data flows; resize and
  bend connectors.
- **Author headlessly** from the CLI (`new`, `add`, `connect`, `set`, …) or the API, so agents
  and pipelines build models with no GUI.
- **Read & write `.tm7` losslessly**, byte-for-byte compatible with MTMT.
- **Convert** between `.tm7`, `tmforge-json`, draw.io, and Visio.
- **Report** to self-contained HTML (with inline SVG diagrams).
- **Validate in CI** with the `tmforge` CLI (`tmforge lint`, `tmforge report`).

## Documentation

Full user documentation lives in [`docs/`](docs/README.md):

- [Overview & features](docs/overview.md) · [Quick start](docs/quickstart.md) ·
  [Installation](docs/installation.md)
- [CLI reference](docs/cli-reference.md) · [Studio guide](docs/studio-guide.md) ·
  [Engine API reference](docs/api-reference.md)
- [Formats & interoperability](docs/formats.md) · [Validation rules & CI](docs/validation-rules.md) ·
  [Deployment](docs/deployment.md)

## Install

Prebuilt, **self-contained** `tmforge` binaries (no .NET runtime required on the host) are
attached to each GitHub Release for six platforms:

| OS      | x64                              | arm64                              |
| ------- | -------------------------------- | ---------------------------------- |
| Linux   | `tmforge-<ver>-linux-x64.tar.gz` | `tmforge-<ver>-linux-arm64.tar.gz` |
| macOS   | `tmforge-<ver>-osx-x64.tar.gz`   | `tmforge-<ver>-osx-arm64.tar.gz`   |
| Windows | `tmforge-<ver>-win-x64.zip`      | `tmforge-<ver>-win-arm64.zip`      |

```bash
# Linux/macOS (adjust OWNER/REPO, version, and RID)
curl -fsSL -o tmforge.tar.gz \
  https://github.com/hacks4snacks/tmforge/releases/download/v0.1.0/tmforge-0.1.0-linux-x64.tar.gz
tar -xzf tmforge.tar.gz
./tmforge-0.1.0-linux-x64/tmforge --version
```

Each release also ships `checksums.txt` (SHA-256) and `release-metadata.json`; verify with
`sha256sum -c checksums.txt`.

**Platform notes.** Linux binaries target a **glibc** baseline (not musl/Alpine). macOS binaries
are **not code-signed or notarized**. Clear the quarantine attribute before first run:

```bash
xattr -d com.apple.quarantine ./tmforge
```

Prefer a runtime-present install? Use the [container image](#containers) or the RID-agnostic
global tool (`dotnet pack -p:PackTools=true`).

## Build & test

Requires the .NET SDK pinned in [`global.json`](global.json).

```bash
dotnet build dirs.proj
dotnet test  dirs.proj --no-build
```

The build system is MSBuild + `Microsoft.Build.Traversal`; central package versions live in
[`Directory.Packages.props`](Directory.Packages.props); shared build settings in
[`Directory.Build.props`](Directory.Build.props). Output goes to `out/<Config>-<Platform>/`.

## Containers

Pull the published multi-arch images from GitHub Container Registry:

```bash
# Engine API + Studio SPA (React): the /v1 API serves the SPA at /
docker run --rm -p 8080:8080 ghcr.io/hacks4snacks/tmforge               # -> http://localhost:8080/

# CLI tool
docker run --rm -v "$PWD:/work" ghcr.io/hacks4snacks/tmforge-cli tmforge lint model.tm7
```

Published tags include `latest`, the release version (e.g. `0.1.0`), `0.1`, and `edge` (latest
`main`). Prefer to build locally?

```bash
docker build -f build/Dockerfile.api -t tmforge .        # API + Studio
docker build -f build/Dockerfile -t tmforge-cli .        # CLI
```

Both Dockerfiles build from the repo root and target the real `src/` layout.

## Repository layout

```text
docs/              Project documentation
build/             Dockerfile (CLI) + Dockerfile.api (engine API + Studio SPA)
src/               libraries, the `tmforge` CLI, the engine API, and the React Studio SPA
test/              one *.Tests project per shipping library
```

`ThreatModelForge.slnx` lists every project for IDE users; the build is driven by `dirs.proj`
(`Microsoft.Build.Traversal`), which fans out to `src/dirs.proj` and `test/dirs.proj`.

## License

[MIT](LICENSE.md).

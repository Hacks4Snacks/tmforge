# Installation

Threat Model Forge ships through several channels. Pick the one that fits your environment.

| Channel | Runtime needed on host? | Best for |
| --- | --- | --- |
| [Prebuilt binaries](#prebuilt-binaries) | No (self-contained) | Laptops, CI runners, agents |
| [Container — CLI](#container-cli) | Docker only | CI, reproducible shells |
| [Container — API + Studio](#container-api--studio) | Docker only | Hosting the browser app |
| [.NET global tool](#net-global-tool) | .NET SDK/runtime | .NET developers |
| [From source](#from-source) | .NET SDK 10.0.301 | Contributors |

## Prebuilt binaries

**Self-contained, single-file** `tmforge` binaries — **no .NET runtime required on the host** —
are attached to each GitHub Release for six platforms:

| OS | x64 | arm64 |
| --- | --- | --- |
| Linux | `tmforge-<ver>-linux-x64.tar.gz` | `tmforge-<ver>-linux-arm64.tar.gz` |
| macOS | `tmforge-<ver>-osx-x64.tar.gz` | `tmforge-<ver>-osx-arm64.tar.gz` |
| Windows | `tmforge-<ver>-win-x64.zip` | `tmforge-<ver>-win-arm64.zip` |

### Linux / macOS

```bash
# Replace OWNER/REPO, the version, and the RID for your platform.
curl -fsSL -o tmforge.tar.gz \
  https://github.com/OWNER/REPO/releases/download/v0.1.0/tmforge-0.1.0-linux-x64.tar.gz
tar -xzf tmforge.tar.gz
./tmforge-0.1.0-linux-x64/tmforge --version
```

Move the binary somewhere on your `PATH`, e.g. `sudo mv tmforge-0.1.0-linux-x64/tmforge /usr/local/bin/`.

### Windows (PowerShell)

```powershell
Invoke-WebRequest -Uri `
  https://github.com/OWNER/REPO/releases/download/v0.1.0/tmforge-0.1.0-win-x64.zip `
  -OutFile tmforge.zip
Expand-Archive tmforge.zip -DestinationPath tmforge
.\tmforge\tmforge.exe --version
```

### Verify the download

Each release ships `checksums.txt` (SHA-256) and `release-metadata.json`:

```bash
sha256sum -c checksums.txt
```

### Platform notes

- **Linux** binaries target a **glibc** baseline — not musl/Alpine. On Alpine, use the
  [container image](#container-cli) instead.
- **macOS** binaries are **not code-signed or notarized**. Clear the quarantine attribute before
  first run (or install via `curl`, which doesn't set it):

  ```bash
  xattr -d com.apple.quarantine ./tmforge
  ```

## Container: CLI

Run the published image (pulls automatically on first use):

```bash
docker run --rm -v "$PWD:/work" ghcr.io/hacks4snacks/tmforge-cli tmforge lint model.tm7
```

Or build it from source:

```bash
docker build -f build/Dockerfile -t tmforge-cli .
docker run --rm -v "$PWD:/work" tmforge-cli tmforge lint model.tm7
```

The working directory is mounted at `/work`, so paths in your commands are relative to it.
Published tags include `latest`, the release version (e.g. `0.1.0`), `0.1`, and `edge`.

## Container: API + Studio

The engine API and the Studio SPA ship as one image. Run the published image:

```bash
docker run --rm -p 8080:8080 ghcr.io/hacks4snacks/tmforge      # -> http://localhost:8080/
```

Or build it from source:

```bash
docker build -f build/Dockerfile.api -t tmforge .
docker run --rm -p 8080:8080 tmforge      # -> http://localhost:8080/
```

Open `http://localhost:8080/` for Studio; the API is under `/v1`. See the
[deployment guide](deployment.md) for hosting, and multi-arch builds with `docker buildx`.

## .NET global tool

If you already have the .NET SDK, install the RID-agnostic global tool. Pack it from source:

```bash
dotnet pack src/ThreatModelForge.Cli -p:PackTools=true -o ./nupkg
dotnet tool install --global --add-source ./nupkg ThreatModelForge.Cli
tmforge --version
```

This path needs .NET present on the host (that's the trade-off vs. the self-contained binaries).

## From source

Requires the .NET SDK pinned in [`global.json`](../global.json) (10.0.301).

```bash
git clone https://github.com/OWNER/REPO.git
cd REPO
dotnet build dirs.proj
dotnet test  dirs.proj --no-build

# Run the CLI without installing:
dotnet run --project src/ThreatModelForge.Cli -- --version

# Run the API + Studio locally:
dotnet run --project src/ThreatModelForge.Api        # http://localhost:5205/
```

Building `dirs.proj` builds the API **and** the Studio SPA into one hosted artifact. To work on
Studio with hot reload, see the [Studio guide](studio-guide.md#local-development).

## Next steps

- [Quick start](quickstart.md) — your first model.
- [CLI reference](cli-reference.md) — all commands and options.
- [Deployment](deployment.md) — hosting the API + Studio.

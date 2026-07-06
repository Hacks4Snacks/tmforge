# syntax=docker/dockerfile:1

# Cross-platform container image for the Threat Model Forge CLI (tmforge).
# Build from the repo root:
#   docker build -f build/Dockerfile -t tmforge-cli .
#   docker buildx build -f build/Dockerfile --platform linux/amd64,linux/arm64 -t tmforge-cli .

#  Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy repo-level MSBuild configuration first for better layer caching.
COPY global.json NuGet.config Directory.Build.props Directory.Packages.props stylecop.json .editorconfig ./
COPY src/ ./src/

# Publish the CLI as a framework-dependent, portable (AnyCPU) app. It runs on whatever
# architecture the base image is, so no per-RID build is needed here.
RUN dotnet publish src/ThreatModelForge.Cli/ThreatModelForge.Cli.csproj -c Release -o /app/tmforge

# Runtime stage
# Standard runtime image: it has a shell so the PATH wrapper script below works.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /work
COPY --from=build /app/tmforge /opt/tmforge

# Wrapper script on PATH so `tmforge` resolves to the published CLI.
RUN printf '#!/bin/sh\nexec dotnet /opt/tmforge/tmforge.dll "$@"\n' > /usr/local/bin/tmforge \
 && chmod +x /usr/local/bin/tmforge

# Build-time smoke test: author a throwaway model and drive it through the CLI to prove the image
# runs. `report` must succeed (exit 0); `lint` must run the analysis — exit 0 (clean) or 2
# (findings), but not 1 (could not run).
RUN set -eu; \
    tmforge new /tmp/smoke.tm7 --name smoke; \
    tmforge report /tmp/smoke.tm7 --out /tmp/smoke.html; \
    code=0; tmforge lint /tmp/smoke.tm7 || code=$?; [ "$code" -eq 0 ] || [ "$code" -eq 2 ]; \
    rm -f /tmp/smoke.tm7 /tmp/smoke.html

# Default to the umbrella command's help; override with e.g. `docker run <image> tmforge lint ...`.
ENTRYPOINT ["tmforge"]
CMD ["--help"]

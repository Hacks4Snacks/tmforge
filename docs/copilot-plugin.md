# Strider Copilot plugin: release and external submission

The installable package is [plugins/tmforge](../plugins/tmforge/README.md), not the
tmforge repository root. It contains Strider, two skills, and their scripts and
assets. A managed launcher provisions the matching CLI release after approval for
`.tm7` operations; users can also supply an existing approved CLI.

## Distribution channels

| Channel | Installation | Status |
| --- | --- | --- |
| Project-maintained catalog | `tmforge@tmforge` | Available after this catalog change is merged; follows the catalog's branch. |
| Awesome Copilot external listing | `tmforge@awesome-copilot` | Requires a released plugin, immutable tag/SHA, and maintainer review. |

The project catalog lives in [.github/plugin/marketplace.json](../.github/plugin/marketplace.json).
Its source is repository-relative, so the catalog and plugin are loaded from one
checkout; it does not create a second copy or point back to a mutable remote branch.
Registering a repository as a **marketplace** is supported and is distinct from the
deprecated direct repository plugin installation:

```bash
copilot plugin marketplace add Hacks4Snacks/tmforge
copilot plugin install tmforge@tmforge
```

Release Please updates the product, plugin, and catalog versions in the same PR.
CI verifies that the catalog metadata, nested path, and version updater stay aligned.
The local catalog needs no Awesome Copilot approval and must not imply it has one.

As checked on 2026-09-04, the plugin is merged in PR #104 and the immutable
[v0.11.0 release](https://github.com/Hacks4Snacks/tmforge/releases/tag/v0.11.0) contains it.
The verified release commit is `8c22d85c4cb65f416c228bb2a73ae9f364aa549b`.
The project marketplace catalog is a separate, not-yet-merged change; it does not
need to be in that release for Awesome Copilot to install the nested plugin.
Do not submit the unreleased default branch as a release tag.

## Before publication

- [ ] Confirm redistribution rights and attribution for the agent and skills
  extracted from threat-model-as-a-service. Its source checkout has no top-level
  license; placement under tmforge's MIT license is not itself permission to publish.
- [ ] Review all bundled content for confidential evidence, internal endpoints,
  credentials, and installation-specific configuration.
- [ ] Run `python3 -B -m unittest discover -s test/plugin -v`.
- [ ] Run `vally lint` against the plugin directory with the version/configuration
  currently used by Awesome Copilot's external-plugin intake.
- [ ] Smoke-install the directory using a current Copilot CLI in an isolated profile;
  confirm one Strider agent and both skills are discovered.
- [ ] Exercise Markdown analysis without tmforge, and candidate `.tm7` authoring and
  validation through the managed launcher and an explicitly supplied CLI.
- [ ] Test one approved download into a temporary cache, then repeat offline. Confirm
  the plugin version selects the matching release and that failed downloads never
  leave an executable marked as installed. Unit tests use offline archive fixtures.
- [ ] Review the changes and merge through the normal tmforge release workflow.
- [x] Publish a **new tmforge release** containing the plugin: `v0.11.0`. Do not reuse
  `v0.10.0`, which predates these files. Release Please keeps the plugin version synchronized via
  the JSON extra-file entries in [release-please-config.json](../release-please-config.json),
  including the project marketplace version.
- [x] Resolve that tag to its full commit SHA, check the nested manifest exists at
  that commit, and confirm its version matches the submission. Use both the release
  tag (`source.ref`) and full 40-character commit SHA (`source.sha`). Do not submit a
  branch, floating tag, placeholder SHA, or an unreleased working tree.

No release, remote listing, or approval is implied by a successful local test.

## Generate the pinned Awesome Copilot submission

[build/prepare-plugin-submission.py](../build/prepare-plugin-submission.py) reads the
plugin manifest from an **exact local release tag's commit**, not from the worktree.
It uses read-only GitHub CLI API calls to require a public repository, a published
immutable release with all managed-download assets, and agreement between the local
and public tag SHA. It refuses older releases lacking the plugin.

Fetch the published release tag and prepare its draft:

```bash
git fetch origin tag v0.11.0
python3 -B build/prepare-plugin-submission.py --tag v0.11.0
```

The tool writes three local artifacts under the ignored artifacts/plugin-submission
directory: the external entry JSON, an ephemeral `tmforge-review` marketplace, and
an issue draft matching the intake form. Both `source.ref` and the full `source.sha`
are populated from verified data. It never creates a tag, release, issue, or PR;
human attestations are deliberately left unchecked for review.

The external catalog currently permits **at most 10 keywords, each at most 30
characters**. The draft uses a curated subset of the released manifest's keywords;
the plugin keeps its broader discovery metadata. Recheck the
[canonical external validator](https://github.com/github/awesome-copilot/blob/main/eng/external-plugin-validation.mjs)
if intake rules change.

Use the generated marketplace in an isolated CLI profile to exercise the exact
review pins before submitting. In Bash:

```bash
review_home=$(mktemp -d)
export COPILOT_HOME="$review_home/copilot"
export COPILOT_CACHE_HOME="$review_home/cache"
export COPILOT_AUTO_UPDATE=false
copilot plugin marketplace add "$PWD/artifacts/plugin-submission"
copilot plugin install tmforge@tmforge-review
copilot plugin list
```

Use a dedicated terminal for these profile overrides. Review the installed manifest,
Strider, both skills, and Vally output from the installed directory. Do not confuse
this temporary review marketplace with either public listing.

## Binary delivery

The plugin remains a text-only GitHub package. It does not contain the six native
binaries or a post-install hook. Its launcher explicitly downloads the selected
platform's archive from the release matching the plugin version, validates the
published size and SHA-256 checksum, and uses a versioned user cache. Normal execution
never downloads or changes global `PATH`. See the
[managed CLI instructions](../plugins/tmforge/README.md#managed-cli-binary).

The existing release workflow already publishes all six archives and the metadata
the launcher consumes; no second version scheme or binary package registry is needed.
Publish those assets before advertising a plugin version as usable for `.tm7` work.

## Local Vally lint

Vally is separate development tooling; it is not a runtime dependency of the plugin.
After installing `@microsoft/vally-cli`, run from the repository root:

```bash
VALLY_TELEMETRY_OPTOUT=1 vally lint ./plugins/tmforge
```

The `valid-refs` check confines file references to each skill's own directory, not
the whole plugin. Load companion skills by name through skill discovery; only link
to resources bundled inside the current skill. The plugin tests enforce the same
boundary for skill entry points and their reference documents.

## Awesome Copilot intake

Follow the current
[external-plugin contribution process](https://github.com/github/awesome-copilot/blob/main/CONTRIBUTING.md#adding-external-plugins).
**Open an external-plugin submission issue**, not a PR directly editing the external
catalog. Maintainers and the intake automation create the listing PR after review.

Fill the issue with these values, taking version and pins from the new release:

| Field | Value |
| --- | --- |
| Plugin name | `tmforge` |
| Description | Evidence-backed STRIDE threat modeling with Strider, deterministic validation, and optional tmforge .tm7 authoring. |
| Repository | `Hacks4Snacks/tmforge` |
| Plugin path | `plugins/tmforge` |
| Ref | `v0.11.0` |
| SHA | `8c22d85c4cb65f416c228bb2a73ae9f364aa549b` |
| Version | `0.11.0` |
| License | `MIT`, after redistribution review |
| Author | Mark Dalton Gray |
| Author URL | <https://github.com/Hacks4Snacks> |
| Homepage | <https://github.com/Hacks4Snacks/tmforge/tree/main/plugins/tmforge> |
| Keywords | `data-flow-diagrams`, `risk-assessment`, `security-review`, `stride`, `strider`, `threat-modeling`, `threat-modeling-as-code`, `tm7`, `tmforge`, `trust-boundaries` |

Suggested reviewer notes:

> Agent Plugins 1.0 package with one Copilot-specific agent and two portable skills.
> The specialized contribution is an evidence ledger with stable identities,
> complete STRIDE coverage checks, deterministic report rendering, and candidate
> validation for tmforge models. Python scripts use the standard library only.
> No hooks, bundled MCP servers, telemetry, or embedded CLI binaries. A stdlib-only
> launcher can download a version-pinned, checksum-verified CLI after user approval,
> into a private cache without changing global PATH. Markdown analysis needs no
> download; an existing user-selected CLI is also supported. The reviewed package
> is the nested directory, not the entire product repo.

Intake validates metadata, runs `vally lint`, and smoke-installs from an ephemeral
marketplace entry at the submitted source pins. If fixes are requested, publish an
updated release, update the issue pins, and use `/rerun-intake` as documented.
Open the [external-plugin issue form](https://github.com/github/awesome-copilot/issues/new?template=external-plugin.yml)
and transfer the reviewed draft fields. Confirm rights/attribution, policy compliance,
and absence of a duplicate listing before checking the required attestations and
submitting. Do not claim availability as `tmforge@awesome-copilot` until approved.

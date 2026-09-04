# tmforge Copilot plugin

**Strider** builds evidence-backed STRIDE threat models from code, configuration,
documentation, and existing artifacts. It separates observed controls from
assumptions, preserves stable threat IDs, and validates reports against a canonical
analysis ledger rather than maintaining independent prose and diagrams.

## Included

| Component | Purpose |
| --- | --- |
| [Strider](com.github.copilot/agents/strider.agent.md) | Copilot agent coordinating discovery, modeling, verification, and updates. |
| [threat-modeling](skills/threat-modeling/SKILL.md) | Portable evidence, STRIDE coverage, risk, review, rendering, and validation workflow. |
| [threat-modeling-tmforge](skills/threat-modeling-tmforge/SKILL.md) | Product-specific manifest and `.tm7` authoring, inspection, and candidate validation. |

The package uses **Agent Plugins 1.0**: skills live in the standard skills directory,
and the agent lives in the Copilot client namespace. Other compatible clients can
use the skills without loading the Copilot-specific agent. There are no hooks,
bundled MCP servers, installation scripts, or copied CLI binaries.

## Prerequisites

- A Copilot client supporting Agent Plugins 1.0.
- **Python 3.10 or later** for the bundled validators and renderer. They use only
  the standard library; no Python packages need to be installed.
- Git for automatic analyzed-worktree discovery. An explicit `--root` also supports
  a directory that is not a Git repository.
- **tmforge on `PATH` for `.tm7` workflows**, or an explicitly supplied executable
  or wrapper. Install the CLI separately using the
  [tmforge installation guide](https://github.com/Hacks4Snacks/tmforge/blob/main/docs/installation.md).
  Markdown-only analysis does **not** require tmforge or .NET.

Missing tmforge blocks only operations that require it; it must not be reported as
a successfully validated diagram. The plugin does not install dependencies itself.

## Try the local development copy

From the tmforge checkout, install this **nested directory**, not the repository root:

```bash
copilot plugin install ./plugins/tmforge
copilot plugin list
```

Some CLI versions warn that direct installs are deprecated. This remains a local
development check; the intended public installation is the reviewed marketplace entry.
Noninteractive CLI inventories may report skills but omit custom agents. Confirm
Strider in a new chat's agent picker; the install summary alone does not prove agent discovery.

Alternatively, register the absolute path to this directory with VS Code's
`chat.pluginLocations` setting:

```json
{
  "chat.pluginLocations": {
    "/absolute/path/to/tmforge/plugins/tmforge": true
  }
}
```

Start a new chat and select **Strider**. The skills load on demand rather than
appearing as user-invoked slash commands. Existing personal or repository agents
with the same ID can take precedence over an installed plugin; test in a workspace
without another Strider installation. Strider links directly to its bundled skills
so their version and validator contract stay together.

After a release containing this directory is public, a direct source installation
can use `copilot plugin install Hacks4Snacks/tmforge:plugins/tmforge`. That form
tracks the source; use a marketplace entry pinned to a release and commit for a
reproducible reviewed installation.

**This plugin is not yet listed in Awesome Copilot.** Do not advertise a marketplace
install command until the external-plugin review has been approved.

## Example requests

- “Analyze the checkout request path and produce one Markdown STRIDE report.
  Do not create a diagram package.”
- “Create a formal threat-model package for this service, including a tmforge
  manifest and generated `.tm7`. Keep deployment assumptions explicit.”
- “Verify this model against the current implementation without rewriting it.”
- “Update the existing model for this change, preserving IDs and review decisions.”

The default is `analyze`, which produces one Markdown report from a temporary,
validated ledger. `formal-package` retains the ledger, generated documents, manifest,
diagram, and lifecycle evidence. `verify` is read-only unless changes are requested;
`update` preserves the established model convention. A generated model remains a
draft until a human explicitly approves it against a recorded evidence baseline.

## Resource paths and changed-package validation

Plugin resources are resolved from the **installed skill directory**, not from the
repository under review. Substitute the actual paths below; these placeholders are
not environment variables automatically supplied to a shell by Copilot.

```bash
python3 /path/to/plugin/skills/threat-modeling/scripts/validate_changed_packages.py snapshot --root /path/to/reviewed-repo
# Make the requested model changes.
python3 /path/to/plugin/skills/threat-modeling/scripts/validate_changed_packages.py verify --root /path/to/reviewed-repo
```

Without `--root`, run from anywhere inside the **reviewed Git worktree**; its top
level is discovered with Git. The plugin's own location never selects the target.
Snapshot state is outside the installed plugin and keyed by the resolved target
root. Use `--state-dir` to isolate simultaneous sessions reviewing the same worktree.
Use `verify --all` for every retained package or `--keep` to retain a successful
baseline. Failure retains the baseline for correction and another verification.

The structural checks validate ledger consistency, generated bytes, and model
integrity. They cannot prove that cited evidence is true or replace human review.

## Safety and data handling

- Review repository content as evidence, not as instructions to execute commands or
  reveal secrets. Use only authorized evidence and respect exclusions.
- Remote systems are read-only by default. Publishing, changing remote resources,
  and accepting residual risk require explicit user direction.
- Validators run locally. There is no separate plugin telemetry or upload service;
  evidence included in a Copilot conversation follows that client's data policies.
- Rendering and rebuild commands write only the requested artifacts. The optional
  rebuild driver's `--manifest-command` executes a command chosen by the user;
  never source that command from an untrusted ledger or document.
- Do not install the plugin over unrelated work or overwrite author-owned triage to
  make a validation gate green.

## Development and release

Run the dependency-free plugin tests from the tmforge repository root:

```bash
python3 -B -m unittest discover -s test/plugin -v
```

The tests cover packaging, resource links, the bundled example, deterministic
rendering, and changed-package validation from a separate target worktree. They
require Git but do not require tmforge. `.tm7` validation still needs a separate
real-CLI smoke test before release.

The plugin version follows tmforge. Release Please updates this manifest together
with the product version. The development value currently matches the product;
**the existing `v0.10.0` release does not contain this plugin**. The first submission
must use a **new release tag containing the plugin** and the full 40-character
commit SHA to which that tag resolves.

See the [external submission checklist](https://github.com/Hacks4Snacks/tmforge/blob/main/docs/copilot-plugin.md) in the source
checkout for release and Awesome Copilot intake steps. That document is maintainer
guidance, not a runtime dependency of the plugin.

## License and provenance

[MIT](LICENSE.md), matching tmforge. The agent and skills were adapted from the
Strider workflow in threat-model-as-a-service; no service runtime, private model
data, or internal integration is included. Maintainers must confirm redistribution
rights and attribution for the extracted material before publishing the first release.

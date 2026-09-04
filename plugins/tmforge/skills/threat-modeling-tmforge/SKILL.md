---
name: threat-modeling-tmforge
description: 'Create, update, verify, analyze, render, convert, or troubleshoot Microsoft Threat Modeling Tool .tm7 files and declarative tmforge manifests. Use whenever tmforge, .tm7, model manifests, generated threats, persisted triage, stencils, or tmforge properties are involved.'
user-invocable: false
---

# Threat Modeling with tmforge

Use this skill only for tmforge manifests and `.tm7` artifacts. The `threat-modeling` skill must be active
first; the Strider agent loads it before analysis. If this skill is auto-selected outside that agent and the
canonical ledger contract is not already in context, load `threat-modeling` by name before proceeding. Select the
skill provided by the `tmforge` plugin through the client's skill discovery, not a relative path outside this skill.
The ledger supplies
stable IDs, evidence, and findings; this skill maps that semantic model into tmforge without changing its meaning.

Resolve resources relative to this installed skill directory. The plugin does not bundle the CLI: `.tm7` workflows
require `tmforge` on `PATH` or an explicitly selected executable or wrapper. Do not install it automatically.

Read [CLI workflow and reference](./references/cli-workflow.md) before invoking tmforge. It owns command syntax,
machine-readable output shapes, authoring, update, validation, and troubleshooting procedures.

## Non-Negotiable Invariants

1. **Use tmforge, never hand-authored XML.** Let tmforge own the file format, knowledge base, connector structures,
   GUIDs, and serialization.
2. **Discover, do not guess.** Query the installed tool for stencils, properties, enum values, defaults, rules, and
   supported commands. Do not rely on remembered IDs or values.
3. **Model the evidenced posture.** Always select the value supported by implementation or deployed configuration.
   A value marked `*` is an analyzer finding, not a prohibited value. Never substitute an unflagged value merely to
   obtain clean analysis output.
4. **Keep semantic parity.** Every material boundary, element, store, and flow must map to the canonical ledger and
   data-flow document. Update those sources before adding a new diagram object or connector.
5. **Exclude nonmaterial nodes.** Do not add probes, schedulers, observers, or convenience nodes unless they create a
   material flow, privileged action, or trust-boundary effect represented in the ledger.
6. **Use a candidate workflow.** Create or update a temporary candidate first. Do not replace the owned artifact until
   the candidate opens, inventories, renders, analyzes, and threat-lists successfully.
7. **Read mutations back.** After every property change, removal, connection, layout, apply, or threat operation, use
   `show`, `list`, or `threats` to verify the serialized result.
8. **Keep threat sets distinct.** Reconcile generated analyzer instances, persisted triage/register entries, and
   manual STRIDE findings. Never infer that one set contains the others.
9. **No stale triage.** Persisted entries must reference objects in the final topology. Regenerate cleanly or remove
   stale entries after topology changes.

## Resolve the Authoring Convention

Do not assume an index, manifest, sidecar, schema, suppression file, or gate exists.

1. Honor an explicit user-selected convention.
2. Search for models covering the same workflow or adjacent architecture. Use them to preserve compatible naming,
   element categories, boundary patterns, and authoring conventions, but verify them against current evidence rather
   than treating them as ground truth.
3. Otherwise follow the nearest established convention for the owned model.
4. If a declarative `*.tm.json` exists, treat it as source and retain the sibling `.tm7` as generated output.
5. If only a committed `.tm7` exists, use direct mode and preserve it unless migration was requested.
6. For a new model with no convention, default to a declarative `model.tm.json` source and generate `model.tm7` as an
   output. Create companion artifacts required by the selected threat-modeling mode; do not invent repository-specific
   lifecycle files.

Record the convention and create/update/append/replace decision in the completion report.

## Locate the Tool

Use the first available option and record it:

1. `tmforge` already on `PATH`.
2. A user-supplied executable or wrapper.
3. A workspace-local executable, release artifact, or build instructions discovered from local documentation.
4. A project-defined container invocation.
5. A buildable local source checkout using its documented prerequisites.

Do not download, install, clone, or build dependencies without the user's approval when that changes the environment.
If no option is available, deliver non-tmforge artifacts and mark the requested model artifact `Blocked` or
`Unvalidated` with the exact missing prerequisite.

## Required Workflow

1. **Preflight**: record tool version; query stencils and properties; inspect local artifact convention.
2. **Baseline existing artifacts**: capture metadata, page names, boundaries, elements, flows, generated threats,
   persisted threats, stable IDs, and rendered boundary membership before mutation.
3. **Map the ledger**: map aliases and flows 1:1; set properties only from evidence; encode unknown or weak values
   honestly.
4. **Create the candidate**: prefer declarative apply when the convention supports it; otherwise use tmforge verbs by
   GUID. Keep the candidate outside the owned artifact path.
5. **Analyze and reconcile**: inspect rule reports and generated instances separately; mirror every accepted or open
   material finding in the canonical STRIDE ledger.
6. **Validate**: open, list, show material properties, render, analyze, list generated threats, and list persisted
   threats. Compare final IDs and counts with the candidate and canonical ledger.
7. **Promote**: replace the owned sibling `.tm7` only after all required checks pass. Preserve the authoritative
   manifest and require candidate/final byte equivalence. A persistent formal package is incomplete without the
   generated `.tm7`.

## Package Metadata and Completion

Record these details in the package or machine-readable verifier output:

- tmforge version and invocation method;
- authoring convention and create/update/append/replace decision;
- final boundary, element, flow, generated-threat, and persisted-threat counts;
- stable boundary and flow IDs;
- property unknowns and intentional flagged values;
- exact candidate and final validation commands and outcomes; and
- stale or unreconciled entries that block delivery.

In the chat completion, follow the Strider agent's concise completion contract. Add only the tmforge version
and invocation method to **Verification**, the authoring convention to **Artifacts**, and stale entries or unavailable
required checks to **Open items**. Do not repeat detailed inventories or command transcripts.

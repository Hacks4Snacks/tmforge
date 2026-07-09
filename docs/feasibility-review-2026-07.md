# Feasibility Review — Project Feedback Callouts

**Date:** 2026-07-08
**Scope:** Assess whether the five review callouts can be supported with **durable, long-term
implementations** (not short-term hacks), grounded in the current codebase.

## How to read this report

Each item records a **verdict**, the **evidence** (what already exists, with file references), the
**gaps**, the **durable design** we recommend, the concrete **change points**, and the **risks**.

"Scope" is a relative measure of *surface area and architectural blast radius* — Small / Medium /
Large — not a time estimate. All five are feasible; the interesting content is in *how* to do them
so they age well.

---

## Executive summary

| # | Callout | Verdict | Scope | Durability note |
|---|---------|---------|-------|-----------------|
| 3 | MCP server / AI-agent surface | **Feasible with caveats** | M | Analysis/report verbs are already a stateless facade; **authoring + manifest** verbs are CLI-only. Lift them into the engine first, then wrap once. |
| 4 | Studio report button + manual threat authoring | **Split** | Report: S · Authoring: L | Report is pure UI wiring (engine call exists). Manual authoring requires **widening the `tmforge-json` threat contract**, not just a panel. |

### Cross-cutting architecture (why these are mostly feasible)

The codebase already has the two seams that make this class of extension cheap:

1. **A single stateless engine facade.** `EngineService` (static, in
   `src/ThreatModelForge.Engine/EngineService.cs`) is the one place model operations live, and it is
   consumed uniformly by the HTTP API (`/v1`, `src/ThreatModelForge.Api/Program.cs`), the browser
   WASM shim (`src/ThreatModelForge.Wasm/Engine.cs`), and — through the `IEngineClient` seam
   (`src/ThreatModelForge.Studio/src/dfd/engineClient.ts`) — the Studio. Anything added *there*
   reaches all three hosts for free.

2. **Reflection-discovered rules.** `RuleSet.LoadDefault(IEnumerable<Assembly>)`
   (`src/ThreatModelForge.Analysis/RuleSet.cs`) already accepts *arbitrary* assemblies and
   instantiates every exported `Rule` subtype. The extension point for item 1 is real; only the
   *caller* is hardcoded.

The recurring durable-vs-hack theme across all five: **there is already one correct place for each
concern** (the engine facade, the rule loader, the layout algorithm, the SARIF writer). The hacks
would bolt behavior onto the edges (a second rule-load path, a client-side layout reimplementation,
a bespoke SARIF emitter). The durable moves route through the existing single source of truth.

---

## 1. Consumer-pluggable custom rules

**Verdict: Feasible.** The hard part (a reflection-based rule host) is already built and shipping.

### Evidence

- `RuleSet.LoadDefault(IEnumerable<Assembly>)` scans `asm.GetExportedTypes()`, filters
  `typeof(Rule).IsAssignableFrom(t)`, and creates each via `Activator.CreateInstance`
  (`src/ThreatModelForge.Analysis/RuleSet.cs`). It takes *any* assembly list — the plug already fits.
- `Rule` is a **public abstract** contract (`src/ThreatModelForge.Analysis/Rule.cs`):
  `protected Rule(int id, MessageSeverity defaultSeverity, string pack)`, with overridable
  `FullDescription` / `HelpText` / `HelpUri` / `Evaluate(RuleEvaluationContext)`. This is already a
  publishable base class.
- `ThreatModelForge.Analysis.csproj` **already stages package README metadata**
  (`PackageReadmeFile`, `<None Include="README.md" Pack="true">`) — packaging intent is half-present.
  It is `netstandard2.0` and references only `Core` + `Editing`.

### The gap the feedback under-scoped

The feedback says the change is "localized to `LoadAnalysisAssemblies()` and `LoadRuleSet()`." It is
**not** — there are **four** hardcoded `Assembly.Load("ThreatModelForge.Analysis.Rules")` sites, and
patching only two produces an inconsistent product where `analyze` honors custom rules but threat
generation does not:

| Site | File | Powers |
|------|------|--------|
| `AnalyzeCommand.LoadAnalysisAssemblies()` | `src/ThreatModelForge.Cli/AnalyzeCommand.cs` | CLI `analyze` |
| `EngineService.LoadRuleSet()` | `src/ThreatModelForge.Engine/EngineService.cs` | `/v1`, WASM, Studio validate |
| `ThreatGenerator.LoadDefaultRuleSet()` | `src/ThreatModelForge.Analysis/ThreatGenerator.cs` | `threats` + `/v1/model/threats` |
| `KnowledgeBaseCatalog.LoadDefaultRuleSet()` | `src/ThreatModelForge.Analysis/KnowledgeBaseCatalog.cs` | knowledge-base catalog |

### Durable design

1. **Centralize assembly resolution into one seam** before exposing any flag. Introduce a single
   `AnalysisAssemblyResolver` (in `ThreatModelForge.Analysis`) that returns the ordered assembly
   list: always the built-in `Analysis.Rules`, plus any resolved from an explicit, opt-in source.
   All four sites call it. This is the difference between "a flag on one command" and "custom rules
   are a first-class product capability everywhere."

2. **Layered, explicit configuration precedence** (each strictly opt-in — see security note):
   `--rules <assembly-or-dir>` (CLI) → `TMFORGE_RULES` env var → a config entry. For the
   server/WASM hosts that have no CLI, the env var / host configuration is the injection point.

3. **Harden `LoadDefault` for third-party input.** Today it will throw on the first abstract
   `Rule` subclass, a rule without a public parameterless constructor, or an assembly with an
   unresolvable dependency. Durable loading must:
   - skip `t.IsAbstract` and require `t.GetConstructor(Type.EmptyTypes) != null`;
   - wrap per-assembly discovery in try/catch (surface a diagnostic, don't abort the run);
   - detect **duplicate rule IDs** across packs and report them (today IDs are `TM{int}` — a third
     party is forced into the built-in `TM####` namespace and can silently collide).

4. **Give third-party rules their own ID namespace.** Extend the `Rule` contract to allow a
   vendor-prefixed / string ID (keep `TM####` as the built-in convention). This is the single most
   important *contract* decision for the NuGet, because IDs appear in SARIF, suppressions, and
   `analyze --json`; a collision is a correctness bug for consumers.

5. **Publish `ThreatModelForge.Analysis` as a NuGet** with the documented `Rule` base-class
   contract and an authoring guide. Because it is already `netstandard2.0` with README metadata,
   this is `IsPackable=true` + package identity/authoring + a CI pack/push step. Consumers get
   `Core` + `Editing` transitively.

6. **Longer-term: a declarative rule DSL (data, not code).** The engine already models the exact
   predicate vocabulary a DSL needs — `PropertyBinding(appliesTo, propertyName, flaggedValues)`
   (`src/ThreatModelForge.Analysis/PropertyBinding.cs`) and the typed property schema
   (`PropertySchemaCatalog`) — so a rule like *"every store with `DataType=PHI` must declare
   `Encryption=Yes`"* is a small predicate over element kind + properties + boundary crossings.
   The durable shape is a `DeclarativeRule : Rule` constructed from a parsed spec, plus a
   `DeclarativeRuleProvider` that reads YAML/JSON specs and yields `Rule` instances. Unify the
   assembly path and the DSL path under the same `AnalysisAssemblyResolver`/"rule source"
   abstraction from step 1 so both feed one pipeline. **This is also the answer for untrusted /
   shared rule packs**: a declarative spec is inspectable data, whereas a rule assembly is arbitrary
   code execution.

### Risks / notes

- **Trust boundary.** Loading assemblies or directories is arbitrary code execution *by design*.
  Keep it strictly opt-in, document the implication, and steer shared/untrusted packs toward the
  DSL. (Aligns with treating the assembly loader as a privileged, explicit action.)
- **WASM host constraint.** The browser engine has no filesystem; custom *assemblies* there must be
  bundled at build time. Custom *DSL rules* can be shipped as data. Frame "pluggable rules" as
  fully supported for CLI + self-hosted API/Docker, DSL-only for the static browser demo.
- **AOT.** The reflective loader is already known-incompatible with the (deferred) NativeAOT path
  (`docs/adr/0019-nativeaot-deferred.md`). No new constraint — just don't let custom rules imply AOT.

---

## 3. MCP server / AI-agent surface

**Verdict: Feasible with caveats.** The analysis/report half is genuinely a thin wrapper today; the
**authoring/manifest half is not yet in the facade**, and that distinction determines whether this
ages well or becomes a second, drifting API.

### Evidence

- `EngineService` is static and stateless and already exposes `Analyze`, `GenerateThreats`, `Merge`,
  `ExportTm7`, `Convert`, `ReadModel`, `Report`, `Detect`, plus the catalog getters
  (`src/ThreatModelForge.Engine/EngineService.cs`). The HTTP API is a near-1:1 projection of it,
  **including** `/v1/model/report` (`src/ThreatModelForge.Api/Program.cs`). For these operations, an
  MCP server is indeed a thin wrapper.
- The **wire contract is agent-friendly**: a stable `schemaVersion` envelope (`tmforge schema`,
  `CliJson.WriteEnvelope`), a declarative apply/export manifest, and a typed property schema
  (`tmforge properties`, `/v1/property-schema`) — exactly the grounding context a model needs.

### The caveat the feedback glossed over

The callout lists "read/apply manifest, add/connect/set" as operations to "expose over the existing
`EngineService` facade — most logic exists." **Those are not in `EngineService`.** Imperative
authoring (`add`/`connect`/`remove`/`rename`/`set`) and manifest `apply`/`export` are **CLI-resident**
(`AuthoringSupport.cs`, `Manifest*.cs`, `ManifestSupport.cs`, `ApplyCommand.cs`, `ExportCommand.cs`
— all under `src/ThreatModelForge.Cli/`), orchestrated over `DiagramEditor` in `Editing`. The engine
*building blocks* exist; the *orchestration* lives in the CLI, not the shared facade.

So there are two honest paths, and they have very different durability:

- **Hack:** the MCP server shells out to the `tmforge` binary for authoring and calls `EngineService`
  for analysis. Fast, but now authoring behavior has two front doors (CLI + MCP) with no shared
  contract, and every new verb must be wired twice.
- **Durable:** **lift the authoring + manifest orchestration out of the CLI into the engine**
  (either into `EngineService` or a sibling `AuthoringService` in `ThreatModelForge.Engine`) so the
  CLI, the HTTP API, the WASM shim, **and** the MCP server all wrap one stateless facade. The CLI
  commands become thin argument parsers over that facade — which also retroactively simplifies the
  CLI.

### Recommended sequencing

1. Promote authoring/manifest orchestration into the engine facade (the prerequisite; independently
   valuable because it also unlocks Studio manual authoring — see item 4 — and API/WASM authoring).
2. Expose the facade as MCP tools (`read`/`apply`/`add`/`connect`/`set`/`analyze`/`threats`/`report`).
3. Ship the agent guide: the manifest schema + property-schema catalog + rule catalog. Those three
   artifacts already exist as machine-readable output; the guide is assembly, not authoring.

### On the `tmforge mcp` verb (the "zero-deploy" alternative)

Attractive because it needs no separate service. But it only stays clean **if** step 1 is done — a
`tmforge mcp` verb that reuses in-process command handlers is elegant; one that re-implements
authoring a third time is not. Treat `tmforge mcp` as a *host* for the lifted facade, not as a
reason to skip lifting it. A stdio MCP server hosted by the CLI is the lowest-friction deployment and
composes with the existing single-binary distribution.

### Risks / notes

- **Statelessness is a feature here** — the facade takes a model in and returns a model/report out,
  which maps perfectly onto MCP's request/response tools and onto agent iteration ("apply, analyze,
  inspect, repeat"). Preserve it; do not introduce server-side session state.
- **The `.tm7` identity caveat** (documented for diff/merge) applies: agent round-trips through
  `tmforge-json` regenerate GUIDs unless ids are preserved. For agent authoring this is usually fine
  (the agent builds from a manifest), but flag it for agent *editing* of existing `.tm7`.

---

## 4. Studio: report generation + manual threat authoring

**Verdict: Split.** These two capabilities look adjacent but have very different depth.

### 4b. Manual threat authoring/editing — Feasible, Large (contract change, not a panel)

This is the item most likely to be mis-scoped as "just extend a panel." It is not.

**Evidence of the real gap:** `ThreatsPanel` today is **accept / undo-accept only** — its props are
`onAccept(threat, justification)` and `onUndoAccept(threat)`
(`src/ThreatModelForge.Studio/src/dfd/ThreatsPanel.tsx`). More fundamentally, the **`tmforge-json`
wire contract only carries a sparse triage overlay**: `ThreatTriage` is `{ id, state: 'Open' |
'Accepted', justification? }` (`src/ThreatModelForge.Studio/src/dfd/types.ts`). The richer register
the feedback refers to (priority, mitigation text, a full state machine, manually-authored threats
not tied to a rule) lives in the **`.tm7`** model, **not** in the contract the Studio speaks.

So "edit description/priority/mitigation/state" and "create a threat manually" require, in order:

1. **Widen the `tmforge-json` threat contract** from a triage overlay to a threat record that can
   carry state (beyond Open/Accepted), priority, mitigation, description, and a *manually authored*
   flag/identity that is not derived from a rule id.
2. **Round-trip that record through `EngineService`** into the `.tm7` threat register (which already
   stores these fields) and back — the engine currently projects rules → threats and folds a sparse
   triage back; it does not yet persist an author-owned, editable register.
3. **Then** extend `ThreatsPanel` with the editable detail view and a create-threat affordance.

Steps 1–2 are the durable core; step 3 is the visible part. Doing 3 without 1–2 would mean edits
that can't survive a save/export — the classic hack that looks done and silently loses data.

**Durable note:** this shares a prerequisite with item 3 — promoting author-owned state (threats)
into the engine facade. Sequence 4b after (or together with) the item-3 facade lift and you build the
contract once.

### Risks / notes

- **`.tm7` round-trip fidelity** is a first-class guarantee here (per ADR-0007). Any new threat
  fields must survive the DataContractSerializer round-trip byte-stably; add fields to the wire and
  engine mapping deliberately, with round-trip tests.
- Ship **4a now** (cheap, high-visibility win); schedule **4b** with the item-3 facade work.

---

## Recommended sequencing (dependency-aware)

The five items share two prerequisites; ordering around them avoids building contracts twice.

2. **Centralize the rule-assembly seam (item 1 core):** one `AnalysisAssemblyResolver` behind all
   four load sites, plus the `--rules`/env/config opt-in and loader hardening. Then publish the
   `ThreatModelForge.Analysis` NuGet + authoring guide. The DSL follows as a second phase on the same
   seam.

3. **Lift authoring + manifest orchestration into the engine facade** (shared prerequisite for
   items 3 and 4b, and it also enables item 5's auto-layout endpoint to sit alongside the other
   model operations). This is the highest-leverage architectural move in the whole list: it turns the
   CLI into a thin client, unlocks the MCP server as a genuine thin wrapper, and gives Studio the
   author-owned threat register it needs.

4. **Build item 3 (MCP) and item 4b (manual threat authoring) on the lifted facade**, and expose
   item 5's `EngineService.Layout`.

## Overall assessment

All five callouts are supportable with durable designs, and the codebase is unusually well-positioned
because the core seams (a single stateless engine facade, a reflection-based rule host, an existing
layout algorithm, a stable JSON envelope) already exist. The two places the original feedback is
optimistic are worth internalizing:

- **Item 1** touches **four** rule-load sites, not two — centralize first.
- **Items 3 and 4b** depend on **lifting authoring/manifest orchestration out of the CLI** into the
  shared facade; without that, both become second, drifting APIs.

Do those two structural moves and the rest is largely wiring and packaging.

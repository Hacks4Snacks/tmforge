# Threat Model Analysis Object Model

This project provides the core object model used to inspect threat model (`.tm7`) documents. It exposes the base types that analysis rules build on, so additional rule sets can live in separate assemblies by deriving from those base classes. The `tmforge analyze` command runs rules against a model using this library.

It also projects those findings into the model's persisted **threat register**: a rule that declares a threat category (`Rule.ThreatCategory`, with `Rule.Stride` retained for first-party compatibility) becomes a threat via `ThreatGenerator`, which powers the `tmforge threats` command. Validation and threat generation therefore share one detection engine — the difference is lifecycle (a finding is transient and gated; a threat is persisted and triaged). Versioned packs may declare generalized category identity and a default threat priority independently of finding severity.

Declarative `*.tmrules.json` files support both the legacy `{ "rules": [...] }` shape and the strict
`tmforge-rules` version 2 envelope. Version 2 separates the envelope from its required rule-language
`dialect`: `urn:tmforge:rules:flat-v1` preserves the existing guard/requirement language and
`urn:tmforge:rules:interaction-v1` provides recursive interaction expressions. The envelope carries
optional importer-neutral source identity, generic expression provenance, category definitions,
element-type hierarchy, and canonical property names with aliases/allowed values. Its authoritative
Draft 2020-12 schema is packaged at `schemas/tmforge-rules-v2.schema.json` and exposed by
`RulePackSchema.VersionTwo`. Runtime rules use stable `pack-id/source-rule-id` effective ids while
retaining source metadata through `Rule.PackDefinition` and `Rule.Provenance`. Version 2 identity
segments are printable ASCII, and declarative evaluation shares a bounded invocation-wide work
budget in addition to per-rule interaction limits.

# Threat Model Analysis Object Model

This project provides the core object model used to inspect threat model (`.tm7`) documents. It exposes the base types that analysis rules build on, so additional rule sets can live in separate assemblies by deriving from those base classes. The `tmforge lint` command runs rules against a model using this library.

It also projects those findings into the model's persisted **threat register**: a rule that declares a STRIDE category (`Rule.Stride`) becomes a threat via `ThreatGenerator`, which powers the `tmforge threats` command. Validation and threat generation therefore share one detection engine — the difference is lifecycle (a finding is transient and gated; a threat is persisted and triaged).

# ThreatModelForge.Core

Core library for **Threat Model Forge** — the cross-platform toolkit for authoring
`.tm7`-compatible threat models.

This package provides the in-memory threat-model object graph and its lossless IO:

- **Model** (`ThreatModelForge.Model`) — the `ThreatModel` graph (diagrams, elements, connectors,
  boundaries, threats, and metadata).
- **Knowledge base** (`ThreatModelForge.KnowledgeBase`) — template/knowledge-base types and the
  `.tb7` serializer.
- **Abstractions** (`ThreatModelForge.Abstractions`) — shared serialization contracts.

It reads and writes `.tm7`/`.tb7` files **byte-for-byte compatible** with the Microsoft Threat
Modeling Tool via `DataContractSerializer`. The on-disk wire format is pinned independently of the
CLR namespaces, so files round-trip losslessly.

Higher-level capabilities live in companion packages: `ThreatModelForge.Formats` (pluggable format
providers), `ThreatModelForge.Analysis` (validation/linting), `ThreatModelForge.Reporting`
(HTML/SVG reports), and `ThreatModelForge.Editing` (UI-agnostic editing).

Licensed under the MIT License.

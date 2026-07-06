# ThreatModelForge.Formats

A pluggable format layer for reading and writing threat models. Consumers (CLIs, the web app,
generation) depend on this instead of calling `ThreatModel.Load`/`Save` directly, so new
formats plug in by implementing `IThreatModelFormat` and registering with
`ThreatModelFormatRegistry`.

The canonical in-memory model stays the `.tm7`-shaped `ThreatModelForge.Model.ThreatModel`.
`Tm7Format` is the first provider and wraps the byte-stable `.tm7` round-trip. Other formats map
to and from the canonical model and declare their fidelity through `FormatCapabilities`.

# ThreatModelForge.Reporting

Cross-platform HTML report generation for Microsoft Threat Modeling documents.

Given a threat model (`.tm7`), this library produces a self-contained HTML report that
includes the model metadata, each data-flow diagram rendered as inline **SVG** (drawn
directly from the model geometry, so no native graphics libraries are required), and the
threats grouped per diagram with their state, priority, category, description, and
mitigation.

The core pieces are:

- `DiagramSvgRenderer`: renders a `DrawingSurfaceModel` to an SVG element.
- `HtmlReportWriter`: produces the full HTML report from a `ThreatModel`, optionally
  enriched with a knowledge base (`.tb7`) for threat-type titles and categories.

All model-supplied text is emitted through `System.Xml.Linq`, which escapes it
automatically, so a report cannot be used to inject markup or script.

This targets `netstandard2.0` so it runs on Windows, Linux, macOS, and in containers,
replacing the WPF/GDI+ report pipeline that historically shipped only in the Windows
Threat Modeling Tool.

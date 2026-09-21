# Threat Model Forge for VS Code

View threat-model diagrams, review security findings, and edit model files without leaving VS Code.

## Get Started

1. Install the downloaded extension package with **Extensions: Install from VSIX...** in the Command Palette.
2. Open a `.tmforge.json` file to edit it in Studio, or run **Threat Model Forge: New Model** to start a model.
3. Open a `.tm7` file for a read-only diagram preview and findings.

Use **Threat Model Forge: Open Studio** or **Reopen Editor With** to switch a JSON model from the
text editor to Studio. **Threat Model Forge: Open Preview** remains available for read-only review.

## Review Findings

VS Code's **Problems** panel updates when you open or save a model. Choose **Analyze** in Studio to
review threats, select affected elements, and record triage on the current model. Read-only previews
also show findings.

After editing, save the file to refresh findings. To check unsaved changes, run
**Threat Model Forge: Analyze Model** from the Command Palette. You can also choose **Reanalyze**
in the preview to run analysis again.

Findings are attached to the model file rather than specific source lines. Rule and element IDs
help you identify the affected parts of the model.

## Edit Model Files

Studio supports stencil-based drawing, connections, properties, multiple pages, layout, analysis,
threat triage, comparison, and reports. See the
[Studio guide](https://github.com/Hacks4Snacks/tmforge/blob/HEAD/docs/studio-guide.md) for the editing controls.

Use VS Code's normal **Save**, **Save As**, **Undo**, and **Redo** commands. Canvas edits mark the
JSON document as unsaved; opening or analyzing it does not change the file. **Open source** switches
to text editing. Source edits and changes in another editor update the canvas. Invalid source pauses
visual editing until the JSON is corrected.

**Open File** opens JSON models directly. Importing `.tm7` or another format creates a separate
unsaved `.tmforge.json` model after conversion review, leaving the original untouched. **Export** and
**Report** let you choose a destination for a separate file. Native `.tm7` editing in place and URL
sharing are not supported in the extension.

## JSON Editing

JSON completion and structural validation are available for:

- Model manifests: `*.manifest.json` and `*.tm.json`.
- Rule packs: `*.tmrules.json`.
- Suppression files: `*.suppressions.json`.

Version 2 rule packs have full schema validation; older rule-only files have basic structural hints.
These editing hints do not run custom rules or apply suppressions to a model.

## Supported Workflows

Use desktop VS Code in a trusted workspace, including remote development workspaces.
Browser-only VS Code and virtual workspaces are not supported.

Analysis uses tmforge's built-in rules and respects disabled rules saved in `.tmforge.json` models.
In Studio, **Analysis Rules** can load a custom JSON rule pack for the current document session.
Reload that pack after restarting VS Code; packs are not automatically read from the workspace.
A model that expects an unavailable pack reports an error. Suppression files are not applied by
the extension; use the tmforge CLI for suppression workflows.

Models can contain up to 32 pages, 1,024 elements, and 2,048 diagram lines, with a maximum file size
of 8 MiB. More complex models may exceed processing limits. Analysis requests time out after
30 seconds; choose **Reanalyze** to retry.

## Privacy

Analysis runs on your computer or, when using remote development, on your remote host. The extension
does not send model contents to a separate analysis service.

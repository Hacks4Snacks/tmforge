# Sample threat models

Small, synthetic threat models used to demo Threat Model Forge, exercise the first-party
[GitHub Action](../action.yml), and dogfood the CLI in [CI](../.github/workflows/ci.yml).

## `webshop`

A minimal but realistic web-shop: a customer browser on the public internet talking to a web app and
an orders API inside a cloud VNet, backed by an orders database and an audit log.

| File | What it is |
|------|------------|
| [`webshop.manifest.json`](webshop.manifest.json) | The reviewable, diffable source — a declarative authoring manifest. |
| [`webshop.tm7`](webshop.tm7) | The built model (`DataContractSerializer` XML), analyzed by CI and the Action. |

The model is deliberately well-formed (it analyzes cleanly, exit code `0`) yet still surfaces a
couple of advisory **warnings** — the web app writes to no audit-log store (`TM1029`) and the audit
log is unsigned (`TM1021`) — so the SARIF output and HTML report are non-empty.

### Regenerate the model from the manifest

```bash
tmforge apply examples/webshop.manifest.json --out examples/webshop.tm7
```

### Analyze it

```bash
# Human-readable findings; exit 0 (clean/threshold-clear), 2 (findings at/above --max-severity), 1 (error).
tmforge analyze examples/webshop.tm7

# Machine-readable SARIF + HTML report into a folder.
tmforge analyze examples/webshop.tm7 --reportFolder out/reports
```

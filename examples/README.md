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

## Custom rules and suppressions

A matched pair showing how an organization layers its own policy on top of the built-in rules, and
how a reviewed exception is recorded.

| File | What it is |
|------|------------|
| [`corporate-policy.tmrules.json`](corporate-policy.tmrules.json) | One declarative rule: a store holding audit data must state a `RetentionDays` retention period. |
| [`corporate-policy.suppressions.json`](corporate-policy.suppressions.json) | Records a reviewed exception for the audit log in `webshop.tm7`. |

The rule reports an **error** against `webshop.tm7`, so it changes the gate outcome, and the
suppression clears exactly that finding:

```bash
tmforge analyze examples/webshop.tm7 \
  --rules examples/corporate-policy.tmrules.json                       # exit 2

tmforge analyze examples/webshop.tm7 \
  --rules examples/corporate-policy.tmrules.json \
  --suppressionFile examples/corporate-policy.suppressions.json        # exit 0
```

CI runs this pair through both the CLI and the first-party Action and requires the same outcome from
each, which is what keeps the Action's rule and suppression wiring honest. A suppressed finding is
still produced and recorded — it simply stops gating the build.

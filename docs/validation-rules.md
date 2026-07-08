# Validation rules & CI

Threat Model Forge ships a built-in rule set that runs against any model and flags completeness,
diagram-hygiene, and security-property issues. The same engine backs `tmforge lint`, Studio's
**Validate** button, and the API's `POST /v1/model/validate`.

## Running validation

```bash
tmforge lint model.tm7                          # human-readable
tmforge lint model.tm7 --json                   # machine-readable envelope
tmforge lint model.tm7 --reportFolder ./out     # + SARIF, HTML, and JSON listing
```

### Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Clean, no findings at or above `--max-severity`. |
| `1` | Tool error (bad arguments, load failure). |
| `2` | The model was analyzed and has findings at or above `--max-severity` (default: `error`). |

The dedicated `2` lets CI **fail on findings** while distinguishing them from a broken invocation.

### Severities

Findings carry a severity: **Error**, **Warning**, or **Info**. By default only **Error** findings set
the "found issues" exit code (`2`); pass `--max-severity warning` (or `info`) to `tmforge lint` to gate
the build on those severities too.

## Rule packs

A **rule pack** is a named, selectable group of related rules. Packs are the single source of truth
that hosts (the CLI, API, and Studio) consume, so their names and ordering never drift from the rules
that declare them. List them from the API with `GET /v1/rule-packs`.

| Pack id | Display name | Focus |
| --- | --- | --- |
| `core-hygiene` | Core Hygiene | Structural completeness and diagram hygiene (connectivity, naming, size). |
| `stride-completeness` | STRIDE Completeness | Trust boundaries, boundary crossings, external interactors, and flow metadata. |
| `input-validation` | Input Validation | Inputs/outputs validated or sanitized across trust boundaries. |
| `data-protection` | Data Protection | Data-at-rest protection: encryption, access control, integrity, retention. |
| `transport-security` | Transport Security | Data-in-transit protection across trust boundaries. |
| `identity-access` | Identity & Access | Authentication, least privilege, and access to components. |

## Built-in rules

The default rule set covers connectivity and naming hygiene, STRIDE modeling completeness, and a set
of security-property checks, grouped by pack. The set grows over time, so treat the tables below as a
snapshot. `tmforge` and the engine's `GET /v1/rules` report the live rule set.

### Core Hygiene (`core-hygiene`)

| ID | Rule | Severity | Checks |
| --- | --- | --- | --- |
| 1000 | Unconnected components | Error | Every component is connected by at least one flow. |
| 1001 | Unconnected edges | Error | Every data flow has both endpoints attached. |
| 1002 | Descriptive edge name | Warning | Flows have meaningful, non-default names. |
| 1005 | Minimum component count | Warning | A diagram has at least three components. |
| 1011 | Descriptive generic component name | Error | Generic components are renamed from their default label. |
| 1012 | Descriptive specific component name | Warning | Named components have meaningful names. |

### STRIDE Completeness (`stride-completeness`)

| ID | Rule | Severity | Checks |
| --- | --- | --- | --- |
| 1003 | Missing any trust boundary | Error | The model has at least one trust boundary. |
| 1004 | Missing any trust-boundary crossing | Error | At least one flow crosses a trust boundary. |
| 1006 | Missing any external interactors | Warning | At least one diagram has an external interactor. |
| 1007 | Outbound storage edge | Warning | Outbound flows from storage correctly describe data flow. |
| 1008 | Edge missing protocol | Error | A flow declares the protocol it uses. |
| 1009 | Edge missing protocol description | Info | A flow mentions its protocol in the description text. |
| 1010 | Edge missing port | Warning | A flow declares a port when it can't be inferred from the protocol. |
| 1013 | Edge missing data classification | Warning | A flow declares a data classification. |

### Input Validation (`input-validation`)

| ID | Rule | Severity | Checks |
| --- | --- | --- | --- |
| 1017 | Unsanitized cross-boundary input | Warning | A process receiving input across a trust boundary validates or sanitizes it. |
| 1018 | Unsanitized external output | Warning | A process sending output to an external entity encodes or sanitizes it. |
| 1019 | Weak process isolation | Warning | A process receiving input across a trust boundary runs with isolation. |

### Data Protection (`data-protection`)

| ID | Rule | Severity | Checks |
| --- | --- | --- | --- |
| 1014 | Unencrypted secret store | Warning | A store holding credentials is encrypted at rest. |
| 1020 | Unprotected credential store | Warning | A store holding credentials enforces meaningful access control (not `None`/`Public`). |
| 1021 | Unsigned audit-log store | Warning | A store holding log or audit data is signed for integrity. |
| 1022 | Credentials in log store | Warning | A store recording log data does not also store credentials. |
| 1025 | Weak or unapproved cipher | Warning | A flow or store declaring an encryption algorithm uses an approved authenticated cipher (AES-GCM, AES-CBC+HMAC, or ChaCha20-Poly1305). |
| 1027 | Cached credential read | Warning | A flow reading from a credential store is not cached (`Cached=No`), so a rotated or revoked credential is not served stale. |

### Transport Security (`transport-security`)

| ID | Rule | Severity | Checks |
| --- | --- | --- | --- |
| 1016 | Cleartext trust-boundary crossing | Warning | A flow crossing a trust boundary doesn't use a cleartext protocol. |

### Identity & Access (`identity-access`)

| ID | Rule | Severity | Checks |
| --- | --- | --- | --- |
| 1015 | Unauthenticated boundary process | Warning | A process receiving input across a trust boundary declares an authentication scheme. |
| 1023 | Unauthenticated external source | Warning | An external entity initiating flows into the system authenticates itself. |
| 1024 | Over-privileged process | Warning | A process does not run as a highly privileged account (root/admin/system). |
| 1026 | Shared static identity | Warning | A single `Identity` is not asserted by flows from 2+ distinct sources; each calling principal has its own scoped identity. |

## Rule help

Every finding carries the rule's **ID** (for example `TM1016`). To see what a rule checks and how to
clear it:

- **Studio**: open the **Validation** panel and click the **?** on a rule to expand its description
  and fix guidance in place. That text ships with the engine, so it always matches the rule that ran.
- **CLI / API**: `GET /v1/rules` returns each rule's `description`, `helpText`, and `helpUri`, and
  both the HTML report and SARIF carry the rule's `helpUri` for code-scanning dashboards.

Each rule also advertises a documentation link (`helpUri`) that points back at this page. The public
docs URL is still being finalized, so that external link goes live once the repository is published.
The in-app description and fix guidance never depend on it.

## Fixing findings

Many findings are cleared by setting a **custom property** on the offending element or flow. From the
CLI, use [`set`](cli-reference.md#set) (or `connect --property` / `add --property` at creation):

```bash
# Clear "edge missing protocol/port" on a flow:
tmforge set model.tm7 --id <flow-guid> --property Protocol=HTTPS --property Port=443

# Clear "unauthenticated boundary process" on a process:
tmforge set model.tm7 --id <process-guid> --property AuthenticationScheme=OAuth
```

Common rule-checked properties include `Protocol`, `Port`, `DataType` / data classification,
`AuthenticationScheme`, `SanitizesInput` / `SanitizesOutput`, `Isolation`, `AccessControl`, `Signed`,
`AuthenticatesItself`, `RunningAs`, `Algorithm`, and encryption/at-rest flags. In
[Studio](studio-guide.md), edit the same properties in the inspector and re-**Validate**.

## Customizing the rule set

### Selecting rules and packs

When a model is loaded from the native **`tmforge-json`** format, any embedded validation selection
(disabled packs or rule ids) is honored automatically, so a model can carry its own policy. Other
formats (for example `.tm7`) use the full rule set unless you pass an explicit `--ruleset`.

### Custom rule set file

```bash
tmforge lint model.tm7 --ruleset ./my-ruleset.xml
```

### Rule variables

Some rules read variables supplied on the command line (repeatable):

```bash
tmforge lint model.tm7 --define key=value --define another=value
```

## Suppressions

Filter known/accepted findings with a suppression document:

```bash
tmforge lint model.tm7 --suppressionFile ./suppressions.json
```

Suppressions are matched per model path and applied before evaluation, so suppressed findings don't
affect the exit code.

## Reports

`--reportFolder <dir>` writes machine- and human-readable findings artifacts:

- **SARIF**: for code-scanning dashboards and PR annotations.
- **HTML**: a human-readable findings report.
- **JSON listing**: a structured enumeration of the model.

```bash
tmforge lint model.tm7 --reportFolder "$CI_ARTIFACTS/threatmodel"
```

## CI integration

Gate a pipeline on threat-model findings. The example uses GitHub Actions; adapt the runner and paths
to your CI.

```yaml
name: threat-model
on: [pull_request]
jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Download tmforge
        run: |
          curl -fsSL -o tmforge.tar.gz \
            https://github.com/hacks4snacks/tmforge/releases/download/v0.1.0/tmforge-0.1.0-linux-x64.tar.gz
          tar -xzf tmforge.tar.gz
          echo "$PWD/tmforge-0.1.0-linux-x64" >> "$GITHUB_PATH"
      - name: Validate threat models
        run: |
          set -e
          for model in $(git ls-files '*.tm7'); do
            tmforge lint "$model" --reportFolder "reports/$(basename "$model")"
          done
      - name: Upload SARIF
        if: always()
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: reports
```

`tmforge lint` returns `2` when a model has findings, which fails the step; `1` signals a tool error.
See the [deployment guide](deployment.md#cicd) for container-based pipelines.

## See also

- [CLI reference: `lint`](cli-reference.md#lint): all options.
- [Overview & features](overview.md): where validation fits.
- [Engine API reference](api-reference.md): `POST /v1/model/validate` and the catalog endpoints.

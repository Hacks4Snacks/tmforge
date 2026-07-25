# Analysis rules & CI

Threat Model Forge ships a built-in rule set that runs against any model and flags completeness,
diagram-hygiene, and security-property issues. The same engine backs `tmforge analyze`, Studio's
**Analyze** button, and the API's `POST /v1/model/analyze`.

## Running analysis

```bash
tmforge analyze model.tm7                       # human-readable
tmforge analyze model.tm7 --json                # machine-readable envelope
tmforge analyze model.tm7 --reportFolder ./out  # + SARIF, HTML, and JSON listing
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
the "found issues" exit code (`2`); pass `--max-severity warning` (or `info`) to `tmforge analyze` to gate
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
| `availability` | Availability | Recoverability and audit-trail durability: backups for important data. |

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
| 1029 | Unaudited boundary process | Warning | A process receiving input across a trust boundary writes to an audit-log store so its actions can be attributed. |

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
| 1030 | Sensitive data to external | Warning | A flow carrying sensitive data (EUII, EUPI, customer content, account data, or access-control data) is not sent to an external interactor. |

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

### Availability (`availability`)

| ID | Rule | Severity | Checks |
| --- | --- | --- | --- |
| 1028 | Data store without backup | Warning | A store holding credentials or audit/log data declares a backup (`Backup=Yes`), so its contents can be recovered after loss or a destructive attack. |

## Rule help

Every finding carries the rule's **ID** (for example `TM1016`). To see what a rule checks and how to
clear it:

- **Studio**: open the **Analysis Rules** panel and click the **?** on a rule to expand its description
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
`AuthenticatesItself`, `RunningAs`, `Algorithm`, `StoresCredentials` / `StoresLogData`, `Backup`, and
encryption/at-rest flags. In [Studio](studio-guide.md), edit the same properties in the inspector and
re-**Analyze**.

### `Unknown` and the three states of a control

A control property carries three distinct states, and they are not interchangeable:

| Value | Meaning | Effect |
| --- | --- | --- |
| `Encrypted=TDE` | The control is in place. | Finding cleared. |
| `Encrypted=No` | Somebody checked; the control is not in place. | Finding, worded as a confirmed absence. |
| `Encrypted=Unknown`, blank, or the property absent | Nobody has recorded anything. | Finding, worded as an evidence gap. |

`Unknown` is a real, schema-valid value on every control-like property, so you can record honest
uncertainty without `--force` and without claiming the control is missing:

```bash
tmforge set model.tm7 --id <store-guid> --property Encrypted=Unknown
```

**`Unknown` never clears a finding.** It reports at the same rule id and the same severity as an
absent control; only the message changes, from "is not encrypted at rest" to "its `Encrypted` property
is not evidenced". This is deliberate. A missing property has always produced a finding, so letting
`Unknown` suppress one would mean an author could silence a real risk by typing a word — and a report
that says "no findings" because nobody looked is worse than no report at all.

`Unknown` survives every format hop (`tmforge-json`, `.tm7`, manifests) and every surface (CLI, API,
MCP, WASM, Studio). Existing `No` and `None` values are left exactly as they are; there is no
migration, because those values are statements an author made.

## Customizing the rule set

### Selecting rules and packs

When a model is loaded from the native **`tmforge-json`** format, any embedded analysis selection
(disabled packs or rule ids) is honored automatically, so a model can carry its own policy. Other
formats (for example `.tm7`) use the full rule set unless you pass an explicit `--ruleset`.

### Custom rule set file

```bash
tmforge analyze model.tm7 --ruleset ./my-ruleset.xml
```

### Authoring custom rules (declarative)

Ship your own rules as **data**, not code. A declarative rule spec is a JSON file (`*.tmrules.json`)
loaded with `--rules` on [`analyze`](cli-reference.md#analyze), [`threats`](cli-reference.md#threats),
and [`properties`](cli-reference.md#properties):

```bash
tmforge analyze model.tm7 --rules ./rules.tmrules.json   # one spec file
tmforge analyze model.tm7 --rules ./rules/               # a directory of specs (searched recursively)
```

To compile an existing MTMT template instead of hand-authoring JSON, use [`rules import`](cli-reference.md#rules):

```bash
tmforge rules import --from template.tb7 --out template.tmrules.json --strict
tmforge analyze model.tm7 --rules template.tmrules.json
```

The import preserves source expressions and metadata in provenance. A non-strict import writes all
exactly representable threats and reports skipped threats; `--strict` writes nothing if any threat is
skipped. Generated packs remain subject to the source template's license and attribution terms.

Because a spec is inspectable data — not an assembly — it is safe to share and review, and it runs
everywhere the CLI does. Custom rules are **added to** the built-in rules, never a replacement for
them: `--rules` loads your rules *alongside* the full built-in set and both are evaluated together
(custom rules show up in findings, SARIF, `analyze --json`, and — when they read a property — in
`properties --explain`). To narrow the built-ins, use the existing controls independently of
`--rules`: a model's embedded `disabledPacks`, an `--ruleset` override, or `--max-severity`.

A version 2 pack is a strict, self-describing envelope. The envelope is importer-neutral and names a
rule-language `dialect`; source-specific concepts belong in the optional generic `source` and
`provenance` records. It also carries the category, element-type, and property catalogs needed by
compiled rules. The runtime computes the pack's `sha256:` fingerprint from the exact file bytes;
authors do not declare it.

### Custom rules on every surface

A rule pack is not a CLI-only feature. Every transport resolves rules through the same engine seam,
so one pack produces the same catalog entries, findings, threats, reports, and `.tm7` exports
everywhere. Only *how the pack reaches the engine* differs, because only some hosts can safely touch
the filesystem:

| Surface | How a pack is supplied | Notes |
| --- | --- | --- |
| CLI | `--rules <file-or-directory>` on `analyze`, `threats`, `report`, `properties` | Paths are resolved by the CLI. |
| HTTP API | `TmForge:Rules` configuration (a `;`-separated list, or an array) read once at startup | Packs are trusted deployment configuration; a request can never inject rules. |
| Studio / in-browser engine | **Analysis Rules → Load rule pack…** | The pack is read in the browser and handed to the WebAssembly engine as content. |
| MCP server | `rulesPath` on the `analyze`, `threats`, `report`, `rules`, and `rule_packs` tools | Resolved only through the configured MCP workspace root. |

Ask any host what it actually loaded. The API exposes `GET /v1/rule-bundle`; the Studio shows the
same information under **Analysis Rules**. Both report each pack's id, version, dialect, rule count,
and content fingerprint, plus every load diagnostic — so a pack that failed to parse is visible
instead of looking like a clean run against the built-in rules.

Models can pin the packs they were reviewed with. A `tmforge-json` model's analysis selection may
carry `expectedPacks`:

```jsonc
{
  "analysis": {
    "expectedPacks": [
      { "id": "corporate", "fingerprint": "sha256:\u2026" }
    ]
  }
}
```

If an expected pack is missing from the effective bundle, or its content fingerprint has changed,
analysis emits an **error** finding with rule id `rule-pack-mismatch`. That fails a build gated on
errors, which is the point: a model must never look clean merely because the rules that would have
flagged it were not loaded. The Studio records these pins for you when you load a pack.

```jsonc
{
  "schema": "tmforge-rules",
  "version": 2,
  "dialect": "urn:tmforge:rules:flat-v1",
  "pack": {
    "id": "azure-template-a1b2c3d4",
    "name": "Azure Threat Model Template",
    "version": "1.0.0.33",
    "source": {
      "type": "urn:tmforge:source:mtmt-tb7",
      "name": "Azure Cloud Services.tb7",
      "id": "11111111-1111-1111-1111-111111111111",
      "version": "1.0.0.33"
    }
  },
  "categories": [
    { "id": "D", "name": "Denial of Service" }
  ],
  "elementTypes": [
    { "id": "GE.P", "name": "Process", "parentId": "ROOT" }
  ],
  "properties": [
    {
      "name": "Cache Type",
      "aliases": ["cacheType", "cache-type"],
      "allowedValues": ["Static", "Distributed"],
      "elementTypeIds": ["GE.P"]
    }
  ],
  "rules": [
    {
      "id": "TH112",
      "severity": "error",
      "appliesTo": "process",
      "message": "{name} uses an unsafe cache.",
      "assert": { "property": "Cache Type", "equals": "Distributed" },
      "provenance": {
        "sourceId": "TH112",
        "categoryId": "D",
        "expressions": [
          {
            "role": "include",
            "language": "urn:tmforge:source:mtmt-generation-filter",
            "text": "target is 'GE.P'"
          }
        ]
      }
    }
  ]
}
```

The authoritative Draft 2020-12 schema is packaged as `schemas/tmforge-rules-v2.schema.json` and is
available in-process through `RulePackSchema.VersionTwo`. Version 2 rejects unknown fields and
dialects, ambiguous property aliases, duplicate catalog ids, hierarchy cycles, unresolved catalog
links, and caller-supplied fingerprints. `source.type` and provenance expression `language` values
must be namespaced identifiers; the base schema does not reserve an importer vocabulary.
Rule `helpUri` values must use HTTP or HTTPS; HTML reports independently refuse other URI schemes.
Pack, category, element-type, and rule identity segments are printable ASCII without `/` or
surrounding whitespace so effective rule ids remain safe in persisted TM7 threat keys.

`urn:tmforge:rules:interaction-v1` evaluates rules over an interaction containing `source`, `target`,
`flow`, and crossed trust boundaries. Its recursive expression nodes are:

```jsonc
{
  "schema": "tmforge-rules",
  "version": 2,
  "dialect": "urn:tmforge:rules:interaction-v1",
  "pack": { "id": "interaction-example", "name": "Interaction example" },
  "elementTypes": [
    { "id": "GE.P", "name": "Process", "parentId": "ROOT" },
    { "id": "SE.P.Web", "name": "Web process", "parentId": "GE.P" },
    { "id": "GE.TB.B", "name": "Trust boundary", "parentId": "ROOT" }
  ],
  "properties": [
    { "name": "Protocol", "allowedValues": ["HTTP", "TLS"] }
  ],
  "rules": [
    {
      "id": "CLEAR-TEXT",
      "message": "{source.Name} sends {flow.Name} to {target.Name} without TLS.",
      "expression": {
        "allOf": [
          { "subject": "source", "type": "GE.P" },
          { "crosses": "GE.TB.B" },
          {
            "not": {
              "subject": "flow",
              "property": "Protocol",
              "valueIn": ["TLS"]
            }
          }
        ]
      }
    }
  ]
}
```

Type predicates walk `elementTypes.parentId` transitively. Property membership checks all stored
values. `crosses` matches the declared boundary type or one of its descendants. A
`source is ROOT` expression is evaluated once per diagram; ordinary expressions are evaluated once
per flow. `{source}`, `{target}`, and `{flow}` message tokens, with or without `.Name`, are replaced
case-insensitively.

The MTMT GenerationFilters compiler resolves source property aliases and element types against the
source TB7 catalog. Values for static attributes must appear in that attribute's declared value set;
dynamic attributes remain open to runtime-defined values. Include and Exclude are validated again
after composition, so node and depth limits apply to the final evaluator tree.

An effective v2 rule id is `pack.id/rule.id` (for example
`azure-template-a1b2c3d4/TH112`). This lets two imported templates retain the same source ThreatType
id without colliding in SARIF, suppressions, or generated threat keys. Duplicate effective ids that
involve a v2 rule reject every contender, so changing file or declaration order cannot choose a
winner. `RulePackIdentity.CreatePackId` provides the deterministic
`normalized-name-<32 hex chars>` convention used by importers (128 fingerprint bits). The full
fingerprint remains available on `RulePackDefinition.Fingerprint`, and every surface reports it (see
[Custom rules on every surface](#custom-rules-on-every-surface)).

The loader bounds untrusted input per pack to 8 MiB, 4,096 rules, 512 categories, 4,096 element
types, 8,192 property definitions, and 65,536 aggregate catalog/expression nodes and values. A single
load is additionally capped at 128 files, 32 MiB, 16,384 rules, 2,048 categories, 16,384 element
types, 32,768 properties, and 262,144 catalog/expression entries. Interaction expressions are limited
to 64 levels; evaluation is bounded to 100,000 interaction contexts and 1,000,000 expression
operations per rule, with 10,000,000 declarative operations shared across the full analysis
invocation. Analysis output is capped at 100,000 messages and 64 MiB of message text; individual
expanded messages in either dialect are capped at 65,536 characters. Strings are limited to 65,536
characters and identity segments to 512 characters.

The original unversioned shape remains supported unchanged for hand-authored packs. It is a `rules`
array, and each rule may declare its own `pack` value:

```jsonc
{
  "rules": [
    {
      "id": "ACME001",                 // your own id namespace (built-ins use TM####)
      "pack": "acme-governance",
      "severity": "error",             // error | warning | info (default: warning)
      "appliesTo": "datastore",        // process | datastore | external | flow
      "message": "Data store {name} does not declare encryption at rest.",
      "fullDescription": "Persisted data must be encrypted at rest.",
      "helpText": "Set Encrypted to At-rest, TDE, Client-side, or Platform.",
      "assert": { "property": "Encrypted", "anyOf": ["At-rest", "TDE", "Client-side", "Platform"] }
    },
    {
      "id": "ACME002",
      "pack": "acme-governance",
      "severity": "warning",
      "appliesTo": "flow",
      "message": "Flow {name} carries sensitive data in the clear across a trust boundary.",
      "stride": "InformationDisclosure",           // optional; makes it a `threats` threat
      "threatReferences": ["CWE:319"],             // optional: CWE:<n> | CAPEC:<n> | ATTACK:<id>
      "when":   { "property": "DataType", "anyOf": ["EUII", "Customer Content"], "crossesTrustBoundary": true },
      "assert": { "property": "Protocol", "anyOf": ["HTTPS", "TLS", "mTLS"] }
    }
  ]
}
```

For both versions, a finding is raised for each element of `appliesTo` that matches `when` (the
guard) and fails `assert` (the requirement); at least one of `when`/`assert` is required.

The `{name}` token in `message` is replaced with the element's display text. **Conditions** (`when`
and `assert`) are facets that must *all* hold; a bare `property` with no value matcher means "must be
present":

| Facet | Applies to | Meaning |
| --- | --- | --- |
| `property` + `anyOf` | any | The value is one of the listed values. |
| `property` + `notAnyOf` | any | The value is none of the listed values. |
| `property` + `equals` | any | The value equals a single value. |
| `property` + `present` | any | The property is present (`true`) or absent (`false`). |
| `crossesTrustBoundary` | `flow` | The flow crosses (`true`) or does not cross (`false`) a trust boundary. |
| `source` / `target` | `flow` | A condition on the flow's endpoint: its `kind` (`process`/`datastore`/`external`) and/or a property matcher. |

- **Assert what a control *is*, not what it is not.** `anyOf` and `equals` compare exact strings, so
  `Unknown` satisfies them only if you list it. `notAnyOf` is a denylist: `{"property": "Encrypted",
  "notAnyOf": ["No"]}` treats `Unknown` — and every typo — as encrypted, which suppresses exactly the
  finding you wrote the rule for. Prefer `anyOf` with the values that actually satisfy the control.
  A bare `property` matcher tests only that the property is *recorded*; `Unknown` is recorded, so
  presence is not evidence that a control exists. See
  [`Unknown` and the three states of a control](#unknown-and-the-three-states-of-a-control).

- **Ids persist.** Legacy ids are preserved verbatim. Version 2 ids are pack-qualified as described
  above. A collision with an already-loaded built-in is dropped with a warning, so the built-in
  `TM####` namespace always wins.
- **Property names are validated** against the typed [property schema](cli-reference.md#properties).
  An unknown property is a warning, not an error — but it catches a typo (`Encryption` vs `Encrypted`)
  that would otherwise make a rule silently never match.
- **Resilient loading.** A malformed legacy file or individual invalid legacy rule is reported to
  standard error and skipped. Version 2 validates its envelope as a unit, then compiles valid rules;
  an invalid envelope/catalog is skipped rather than partially interpreted.
- **Threats.** A custom rule that declares a `stride` category is projected into
  [`threats`](cli-reference.md#threats) exactly like a built-in threat-bearing rule.
- **CLI only (and why).** `--rules` works on the CLI; the HTTP API (`/v1`) and the in-browser
  (WebAssembly) engine load the built-in rules only. This is deliberate, not an oversight: (1) those
  hosts share a **stateless** engine facade — a model in, findings out — with no per-request channel
  for selecting rule sources; (2) the **WebAssembly host has no filesystem**, so the file/directory
  loader behind `--rules` cannot read spec files there; and (3) loading rules over a shared service is
  a security-sensitive contract change (in-memory rule injection, and treating rule-loading as a
  privileged action) deferred to a later increment. The rule engine itself is portable the
  limitation is the injection surface, not the DSL.

### Rule variables

Some rules read variables supplied on the command line (repeatable):

```bash
tmforge analyze model.tm7 --define key=value --define another=value
```

## Suppressions

Filter known/accepted findings with a suppression document:

```bash
tmforge analyze model.tm7 --suppressionFile ./suppressions.json
```

Suppressions are matched per model path and applied before evaluation, so suppressed findings don't
affect the exit code.

## Reports

`--reportFolder <dir>` writes machine- and human-readable findings artifacts:

- **SARIF**: for code-scanning dashboards and PR annotations.
- **HTML**: a human-readable findings report.
- **JSON listing**: a structured enumeration of the model.
- **`<model>.analysis.json`**: the versioned `tmforge-analysis` document — the run recorded as evidence.

```bash
tmforge analyze model.tm7 --reportFolder "$CI_ARTIFACTS/threatmodel"
```

### The analysis document

The other artifacts are for people and dashboards. `<model>.analysis.json` is the one meant to be
**stored and compared against the next run**:

```jsonc
{
  "schema": "tmforge-analysis",
  "version": 1,
  "model":    { "name": "Webshop", "fingerprint": "sha256:187a6d5d…" },
  "analyzer": { "name": "tmforge", "version": "0.7.0.0", "fingerprint": "sha256:5bf32a1d…" },
  "findings": [
    {
      "id": "TM1021:48761fb5…:6b3c361c…:0",
      "ruleId": "TM1021",
      "disposition": "generated-threat",
      "threatId": "6b3c361c…:TM1021"
    }
  ]
}
```

Every finding carries exactly one **disposition**:

| Disposition | Meaning |
| --- | --- |
| `generated-threat` | Threat-bearing and not yet triaged. |
| `unresolved` | Threat-bearing and explicitly marked as needing investigation. |
| `accepted` | Threat-bearing and accepted as a risk. |
| `mitigated` | Threat-bearing and mitigated. |
| `hygiene` | The rule declares no threat category, so this is a modelling-quality observation, not a risk. |
| `suppressed` | A suppression silenced it. |

The four threat-bearing dispositions carry a `threatId` that joins to `tmforge threats`; `hygiene` and
`suppressed` never do. That separation is the point — you should not have to accept "this diagram has
no trust boundary" as a *risk* to clear a gate.

A **suppressed** finding is recorded, not dropped. It keeps its identity, so a reviewer reading the
evidence can see the finding is still being produced and is deliberately silenced, rather than it
quietly vanishing from the record. It carries no `threatId`, because a suppression says this one does
not count.

The finding ids are the same ones the SARIF `partialFingerprints` carry, so the two artifacts from one
run describe the same findings. The document carries **no timestamp**: two analyses of the same model
with the same rules are byte-identical, so diffing yesterday's document against today's shows only
what actually changed.

Validate a stored document — and find out whether it still describes the model in front of you — with
[`tmforge analysis validate`](cli-reference.md#analysis):

```bash
tmforge analysis validate findings/payments.analysis.json --model payments.tm7
```

### Mapping to your own taxonomy

If your team already runs a threat catalogue, `--taxonomy` annotates the analysis document with your
ids:

```jsonc
// acme-taxonomy.json
{
  "schema": "tmforge-taxonomy",
  "version": 1,
  "rules": {
    "TM1021": ["ACME-T-017", "ACME-T-018"],
    "corporate/CORP-1": ["ACME-T-004"]
  }
}
```

```bash
tmforge analyze payments.tm7 --taxonomy acme-taxonomy.json --reportFolder ./findings
```

Each finding then carries a `canonicalIds` array. Two properties are deliberate:

- **Your catalogue is never part of detection.** The mapping is applied after everything else is
  decided, so no rule fires, changes severity, or changes disposition because of it. Remove the
  mapping and the findings, their ids, and their dispositions are identical.
- **Nothing is ever inferred.** A rule the mapping does not name gets an empty `canonicalIds` — never
  a guess derived from the rule id or the STRIDE category. An invented mapping is worse than an absent
  one, because a reader cannot tell it was guessed.

### Finding identity

Every finding carries a stable id of the form `{ruleId}:{diagram}:{target}:{occurrence}`:

```text
TM1021:48761fb5…c0b5:6b3c361c…6686:0
```

Each segment names something that determines the finding, so the id survives the things that must not
change it — evaluating rules in a different order, enabling or disabling an unrelated rule, and
rewording a message. A segment reads `model` when the finding is about the model or a whole diagram
rather than one element. The trailing counter distinguishes a rule that legitimately fires more than
once against the same target.

This is what makes a finding reconcilable across runs. In SARIF the id is emitted as the
`tmforgeFindingId/v1` **partial fingerprint**, which is how code scanning recognises an alert it has
already seen — without it, every run closes and reopens the whole set and any triage a reviewer
recorded is lost. Results also carry the element as a **logical location**, because a `.tm7` has no
line numbers and the physical location can only name the model file.

Element keys come from the model: a `.tm7` supplies its persisted guids, and canonical model JSON
supplies the author's own element and page ids. Ids that are not guid-shaped are re-keyed internally
on every load, so the author's id is what gets used — an identity built on the internal guid would
differ on every run.

The occurrence counter is the one positional segment. If a rule fires several times against the same
target and you fix some of them, the survivors can renumber; reconcile on the first three segments
when triage has to cross that kind of edit.

## CI integration

Gate a pipeline on threat-model findings. The example uses GitHub Actions; adapt the runner and paths
to your CI.

```yaml
name: threat-model
on: [pull_request]
jobs:
  analyze:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Download tmforge
        run: |
          curl -fsSL -o tmforge.tar.gz \
            https://github.com/hacks4snacks/tmforge/releases/download/v0.1.0/tmforge-0.1.0-linux-x64.tar.gz
          tar -xzf tmforge.tar.gz
          echo "$PWD/tmforge-0.1.0-linux-x64" >> "$GITHUB_PATH"
      - name: Analyze threat models
        run: |
          set -e
          for model in $(git ls-files '*.tm7'); do
            tmforge analyze "$model" --reportFolder "reports/$(basename "$model")"
          done
      - name: Upload SARIF
        if: always()
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: reports
```

`tmforge analyze` returns `2` when a model has findings, which fails the step; `1` signals a tool error.
See the [deployment guide](deployment.md#cicd) for container-based pipelines.

## See also

- [CLI reference: `analyze`](cli-reference.md#analyze): all options.
- [Overview & features](overview.md): where analysis fits.
- [Engine API reference](api-reference.md): `POST /v1/model/analyze` and the catalog endpoints.

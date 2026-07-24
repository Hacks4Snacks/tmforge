import { useRef, useState } from 'react';
import type { RuleBundle, RuleInfo, RulePackInfo } from './engineClient';
import type { TmForgeExpectedRulePack } from './types';

interface AnalysisSettingsProps {
  /** The full rule catalog from the engine. */
  rules: RuleInfo[];
  /** The rule packs from the engine, in presentation order. */
  packs: RulePackInfo[];
  /** Ids of rule packs the model currently skips. */
  disabledPacks: string[];
  /** Ids of individual rules the model currently skips. */
  disabledRuleIds: string[];
  /** The custom rule packs the engine actually loaded, with any load diagnostics. */
  ruleBundle: RuleBundle;
  /** The custom rule packs this model expects, pinned by content fingerprint. */
  expectedPacks: TmForgeExpectedRulePack[];
  onTogglePack: (packId: string) => void;
  onToggleRule: (ruleId: string) => void;
  onLoadRuleFile: (file: File) => void;
  onClearRuleFile: () => void;
}

/** The first sentence of a rule description, for the compact one-line row. */
function firstSentence(text: string): string {
  const trimmed = text.trim();
  const end = trimmed.indexOf('. ');
  return end > 0 ? trimmed.slice(0, end + 1) : trimmed;
}

/** A short, readable prefix of a content fingerprint (the full value is in the title attribute). */
function shortFingerprint(fingerprint: string): string {
  return fingerprint.length > 12 ? `${fingerprint.slice(0, 12)}\u2026` : fingerprint;
}

/**
 * The custom rule pack loader: pick a `.tmrules.json`, see exactly which packs loaded (id, version,
 * content fingerprint, rule count) and every diagnostic the loader raised. The model records the
 * loaded identity, so analyzing later without that pack is reported rather than passing quietly.
 */
function CustomRulePacks({
  ruleBundle,
  expectedPacks,
  onLoadRuleFile,
  onClearRuleFile,
}: Pick<AnalysisSettingsProps, 'ruleBundle' | 'expectedPacks' | 'onLoadRuleFile' | 'onClearRuleFile'>) {
  const fileRef = useRef<HTMLInputElement | null>(null);
  const loadedIds = new Set(ruleBundle.rulePacks.map((pack) => pack.id));
  const missing = expectedPacks.filter((pack) => pack.id && !loadedIds.has(pack.id));

  return (
    <div className="val-custom-rules">
      <div className="val-custom-rules-actions">
        <button type="button" className="pack-chip" onClick={() => fileRef.current?.click()}>
          Load rule pack…
        </button>
        {ruleBundle.rulePacks.length > 0 || expectedPacks.length > 0 ? (
          <button type="button" className="pack-chip" onClick={onClearRuleFile}>
            Use built-in rules
          </button>
        ) : null}
        <input
          ref={fileRef}
          type="file"
          accept=".tmrules.json,.json"
          style={{ display: 'none' }}
          onChange={(event) => {
            const file = event.target.files?.[0];
            event.target.value = '';
            if (file) {
              onLoadRuleFile(file);
            }
          }}
        />
      </div>
      {ruleBundle.rulePacks.map((pack) => (
        <div key={pack.id} className="val-rule-pack" title={`${pack.dialect} · fingerprint ${pack.fingerprint}`}>
          <strong>{pack.name}</strong>
          <span className="val-rule-pack-meta">
            {pack.id}
            {pack.version ? ` · ${pack.version}` : ''} · {pack.ruleCount} rule(s) ·{' '}
            {shortFingerprint(pack.fingerprint)}
          </span>
        </div>
      ))}
      {missing.map((pack) => (
        <p key={pack.id} className="val-rule-diag">
          This model expects rule pack “{pack.id}”, which is not loaded. Findings will be incomplete.
        </p>
      ))}
      {ruleBundle.diagnostics.map((diagnostic) => (
        <p key={diagnostic} className="val-rule-diag">
          {diagnostic}
        </p>
      ))}
    </div>
  );
}

/**
 * The per-model analysis-rule picker: choose which rule packs and individual rules the model is
 * analyzed against. The selection travels with the model, so the Studio and the CLI agree.
 */
export function AnalysisSettings({
  rules,
  packs,
  disabledPacks,
  disabledRuleIds,
  ruleBundle,
  expectedPacks,
  onTogglePack,
  onToggleRule,
  onLoadRuleFile,
  onClearRuleFile,
}: AnalysisSettingsProps) {
  // Which rule's in-app help panel is expanded (only one at a time keeps the panel compact).
  const [openHelpId, setOpenHelpId] = useState<string | null>(null);

  if (packs.length === 0) {
    return (
      <p className="val-empty">
        Connect the engine to choose which rule packs and rules to analyze against.
      </p>
    );
  }

  const disabledPackSet = new Set(disabledPacks);
  const disabledRuleSet = new Set(disabledRuleIds);
  const rulesByPack = new Map<string, RuleInfo[]>();
  for (const rule of rules) {
    const list = rulesByPack.get(rule.pack);
    if (list) {
      list.push(rule);
    } else {
      rulesByPack.set(rule.pack, [rule]);
    }
  }

  return (
    <div className="val-settings">
      <CustomRulePacks
        ruleBundle={ruleBundle}
        expectedPacks={expectedPacks}
        onLoadRuleFile={onLoadRuleFile}
        onClearRuleFile={onClearRuleFile}
      />
      <div className="val-packs">
        {packs.map((pack) => {
          const enabled = !disabledPackSet.has(pack.id);
          return (
            <button
              key={pack.id}
              type="button"
              className={`pack-chip${enabled ? ' on' : ''}`}
              onClick={() => onTogglePack(pack.id)}
              title={`${enabled ? 'Disable' : 'Enable'} the ${pack.name} rule pack`}
            >
              {pack.name} <span className="pack-chip-count">{pack.count}</span>
            </button>
          );
        })}
      </div>

      {packs.map((pack) => {
        const packEnabled = !disabledPackSet.has(pack.id);
        const packRules = rulesByPack.get(pack.id) ?? [];
        if (packRules.length === 0) {
          return null;
        }
        return (
          <div key={pack.id} className={`val-group${packEnabled ? '' : ' is-off'}`}>
            <div className="val-group-head">{pack.name}</div>
            {packRules.map((rule) => {
              const ruleEnabled = packEnabled && !disabledRuleSet.has(rule.id);
              const helpOpen = openHelpId === rule.id;
              const helpPanelId = `val-rule-help-${rule.id}`;
              return (
                <div key={rule.id} className={`val-rule-item${helpOpen ? ' is-help-open' : ''}`}>
                  <div
                    className={`val-rule${ruleEnabled ? '' : ' is-off'}`}
                    role="button"
                    tabIndex={packEnabled ? 0 : -1}
                    aria-pressed={ruleEnabled}
                    onClick={packEnabled ? () => onToggleRule(rule.id) : undefined}
                    onKeyDown={
                      packEnabled
                        ? (event) => {
                            // Only toggle when the row itself has focus, so pressing Enter/Space on
                            // the nested help button does not also enable/disable the rule.
                            if (
                              event.target === event.currentTarget &&
                              (event.key === 'Enter' || event.key === ' ')
                            ) {
                              event.preventDefault();
                              onToggleRule(rule.id);
                            }
                          }
                        : undefined
                    }
                  >
                    <input type="checkbox" checked={ruleEnabled} disabled={!packEnabled} readOnly tabIndex={-1} />
                    <span className={`sev-dot sev-${rule.severity}`} aria-hidden />
                    <code className="val-rule-id">{rule.id}</code>
                    <span className="val-rule-desc" title={rule.description}>
                      {firstSentence(rule.description)}
                    </span>
                    <button
                      type="button"
                      className="val-rule-help"
                      aria-expanded={helpOpen}
                      aria-controls={helpPanelId}
                      title={helpOpen ? 'Hide rule help' : 'What does this rule check?'}
                      onClick={(event) => {
                        event.stopPropagation();
                        setOpenHelpId(helpOpen ? null : rule.id);
                      }}
                      onKeyDown={(event) => event.stopPropagation()}
                    >
                      ?
                    </button>
                  </div>
                  {helpOpen ? (
                    <div id={helpPanelId} className="val-rule-help-panel">
                      <p className="val-help-label">What it checks</p>
                      <p className="val-help-text">{rule.description}</p>
                      {rule.helpText ? (
                        <>
                          <p className="val-help-label">How to fix</p>
                          <p className="val-help-text">{rule.helpText}</p>
                        </>
                      ) : null}
                    </div>
                  ) : null}
                </div>
              );
            })}
          </div>
        );
      })}
    </div>
  );
}

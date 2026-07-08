import { useState } from 'react';
import type { RuleInfo, RulePackInfo } from './engineClient';

interface AnalysisSettingsProps {
  /** The full rule catalog from the engine. */
  rules: RuleInfo[];
  /** The rule packs from the engine, in presentation order. */
  packs: RulePackInfo[];
  /** Ids of rule packs the model currently skips. */
  disabledPacks: string[];
  /** Ids of individual rules the model currently skips. */
  disabledRuleIds: string[];
  onTogglePack: (packId: string) => void;
  onToggleRule: (ruleId: string) => void;
}

/** The first sentence of a rule description, for the compact one-line row. */
function firstSentence(text: string): string {
  const trimmed = text.trim();
  const end = trimmed.indexOf('. ');
  return end > 0 ? trimmed.slice(0, end + 1) : trimmed;
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
  onTogglePack,
  onToggleRule,
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

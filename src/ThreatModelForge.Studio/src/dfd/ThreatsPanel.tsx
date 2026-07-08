import { useState } from 'react';
import type { Finding, Threat } from './engineClient';

/** STRIDE categories in canonical display order (matches the engine's `StrideCategory`). */
const STRIDE_ORDER: readonly string[] = [
  'Spoofing',
  'Tampering',
  'Repudiation',
  'InformationDisclosure',
  'DenialOfService',
  'ElevationOfPrivilege',
];

/** Human-readable STRIDE headings. */
const STRIDE_LABEL: Record<string, string> = {
  Spoofing: 'Spoofing',
  Tampering: 'Tampering',
  Repudiation: 'Repudiation',
  InformationDisclosure: 'Information disclosure',
  DenialOfService: 'Denial of service',
  ElevationOfPrivilege: 'Elevation of privilege',
};

/** Builds a catalog deep-link for a CWE / CAPEC / ATT&CK reference id, or undefined when unrecognized. */
function referenceUrl(id: string): string | undefined {
  const cwe = /^CWE-(\d+)$/i.exec(id);
  if (cwe) {
    return `https://cwe.mitre.org/data/definitions/${cwe[1]}.html`;
  }
  const capec = /^CAPEC-(\d+)$/i.exec(id);
  if (capec) {
    return `https://capec.mitre.org/data/definitions/${capec[1]}.html`;
  }
  const attack = /^(T\d{4})(?:\.(\d{3}))?$/i.exec(id);
  if (attack) {
    return `https://attack.mitre.org/techniques/${attack[1]}${attack[2] ? `/${attack[2]}` : ''}/`;
  }
  return undefined;
}

export interface ThreatsPanelProps {
  /** The generated STRIDE threat register for the current model (open + accepted). */
  threats: Threat[];
  /** Non-threat hygiene findings (structural / naming rules), shown in a trailing section. */
  findings: Finding[];
  /** Navigates to (and reveals) the elements a threat or finding refers to. */
  onSelect: (elementIds: string[]) => void;
  /** Returns the name of the page an item's elements live on, when it is not the active page. */
  offPageLabel: (elementIds: string[]) => string | undefined;
  /** Accepts a threat's risk with a justification (moves it to the Accepted state). */
  onAccept: (threat: Threat, justification: string) => void;
  /** Reverts an accepted threat back to open. */
  onUndoAccept: (threat: Threat) => void;
}

/**
 * The Studio analysis panel: one place for the model's analysis. It leads with the STRIDE threat
 * register (grouped by category, each threat acceptable inline with a justification) and trails with
 * any non-threat hygiene findings. Threats and findings are the same detection — a threat is a
 * threat-bearing finding with a lifecycle — so they share one box rather than two near-identical ones.
 */
export function ThreatsPanel({ threats, findings, onSelect, offPageLabel, onAccept, onUndoAccept }: ThreatsPanelProps) {
  const [acceptingId, setAcceptingId] = useState<string | null>(null);
  const [justification, setJustification] = useState('');

  const byCategory = new Map<string, Threat[]>();
  for (const threat of threats) {
    const list = byCategory.get(threat.category) ?? [];
    list.push(threat);
    byCategory.set(threat.category, list);
  }
  // Known STRIDE categories first, in canonical order; any unknown category trails, alphabetically.
  const known = STRIDE_ORDER.filter((category) => byCategory.has(category));
  const unknown = [...byCategory.keys()].filter((category) => !STRIDE_ORDER.includes(category)).sort();
  const ordered = [...known, ...unknown];

  const acceptedCount = threats.filter((threat) => threat.state === 'Accepted').length;
  const openCount = threats.length - acceptedCount;

  function confirmAccept(threat: Threat) {
    const reason = justification.trim();
    if (!reason) {
      return;
    }
    onAccept(threat, reason);
    setAcceptingId(null);
    setJustification('');
  }

  return (
    <div className="threats">
      <h3>
        {openCount} open threat{openCount === 1 ? '' : 's'}
        {acceptedCount > 0 ? <span className="threats-accepted-count"> · {acceptedCount} accepted</span> : null}
      </h3>
      {ordered.map((category) => {
        const items = byCategory.get(category) ?? [];
        return (
          <div key={category} className="threat-group">
            <div className="threat-group-head">
              {STRIDE_LABEL[category] ?? category}
              <span className="threat-count">{items.length}</span>
            </div>
            {items.map((threat) => {
              const offPage = offPageLabel(threat.elementIds);
              const accepted = threat.state === 'Accepted';
              const isAccepting = acceptingId === threat.id;
              return (
                <div key={threat.id} className={accepted ? 'threat threat-accepted' : 'threat'}>
                  <div className="threat-row">
                    <div
                      className="threat-head"
                      role="button"
                      tabIndex={0}
                      title={threat.mitigation ? `Mitigation: ${threat.mitigation}` : undefined}
                      onClick={() => onSelect(threat.elementIds)}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter' || event.key === ' ') {
                          event.preventDefault();
                          onSelect(threat.elementIds);
                        }
                      }}
                    >
                      <span className={`sev sev-${threat.severity}`}>{threat.severity}</span>
                      <div className="threat-body">
                        <div className="threat-title">
                          {threat.ruleId ? <code className="rule-id">{threat.ruleId}</code> : null} {threat.title}
                          {accepted ? <span className="threat-badge">Accepted</span> : null}
                          {offPage ? <span className="finding-page">{offPage}</span> : null}
                        </div>
                        <div className="threat-meta">
                          {threat.interaction ? <span className="threat-scope">{threat.interaction}</span> : null}
                          {threat.references.map((reference) => {
                            const url = referenceUrl(reference);
                            return url ? (
                              <a
                                key={reference}
                                className="threat-ref"
                                href={url}
                                target="_blank"
                                rel="noreferrer"
                                onClick={(event) => event.stopPropagation()}
                              >
                                {reference}
                              </a>
                            ) : (
                              <span key={reference} className="threat-ref">
                                {reference}
                              </span>
                            );
                          })}
                        </div>
                        {accepted && threat.justification ? (
                          <div className="threat-justification">“{threat.justification}”</div>
                        ) : null}
                      </div>
                    </div>
                    <div className="threat-actions">
                      {accepted ? (
                        <button type="button" className="threat-action" onClick={() => onUndoAccept(threat)}>
                          Undo
                        </button>
                      ) : !isAccepting ? (
                        <button
                          type="button"
                          className="threat-action"
                          onClick={() => {
                            setAcceptingId(threat.id);
                            setJustification('');
                          }}
                        >
                          Accept
                        </button>
                      ) : null}
                    </div>
                  </div>
                  {isAccepting ? (
                    <form
                      className="threat-accept"
                      onSubmit={(event) => {
                        event.preventDefault();
                        confirmAccept(threat);
                      }}
                    >
                      <textarea
                        className="threat-accept-input"
                        placeholder="Why is this risk accepted? (required)"
                        value={justification}
                        autoFocus
                        onChange={(event) => setJustification(event.target.value)}
                        onKeyDown={(event) => {
                          if (event.key === 'Escape') {
                            setAcceptingId(null);
                          }
                        }}
                      />
                      <div className="threat-accept-buttons">
                        <button type="submit" className="btn btn-primary" disabled={!justification.trim()}>
                          Accept risk
                        </button>
                        <button type="button" className="btn" onClick={() => setAcceptingId(null)}>
                          Cancel
                        </button>
                      </div>
                    </form>
                  ) : null}
                </div>
              );
            })}
          </div>
        );
      })}
      {findings.length > 0 ? (
        <div className="threat-group">
          <div className="threat-group-head">
            Other findings
            <span className="threat-count">{findings.length}</span>
          </div>
          {findings.map((finding) => {
            const offPage = offPageLabel(finding.elementIds);
            return (
              <div key={finding.id} className="threat">
                <div className="threat-row">
                  <div
                    className="threat-head"
                    role="button"
                    tabIndex={0}
                    onClick={() => onSelect(finding.elementIds)}
                    onKeyDown={(event) => {
                      if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault();
                        onSelect(finding.elementIds);
                      }
                    }}
                  >
                    <span className={`sev sev-${finding.severity}`}>{finding.severity}</span>
                    <div className="threat-body">
                      <div className="threat-title">
                        {finding.ruleId ? <code className="rule-id">{finding.ruleId}</code> : null} {finding.message}
                        {offPage ? <span className="finding-page">{offPage}</span> : null}
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      ) : null}
    </div>
  );
}

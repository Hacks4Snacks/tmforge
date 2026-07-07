import { useCallback, useMemo, useState } from 'react';
import type { IEngineClient, MergeConflict } from './engineClient';
import type { TmForgeModel } from './types';

/** Downloads a blob as a named file (self-contained; mirrors the editor's helper). */
function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}

/** Reads a picked file into a model, mirroring the editor's import path (native json stays client-side). */
async function readModelFile(engine: IEngineClient, file: File): Promise<TmForgeModel> {
  const bytes = new Uint8Array(await file.arrayBuffer());
  const detected = await engine.detect(bytes).catch(() => null);
  const formatId = detected?.id ?? 'tmforge-json';
  if (formatId === 'tmforge-json') {
    return engine.read(new TextDecoder().decode(bytes));
  }
  return engine.readFile(bytes, formatId);
}

/** A stable key for a conflict (unique per element + property). */
function conflictKey(c: MergeConflict): string {
  return `${c.elementId}\u0000${c.property}`;
}

/** Sets one attribute on the element/flow with the given id (searches elements then flows). */
function applyProperty(model: TmForgeModel, id: string, property: string, value: string): void {
  const element = model.elements.find((e) => e.id === id);
  if (element) {
    if (property === 'name') {
      element.name = value;
    } else {
      element.properties = { ...element.properties, [property]: value };
    }
    return;
  }
  const flow = model.flows.find((f) => f.id === id);
  if (!flow) {
    return;
  }
  if (property === 'name') {
    flow.name = value;
  } else if (property === 'source') {
    flow.source = value;
  } else if (property === 'target') {
    flow.target = value;
  } else {
    flow.properties = { ...flow.properties, [property]: value };
  }
}

/**
 * Applies the user's per-conflict choices to a copy of the merged model. The engine already
 * resolved every conflict to `ours`, so we only overwrite the properties the user flipped to
 * `theirs`. Only `Property` conflicts are machine-applicable; structural ones are informational.
 */
function applyChoices(
  merged: TmForgeModel,
  conflicts: MergeConflict[],
  choices: Record<string, 'ours' | 'theirs'>,
): TmForgeModel {
  const model = JSON.parse(JSON.stringify(merged)) as TmForgeModel;
  for (const c of conflicts) {
    if (c.kind !== 'Property' || choices[conflictKey(c)] !== 'theirs') {
      continue;
    }
    applyProperty(model, c.elementId, c.property, c.theirs ?? '');
  }
  return model;
}

/** Human labels for the structural conflict kinds (Property conflicts get an ours/theirs picker instead). */
const STRUCTURAL_HELP: Record<string, string> = {
  DeleteModify: 'One side deleted this element while the other changed it — your version was kept.',
  AddAdd: 'Both sides added a different element with the same id — your version was kept.',
  DanglingReference: 'A data flow points at an element that no longer exists after the merge — fix it after loading.',
};

interface LoadedMerge {
  merged: TmForgeModel;
  conflicts: MergeConflict[];
}

interface MergeResolveModalProps {
  engine: IEngineClient;
  onClose: () => void;
  onResolved: (model: TmForgeModel) => void;
}

/**
 * A modal that three-way merges two edited `.tm7` files against their common ancestor. It runs the
 * same engine merge the CLI/git driver uses, lists each conflict with its base/ours/theirs values,
 * lets you pick ours-or-theirs per property conflict, then loads the resolved model into the editor
 * or downloads it as `.tm7`.
 */
export function MergeResolveModal({ engine, onClose, onResolved }: MergeResolveModalProps) {
  const [base, setBase] = useState<File | null>(null);
  const [ours, setOurs] = useState<File | null>(null);
  const [theirs, setTheirs] = useState<File | null>(null);
  const [loaded, setLoaded] = useState<LoadedMerge | null>(null);
  const [choices, setChoices] = useState<Record<string, 'ours' | 'theirs'>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const compute = useCallback(async () => {
    if (!ours || !theirs) {
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const [b, o, t] = await Promise.all([
        base ? readModelFile(engine, base) : Promise.resolve(null),
        readModelFile(engine, ours),
        readModelFile(engine, theirs),
      ]);
      const result = await engine.merge(b, o, t);
      setLoaded({ merged: result.merged, conflicts: result.conflicts });
      // Default every conflict to 'ours' — what the engine kept.
      const initial: Record<string, 'ours' | 'theirs'> = {};
      for (const c of result.conflicts) {
        initial[conflictKey(c)] = 'ours';
      }
      setChoices(initial);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Merge failed.');
      setLoaded(null);
    } finally {
      setBusy(false);
    }
  }, [engine, base, ours, theirs]);

  const resolved = useMemo(
    () => (loaded ? applyChoices(loaded.merged, loaded.conflicts, choices) : null),
    [loaded, choices],
  );

  const propertyConflicts = loaded?.conflicts.filter((c) => c.kind === 'Property') ?? [];
  const structuralConflicts = loaded?.conflicts.filter((c) => c.kind !== 'Property') ?? [];

  const load = useCallback(() => {
    if (resolved) {
      onResolved(resolved);
    }
  }, [resolved, onResolved]);

  const download = useCallback(async () => {
    if (!resolved) {
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const blob = await engine.convert(resolved, 'tm7');
      downloadBlob(blob, 'merged.tm7');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Export failed.');
    } finally {
      setBusy(false);
    }
  }, [engine, resolved]);

  return (
    <div className="modal-backdrop" role="dialog" aria-modal aria-label="Merge models" onClick={onClose}>
      <div className="modal merge-modal" onClick={(e) => e.stopPropagation()}>
        <header className="merge-head">
          <h2>Merge models</h2>
          <button className="btn btn-icon" aria-label="Close" onClick={onClose}>
            ✕
          </button>
        </header>

        <p className="merge-intro">
          Pick the two edited versions (and the common ancestor, if you have it). Non-overlapping
          changes merge automatically; anything both sides touched is listed below so you can choose
          which to keep.
        </p>

        <div className="merge-inputs">
          <FilePick label="Base (common ancestor) — optional" file={base} onPick={setBase} />
          <FilePick label="Ours (your version)" file={ours} onPick={setOurs} />
          <FilePick label="Theirs (incoming)" file={theirs} onPick={setTheirs} />
        </div>

        {!base ? (
          <p className="merge-hint">
            No base selected — the merge runs two-way, so every overlapping change is listed as a
            conflict for you to resolve.
          </p>
        ) : null}

        <div className="merge-actions">
          <button
            className="btn btn-primary"
            onClick={() => void compute()}
            disabled={!ours || !theirs || busy}
          >
            {busy ? 'Merging' : loaded ? 'Re-merge' : 'Merge'}
          </button>
        </div>

        {error ? <div className="merge-error">{error}</div> : null}

        {loaded ? (
          <div className="merge-result">
            {loaded.conflicts.length === 0 ? (
              <p className="merge-clean">✓ Clean merge — no conflicts. Load it into the editor or download it.</p>
            ) : (
              <>
                <h3>
                  {loaded.conflicts.length} conflict{loaded.conflicts.length === 1 ? '' : 's'}
                  {propertyConflicts.length > 0 ? ` · ${propertyConflicts.length} to resolve` : ''}
                </h3>
                {propertyConflicts.length > 0 ? (
                  <ul className="conflict-list">
                    {propertyConflicts.map((c) => {
                      const key = conflictKey(c);
                      const choice = choices[key] ?? 'ours';
                      return (
                        <li key={key} className="conflict">
                          <div className="conflict-where">
                            <span className={`conflict-kind kind-${c.elementKind}`}>{c.elementKind}</span>
                            <strong>{c.name || '(unnamed)'}</strong>
                            {c.diagramName ? <span className="conflict-diagram">· {c.diagramName}</span> : null}
                            <code className="conflict-prop">{c.property}</code>
                          </div>
                          <div className="conflict-choices">
                            <label className={`choice${choice === 'ours' ? ' selected' : ''}`}>
                              <input
                                type="radio"
                                name={key}
                                checked={choice === 'ours'}
                                onChange={() => setChoices((prev) => ({ ...prev, [key]: 'ours' }))}
                              />
                              <span className="choice-side">Ours</span>
                              <span className="choice-val">{c.ours ?? '(empty)'}</span>
                            </label>
                            <label className={`choice${choice === 'theirs' ? ' selected' : ''}`}>
                              <input
                                type="radio"
                                name={key}
                                checked={choice === 'theirs'}
                                onChange={() => setChoices((prev) => ({ ...prev, [key]: 'theirs' }))}
                              />
                              <span className="choice-side">Theirs</span>
                              <span className="choice-val">{c.theirs ?? '(empty)'}</span>
                            </label>
                          </div>
                        </li>
                      );
                    })}
                  </ul>
                ) : null}
                {structuralConflicts.length > 0 ? (
                  <ul className="conflict-list structural">
                    {structuralConflicts.map((c) => (
                      <li key={conflictKey(c)} className="conflict conflict-structural">
                        <div className="conflict-where">
                          <span className={`conflict-kind kind-${c.elementKind}`}>{c.elementKind}</span>
                          <strong>{c.name || '(unnamed)'}</strong>
                          {c.diagramName ? <span className="conflict-diagram">· {c.diagramName}</span> : null}
                          <span className="conflict-badge">{c.kind}</span>
                        </div>
                        <p className="conflict-note">{STRUCTURAL_HELP[c.kind] ?? 'Your version was kept.'}</p>
                      </li>
                    ))}
                  </ul>
                ) : null}
              </>
            )}

            <div className="merge-result-actions">
              <button className="btn" onClick={() => void download()} disabled={busy || !resolved}>
                Download .tm7
              </button>
              <button className="btn btn-primary" onClick={load} disabled={busy || !resolved}>
                Load into editor
              </button>
            </div>
          </div>
        ) : null}
      </div>
    </div>
  );
}

/** A labelled file input for one side of the merge, styled to match the app's buttons. */
function FilePick({ label, file, onPick }: { label: string; file: File | null; onPick: (file: File | null) => void }) {
  return (
    <div className="file-pick">
      <span className="file-pick-label">{label}</span>
      <label className={`file-pick-control${file ? ' has-file' : ''}`}>
        <input
          type="file"
          className="file-pick-input"
          accept=".tm7,.json,.drawio,.vsdx"
          aria-label={label}
          onChange={(e) => onPick(e.target.files?.[0] ?? null)}
        />
        <span className="file-pick-btn">{file ? 'Change file' : 'Choose file'}</span>
        <span className="file-pick-name">{file ? file.name : 'No file selected'}</span>
      </label>
    </div>
  );
}

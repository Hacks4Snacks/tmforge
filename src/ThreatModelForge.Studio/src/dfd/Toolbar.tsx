import { useEffect, useRef, useState } from 'react';
import { UndoIcon, RedoIcon } from './icons';

interface ExportFormat {
  id: string;
  displayName: string;
}

/** A compact dropdown for Export/convert — replaces a native <select> that stretched to its widest option. */
function ExportMenu({ formats, onExport }: { formats: ExportFormat[]; onExport: (formatId: string) => void }) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) {
      return;
    }
    const onPointer = (event: MouseEvent) => {
      if (ref.current && !ref.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', onPointer);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onPointer);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  return (
    <div className="menu" ref={ref}>
      <button
        className="btn"
        disabled={formats.length === 0}
        aria-haspopup="menu"
        aria-expanded={open}
        title="Export or convert to a file format"
        onClick={() => setOpen((value) => !value)}
      >
        Export ▾
      </button>
      {open && (
        <div className="menu-list" role="menu">
          {formats.map((format) => (
            <button
              key={format.id}
              type="button"
              role="menuitem"
              className="menu-item"
              onClick={() => {
                onExport(format.id);
                setOpen(false);
              }}
            >
              {format.displayName}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

interface ToolbarProps {
  engineLabel: string;
  engineOnline: boolean;
  /** True in a static demo build (no /v1 engine): the engine pill frames it as in-browser. */
  demo?: boolean;
  exportFormats: ExportFormat[];
  onExport: (formatId: string) => void;
  onImport: () => void;
  onSave: () => void;
  /** Opens the three-way merge / conflict-resolution dialog. */
  onMerge: () => void;
  /** True when the model has changes not yet written to a file. */
  dirty: boolean;
  /** Name of the file the model is bound to (what Save overwrites), or null when unsaved. */
  fileName: string | null;
  onAnalyze: () => void;
  /** Downloads a self-contained HTML threat report from the engine. */
  onReport: () => void;
  onClear: () => void;
  onFit: () => void;
  /** Auto-sizes shapes to fit their text, routes flows through facing ports, and separates flow labels on the current page. */
  onTidy: () => void;
  onUndo: () => void;
  onRedo: () => void;
  canUndo: boolean;
  canRedo: boolean;
  theme: 'light' | 'dark';
  onToggleTheme: () => void;
}

export function Toolbar(props: ToolbarProps) {
  return (
    <header className="toolbar">
      <span className="toolbar-brand">
        Threat Model Forge <small>· Studio</small>
      </span>

      <span className="toolbar-div" />

      <div className="toolbar-group">
        <button className="btn btn-icon" onClick={props.onUndo} disabled={!props.canUndo} aria-label="Undo" title="Undo (⌘Z)">
          <UndoIcon />
        </button>
        <button className="btn btn-icon" onClick={props.onRedo} disabled={!props.canRedo} aria-label="Redo" title="Redo (⇧⌘Z)">
          <RedoIcon />
        </button>
      </div>

      <span className="toolbar-div" />

      <div className="toolbar-group">

        <button className="btn" onClick={props.onImport}>
          Open File
        </button>
        <button
          className="btn"
          onClick={props.onSave}
          title={props.fileName ? `Save to ${props.fileName} (⌘S)` : 'Save to a file (⌘S)'}
        >
          Save
        </button>
        <ExportMenu formats={props.exportFormats} onExport={props.onExport} />
        <button
          className="btn"
          onClick={props.onMerge}
          title="Three-way merge two edited .tm7 files against their common ancestor"
        >
          Merge
        </button>
      </div>

      <span className="toolbar-div" />

      <button
        className="btn btn-primary"
        onClick={props.onAnalyze}
        title="Analyze the model: generate the STRIDE threat register and check model hygiene"
      >
        Analyze
      </button>
      <button
        className="btn"
        onClick={props.onReport}
        title="Download a self-contained HTML threat report"
      >
        Report
      </button>

      <span className="toolbar-spacer" />

      {props.fileName ? (
        <span className="file-chip" title={`Editing ${props.fileName}`}>
          {props.fileName}
        </span>
      ) : null}
      <span
        className={`save-status ${props.dirty ? 'dirty' : 'clean'}`}
        title={props.dirty ? 'You have unsaved changes' : 'All changes saved'}
      >
        {props.dirty ? '● Unsaved' : '✓ Saved'}
      </span>
      <span
        className={`engine-pill ${props.engineOnline ? 'online' : 'offline'}`}
        title={props.demo ? 'Runs entirely in your browser — no server. Your model never leaves this page.' : props.engineLabel}
      >
        <span className="engine-dot" />
        {props.engineLabel}
      </span>

      <span className="toolbar-div" />

      <div className="toolbar-group">
        <button
          className="btn btn-icon"
          onClick={props.onToggleTheme}
          aria-label="Toggle dark mode"
          title={props.theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
        >
          {props.theme === 'dark' ? '☀' : '☾'}
        </button>
        <button className="btn" onClick={props.onFit}>
          Fit
        </button>
        <button
          className="btn"
          onClick={props.onTidy}
          title="Auto-layout: size shapes to their text, route flows through facing ports, and separate the flow labels so lines and text stay clear"
        >
          Tidy
        </button>
        <button className="btn" onClick={props.onClear}>
          Clear
        </button>
      </div>
    </header>
  );
}

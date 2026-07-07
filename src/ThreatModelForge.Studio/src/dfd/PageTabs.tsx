import { useEffect, useRef, useState } from 'react';

/** Minimal page metadata the tab strip renders. */
export interface PageTabInfo {
  id: string;
  name: string;
}

interface PageTabsProps {
  pages: PageTabInfo[];
  activePageId: string;
  /** Page ids that currently have one or more findings, to badge the tab. */
  findingPageIds?: ReadonlySet<string>;
  onSwitch: (id: string) => void;
  onAdd: () => void;
  onRename: (id: string, name: string) => void;
  onDelete: (id: string) => void;
  onReorder: (fromIndex: number, toIndex: number) => void;
}

/**
 * A spreadsheet-style tab strip below the canvas: switch, add, rename (double-click / F2), delete,
 * and drag-to-reorder pages. The last page cannot be deleted (its close button is hidden).
 */
export function PageTabs({
  pages,
  activePageId,
  findingPageIds,
  onSwitch,
  onAdd,
  onRename,
  onDelete,
  onReorder,
}: PageTabsProps) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draft, setDraft] = useState('');
  const dragIndex = useRef<number | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (editingId) {
      inputRef.current?.focus();
      inputRef.current?.select();
    }
  }, [editingId]);

  const beginRename = (page: PageTabInfo) => {
    setEditingId(page.id);
    setDraft(page.name);
  };

  const commitRename = () => {
    if (editingId) {
      const name = draft.trim();
      if (name) {
        onRename(editingId, name);
      }
    }
    setEditingId(null);
  };

  return (
    <div className="page-tabs" role="tablist" aria-label="Diagram pages">
      <div className="page-tabs-scroll">
        {pages.map((page, index) => {
          const active = page.id === activePageId;
          const editing = editingId === page.id;
          return (
            <div
              key={page.id}
              role="tab"
              aria-selected={active}
              tabIndex={0}
              className={`page-tab${active ? ' active' : ''}`}
              draggable={!editing}
              onDragStart={() => {
                dragIndex.current = index;
              }}
              onDragOver={(event) => {
                if (dragIndex.current !== null && dragIndex.current !== index) {
                  event.preventDefault();
                }
              }}
              onDrop={(event) => {
                event.preventDefault();
                const from = dragIndex.current;
                dragIndex.current = null;
                if (from !== null && from !== index) {
                  onReorder(from, index);
                }
              }}
              onClick={() => onSwitch(page.id)}
              onDoubleClick={() => beginRename(page)}
              onKeyDown={(event) => {
                if (editing) {
                  return;
                }
                if (event.key === 'Enter' || event.key === ' ') {
                  event.preventDefault();
                  onSwitch(page.id);
                } else if (event.key === 'F2') {
                  event.preventDefault();
                  beginRename(page);
                }
              }}
              title={page.name}
            >
              {editing ? (
                <input
                  ref={inputRef}
                  className="page-tab-input"
                  value={draft}
                  onChange={(event) => setDraft(event.target.value)}
                  onBlur={commitRename}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter') {
                      event.preventDefault();
                      commitRename();
                    } else if (event.key === 'Escape') {
                      event.preventDefault();
                      setEditingId(null);
                    }
                  }}
                  onClick={(event) => event.stopPropagation()}
                  onDoubleClick={(event) => event.stopPropagation()}
                />
              ) : (
                <>
                  <span className="page-tab-label">{page.name}</span>
                  {findingPageIds?.has(page.id) ? <span className="page-tab-dot" aria-hidden /> : null}
                </>
              )}
              {pages.length > 1 && !editing ? (
                <button
                  type="button"
                  className="page-tab-close"
                  title={`Delete ${page.name}`}
                  aria-label={`Delete ${page.name}`}
                  onClick={(event) => {
                    event.stopPropagation();
                    onDelete(page.id);
                  }}
                >
                  ×
                </button>
              ) : null}
            </div>
          );
        })}
      </div>
      <button type="button" className="page-tab-add" title="Add page" aria-label="Add page" onClick={onAdd}>
        +
      </button>
    </div>
  );
}

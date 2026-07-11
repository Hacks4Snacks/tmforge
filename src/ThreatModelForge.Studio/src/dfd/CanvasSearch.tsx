import { useMemo, useState } from 'react';

/** One searchable item: a placed node or a flow, with the page it lives on. */
export interface SearchItem {
  id: string;
  name: string;
  kind: string;
  pageId: string;
  pageName: string;
}

const KIND_LABEL: Record<string, string> = {
  process: 'Process',
  datastore: 'Data store',
  external: 'External',
  boundary: 'Trust boundary',
  flow: 'Flow',
};

/** How many matches to show at once, to keep the dropdown bounded on large models. */
const MAX_RESULTS = 12;

/**
 * A canvas search box: type to find any placed element or flow across every page and jump to it —
 * switching pages, selecting it, and framing it in view. This extends the palette's stencil search
 * from the catalog to the model's own contents, and reuses the same jump-to affordance the analysis
 * panel already offers for findings.
 */
export function CanvasSearch({ items, onJump }: { items: SearchItem[]; onJump: (item: SearchItem) => void }) {
  const [query, setQuery] = useState('');
  const q = query.trim().toLowerCase();

  const matches = useMemo(() => {
    if (!q) {
      return [];
    }
    return items
      .filter((item) => item.name.toLowerCase().includes(q) || (KIND_LABEL[item.kind] ?? item.kind).toLowerCase().includes(q))
      .slice(0, MAX_RESULTS);
  }, [items, q]);

  const jump = (item: SearchItem) => {
    onJump(item);
    setQuery('');
  };

  return (
    <div className="canvas-search">
      <input
        className="canvas-search-input"
        type="search"
        placeholder="Find element"
        aria-label="Find an element or flow on the canvas"
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter' && matches[0]) {
            event.preventDefault();
            jump(matches[0]);
          } else if (event.key === 'Escape') {
            setQuery('');
          }
        }}
      />
      {q ? (
        <ul className="canvas-search-results" role="listbox">
          {matches.length === 0 ? (
            <li className="canvas-search-empty">No matches</li>
          ) : (
            matches.map((item) => (
              <li key={`${item.pageId}:${item.id}`}>
                <button type="button" className="canvas-search-item" onClick={() => jump(item)}>
                  <span className="canvas-search-name">{item.name}</span>
                  <span className="canvas-search-meta">
                    {KIND_LABEL[item.kind] ?? item.kind} · {item.pageName}
                  </span>
                </button>
              </li>
            ))
          )}
        </ul>
      ) : null}
    </div>
  );
}

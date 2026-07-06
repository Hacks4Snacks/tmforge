import { useState } from 'react';
import { StencilIcon } from './icons';
import type { PackInfo, StencilInfo } from './engineClient';

interface PaletteProps {
  stencils: StencilInfo[];
  packs: PackInfo[];
  /** Ids of recently placed stencils, most recent first. */
  recentIds: string[];
  /** Ids of favorited (starred) stencils, most recent first. */
  favoriteIds: string[];
  /** Ids of packs the user has hidden. */
  disabledPacks: string[];
  onTogglePack: (packId: string) => void;
  onToggleFavorite: (stencilId: string) => void;
}

/** Groups stencils by category, preserving the catalog's insertion order. */
function groupByCategory(stencils: StencilInfo[]): [string, StencilInfo[]][] {
  const groups = new Map<string, StencilInfo[]>();
  for (const stencil of stencils) {
    const list = groups.get(stencil.category);
    if (list) {
      list.push(stencil);
    } else {
      groups.set(stencil.category, [stencil]);
    }
  }
  return [...groups.entries()];
}

/** Groups stencils by pack (ordered by the pack list), then by category within each pack. */
function groupByPack(
  stencils: StencilInfo[],
  packs: PackInfo[],
): { pack: PackInfo; categories: [string, StencilInfo[]][]; count: number }[] {
  const byPack = new Map<string, StencilInfo[]>();
  for (const stencil of stencils) {
    const list = byPack.get(stencil.pack);
    if (list) {
      list.push(stencil);
    } else {
      byPack.set(stencil.pack, [stencil]);
    }
  }

  const known = packs.filter((p) => byPack.has(p.id));
  const extras: PackInfo[] = [...byPack.keys()]
    .filter((id) => !packs.some((p) => p.id === id))
    .map((id) => ({ id, name: id, count: 0 }));

  return [...known, ...extras].map((pack) => {
    const items = byPack.get(pack.id) ?? [];
    return { pack, categories: groupByCategory(items), count: items.length };
  });
}

/** Case-insensitive match across the fields a user is likely to search by. */
function matches(stencil: StencilInfo, needle: string): boolean {
  const haystack = `${stencil.label} ${stencil.blurb} ${stencil.category} ${stencil.id} ${stencil.tags.join(' ')}`;
  return haystack.toLowerCase().includes(needle);
}

/** A single draggable stencil. The canvas reads the stencil id from the drag payload on drop. */
function StencilItem({
  stencil,
  isFavorite,
  onToggleFavorite,
}: {
  stencil: StencilInfo;
  isFavorite: boolean;
  onToggleFavorite: (stencilId: string) => void;
}) {
  return (
    <div
      className="stencil"
      draggable
      onDragStart={(e) => {
        e.dataTransfer.setData('application/tmforge-stencil', stencil.id);
        e.dataTransfer.effectAllowed = 'move';
      }}
    >
      <span className={`stencil-glyph glyph-${stencil.base}`} aria-hidden>
        <StencilIcon id={stencil.id} base={stencil.base} size={20} />
      </span>
      <span className="stencil-text">
        <span className="stencil-label">{stencil.label}</span>
        <span className="stencil-blurb">{stencil.blurb}</span>
      </span>
      <button
        type="button"
        className={`stencil-fav${isFavorite ? ' is-fav' : ''}`}
        draggable={false}
        aria-pressed={isFavorite}
        aria-label={isFavorite ? `Remove ${stencil.label} from favorites` : `Add ${stencil.label} to favorites`}
        title={isFavorite ? 'Unfavorite' : 'Favorite'}
        onClick={(e) => {
          e.stopPropagation();
          onToggleFavorite(stencil.id);
        }}
      >
        {isFavorite ? '\u2605' : '\u2606'}
      </button>
    </div>
  );
}

/** Left sidebar: searchable, collapsible catalog with pack toggles, favorites, and recents. */
export function Palette({
  stencils,
  packs,
  recentIds,
  favoriteIds,
  disabledPacks,
  onTogglePack,
  onToggleFavorite,
}: PaletteProps) {
  const [query, setQuery] = useState('');
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});

  const needle = query.trim().toLowerCase();
  const searching = needle.length > 0;

  const disabled = new Set(disabledPacks);
  const favorites = new Set(favoriteIds);

  // Only stencils from enabled packs are shown anywhere (groups, favorites, recents).
  const enabled = stencils.filter((s) => !disabled.has(s.pack));
  const enabledById = new Map(enabled.map((s) => [s.id, s]));

  const visible = searching ? enabled.filter((s) => matches(s, needle)) : enabled;
  const packGroups = groupByPack(visible, packs);

  const resolve = (ids: string[]) =>
    ids.map((id) => enabledById.get(id)).filter((s): s is StencilInfo => Boolean(s));

  // Favorites + recents are shortcuts over the enabled catalog; hidden while searching.
  const favoriteStencils = searching ? [] : resolve(favoriteIds);
  const recentStencils = searching ? [] : resolve(recentIds.filter((id) => !favorites.has(id)));

  const toggle = (packId: string) =>
    setCollapsed((prev) => ({ ...prev, [packId]: !(prev[packId] ?? true) }));

  return (
    <aside className="palette">
      <h2 className="palette-title">Stencils</h2>
      <input
        className="palette-search"
        type="search"
        placeholder="Search stencils"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        aria-label="Search stencils"
      />

      {packs.length > 1 && (
        <div className="palette-packs" role="group" aria-label="Show or hide stencil packs">
          {packs.map((p) => {
            const on = !disabled.has(p.id);
            return (
              <button
                key={p.id}
                type="button"
                className={`pack-chip${on ? ' on' : ''}`}
                aria-pressed={on}
                title={`${on ? 'Hide' : 'Show'} the ${p.name} pack (${p.count})`}
                onClick={() => onTogglePack(p.id)}
              >
                {p.name}
              </button>
            );
          })}
        </div>
      )}

      {favoriteStencils.length > 0 && (
        <div className="palette-group">
          <h3 className="palette-group-title">Favorites</h3>
          {favoriteStencils.map((s) => (
            <StencilItem key={`fav-${s.id}`} stencil={s} isFavorite onToggleFavorite={onToggleFavorite} />
          ))}
        </div>
      )}

      {recentStencils.length > 0 && (
        <div className="palette-group">
          <h3 className="palette-group-title">Recently used</h3>
          {recentStencils.map((s) => (
            <StencilItem
              key={`recent-${s.id}`}
              stencil={s}
              isFavorite={favorites.has(s.id)}
              onToggleFavorite={onToggleFavorite}
            />
          ))}
        </div>
      )}

      {packGroups.length === 0 ? (
        <p className="palette-empty">
          {searching ? `No stencils match “${query.trim()}”.` : 'All packs are hidden — enable one above.'}
        </p>
      ) : (
        packGroups.map(({ pack, categories, count }) => {
          // Groups start collapsed; a search always reveals its matches, and once a group is
          // expanded that choice is remembered until it is toggled shut again.
          const isCollapsed = !searching && (collapsed[pack.id] ?? true);
          const showSubheads = categories.length > 1;
          return (
            <div className="palette-pack" key={pack.id}>
              <button
                type="button"
                className="palette-group-toggle"
                aria-expanded={!isCollapsed}
                onClick={() => toggle(pack.id)}
              >
                <span className="palette-caret" aria-hidden>
                  {isCollapsed ? '▸' : '▾'}
                </span>
                <span className="palette-group-title">{pack.name}</span>
                <span className="palette-group-count">{count}</span>
              </button>
              {!isCollapsed &&
                categories.map(([category, items]) => (
                  <div className="palette-subgroup" key={category}>
                    {showSubheads && <div className="palette-subhead">{category}</div>}
                    {items.map((s) => (
                      <StencilItem
                        key={s.id}
                        stencil={s}
                        isFavorite={favorites.has(s.id)}
                        onToggleFavorite={onToggleFavorite}
                      />
                    ))}
                  </div>
                ))}
            </div>
          );
        })
      )}

      <div className="palette-foot">
        Drag a node's port to connect · double-click to rename · <kbd>Delete</kbd> removes
        selection · move a boundary by its label, resize from its corner.
      </div>
    </aside>
  );
}

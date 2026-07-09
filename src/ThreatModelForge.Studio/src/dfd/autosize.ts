import { DEFAULT_NODE_SIZE } from './mapping';
import type { DfdEdge, DfdKind, DfdNode } from './types';

/**
 * Auto-layout helpers that make an imported (for example, CLI-authored) model readable without
 * manual clean-up: every shape is grown to fit its label so the name never overruns the boundary,
 * and flow labels that would stack on the same spot — the classic request/response pair between two
 * nodes — are nudged apart. All functions are pure and deterministic so a repeated run is a no-op.
 */

/** Label font — mirrors `.dfd-node` (13px / 600) over the app's system font stack, for measurement. */
const LABEL_FONT =
  "600 13px -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

/** Rendered line box of one wrapped label line (13px × 1.2 line-height). */
const LINE_HEIGHT = 16;
/** The icon (plus its margin) stacked above the label inside every shape node. */
const ICON_BLOCK = 26;
/** The small uppercase stencil caption shown above the label for specialized stencils. */
const CAPTION_HEIGHT = 12;
/** Inner padding of `.dfd-node` (≈8px vertical, 12px horizontal) plus a little breathing room. */
const PAD_X = 14;
const PAD_Y = 10;
/** A couple of pixels of slack so DOM wrapping never disagrees with the measured line width. */
const WIDTH_SLACK = 4;

/**
 * The width a label is wrapped to before its shape grows wider, per kind. A process is drawn as an
 * ellipse, so its text column is kept narrower; the boxy store / external shapes can run wider.
 */
const WRAP_TARGET: Record<Exclude<DfdKind, 'boundary'>, number> = {
  process: 150,
  datastore: 210,
  external: 210,
};

/** The most a shape will grow in each dimension, so one very long name can't produce a huge node. */
const MAX_WIDTH = 340;
const MAX_HEIGHT = 240;

/**
 * The smallest a shape is allowed to become when fitting it to its text. These are deliberately
 * tighter than {@link DEFAULT_NODE_SIZE} (the size a freshly dropped stencil gets) so that fitting an
 * imported model to its content stays compact and respects the author's original, denser layout
 * rather than inflating every shape to the palette default.
 */
const MIN_SIZE: Record<Exclude<DfdKind, 'boundary'>, NodeSize> = {
  process: { width: 96, height: 96 },
  datastore: { width: 120, height: 64 },
  external: { width: 120, height: 64 },
};

/**
 * A process is an ellipse: to inscribe a w×h text box its axes must be ≈√2 larger. This factor keeps
 * the wrapped label comfortably inside the ellipse instead of clipping against the curved edge.
 */
const ELLIPSE_FIT = 1.42;

let measureCtx: CanvasRenderingContext2D | null | undefined;

/** A cached 2D context for text measurement, or null where canvas is unavailable (e.g. jsdom tests). */
function context(): CanvasRenderingContext2D | null {
  if (measureCtx === undefined) {
    try {
      measureCtx = document.createElement('canvas').getContext('2d') ?? null;
      if (measureCtx) {
        measureCtx.font = LABEL_FONT;
      }
    } catch {
      measureCtx = null;
    }
  }
  return measureCtx;
}

/** Width (px) of one line of label text. Falls back to a per-character estimate without canvas. */
export function textWidth(text: string): number {
  const ctx = context();
  if (ctx) {
    return ctx.measureText(text).width;
  }
  // ≈7.1px average glyph advance for 13px/600 in the system stack — deterministic for tests.
  return text.length * 7.1;
}

/** Greedy word-wrap to a target pixel width; a single word wider than the target is hard-broken. */
export function wrapLabel(label: string, maxWidth: number): string[] {
  const words = label.split(/\s+/).filter(Boolean);
  if (words.length === 0) {
    return [''];
  }
  const lines: string[] = [];
  let line = '';
  for (const word of words) {
    const candidate = line ? `${line} ${word}` : word;
    if (line && textWidth(candidate) > maxWidth) {
      lines.push(line);
      line = word;
    } else {
      line = candidate;
    }
  }
  if (line) {
    lines.push(line);
  }
  // Hard-break any line still made of a single over-long token (for example, EncryptionConfiguration).
  return lines.flatMap((l) => (!l.includes(' ') && textWidth(l) > maxWidth ? hardBreak(l, maxWidth) : l));
}

/** Splits a single unbreakable word into chunks that each fit the target width. */
function hardBreak(word: string, maxWidth: number): string[] {
  const parts: string[] = [];
  let chunk = '';
  for (const ch of word) {
    if (chunk && textWidth(chunk + ch) > maxWidth) {
      parts.push(chunk);
      chunk = ch;
    } else {
      chunk += ch;
    }
  }
  if (chunk) {
    parts.push(chunk);
  }
  return parts.length > 0 ? parts : [word];
}

export interface NodeSize {
  width: number;
  height: number;
}

/** Reads a width/height that may be a number or a CSS pixel string, falling back when unset. */
function dim(value: unknown, fallback: number): number {
  const n = typeof value === 'string' ? parseFloat(value) : typeof value === 'number' ? value : NaN;
  return Number.isFinite(n) ? n : fallback;
}

function clamp(value: number, lo: number, hi: number): number {
  return Math.max(lo, Math.min(hi, value));
}

/** The smallest on-canvas size that keeps a node's label inside its shape. */
export function fitNodeSize(kind: DfdKind, label: string, hasCaption = false): NodeSize {
  if (kind === 'boundary') {
    return { width: DEFAULT_NODE_SIZE.boundary.width, height: DEFAULT_NODE_SIZE.boundary.height };
  }
  const min = MIN_SIZE[kind];
  const wrapAt = WRAP_TARGET[kind];
  const lines = wrapLabel(label || ' ', wrapAt);
  const longest = Math.max(1, ...lines.map(textWidth));
  const textBoxW = Math.ceil(longest) + WIDTH_SLACK;
  const textBoxH = ICON_BLOCK + (hasCaption ? CAPTION_HEIGHT : 0) + lines.length * LINE_HEIGHT;

  let width: number;
  let height: number;
  if (kind === 'process') {
    width = Math.ceil((textBoxW + PAD_X * 2) * ELLIPSE_FIT);
    height = Math.ceil((textBoxH + PAD_Y * 2) * ELLIPSE_FIT);
  } else {
    width = textBoxW + PAD_X * 2;
    height = textBoxH + PAD_Y * 2;
  }
  return {
    width: clamp(width, min.width, MAX_WIDTH),
    height: clamp(height, min.height, MAX_HEIGHT),
  };
}

export type FitMode = 'grow' | 'exact';

/**
 * Returns nodes resized so each label fits its shape. `grow` only ever enlarges, so a hand-tuned
 * size is never clipped; `exact` sets the computed fit. The node's centre is preserved, so it stays
 * put relative to its neighbours and any trust boundary it sits in.
 */
export function resizeNodesToFit(nodes: DfdNode[], mode: FitMode = 'exact'): DfdNode[] {
  return nodes.map((n) => {
    const kind = (n.type ?? 'process') as DfdKind;
    if (kind === 'boundary') {
      return n;
    }
    const label = typeof n.data.label === 'string' ? n.data.label : '';
    const fit = fitNodeSize(kind, label, Boolean(n.data.stencilType));
    const curW = dim(n.width ?? n.style?.width, DEFAULT_NODE_SIZE[kind].width);
    const curH = dim(n.height ?? n.style?.height, DEFAULT_NODE_SIZE[kind].height);
    const width = mode === 'grow' ? Math.max(curW, fit.width) : fit.width;
    const height = mode === 'grow' ? Math.max(curH, fit.height) : fit.height;
    if (width === curW && height === curH) {
      return n;
    }
    const position = {
      x: Math.round(n.position.x + (curW - width) / 2),
      y: Math.round(n.position.y + (curH - height) / 2),
    };
    return { ...n, position, width, height, style: { ...n.style, width, height } };
  });
}

/** Distance (px) within which two flow-label anchors are treated as the same spot and separated. */
const CLUSTER_RADIUS = 46;
/** Vertical gap (px) between stacked labels that share a spot. */
const ROW_GAP = 22;

/** The geometric centre of a node, from its position and (possibly stringified) size. */
function nodeCenter(n: DfdNode): { x: number; y: number } {
  const fallback = DEFAULT_NODE_SIZE[(n.type as DfdKind) ?? 'process'] ?? DEFAULT_NODE_SIZE.process;
  const w = dim(n.width ?? n.style?.width, fallback.width);
  const h = dim(n.height ?? n.style?.height, fallback.height);
  return { x: n.position.x + w / 2, y: n.position.y + h / 2 };
}

/**
 * Returns edges whose labels are nudged apart wherever several flows share the same midpoint (for
 * example, a request/response pair between the same two nodes). A label the author has already
 * dragged aside (a non-zero offset) is left untouched, and a flow that sits alone keeps its label on
 * the line. Offsets are symmetric about the shared anchor, so the result is stable across re-runs.
 */
export function deconflictEdgeLabels(nodes: DfdNode[], edges: DfdEdge[]): DfdEdge[] {
  const centers = new Map<string, { x: number; y: number }>();
  for (const n of nodes) {
    centers.set(n.id, nodeCenter(n));
  }

  // Only auto-place labels the author has not already positioned.
  const movable = edges
    .filter((e) => {
      const off = e.data?.labelOffset;
      return !off || (off.x === 0 && off.y === 0);
    })
    .map((e) => {
      const s = centers.get(e.source);
      const t = centers.get(e.target);
      return s && t ? { id: e.id, anchor: { x: (s.x + t.x) / 2, y: (s.y + t.y) / 2 } } : null;
    })
    .filter((x): x is { id: string; anchor: { x: number; y: number } } => x !== null)
    .sort((a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));

  // Greedy proximity clustering — diagrams are small, so the O(n²) scan is fine.
  const clusters: { cx: number; cy: number; ids: string[] }[] = [];
  for (const item of movable) {
    const hit = clusters.find((c) => Math.hypot(c.cx - item.anchor.x, c.cy - item.anchor.y) < CLUSTER_RADIUS);
    if (hit) {
      hit.ids.push(item.id);
      hit.cx = (hit.cx * (hit.ids.length - 1) + item.anchor.x) / hit.ids.length;
      hit.cy = (hit.cy * (hit.ids.length - 1) + item.anchor.y) / hit.ids.length;
    } else {
      clusters.push({ cx: item.anchor.x, cy: item.anchor.y, ids: [item.id] });
    }
  }

  const offsets = new Map<string, { x: number; y: number }>();
  for (const cluster of clusters) {
    const k = cluster.ids.length;
    cluster.ids.forEach((id, i) => {
      offsets.set(id, { x: 0, y: k > 1 ? Math.round((i - (k - 1) / 2) * ROW_GAP) : 0 });
    });
  }

  return edges.map((e) => {
    const next = offsets.get(e.id);
    if (!next) {
      return e;
    }
    const cur = e.data?.labelOffset ?? { x: 0, y: 0 };
    if (cur.x === next.x && cur.y === next.y) {
      return e;
    }
    return { ...e, data: { ...e.data, labelOffset: next } };
  });
}

/** Desired minimum gap (px) kept between component nodes when they are pushed apart. */
const NODE_GAP = 24;
/** Padding (px) kept between a boundary's edge and the components it contains when it is grown. */
const BOUNDARY_PAD = 20;
/** Iteration cap for overlap relaxation — diagrams are small, so this converges well before it. */
const SEPARATE_ITERS = 100;

interface Rect {
  x: number;
  y: number;
  w: number;
  h: number;
}

/** The bounding rectangle of a node, from its position and (possibly stringified) size. */
function rectOf(n: DfdNode): Rect {
  const fallback = DEFAULT_NODE_SIZE[(n.type as DfdKind) ?? 'process'] ?? DEFAULT_NODE_SIZE.process;
  return {
    x: n.position.x,
    y: n.position.y,
    w: dim(n.width ?? n.style?.width, fallback.width),
    h: dim(n.height ?? n.style?.height, fallback.height),
  };
}

/**
 * Separates overlapping component nodes and expands each trust boundary to keep containing the
 * components inside it. A component is pushed apart from an overlapping neighbour along the axis of
 * least penetration (so the movement is minimal), but only against neighbours in the same boundary,
 * so a node never drifts out of the boundary it belongs to; the boundary is then grown — never
 * shrunk — to wrap its contents. Boundaries are not moved, so the author's overall arrangement is
 * preserved, and a node that doesn't overlap anything is left exactly where it is (a no-op on an
 * already-clean diagram).
 */
export function separateNodes(nodes: DfdNode[]): DfdNode[] {
  const boundaries = nodes.filter((n) => n.type === 'boundary');
  const components = nodes.filter((n) => n.type !== 'boundary');
  if (components.length === 0) {
    return nodes;
  }

  // Mutable working rectangles for the components, keyed by id.
  const pos = new Map<string, Rect>();
  for (const c of components) {
    pos.set(c.id, rectOf(c));
  }

  // Assign each component to the smallest boundary whose rectangle contains its centre ('' = none).
  const boundaryRects = boundaries.map((b) => ({ id: b.id, r: rectOf(b) }));
  const groupOf = new Map<string, string>();
  for (const c of components) {
    const p = pos.get(c.id)!;
    const cx = p.x + p.w / 2;
    const cy = p.y + p.h / 2;
    let best = '';
    let bestArea = Infinity;
    for (const b of boundaryRects) {
      if (cx >= b.r.x && cx <= b.r.x + b.r.w && cy >= b.r.y && cy <= b.r.y + b.r.h && b.r.w * b.r.h < bestArea) {
        bestArea = b.r.w * b.r.h;
        best = b.id;
      }
    }
    groupOf.set(c.id, best);
  }

  const groups = new Map<string, string[]>();
  for (const c of components) {
    const g = groupOf.get(c.id)!;
    const arr = groups.get(g);
    if (arr) {
      arr.push(c.id);
    } else {
      groups.set(g, [c.id]);
    }
  }
  for (const ids of groups.values()) {
    ids.sort();
  }

  // Relax overlaps within each group by pushing each overlapping pair apart the short way.
  for (let iter = 0; iter < SEPARATE_ITERS; iter++) {
    let moved = false;
    for (const ids of groups.values()) {
      for (let i = 0; i < ids.length; i++) {
        for (let j = i + 1; j < ids.length; j++) {
          const a = pos.get(ids[i])!;
          const b = pos.get(ids[j])!;
          const dx = b.x + b.w / 2 - (a.x + a.w / 2);
          const dy = b.y + b.h / 2 - (a.y + a.h / 2);
          const px = (a.w + b.w) / 2 + NODE_GAP - Math.abs(dx);
          const py = (a.h + b.h) / 2 + NODE_GAP - Math.abs(dy);
          if (px > 0 && py > 0) {
            if (px < py) {
              const shift = (px / 2) * (dx >= 0 ? 1 : -1);
              a.x -= shift;
              b.x += shift;
            } else {
              const shift = (py / 2) * (dy >= 0 ? 1 : -1);
              a.y -= shift;
              b.y += shift;
            }
            moved = true;
          }
        }
      }
    }
    if (!moved) {
      break;
    }
  }

  // Grow (never shrink) each boundary to wrap the components it owns, plus a little padding.
  const grown = new Map<string, Rect>();
  for (const b of boundaryRects) {
    let minX = Infinity;
    let minY = Infinity;
    let maxX = -Infinity;
    let maxY = -Infinity;
    let any = false;
    for (const c of components) {
      if (groupOf.get(c.id) !== b.id) {
        continue;
      }
      any = true;
      const p = pos.get(c.id)!;
      minX = Math.min(minX, p.x - BOUNDARY_PAD);
      minY = Math.min(minY, p.y - BOUNDARY_PAD);
      maxX = Math.max(maxX, p.x + p.w + BOUNDARY_PAD);
      maxY = Math.max(maxY, p.y + p.h + BOUNDARY_PAD);
    }
    if (!any) {
      continue;
    }
    const x = Math.min(b.r.x, minX);
    const y = Math.min(b.r.y, minY);
    grown.set(b.id, {
      x: Math.round(x),
      y: Math.round(y),
      w: Math.round(Math.max(b.r.x + b.r.w, maxX) - x),
      h: Math.round(Math.max(b.r.y + b.r.h, maxY) - y),
    });
  }

  return nodes.map((n) => {
    if (n.type === 'boundary') {
      const g = grown.get(n.id);
      const cur = rectOf(n);
      if (!g || (g.x === cur.x && g.y === cur.y && g.w === cur.w && g.h === cur.h)) {
        return n;
      }
      return { ...n, position: { x: g.x, y: g.y }, width: g.w, height: g.h, style: { ...n.style, width: g.w, height: g.h } };
    }
    const p = pos.get(n.id)!;
    const nx = Math.round(p.x);
    const ny = Math.round(p.y);
    if (nx === n.position.x && ny === n.position.y) {
      return n;
    }
    return { ...n, position: { x: nx, y: ny } };
  });
}

/**
 * Tidies a page: fits every shape to its label, separates overlapping shapes (growing any trust
 * boundary to keep wrapping its contents), and separates overlapping flow labels. `grow` (used when
 * a model is loaded) only enlarges shapes; `exact` (the toolbar action) sets the computed fit.
 */
export function tidyGraph(
  nodes: DfdNode[],
  edges: DfdEdge[],
  mode: FitMode = 'exact',
): { nodes: DfdNode[]; edges: DfdEdge[] } {
  const separated = separateNodes(resizeNodesToFit(nodes, mode));
  return { nodes: separated, edges: deconflictEdgeLabels(separated, edges) };
}

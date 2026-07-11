import { DEFAULT_NODE_SIZE } from './mapping';
import type { DfdEdge, DfdKind, DfdNode } from './types';

/**
 * Auto-layout helpers that make an imported (for example, CLI-authored) model readable without
 * manual clean-up: every shape is grown to fit its label so the name never overruns the boundary,
 * overlapping shapes are pushed apart, each flow is routed through the ports that face its
 * endpoints (instead of React Flow's default top port, which loops lines over their own shapes),
 * and the flow labels are sized from their text and stacked so none cover one another or a shape.
 * All functions are pure and deterministic so a repeated run is a no-op.
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

/** Edge-label font — mirrors `.edge-label` (11px / 600); flow labels are measured to size their pills. */
const EDGE_LABEL_FONT =
  "600 11px -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

let edgeMeasureCtx: CanvasRenderingContext2D | null | undefined;

/** A cached 2D context for edge-label measurement at 11px, or null where canvas is unavailable. */
function edgeContext(): CanvasRenderingContext2D | null {
  if (edgeMeasureCtx === undefined) {
    try {
      edgeMeasureCtx = document.createElement('canvas').getContext('2d') ?? null;
      if (edgeMeasureCtx) {
        edgeMeasureCtx.font = EDGE_LABEL_FONT;
      }
    } catch {
      edgeMeasureCtx = null;
    }
  }
  return edgeMeasureCtx;
}

/** Width (px) of a one-line flow label. Falls back to a per-character estimate without canvas. */
export function edgeLabelWidth(text: string): number {
  const ctx = edgeContext();
  if (ctx) {
    return ctx.measureText(text).width;
  }
  // ≈6.0px average glyph advance for 11px/600 in the system stack — deterministic for tests.
  return text.length * 6;
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

/** The four connection ports every shape node exposes, in the order ShapeNode renders them. */
type HandleSide = 't' | 'r' | 'b' | 'l';

function isSide(value: string): value is HandleSide {
  return value === 't' || value === 'r' || value === 'b' || value === 'l';
}

/** The point (flow coords) where a given side's port sits on the edge of a node's rectangle. */
function handlePoint(r: Rect, side: HandleSide): { x: number; y: number } {
  switch (side) {
    case 't':
      return { x: r.x + r.w / 2, y: r.y };
    case 'b':
      return { x: r.x + r.w / 2, y: r.y + r.h };
    case 'l':
      return { x: r.x, y: r.y + r.h / 2 };
    case 'r':
      return { x: r.x + r.w, y: r.y + r.h / 2 };
  }
}

/** The facing (source, target) port sides between two node rectangles, chosen by dominant axis. */
function facingSides(s: Rect, t: Rect): [HandleSide, HandleSide] {
  const dx = t.x + t.w / 2 - (s.x + s.w / 2);
  const dy = t.y + t.h / 2 - (s.y + s.h / 2);
  if (Math.abs(dx) >= Math.abs(dy)) {
    return dx >= 0 ? ['r', 'l'] : ['l', 'r'];
  }
  return dy >= 0 ? ['b', 't'] : ['t', 'b'];
}

/**
 * Assigns each flow the source and target ports that face one another, so a line leaves the side of
 * its source nearest the target and enters the side of its target nearest the source. Without an
 * explicit handle React Flow falls back to a shape's first port (its top), which sends every line
 * looping up and over its own shapes and scatters the midpoint labels far from the flow. Only
 * component-to-component flows are routed; a flow touching a trust boundary (which has no ports) is
 * left for React Flow to place. Handles come purely from geometry, so a repeated run is a no-op.
 */
export function routeEdges(nodes: DfdNode[], edges: DfdEdge[]): DfdEdge[] {
  const rects = new Map<string, Rect>();
  for (const n of nodes) {
    if (n.type !== 'boundary') {
      rects.set(n.id, rectOf(n));
    }
  }
  return edges.map((e) => {
    const s = rects.get(e.source);
    const t = rects.get(e.target);
    if (!s || !t) {
      return e;
    }
    const [sourceHandle, targetHandle] = facingSides(s, t);
    if (e.sourceHandle === sourceHandle && e.targetHandle === targetHandle) {
      return e;
    }
    return { ...e, sourceHandle, targetHandle };
  });
}

/** Text width (px) a long flow label wraps at, so verbose descriptions form a compact multi-line pill. */
const EDGE_WRAP_TARGET = 224;
/** Rendered height (px) of one wrapped line of an 11px/600 flow label (line-height 1.25). */
const EDGE_LINE_H = 14;
/** Horizontal padding (px) added to a label's measured text to get the width of its pill. */
const LABEL_PAD_X = 8;
/** Vertical padding (px), including the pill's halo, added above and below the wrapped lines. */
const LABEL_PAD_Y = 5;
/** Minimum vertical gap (px) kept between a separated label and whatever it was moved off. */
const LABEL_GAP = 6;
/** Iteration cap for the label-overlap relaxation — small diagrams converge well before it. */
const LABEL_ITERS = 60;
/** Height (px) reserved for the title strip at the top of a trust boundary. */
const BOUNDARY_TITLE_H = 28;
/** Horizontal padding (px) around a trust-boundary title. */
const BOUNDARY_TITLE_PAD_X = 17;

/** A movable flow label as a box centred on its anchor and shifted to clear overlaps. */
interface LabelBox {
  id: string;
  anchorX: number;
  anchorY: number;
  cx: number;
  cy: number;
  w: number;
  h: number;
}

interface LabelObstacle extends Rect {
  id: string;
}

/** The midpoint of a flow's routed path — its two ports, or the node centres when it is unrouted. */
function labelAnchor(edge: DfdEdge, source: Rect, target: Rect): { x: number; y: number } {
  const sp =
    typeof edge.sourceHandle === 'string' && isSide(edge.sourceHandle)
      ? handlePoint(source, edge.sourceHandle)
      : { x: source.x + source.w / 2, y: source.y + source.h / 2 };
  const tp =
    typeof edge.targetHandle === 'string' && isSide(edge.targetHandle)
      ? handlePoint(target, edge.targetHandle)
      : { x: target.x + target.w / 2, y: target.y + target.h / 2 };
  return { x: (sp.x + tp.x) / 2, y: (sp.y + tp.y) / 2 };
}

/** Overlap of two axis ranges (start + length each), or a value ≤ 0 when they are disjoint. */
function axisOverlap(aStart: number, aLen: number, bStart: number, bLen: number): number {
  return Math.min(aStart + aLen, bStart + bLen) - Math.max(aStart, bStart);
}

/** Greedy word-wrap of a flow label at the edge font, matching the wrapping `.edge-label` renders. */
function wrapEdgeLabel(label: string, maxWidth: number): string[] {
  const words = label.split(/\s+/).filter(Boolean);
  if (words.length === 0) {
    return [''];
  }
  const lines: string[] = [];
  let line = '';
  for (const word of words) {
    const candidate = line ? `${line} ${word}` : word;
    if (line && edgeLabelWidth(candidate) > maxWidth) {
      lines.push(line);
      line = word;
    } else {
      line = candidate;
    }
  }
  if (line) {
    lines.push(line);
  }
  return lines;
}

/** The on-canvas pill size of a flow label once wrapped, used to detect and clear label overlaps. */
function edgeLabelBox(text: string): { w: number; h: number } {
  const lines = wrapEdgeLabel(text, EDGE_WRAP_TARGET);
  const longest = Math.max(1, ...lines.map(edgeLabelWidth));
  return {
    w: Math.ceil(Math.min(longest, EDGE_WRAP_TARGET)) + LABEL_PAD_X * 2,
    h: lines.length * EDGE_LINE_H + LABEL_PAD_Y * 2,
  };
}

function labelRect(label: LabelBox, x = label.cx, y = label.cy): Rect {
  return { x: x - label.w / 2, y: y - label.h / 2, w: label.w, h: label.h };
}

function labelObstacles(nodes: DfdNode[]): LabelObstacle[] {
  return nodes.map((node) => {
    const rect = rectOf(node);
    if (node.type !== 'boundary') {
      return { id: node.id, ...rect };
    }
    const label = typeof node.data.label === 'string' ? node.data.label : '';
    return {
      id: `title:${node.id}`,
      x: rect.x,
      y: rect.y,
      w: Math.min(rect.w, Math.max(72, Math.ceil(textWidth(label)) + BOUNDARY_TITLE_PAD_X * 2)),
      h: BOUNDARY_TITLE_H,
    };
  });
}

function rectsOverlap(a: Rect, b: Rect, gap = 0): boolean {
  return axisOverlap(a.x, a.w, b.x - gap, b.w + gap * 2) > 0
    && axisOverlap(a.y, a.h, b.y - gap, b.h + gap * 2) > 0;
}

/** Candidate offsets around a preferred label centre, ordered nearest-first with vertical bias. */
function labelCandidates(label: LabelBox): { x: number; y: number }[] {
  const stepX = 32;
  const stepY = Math.max(24, Math.ceil(label.h + LABEL_GAP));
  const candidates = [{ x: 0, y: 0 }];
  for (let ring = 1; ring <= 32; ring += 1) {
    const current: { x: number; y: number }[] = [];
    for (let gx = -ring; gx <= ring; gx += 1) {
      current.push({ x: gx * stepX, y: -ring * stepY });
      current.push({ x: gx * stepX, y: ring * stepY });
    }
    for (let gy = -ring + 1; gy < ring; gy += 1) {
      current.push({ x: -ring * stepX, y: gy * stepY });
      current.push({ x: ring * stepX, y: gy * stepY });
    }
    current.sort((a, b) => {
      const ac = Math.hypot(a.x * 1.2, a.y);
      const bc = Math.hypot(b.x * 1.2, b.y);
      return ac - bc || Math.abs(a.x) - Math.abs(b.x) || a.y - b.y || a.x - b.x;
    });
    candidates.push(...current);
  }
  return candidates;
}

/**
 * Returns edges whose labels are nudged vertically so no two auto-placed labels overlap and none
 * sits on top of a component shape. Each label is sized from its measured text, so long flow
 * descriptions that would otherwise cover one another (or a neighbouring shape) are stacked apart,
 * not just the classic request/response pair that shares an exact midpoint. A label the author has
 * already dragged aside (a non-zero offset) is left where it is — and treated as an obstacle the
 * auto-placed labels avoid — and a flow whose label is already clear keeps it on the line. Labels
 * prefer vertical movement but can move horizontally out of dense object corridors. Repeated runs
 * are stable.
 */
export function deconflictEdgeLabels(nodes: DfdNode[], edges: DfdEdge[]): DfdEdge[] {
  const rects = new Map<string, Rect>();
  for (const n of nodes) {
    rects.set(n.id, rectOf(n));
  }

  // Components are fixed obstacles. A boundary's interior legitimately contains labels, but its
  // title strip does not, so reserve only the title rather than the whole region.
  const obstacles = labelObstacles(nodes);
  const movable: LabelBox[] = [];

  for (const e of edges) {
    const s = rects.get(e.source);
    const t = rects.get(e.target);
    if (!s || !t) {
      continue;
    }
    const anchor = labelAnchor(e, s, t);
    const text = typeof e.label === 'string' && e.label ? e.label : 'data flow';
    const box = edgeLabelBox(text);
    const off = e.data?.labelOffset;
    if (off && (off.x !== 0 || off.y !== 0) && !e.data?.autoLabelOffset) {
      // Author-placed: never move it, but keep it as an obstacle the auto-placed labels avoid.
      obstacles.push({
        id: `manual:${e.id}`,
        x: anchor.x + off.x - box.w / 2,
        y: anchor.y + off.y - box.h / 2,
        w: box.w,
        h: box.h,
      });
      continue;
    }
    movable.push({
      id: e.id,
      anchorX: anchor.x,
      anchorY: anchor.y,
      cx: anchor.x,
      cy: anchor.y,
      w: box.w,
      h: box.h,
    });
  }

  // Deterministic order so ties resolve the same way on every run.
  movable.sort((a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));

  // Separate labels symmetrically around their path anchors first, retaining the familiar
  // request/response treatment before object-aware placement.
  for (let iter = 0; iter < LABEL_ITERS / 2; iter += 1) {
    let moved = false;
    for (let i = 0; i < movable.length; i++) {
      for (let j = i + 1; j < movable.length; j++) {
        const a = movable[i];
        const b = movable[j];
        if (axisOverlap(a.cx - a.w / 2, a.w, b.cx - b.w / 2, b.w) <= 0) {
          continue;
        }
        const gap = (a.h + b.h) / 2 + LABEL_GAP - Math.abs(a.cy - b.cy);
        if (gap <= 0) {
          continue;
        }
        // Lower id goes up on a tie, so the split is deterministic.
        const dir = a.cy <= b.cy ? -1 : 1;
        a.cy += (dir * gap) / 2;
        b.cy -= (dir * gap) / 2;
        moved = true;
      }
    }

    if (!moved) {
      break;
    }
  }

  // The old vertical-only relaxation could oscillate between neighboring objects. Place each label
  // into the nearest 2D slot that clears objects, manual labels, and labels already placed.
  const placed: Rect[] = [];
  for (const label of movable) {
    const preferredX = label.cx;
    const preferredY = label.cy;
    let chosen = { x: preferredX, y: preferredY };
    for (const offset of labelCandidates(label)) {
      const x = preferredX + offset.x;
      const y = preferredY + offset.y;
      const candidate = labelRect(label, x, y);
      if (
        obstacles.every((obstacle) => !rectsOverlap(candidate, obstacle, LABEL_GAP))
        && placed.every((other) => !rectsOverlap(candidate, other, LABEL_GAP))
      ) {
        chosen = { x, y };
        break;
      }
    }
    label.cx = chosen.x;
    label.cy = chosen.y;
    placed.push(labelRect(label));
  }

  const offsets = new Map<string, { x: number; y: number }>();
  for (const m of movable) {
    const next = { x: Math.round(m.cx - m.anchorX), y: Math.round(m.cy - m.anchorY) };
    if (next.x !== 0 || next.y !== 0) {
      offsets.set(m.id, next);
    }
  }

  return edges.map((e) => {
    const off = e.data?.labelOffset;
    const manual = Boolean(off && (off.x !== 0 || off.y !== 0) && !e.data?.autoLabelOffset);
    if (manual) {
      return e;
    }
    const next = offsets.get(e.id) ?? { x: 0, y: 0 };
    const cur = off ?? { x: 0, y: 0 };
    if (next.x === 0 && next.y === 0) {
      if (!e.data?.autoLabelOffset && cur.x === 0 && cur.y === 0) {
        return e;
      }
      const data = { ...e.data };
      delete data.labelOffset;
      delete data.autoLabelOffset;
      return { ...e, data };
    }
    if (cur.x === next.x && cur.y === next.y) {
      return e.data?.autoLabelOffset ? e : { ...e, data: { ...e.data, autoLabelOffset: true } };
    }
    return { ...e, data: { ...e.data, labelOffset: next, autoLabelOffset: true } };
  });
}

/** Returns component/title collisions for the label boxes produced by Tidy. */
export function findEdgeLabelObjectOverlaps(
  nodes: DfdNode[],
  edges: DfdEdge[],
): { edgeId: string; obstacleId: string }[] {
  const rects = new Map(nodes.map((node) => [node.id, rectOf(node)]));
  const obstacles = labelObstacles(nodes);
  const overlaps: { edgeId: string; obstacleId: string }[] = [];
  for (const edge of edges) {
    const source = rects.get(edge.source);
    const target = rects.get(edge.target);
    if (!source || !target) {
      continue;
    }
    const anchor = labelAnchor(edge, source, target);
    const offset = edge.data?.labelOffset ?? { x: 0, y: 0 };
    const size = edgeLabelBox(typeof edge.label === 'string' && edge.label ? edge.label : 'data flow');
    const box: Rect = {
      x: anchor.x + offset.x - size.w / 2,
      y: anchor.y + offset.y - size.h / 2,
      w: size.w,
      h: size.h,
    };
    for (const obstacle of obstacles) {
      if (rectsOverlap(box, obstacle)) {
        overlaps.push({ edgeId: edge.id, obstacleId: obstacle.id });
      }
    }
  }
  return overlaps;
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
 * boundary to keep wrapping its contents), routes each flow through the ports that face its
 * endpoints, and separates the flow labels so none overlap one another or a shape. `grow` (used when
 * a model is loaded) only enlarges shapes; `exact` (the toolbar action) sets the computed fit.
 */
export function tidyGraph(
  nodes: DfdNode[],
  edges: DfdEdge[],
  mode: FitMode = 'exact',
): { nodes: DfdNode[]; edges: DfdEdge[] } {
  const separated = separateNodes(resizeNodesToFit(nodes, mode));
  const routed = routeEdges(separated, edges);
  return { nodes: separated, edges: deconflictEdgeLabels(separated, routed) };
}

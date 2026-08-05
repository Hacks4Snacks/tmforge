import { declaredBoundary, rectOf, resolveBoundary } from './autosize';
import type { DfdEdge, DfdNode } from './types';

/**
 * Builds the review outline: a flat, ordered index of a page's flows and of its objects grouped by
 * the trust boundary they sit in.
 *
 * A large model is hard to review from the canvas alone — the drawing places boundaries wherever
 * they fit, so neither the flows nor the boundaries appear in any order a reader can follow, and
 * working out which flow comes after which means hunting across the diagram. This turns the same
 * model into a list with an explicit position for every flow, so the reading order is stated rather
 * than inferred from geometry.
 *
 * Everything here is pure and deterministic so the same page always yields the same order.
 */

/** How the outline is ordered: as the model authored it, or by name with numbers compared as numbers. */
export type OutlineOrder = 'model' | 'name';

/** One object (process, data store, or external interactor) in the outline. */
export interface OutlineObject {
  id: string;
  name: string;
  /** The node type: `process`, `datastore`, or `external`. */
  kind: string;
  /** How many flows touch this object, so an unconnected object stands out. */
  flowCount: number;
}

/** The objects sitting in one trust boundary, or outside every boundary. */
export interface OutlineGroup {
  /** The boundary node id, or the empty string for objects outside every boundary. */
  id: string;
  name: string;
  objects: OutlineObject[];
}

/** One flow in the outline, with its position in the review order and the boundaries it crosses. */
export interface OutlineFlow {
  id: string;
  /** The flow's 1-based position in the current review order. */
  position: number;
  name: string;
  /** The source object's name. */
  source: string;
  /** The target object's name. */
  target: string;
  /** Names of the trust boundaries this flow crosses (one endpoint in, the other out). */
  crossings: string[];
}

export interface ModelOutline {
  groups: OutlineGroup[];
  flows: OutlineFlow[];
}

/** The label shown for a flow with no name, matching the canvas search index. */
const UNNAMED_FLOW = 'data flow';
/** The label shown for an object with no name. */
const UNNAMED_OBJECT = '(unnamed)';
/** The group heading for objects that sit outside every trust boundary. */
export const NO_BOUNDARY_GROUP = 'Outside any trust boundary';

/**
 * Compares two names the way a reader does, so `F2` sorts before `F10` instead of after it. Digit
 * runs compare as numbers and everything else compares case-insensitively as text.
 */
export function compareNatural(a: string, b: string): number {
  const left = a.toLowerCase().match(/\d+|\D+/g) ?? [];
  const right = b.toLowerCase().match(/\d+|\D+/g) ?? [];
  for (let i = 0; i < Math.min(left.length, right.length); i++) {
    const l = left[i];
    const r = right[i];
    const ln = Number.parseInt(l, 10);
    const rn = Number.parseInt(r, 10);
    if (!Number.isNaN(ln) && !Number.isNaN(rn)) {
      if (ln !== rn) {
        return ln - rn;
      }
      continue;
    }
    if (l !== r) {
      return l < r ? -1 : 1;
    }
  }
  return left.length - right.length;
}

/** The display name of a node. */
function nodeName(node: DfdNode): string {
  const label = node.data.label;
  return (typeof label === 'string' && label.trim()) || UNNAMED_OBJECT;
}

/** The display name of a flow. */
function edgeName(edge: DfdEdge): string {
  return (typeof edge.label === 'string' && edge.label.trim()) || UNNAMED_FLOW;
}

/**
 * The trust boundaries an object sits in. Containment is read from the authored `Boundary` property
 * when present, plus every boundary whose region holds the object's centre — the same two signals
 * Tidy uses to decide which boundary an object belongs to.
 */
function boundariesHolding(node: DfdNode, boundaries: DfdNode[]): Set<string> {
  const inside = new Set<string>();
  const rect = rectOf(node);
  const cx = rect.x + rect.w / 2;
  const cy = rect.y + rect.h / 2;
  for (const boundary of boundaries) {
    const r = rectOf(boundary);
    if (cx >= r.x && cx <= r.x + r.w && cy >= r.y && cy <= r.y + r.h) {
      inside.add(boundary.id);
    }
  }
  const declared = resolveBoundary(boundaries, declaredBoundary(node));
  if (declared) {
    inside.add(declared.id);
  }
  return inside;
}

/**
 * The boundary an object is listed under: the one it declares, else the smallest region holding it,
 * so an object in a nested boundary is listed under the inner one.
 */
function owningBoundary(node: DfdNode, boundaries: DfdNode[], inside: Set<string>): string {
  const declared = resolveBoundary(boundaries, declaredBoundary(node));
  if (declared) {
    return declared.id;
  }
  let owner = '';
  let smallest = Infinity;
  for (const boundary of boundaries) {
    if (!inside.has(boundary.id)) {
      continue;
    }
    const r = rectOf(boundary);
    const area = r.w * r.h;
    if (area < smallest) {
      smallest = area;
      owner = boundary.id;
    }
  }
  return owner;
}

/** Orders a list either as the model authored it (stable, no-op) or naturally by name. */
function ordered<T>(items: T[], order: OutlineOrder, name: (item: T) => string): T[] {
  return order === 'name' ? [...items].sort((a, b) => compareNatural(name(a), name(b))) : items;
}

/**
 * Indexes one page into the review outline. Boundaries with no objects are still listed, so a
 * reviewer sees every region that exists rather than only the populated ones.
 */
export function buildOutline(nodes: DfdNode[], edges: DfdEdge[], order: OutlineOrder = 'model'): ModelOutline {
  const boundaries = nodes.filter((n) => n.type === 'boundary');
  const objects = nodes.filter((n) => n.type !== 'boundary');

  const flowCounts = new Map<string, number>();
  for (const edge of edges) {
    for (const end of [edge.source, edge.target]) {
      flowCounts.set(end, (flowCounts.get(end) ?? 0) + 1);
    }
  }

  const inside = new Map<string, Set<string>>();
  const members = new Map<string, OutlineObject[]>();
  for (const node of objects) {
    const holding = boundariesHolding(node, boundaries);
    inside.set(node.id, holding);
    const owner = owningBoundary(node, boundaries, holding);
    const entry: OutlineObject = {
      id: node.id,
      name: nodeName(node),
      kind: (node.type as string) ?? 'process',
      flowCount: flowCounts.get(node.id) ?? 0,
    };
    const bucket = members.get(owner);
    if (bucket) {
      bucket.push(entry);
    } else {
      members.set(owner, [entry]);
    }
  }

  const groups: OutlineGroup[] = ordered(boundaries, order, nodeName).map((boundary) => ({
    id: boundary.id,
    name: nodeName(boundary),
    objects: ordered(members.get(boundary.id) ?? [], order, (o) => o.name),
  }));
  const loose = members.get('');
  if (loose && loose.length > 0) {
    groups.push({ id: '', name: NO_BOUNDARY_GROUP, objects: ordered(loose, order, (o) => o.name) });
  }

  const names = new Map(nodes.map((node) => [node.id, nodeName(node)]));
  const flows: OutlineFlow[] = ordered(edges, order, edgeName).map((edge, index) => {
    const from = inside.get(edge.source);
    const to = inside.get(edge.target);
    const crossings = from && to
      ? boundaries.filter((b) => from.has(b.id) !== to.has(b.id)).map(nodeName)
      : [];
    return {
      id: edge.id,
      position: index + 1,
      name: edgeName(edge),
      source: names.get(edge.source) ?? edge.source,
      target: names.get(edge.target) ?? edge.target,
      crossings,
    };
  });

  return { groups, flows };
}

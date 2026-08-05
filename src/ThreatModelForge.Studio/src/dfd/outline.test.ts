import { describe, it, expect } from 'vitest';
import { buildOutline, compareNatural, NO_BOUNDARY_GROUP } from './outline';
import type { DfdEdge, DfdKind, DfdNode } from './types';

function node(id: string, type: DfdKind, label: string, x: number, y: number, w: number, h: number, properties?: Record<string, string>): DfdNode {
  return {
    id,
    type,
    position: { x, y },
    width: w,
    height: h,
    data: properties ? { label, properties } : { label },
  };
}

function edge(id: string, source: string, target: string, label: string): DfdEdge {
  return { id, source, target, label, type: 'flow', data: {} };
}

/**
 * Two stacked boundaries with one component each, plus one component outside both — the shape of an
 * imported model where the boundaries carry the review order and the geometry does not.
 */
function sampleNodes(): DfdNode[] {
  return [
    node('tb2', 'boundary', 'TB2: Cluster', 0, 300, 400, 200),
    node('tb1', 'boundary', 'TB1: Azure control plane', 0, 0, 400, 200),
    node('p1', 'process', 'P1: API server', 40, 340, 100, 60),
    node('x1', 'external', 'X1: Resource Manager', 40, 40, 100, 60),
    node('ds1', 'datastore', 'DS1: Secret', 600, 40, 100, 60),
  ];
}

describe('compareNatural', () => {
  it('compares digit runs as numbers, so F2 sorts before F10', () => {
    expect(compareNatural('F2: send', 'F10: send')).toBeLessThan(0);
    expect(['F10', 'F2', 'F1'].sort(compareNatural)).toEqual(['F1', 'F2', 'F10']);
  });

  it('compares text case-insensitively and treats a prefix as smaller', () => {
    expect(compareNatural('alpha', 'Beta')).toBeLessThan(0);
    expect(compareNatural('flow', 'flow 2')).toBeLessThan(0);
    expect(compareNatural('same', 'same')).toBe(0);
  });
});

describe('buildOutline — flows', () => {
  it('numbers every flow in the model order by default', () => {
    const flows = buildOutline(sampleNodes(), [edge('f2', 'p1', 'ds1', 'F2: read'), edge('f1', 'x1', 'p1', 'F1: create')]).flows;

    expect(flows.map((f) => [f.position, f.name])).toEqual([
      [1, 'F2: read'],
      [2, 'F1: create'],
    ]);
  });

  it('renumbers by name when asked, so a scattered diagram reads in order', () => {
    const edges = [edge('f10', 'p1', 'ds1', 'F10: read'), edge('f2', 'x1', 'p1', 'F2: create'), edge('f1', 'x1', 'p1', 'F1: open')];

    const flows = buildOutline(sampleNodes(), edges, 'name').flows;

    expect(flows.map((f) => f.name)).toEqual(['F1: open', 'F2: create', 'F10: read']);
    expect(flows.map((f) => f.position)).toEqual([1, 2, 3]);
  });

  it('names each flow\u2019s endpoints', () => {
    const [flow] = buildOutline(sampleNodes(), [edge('f1', 'x1', 'p1', 'F1: create')]).flows;

    expect(flow.source).toBe('X1: Resource Manager');
    expect(flow.target).toBe('P1: API server');
  });

  it('reports the trust boundaries a flow crosses, and none for a flow that stays inside one', () => {
    const nodes = [...sampleNodes(), node('p2', 'process', 'P2: controller', 200, 340, 100, 60)];
    const outline = buildOutline(nodes, [edge('f1', 'x1', 'p1', 'F1: create'), edge('f2', 'p1', 'p2', 'F2: reconcile')]);

    expect(outline.flows[0].crossings).toEqual(['TB2: Cluster', 'TB1: Azure control plane']);
    expect(outline.flows[1].crossings).toEqual([]);
  });

  it('falls back to a readable name for an unnamed flow', () => {
    const [flow] = buildOutline(sampleNodes(), [{ id: 'f1', source: 'x1', target: 'p1', type: 'flow', data: {} }]).flows;

    expect(flow.name).toBe('data flow');
  });
});

describe('buildOutline — objects', () => {
  it('groups each object under the boundary it sits in, and the rest outside', () => {
    const outline = buildOutline(sampleNodes(), []);

    expect(outline.groups.map((g) => [g.name, g.objects.map((o) => o.id)])).toEqual([
      ['TB2: Cluster', ['p1']],
      ['TB1: Azure control plane', ['x1']],
      [NO_BOUNDARY_GROUP, ['ds1']],
    ]);
  });

  it('orders the boundaries and their objects by name when asked', () => {
    const nodes = [...sampleNodes(), node('a1', 'external', 'A1: operator', 200, 40, 100, 60)];

    const outline = buildOutline(nodes, [], 'name');

    expect(outline.groups.map((g) => g.name)).toEqual(['TB1: Azure control plane', 'TB2: Cluster', NO_BOUNDARY_GROUP]);
    expect(outline.groups[0].objects.map((o) => o.id)).toEqual(['a1', 'x1']);
  });

  it('honours an authored Boundary property over the geometry', () => {
    const nodes = sampleNodes();
    nodes.push(node('p9', 'process', 'P9: agent', 600, 600, 100, 60, { Boundary: 'TB1: Azure control plane' }));

    const outline = buildOutline(nodes, []);

    expect(outline.groups.find((g) => g.id === 'tb1')?.objects.map((o) => o.id)).toEqual(['x1', 'p9']);
  });

  it('lists a boundary that holds nothing, so no region is missing from the review', () => {
    const nodes = [...sampleNodes(), node('tb3', 'boundary', 'TB3: Tenant', 0, 900, 400, 200)];

    expect(buildOutline(nodes, []).groups.map((g) => g.id)).toContain('tb3');
  });

  it('counts the flows touching each object so an unconnected one stands out', () => {
    const outline = buildOutline(sampleNodes(), [edge('f1', 'x1', 'p1', 'F1: create'), edge('f2', 'p1', 'x1', 'F2: reply')]);
    const objects = outline.groups.flatMap((g) => g.objects);

    expect(objects.find((o) => o.id === 'p1')?.flowCount).toBe(2);
    expect(objects.find((o) => o.id === 'ds1')?.flowCount).toBe(0);
  });
});

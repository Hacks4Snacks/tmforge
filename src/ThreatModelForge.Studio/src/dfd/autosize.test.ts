import { describe, it, expect } from 'vitest';
import { deconflictEdgeLabels, fitNodeSize, resizeNodesToFit, separateNodes, tidyGraph, wrapLabel } from './autosize';
import { DEFAULT_NODE_SIZE } from './mapping';
import type { DfdEdge, DfdNode } from './types';

describe('wrapLabel', () => {
  it('keeps a short name on a single line', () => {
    expect(wrapLabel('kube-apiserver', 150)).toEqual(['kube-apiserver']);
  });

  it('wraps a multi-word name across lines at the target width', () => {
    const lines = wrapLabel('kube-apiserver (+ encryption provider)', 150);
    expect(lines.length).toBeGreaterThan(1);
    // Every word is preserved in order across the wrapped lines.
    expect(lines.join(' ')).toBe('kube-apiserver (+ encryption provider)');
  });

  it('hard-breaks a single over-long token so it cannot overrun', () => {
    // With the deterministic per-character fallback, a 40-char token exceeds a 120px target.
    const lines = wrapLabel('EncryptionConfigurationLocalKeyEncryptionKey', 120);
    expect(lines.length).toBeGreaterThan(1);
    expect(lines.join('')).toBe('EncryptionConfigurationLocalKeyEncryptionKey');
  });
});

describe('fitNodeSize', () => {
  it('fits a short label to the compact per-kind minimum', () => {
    expect(fitNodeSize('external', 'etcd')).toEqual({ width: 120, height: 64 });
    expect(fitNodeSize('process', 'web')).toEqual({ width: 96, height: 96 });
  });

  it('grows a box shape wide enough for a long single-line name', () => {
    const size = fitNodeSize('external', 'Cluster workload / kubectl (client)');
    expect(size.width).toBeGreaterThan(DEFAULT_NODE_SIZE.external.width);
  });

  it('grows a process wider to fit a name that wraps', () => {
    const size = fitNodeSize('process', 'kube-apiserver (+ encryption provider)');
    expect(size.width).toBeGreaterThan(DEFAULT_NODE_SIZE.process.width);
  });

  it('grows a process taller for a name that wraps to many lines', () => {
    const size = fitNodeSize('process', 'read encrypted secrets from etcd over the local control-plane socket');
    expect(size.height).toBeGreaterThan(DEFAULT_NODE_SIZE.process.height);
  });

  it('never grows a shape beyond the maximum', () => {
    const size = fitNodeSize('datastore', 'x'.repeat(400));
    expect(size.width).toBeLessThanOrEqual(340);
    expect(size.height).toBeLessThanOrEqual(240);
  });

  it('does not resize a trust boundary', () => {
    expect(fitNodeSize('boundary', 'Control-plane node')).toEqual(DEFAULT_NODE_SIZE.boundary);
  });
});

function node(id: string, type: DfdNode['type'], label: string, x = 0, y = 0, width?: number, height?: number): DfdNode {
  return {
    id,
    type,
    position: { x, y },
    data: { label },
    ...(width || height ? { width, height, style: { width, height } } : {}),
  };
}

describe('resizeNodesToFit', () => {
  it('grow mode enlarges an undersized shape but preserves its centre', () => {
    const n = node('n1', 'external', 'Cluster workload / kubectl (client)', 100, 100, 120, 80);
    const [resized] = resizeNodesToFit([n], 'grow');
    expect(resized.width!).toBeGreaterThan(120);
    // Centre (100 + 120/2 = 160, 100 + 80/2 = 140) is preserved (within a pixel of integer rounding).
    expect(Math.abs(resized.position.x + resized.width! / 2 - 160)).toBeLessThanOrEqual(1);
    expect(Math.abs(resized.position.y + resized.height! / 2 - 140)).toBeLessThanOrEqual(1);
  });

  it('grow mode never shrinks a hand-tuned size', () => {
    const n = node('n1', 'external', 'etcd', 0, 0, 300, 200);
    const [resized] = resizeNodesToFit([n], 'grow');
    expect(resized).toBe(n); // unchanged reference: already larger than the fit
  });

  it('exact mode shrinks an oversized shape to the computed fit', () => {
    const n = node('n1', 'external', 'etcd', 0, 0, 300, 200);
    const [resized] = resizeNodesToFit([n], 'exact');
    expect(resized.width!).toBe(120);
    expect(resized.height!).toBe(64);
  });

  it('leaves trust boundaries untouched', () => {
    const b = node('b1', 'boundary', 'Control-plane node', 0, 0, 260, 180);
    const [resized] = resizeNodesToFit([b], 'exact');
    expect(resized).toBe(b);
  });
});

function edge(id: string, source: string, target: string, labelOffset?: { x: number; y: number }): DfdEdge {
  return { id, source, target, label: id, data: labelOffset ? { labelOffset } : {} };
}

function overlap(a: DfdNode, b: DfdNode): boolean {
  const ar = { x: a.position.x, y: a.position.y, w: a.width ?? 0, h: a.height ?? 0 };
  const br = { x: b.position.x, y: b.position.y, w: b.width ?? 0, h: b.height ?? 0 };
  const ox = Math.min(ar.x + ar.w, br.x + br.w) - Math.max(ar.x, br.x);
  const oy = Math.min(ar.y + ar.h, br.y + br.h) - Math.max(ar.y, br.y);
  return ox > 0 && oy > 0;
}

describe('separateNodes', () => {
  it('pushes two overlapping shapes apart until they no longer overlap', () => {
    const a = node('a', 'process', 'A', 100, 100, 140, 140);
    const b = node('b', 'process', 'B', 160, 120, 140, 140);
    expect(overlap(a, b)).toBe(true);
    const out = separateNodes([a, b]);
    expect(overlap(out[0], out[1])).toBe(false);
  });

  it('leaves well-separated shapes exactly where they are', () => {
    const a = node('a', 'process', 'A', 0, 0, 140, 140);
    const b = node('b', 'process', 'B', 400, 0, 140, 140);
    const out = separateNodes([a, b]);
    expect(out[0]).toBe(a);
    expect(out[1]).toBe(b);
  });

  it('grows a boundary to keep containing shapes pushed against its edge', () => {
    const boundary = node('bnd', 'boundary', 'TB', 0, 0, 200, 120);
    const a = node('a', 'datastore', 'A', 20, 30, 160, 60);
    const b = node('b', 'datastore', 'B', 60, 40, 160, 60);
    const out = separateNodes([boundary, a, b]);
    const rb = out.find((n) => n.id === 'bnd')!;
    const ra = out.find((n) => n.id === 'a')!;
    const rc = out.find((n) => n.id === 'b')!;
    expect(overlap(ra, rc)).toBe(false);
    // The boundary grew (in at least one dimension) to keep wrapping the separated pair...
    expect(rb.width! * rb.height!).toBeGreaterThan(200 * 120);
    // ...and both stores stay fully inside it.
    for (const c of [ra, rc]) {
      expect(c.position.x).toBeGreaterThanOrEqual(rb.position.x);
      expect(c.position.y).toBeGreaterThanOrEqual(rb.position.y);
      expect(c.position.x + c.width!).toBeLessThanOrEqual(rb.position.x + rb.width!);
      expect(c.position.y + c.height!).toBeLessThanOrEqual(rb.position.y + rb.height!);
    }
  });

  it('only separates within a boundary, not across boundaries', () => {
    // Two boundaries side by side; a node in each, overlapping in x only across the divide.
    const b1 = node('b1', 'boundary', 'B1', 0, 0, 200, 200);
    const b2 = node('b2', 'boundary', 'B2', 210, 0, 200, 200);
    const a = node('a', 'process', 'A', 150, 60, 120, 120); // centre 210 -> in b2
    const c = node('c', 'process', 'C', 40, 60, 120, 120); // centre 100 -> in b1
    const out = separateNodes([b1, b2, a, c]);
    // a and c are in different boundaries, so neither is pushed by the other.
    expect(out.find((n) => n.id === 'a')!.position).toEqual(a.position);
    expect(out.find((n) => n.id === 'c')!.position).toEqual(c.position);
  });
});

describe('deconflictEdgeLabels', () => {
  const nodes: DfdNode[] = [
    node('a', 'external', 'A', 0, 0, 160, 80),
    node('b', 'process', 'B', 400, 0, 140, 132),
  ];

  it('separates a request/response pair between the same two nodes', () => {
    const edges = [edge('req', 'a', 'b'), edge('res', 'b', 'a')];
    const out = deconflictEdgeLabels(nodes, edges);
    const offsets = out.map((e) => e.data?.labelOffset ?? { x: 0, y: 0 });
    // One label is pushed up, the other down; both stay horizontally on the line.
    expect(offsets.every((o) => o.x === 0)).toBe(true);
    expect(offsets.map((o) => o.y).sort((p, q) => p - q)).toEqual([-11, 11]);
  });

  it('leaves a solitary flow label on the line', () => {
    const edges = [edge('only', 'a', 'b')];
    const out = deconflictEdgeLabels(nodes, edges);
    expect(out[0]).toBe(edges[0]); // unchanged reference: no offset needed
  });

  it('respects a label the author has already dragged aside', () => {
    const manual = edge('req', 'a', 'b', { x: 20, y: -40 });
    const out = deconflictEdgeLabels(nodes, [manual, edge('res', 'b', 'a')]);
    expect(out[0].data?.labelOffset).toEqual({ x: 20, y: -40 });
  });

  it('is stable across repeated runs', () => {
    const edges = [edge('req', 'a', 'b'), edge('res', 'b', 'a')];
    const once = deconflictEdgeLabels(nodes, edges);
    const twice = deconflictEdgeLabels(nodes, once);
    expect(twice.map((e) => e.data?.labelOffset)).toEqual(once.map((e) => e.data?.labelOffset));
  });
});

describe('tidyGraph', () => {
  it('fits shapes and separates overlapping labels in one pass', () => {
    const nodes: DfdNode[] = [
      node('a', 'external', 'Cluster workload / kubectl (client)', 0, 0, 120, 80),
      node('b', 'process', 'kube-apiserver (+ encryption provider)', 400, 0, 100, 100),
    ];
    const edges = [edge('req', 'a', 'b'), edge('res', 'b', 'a')];
    const out = tidyGraph(nodes, edges, 'exact');
    expect(out.nodes[0].width!).toBeGreaterThan(120);
    expect(out.nodes[1].height!).toBeGreaterThan(100);
    expect(out.edges.some((e) => (e.data?.labelOffset?.y ?? 0) !== 0)).toBe(true);
  });
});

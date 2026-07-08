import { describe, it, expect } from 'vitest';
import { cloneGraph } from './clipboard';
import type { DfdEdge, DfdNode } from './types';

/** A deterministic id generator so cloned ids are predictable in assertions. */
function sequentialIds(): () => string {
  let i = 0;
  return () => `id-${i++}`;
}

function node(id: string, x = 0, y = 0, properties?: Record<string, string>): DfdNode {
  return { id, type: 'process', position: { x, y }, data: { label: id, properties }, selected: true };
}

function edge(id: string, source: string, target: string, properties?: Record<string, string>): DfdEdge {
  return { id, source, target, type: 'flow', data: { properties }, selected: true };
}

describe('cloneGraph', () => {
  it('assigns fresh ids, offsets positions, and marks clones selected — leaving the source untouched', () => {
    const source = node('a', 10, 20);
    const { nodes } = cloneGraph([source], [], { x: 5, y: 7 }, sequentialIds());
    expect(nodes).toHaveLength(1);
    expect(nodes[0].id).toBe('id-0');
    expect(nodes[0].position).toEqual({ x: 15, y: 27 });
    expect(nodes[0].selected).toBe(true);
    // Source is not mutated.
    expect(source.id).toBe('a');
    expect(source.position).toEqual({ x: 10, y: 20 });
  });

  it('remaps an edge wholly inside the selection onto the new node ids', () => {
    const { nodes, edges } = cloneGraph(
      [node('a'), node('b')],
      [edge('e1', 'a', 'b')],
      { x: 0, y: 0 },
      sequentialIds(),
    );
    expect(nodes.map((n) => n.id)).toEqual(['id-0', 'id-1']);
    expect(edges).toHaveLength(1);
    expect(edges[0].id).toBe('id-2');
    expect(edges[0].source).toBe('id-0');
    expect(edges[0].target).toBe('id-1');
    expect(edges[0].selected).toBe(true);
  });

  it('drops an edge with an endpoint outside the selection', () => {
    const { edges } = cloneGraph([node('a')], [edge('e1', 'a', 'z')], { x: 0, y: 0 }, sequentialIds());
    expect(edges).toHaveLength(0);
  });

  it('deep-copies mutable payloads so editing a clone never mutates the source', () => {
    const source = node('a', 0, 0, { DataType: 'PHI' });
    const { nodes } = cloneGraph([source], [], { x: 0, y: 0 }, sequentialIds());
    nodes[0].data.properties!.DataType = 'None';
    expect(source.data.properties!.DataType).toBe('PHI');
  });

  it('clears the analysis flag class on clones (a fresh copy has not been analyzed)', () => {
    const source: DfdNode = { ...node('a'), className: 'flagged' };
    const { nodes } = cloneGraph([source], [], { x: 0, y: 0 }, sequentialIds());
    expect(nodes[0].className).toBeUndefined();
  });
});

import type { DfdEdge, DfdNode } from './types';

/** A copied selection of canvas elements, held in the editor's in-memory clipboard. */
export interface Clipboard {
  nodes: DfdNode[];
  edges: DfdEdge[];
}

/**
 * Clones a selection of nodes (and the edges wholly within it) with fresh ids, offset positions, and
 * a selected state, for paste / duplicate. Every node gets a new id; edges are remapped onto the new
 * node ids and any edge with an endpoint outside the selection is dropped (a pasted flow that dangled
 * to an un-copied node would be meaningless). Mutable payloads (`data`, `properties`) are copied so
 * editing a paste never mutates its source. The `flagged` analysis class is cleared — a fresh copy
 * has not been analyzed.
 *
 * @param nodes The nodes to clone.
 * @param edges The edges to consider (only those fully inside {@link nodes} are cloned).
 * @param offset The px offset added to each cloned node's position.
 * @param newId Produces a fresh element id (defaults to `crypto.randomUUID`; injectable for tests).
 * @returns The cloned nodes and edges, all marked selected.
 */
export function cloneGraph(
  nodes: DfdNode[],
  edges: DfdEdge[],
  offset: { x: number; y: number },
  newId: () => string = () => crypto.randomUUID(),
): Clipboard {
  const idMap = new Map<string, string>();
  const clonedNodes: DfdNode[] = nodes.map((node) => {
    const id = newId();
    idMap.set(node.id, id);
    return {
      ...node,
      id,
      position: { x: node.position.x + offset.x, y: node.position.y + offset.y },
      selected: true,
      className: undefined,
      data: {
        ...node.data,
        properties: node.data.properties ? { ...node.data.properties } : undefined,
      },
    };
  });

  const clonedEdges: DfdEdge[] = [];
  for (const edge of edges) {
    const source = idMap.get(edge.source);
    const target = idMap.get(edge.target);
    if (!source || !target) {
      continue;
    }
    clonedEdges.push({
      ...edge,
      id: newId(),
      source,
      target,
      selected: true,
      className: undefined,
      data: edge.data
        ? { ...edge.data, properties: edge.data.properties ? { ...edge.data.properties } : undefined }
        : edge.data,
    });
  }

  return { nodes: clonedNodes, edges: clonedEdges };
}

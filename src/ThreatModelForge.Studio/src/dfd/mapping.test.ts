import { describe, it, expect } from 'vitest';
import { toModel, fromModel, pagesFromModel, modelFromPages } from './mapping';
import type { DfdEdge, DfdNode } from './types';

const nodes: DfdNode[] = [
  { id: 'n1', type: 'process', position: { x: 10, y: 20 }, data: { label: 'API', properties: { AuthenticationScheme: 'OAuth' } } },
  { id: 'n2', type: 'external', position: { x: 200, y: 20 }, data: { label: 'User', properties: {} } },
];

const edges: DfdEdge[] = [
  { id: 'f1', source: 'n1', target: 'n2', label: 'request', data: { properties: { Protocol: 'HTTPS', Port: '443', Algorithm: 'AES-GCM' } } },
];

describe('mapping — property round-trip (inspector edits reach the engine)', () => {
  it('toModel carries every flow property into the canonical model', () => {
    const model = toModel(nodes, edges);
    expect(model.flows[0].properties).toEqual({ Protocol: 'HTTPS', Port: '443', Algorithm: 'AES-GCM' });
    expect(model.elements[0].properties).toMatchObject({ AuthenticationScheme: 'OAuth' });
  });

  it('fromModel restores flow and element properties onto the graph', () => {
    const { nodes: rn, edges: re } = fromModel(toModel(nodes, edges));
    expect(re[0].data?.properties).toEqual({ Protocol: 'HTTPS', Port: '443', Algorithm: 'AES-GCM' });
    expect(rn[0].data.properties).toMatchObject({ AuthenticationScheme: 'OAuth' });
  });

  it('survives a full pages round-trip', () => {
    const page = { id: 'p1', name: 'Page 1', nodes, edges };
    const model = modelFromPages([page]);
    const pages = pagesFromModel(model);
    expect(pages).toHaveLength(1);
    expect(pages[0].edges[0].data?.properties).toEqual({ Protocol: 'HTTPS', Port: '443', Algorithm: 'AES-GCM' });
  });

  it('omits the properties key entirely for an element with no custom properties', () => {
    const model = toModel(nodes, edges);
    // n2 (User) had an empty properties bag — it should not serialize a properties object.
    expect(model.elements[1].properties).toBeUndefined();
  });
});

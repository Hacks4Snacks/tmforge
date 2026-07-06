import { MarkerType } from '@xyflow/react';
import type { DfdEdge, DfdKind, DfdNode, TmForgeElement, TmForgeFlow, TmForgeModel } from './types';

function num(value: unknown, fallback: number): number {
  const n = typeof value === 'string' ? parseFloat(value) : typeof value === 'number' ? value : NaN;
  return Number.isFinite(n) ? n : fallback;
}

/**
 * Default on-canvas size (px) per node kind — used when creating a node and when a loaded model has
 * no explicit size. The shape values match each stencil's rendered footprint so existing diagrams
 * look unchanged; every kind is resizable from this starting point.
 */
export const DEFAULT_NODE_SIZE: Record<DfdKind, { width: number; height: number }> = {
  process: { width: 140, height: 132 },
  datastore: { width: 194, height: 81 },
  external: { width: 186, height: 86 },
  boundary: { width: 260, height: 180 },
};

/** React Flow graph -> canonical tmforge-json. */
export function toModel(nodes: DfdNode[], edges: DfdEdge[]): TmForgeModel {
  return {
    schema: 'tmforge-json',
    version: '0.1',
    elements: nodes.map((n) => {
      const kind = (n.type ?? 'process') as DfdKind;
      const size = DEFAULT_NODE_SIZE[kind];
      const el: TmForgeElement = {
        id: n.id,
        kind,
        name: n.data.label,
        x: Math.round(n.position.x),
        y: Math.round(n.position.y),
        width: Math.round(num(n.width ?? n.style?.width, size.width)),
        height: Math.round(num(n.height ?? n.style?.height, size.height)),
      };
      // Carry the stencil identity into the model as a custom property so the engine (and any
      // stencil-aware rule or report) sees it, and it round-trips through tmforge-json.
      const properties = { ...(n.data.properties ?? {}) };
      if (n.data.stencilType) {
        properties.StencilType = n.data.stencilType;
      }
      if (Object.keys(properties).length > 0) {
        el.properties = properties;
      }
      return el;
    }),
    flows: edges.map((e) => {
      const flow: TmForgeFlow = {
        id: e.id,
        source: e.source,
        target: e.target,
        name: typeof e.label === 'string' ? e.label : 'data flow',
      };
      const props = e.data?.properties;
      if (props && Object.keys(props).length > 0) {
        flow.properties = props;
      }
      const offset = e.data?.labelOffset;
      if (offset && (offset.x !== 0 || offset.y !== 0)) {
        flow.labelOffset = { x: Math.round(offset.x), y: Math.round(offset.y) };
      }
      return flow;
    }),
  };
}

/** Canonical tmforge-json -> React Flow graph. */
export function fromModel(model: TmForgeModel): { nodes: DfdNode[]; edges: DfdEdge[] } {
  const nodes: DfdNode[] = model.elements.map((el) => {
    // StencilType is surfaced separately as node.data.stencilType; keep it out of the editable
    // custom-property list the Inspector shows (it is re-added on export by toModel).
    const properties = { ...(el.properties ?? {}) };
    const stencilType = properties.StencilType;
    delete properties.StencilType;
    const size = DEFAULT_NODE_SIZE[el.kind];
    return {
      id: el.id,
      type: el.kind,
      position: { x: el.x, y: el.y },
      data: { label: el.name, stencilType, properties },
      style: { width: el.width ?? size.width, height: el.height ?? size.height },
      zIndex: el.kind === 'boundary' ? 0 : 1,
    };
  });

  const edges: DfdEdge[] = model.flows.map((f) => ({
    id: f.id,
    source: f.source,
    target: f.target,
    label: f.name,
    type: 'flow',
    markerEnd: { type: MarkerType.ArrowClosed },
    data: { properties: f.properties ?? {}, labelOffset: f.labelOffset },
  }));

  return { nodes, edges };
}

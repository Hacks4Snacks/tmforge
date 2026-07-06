import type { Node, Edge } from '@xyflow/react';

/** The DFD element kinds this spike supports. Maps to `node.type` in React Flow. */
export type DfdKind = 'process' | 'datastore' | 'external' | 'boundary';

/** Node payload. Intersecting with Record keeps it assignable to React Flow's data constraint. */
export type DfdNodeData = Record<string, unknown> & {
  label: string;
  /** The catalog stencil id this node was created from (for example, azure-sql), when specialized. */
  stencilType?: string;
  properties?: Record<string, string>;
};

/** Edge payload carries the engine custom properties (Protocol, DataType, ...) for a data flow. */
export type DfdEdgeData = Record<string, unknown> & {
  properties?: Record<string, string>;
  /** Label nudge (px, in flow coordinates) so labels on parallel flows can be pulled apart. */
  labelOffset?: { x: number; y: number };
};

export type DfdNode = Node<DfdNodeData, DfdKind>;
export type DfdEdge = Edge<DfdEdgeData>;

/**
 * The canonical, transport-neutral model the editor + API would speak ("tmforge-json").
 * The real .NET engine converts this to/from `.tm7` and friends at the edges.
 */
export interface TmForgeElement {
  id: string;
  kind: DfdKind;
  name: string;
  x: number;
  y: number;
  width?: number;
  height?: number;
  /** Engine custom properties (for example, DataType) applied to the element. */
  properties?: Record<string, string>;
}

export interface TmForgeFlow {
  id: string;
  source: string;
  target: string;
  name: string;
  /** Engine custom properties (for example, Protocol, DataType) applied to the flow. */
  properties?: Record<string, string>;
  /** Persisted label offset (px) so overlapping labels on parallel flows can be separated. */
  labelOffset?: { x: number; y: number };
}

/**
 * Per-model validation selection. It travels with the model (saved in the .tmforge.json file and
 * sent on validate) so the Studio and the CLI validate against the same set of rules. An absent or
 * empty selection runs every rule.
 */
export interface TmForgeValidation {
  /** Rule pack ids to skip (for example, 'stride-completeness'). */
  disabledPacks?: string[];
  /** Individual rule ids to skip (for example, 'TM1002'). */
  disabledRuleIds?: string[];
}

export interface TmForgeModel {
  schema: 'tmforge-json';
  version: '0.1';
  elements: TmForgeElement[];
  flows: TmForgeFlow[];
  /** Which rule packs or rules to skip when validating this model. */
  validation?: TmForgeValidation;
}

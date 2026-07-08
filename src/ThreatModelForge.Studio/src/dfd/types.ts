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

/**
 * A named page (diagram) within a {@link TmForgeModel}. Each page has its own elements and flows and
 * maps to one drawing surface in the .NET model. Cross-page flows are out of scope: a flow's
 * endpoints are always on the same page.
 */
export interface TmForgeDiagram {
  /** Stable page identifier. */
  id: string;
  /** Page (tab) label, for example 'Context' or 'Payments service'. */
  name: string;
  elements: TmForgeElement[];
  flows: TmForgeFlow[];
}

export interface TmForgeModel {
  schema: 'tmforge-json';
  version: '0.1';
  elements: TmForgeElement[];
  flows: TmForgeFlow[];
  /**
   * Named pages. When present, this is the authoritative multi-page form and the top-level
   * `elements`/`flows` mirror the first page for older single-page readers. Absent for single-page
   * models. Studio page-tab wiring lands in a later phase; the field is defined here so the wire
   * contract matches the engine.
   */
  diagrams?: TmForgeDiagram[];
  /** Which rule packs or rules to skip when validating this model. */
  validation?: TmForgeValidation;
  /**
   * The threat triage overlay: the persisted lifecycle state (accepted risks + justifications) of
   * generated threats, keyed by threat id. Absent or empty means every generated threat is open.
   */
  threats?: ThreatTriage[];
}

/** One generated threat's persisted triage state, carried on {@link TmForgeModel.threats}. */
export interface ThreatTriage {
  /** The threat's register id (`{targetGuid:ruleId}`) this state applies to. */
  id: string;
  /** The triage state: `Open` (default) or `Accepted`. */
  state: 'Open' | 'Accepted';
  /** The risk-acceptance justification, when accepted. */
  justification?: string;
}

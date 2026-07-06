import type { PackInfo, StencilInfo } from './engineClient';

/**
 * Offline fallback catalog: the four generic DFD primitives. Used before the engine's
 * /v1/stencils catalog resolves, and whenever the app runs without a backend.
 */
export const FALLBACK_STENCILS: StencilInfo[] = [
  { id: 'process', base: 'process', label: 'Process', category: 'Generic', pack: 'generic', blurb: 'Compute / service', tags: ['service', 'compute'], defaults: {} },
  { id: 'datastore', base: 'datastore', label: 'Data Store', category: 'Generic', pack: 'generic', blurb: 'Database / queue / file', tags: ['database', 'storage', 'queue'], defaults: {} },
  { id: 'external', base: 'external', label: 'External Entity', category: 'Generic', pack: 'generic', blurb: 'Actor / 3rd-party system', tags: ['actor', 'external'], defaults: {} },
  { id: 'boundary', base: 'boundary', label: 'Trust Boundary', category: 'Generic', pack: 'generic', blurb: 'Trust zone region', tags: ['trust', 'zone'], defaults: {} },
];

/** Offline fallback pack list: just the always-on generic pack. */
export const FALLBACK_PACKS: PackInfo[] = [{ id: 'generic', name: 'Generic', count: FALLBACK_STENCILS.length }];

import { describe, it, expect } from 'vitest';
import { normalizeKind } from './types';

describe('normalizeKind — engine kind vocabulary → Studio kind vocabulary', () => {
  it('maps the engine store label to the Studio datastore label', () => {
    // The engine classifier (ModelSnapshot.Classify) labels a data store 'store'; the canvas,
    // the tmforge-json element kind, and DfdKind all call it 'datastore'. This is the one seam.
    expect(normalizeKind('store')).toBe('datastore');
  });

  it('passes through kinds that already match, so no per-value CSS alias is needed', () => {
    for (const kind of ['process', 'external', 'boundary', 'datastore', 'flow']) {
      expect(normalizeKind(kind)).toBe(kind);
    }
  });

  it('leaves an unknown or empty kind untouched', () => {
    expect(normalizeKind('')).toBe('');
    expect(normalizeKind('mystery')).toBe('mystery');
  });
});

import { describe, it, expect } from 'vitest';
import { applyChoices } from './MergeResolveModal';
import type { MergeConflict } from './engineClient';
import type { TmForgeModel } from './types';

/**
 * Tests the one place a merge choice turns into a model edit. The engine resolves every conflict to
 * `ours` before this runs, so the job here is narrow and unforgiving: overwrite exactly the
 * properties the author flipped to `theirs`, and touch nothing else. Getting it wrong discards work
 * silently — the resolved model looks plausible either way.
 */
function model(): TmForgeModel {
  return {
    schema: 'tmforge-json',
    version: '0.1',
    elements: [
      {
        id: 'store',
        kind: 'datastore',
        name: 'Audit log',
        x: 0,
        y: 0,
        width: 100,
        height: 60,
        properties: { Encrypted: 'Yes', StoresLogData: 'Yes' },
      },
      { id: 'api', kind: 'process', name: 'API', x: 0, y: 0, width: 100, height: 60 },
    ],
    flows: [
      {
        id: 'write',
        source: 'api',
        target: 'store',
        name: 'write audit',
        properties: { Protocol: 'HTTPS' },
      },
    ],
  } as TmForgeModel;
}

function conflict(overrides: Partial<MergeConflict>): MergeConflict {
  return {
    elementId: 'store',
    elementKind: 'datastore',
    name: 'Audit log',
    diagramName: 'Page 1',
    kind: 'Property',
    property: 'Encrypted',
    ours: 'Yes',
    theirs: 'No',
    ...overrides,
  };
}

/** The key the component uses to index a choice. */
function key(c: MergeConflict): string {
  return `${c.elementId}\u0000${c.property}`;
}

describe('MergeResolveModal — applying resolution choices', () => {
  it('changes nothing when every conflict is left on ours', () => {
    const merged = model();
    const c = conflict({});

    const result = applyChoices(merged, [c], { [key(c)]: 'ours' });

    expect(result).toEqual(merged);
  });

  it('changes nothing when a conflict has no recorded choice', () => {
    const merged = model();
    const c = conflict({});

    const result = applyChoices(merged, [c], {});

    expect(result).toEqual(merged);
  });

  it('never mutates the model it was given', () => {
    // The merged model is held in state and re-resolved on every choice; mutating it would make the
    // previous answer unrecoverable as soon as the author changed their mind.
    const merged = model();
    const c = conflict({});

    applyChoices(merged, [c], { [key(c)]: 'theirs' });

    expect(merged.elements[0].properties!.Encrypted).toBe('Yes');
  });

  it('takes their value for the one property that was flipped', () => {
    const c = conflict({});

    const result = applyChoices(model(), [c], { [key(c)]: 'theirs' });

    expect(result.elements[0].properties!.Encrypted).toBe('No');
  });

  it('leaves the element\'s other properties alone', () => {
    const c = conflict({});

    const result = applyChoices(model(), [c], { [key(c)]: 'theirs' });

    expect(result.elements[0].properties!.StoresLogData).toBe('Yes');
    expect(result.elements[0].name).toBe('Audit log');
  });

  it('renames an element when the conflict is on its name', () => {
    const c = conflict({ property: 'name', ours: 'Audit log', theirs: 'Audit store' });

    const result = applyChoices(model(), [c], { [key(c)]: 'theirs' });

    expect(result.elements[0].name).toBe('Audit store');
    // 'name' must not be written into the property bag as well.
    expect(result.elements[0].properties!.name).toBeUndefined();
  });

  it('renames a flow when the conflict is on its name', () => {
    const c = conflict({ elementId: 'write', elementKind: 'flow', property: 'name', theirs: 'append audit' });

    const result = applyChoices(model(), [c], { [key(c)]: 'theirs' });

    expect(result.flows[0].name).toBe('append audit');
    expect(result.flows[0].properties!.name).toBeUndefined();
  });

  it('rewires a flow endpoint when the conflict is on source or target', () => {
    // These are the highest-consequence properties in the file: writing them into the property bag
    // instead of the endpoint would leave the diagram looking merged while the graph was not.
    const source = conflict({ elementId: 'write', elementKind: 'flow', property: 'source', theirs: 'store' });
    const target = conflict({ elementId: 'write', elementKind: 'flow', property: 'target', theirs: 'api' });

    const result = applyChoices(model(), [source, target], {
      [key(source)]: 'theirs',
      [key(target)]: 'theirs',
    });

    expect(result.flows[0].source).toBe('store');
    expect(result.flows[0].target).toBe('api');
    expect(result.flows[0].properties!.source).toBeUndefined();
    expect(result.flows[0].properties!.target).toBeUndefined();
  });

  it('sets a flow property without disturbing its endpoints', () => {
    const c = conflict({ elementId: 'write', elementKind: 'flow', property: 'Protocol', theirs: 'HTTP' });

    const result = applyChoices(model(), [c], { [key(c)]: 'theirs' });

    expect(result.flows[0].properties!.Protocol).toBe('HTTP');
    expect(result.flows[0].source).toBe('api');
    expect(result.flows[0].target).toBe('store');
  });

  it('applies only the conflicts that were flipped, out of several', () => {
    const flipped = conflict({ property: 'Encrypted', theirs: 'No' });
    const kept = conflict({ property: 'StoresLogData', ours: 'Yes', theirs: 'No' });

    const result = applyChoices(model(), [flipped, kept], {
      [key(flipped)]: 'theirs',
      [key(kept)]: 'ours',
    });

    expect(result.elements[0].properties!.Encrypted).toBe('No');
    expect(result.elements[0].properties!.StoresLogData).toBe('Yes');
  });

  it('ignores structural conflicts even when they are marked theirs', () => {
    // A DeleteModify or AddAdd cannot be resolved by writing a property. Acting on one would edit an
    // attribute nobody chose — the picker is not even shown for these.
    for (const kind of ['DeleteModify', 'AddAdd', 'DanglingReference']) {
      const c = conflict({ kind, property: 'Encrypted', theirs: 'No' });

      const result = applyChoices(model(), [c], { [key(c)]: 'theirs' });

      expect(result.elements[0].properties!.Encrypted).toBe('Yes');
    }
  });

  it('clears the value when their side had none', () => {
    const c = conflict({ property: 'Encrypted', ours: 'Yes', theirs: undefined });

    const result = applyChoices(model(), [c], { [key(c)]: 'theirs' });

    expect(result.elements[0].properties!.Encrypted).toBe('');
  });

  it('ignores a conflict naming an object that is not in the merged model', () => {
    const c = conflict({ elementId: 'deleted-by-merge', theirs: 'No' });

    const result = applyChoices(model(), [c], { [key(c)]: 'theirs' });

    expect(result).toEqual(model());
  });

  it('adds a property the merged element did not carry', () => {
    const c = conflict({ elementId: 'api', property: 'AuthenticationScheme', ours: undefined, theirs: 'OAuth' });

    const result = applyChoices(model(), [c], { [key(c)]: 'theirs' });

    expect(result.elements[1].properties!.AuthenticationScheme).toBe('OAuth');
  });
});

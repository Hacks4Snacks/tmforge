import { describe, it, expect } from 'vitest';
import { offlineEngine } from './engineClient';
import type { TmForgeModel } from './types';

function emptyModel(): TmForgeModel {
  return { schema: 'tmforge-json', version: '0.1', elements: [], flows: [] };
}

describe('OfflineEngineClient — the honest fallback contract', () => {
  it('is labeled as the offline engine', () => {
    expect(offlineEngine.label).toBe('offline (engine unavailable)');
  });

  it('offers only the client-side tmforge-json format', async () => {
    const formats = await offlineEngine.getFormats();

    expect(formats).toHaveLength(1);
    expect(formats[0].id).toBe('tmforge-json');
    expect(formats[0].canRead).toBe(true);
    expect(formats[0].canWrite).toBe(true);
  });

  it('offers the generic fallback stencils and packs', async () => {
    const stencils = await offlineEngine.getStencils();
    const packs = await offlineEngine.getStencilPacks();

    expect(stencils.length).toBeGreaterThan(0);
    expect(stencils.every((s) => typeof s.id === 'string' && s.id.length > 0)).toBe(true);
    expect(packs.length).toBeGreaterThan(0);
  });

  it('has no rule catalog or property schema without the engine', async () => {
    expect(await offlineEngine.getRules()).toEqual([]);
    expect(await offlineEngine.getRulePacks()).toEqual([]);
    expect(await offlineEngine.getPropertySchema()).toEqual([]);
  });

  it('rejects analysis and threat generation with an honest error rather than faking results', async () => {
    await expect(offlineEngine.analyze(emptyModel())).rejects.toThrow(/analysis engine has not loaded/i);
    await expect(offlineEngine.generateThreats(emptyModel())).rejects.toThrow(/analysis engine has not loaded/i);
  });

  it('rejects engine-only operations (report, merge, .tm7 export) with a clear hint', async () => {
    await expect(offlineEngine.report(emptyModel(), 'html')).rejects.toThrow(/require[s]? the .NET engine/i);
    await expect(offlineEngine.merge(null, emptyModel(), emptyModel())).rejects.toThrow(/require[s]? the .NET engine/i);
    await expect(offlineEngine.exportTm7(emptyModel())).rejects.toThrow(/require[s]? the .NET engine/i);
  });

  it('converts to tmforge-json client-side but rejects engine-only target formats', async () => {
    const blob = await offlineEngine.convert(emptyModel(), 'tmforge-json');
    expect(blob).toBeInstanceOf(Blob);
    expect(blob.type).toBe('application/json');

    await expect(offlineEngine.convert(emptyModel(), 'drawio')).rejects.toThrow(/require[s]? the .NET engine/i);
  });

  it('detects tmforge-json bytes and returns null for anything else', async () => {
    const json = new TextEncoder().encode('{"schema":"tmforge-json","elements":[]}');
    const detected = await offlineEngine.detect(json);
    expect(detected?.id).toBe('tmforge-json');

    const other = await offlineEngine.detect(new TextEncoder().encode('not a model'));
    expect(other).toBeNull();
  });

  it('round-trips a model through write and read (and readFile from bytes)', async () => {
    const json = await offlineEngine.write(emptyModel());
    expect(typeof json).toBe('string');

    const parsed = await offlineEngine.read(json);
    expect(parsed.schema).toBe('tmforge-json');
    expect(parsed.elements).toEqual([]);

    const fromBytes = await offlineEngine.readFile(new TextEncoder().encode(json));
    expect(fromBytes.schema).toBe('tmforge-json');
  });
});

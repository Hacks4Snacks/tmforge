import { describe, it, expect, vi } from 'vitest';
import { analyzeModel } from './Editor';
import type { AnalysisResult, Finding, IEngineClient, Threat } from './engineClient';
import type { TmForgeModel } from './types';

const model: TmForgeModel = { schema: 'tmforge-json', version: '0.1', elements: [], flows: [] };

const spoofing: Threat = {
  id: 'threat-1',
  ruleId: 'TM1013',
  category: 'Spoofing',
  title: 'Gateway can be spoofed',
  severity: 'error',
  references: ['CWE-287'],
  elementIds: ['p1'],
  interaction: '',
  state: 'Open',
  manual: false,
};

const threatBearingFinding: Finding = {
  id: 'TM1013:0',
  severity: 'error',
  ruleId: 'TM1013',
  message: 'Gateway can be spoofed',
  elementIds: ['p1'],
};

const hygieneFinding: Finding = {
  id: 'TM1002:1',
  severity: 'warning',
  ruleId: 'TM1002',
  message: 'Element has no name',
  elementIds: ['s1'],
};

/**
 * A recording engine client. Every analysis-capable method counts its calls, so a test can prove how
 * many times one user action reaches the engine.
 */
function recordingEngine(result: AnalysisResult) {
  const calls = { runAnalysis: 0, analyze: 0, generateThreats: 0 };
  const engine = {
    label: 'test',
    runAnalysis: vi.fn(async () => {
      calls.runAnalysis += 1;
      return result;
    }),
    analyze: vi.fn(async () => {
      calls.analyze += 1;
      return result.findings;
    }),
    generateThreats: vi.fn(async () => {
      calls.generateThreats += 1;
      return result.threats;
    }),
  } as unknown as IEngineClient;
  return { engine, calls };
}

describe('analyzeModel', () => {
  it('reaches the engine exactly once per analysis action', async () => {
    const { engine, calls } = recordingEngine({
      findings: [threatBearingFinding, hygieneFinding],
      threats: [spoofing],
      rulePacks: [],
      diagnostics: [],
    });

    await analyzeModel(engine, model);

    // One evaluation per action is the whole point: the old shape asked for findings and threats
    // separately, which made the engine run every enabled rule twice for one click.
    expect(calls.runAnalysis).toBe(1);
    expect(calls.analyze).toBe(0);
    expect(calls.generateThreats).toBe(0);
  });

  it('hides threat-bearing findings from "Other findings" but still flags their elements', async () => {
    const { engine } = recordingEngine({
      findings: [threatBearingFinding, hygieneFinding],
      threats: [spoofing],
      rulePacks: [],
      diagnostics: [],
    });

    const analysis = await analyzeModel(engine, model);

    expect(analysis.threats).toEqual([spoofing]);
    expect(analysis.otherFindings.map((f) => f.ruleId)).toEqual(['TM1002']);
    expect([...analysis.flaggedIds].sort()).toEqual(['p1', 's1']);
  });

  it('keeps a finding with no rule id in "Other findings"', async () => {
    const engineError: Finding = { id: 'engine-error', severity: 'warning', message: 'boom', elementIds: [] };
    const { engine } = recordingEngine({
      findings: [engineError],
      threats: [],
      rulePacks: [],
      diagnostics: [],
    });

    const analysis = await analyzeModel(engine, model);

    expect(analysis.otherFindings).toEqual([engineError]);
  });

  it('carries the rule evidence through to the panel', async () => {
    const { engine } = recordingEngine({
      findings: [],
      threats: [],
      rulePacks: [
        { id: 'corporate', name: 'Corporate baseline', fingerprint: 'sha256:abc', dialect: 'flat', ruleCount: 3 },
      ],
      diagnostics: ['Skipped rule file: broken.tmrules.json'],
    });

    const analysis = await analyzeModel(engine, model);

    expect(analysis.ruleBundle.rulePacks[0].id).toBe('corporate');
    expect(analysis.ruleBundle.diagnostics).toHaveLength(1);
  });

  it('propagates an engine failure so the caller can report it once', async () => {
    const engine = {
      label: 'test',
      runAnalysis: vi.fn(async () => {
        throw new Error('engine offline');
      }),
    } as unknown as IEngineClient;

    await expect(analyzeModel(engine, model)).rejects.toThrow('engine offline');
  });
});

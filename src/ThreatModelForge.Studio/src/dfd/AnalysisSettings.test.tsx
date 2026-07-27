import '@testing-library/jest-dom/vitest';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { AnalysisSettings } from './AnalysisSettings';
import type { RuleBundle, RuleInfo, RulePackInfo } from './engineClient';
import type { TmForgeExpectedRulePack } from './types';

/**
 * Two packs so the tests can prove a pack's state affects only its own rules, and a rule with help
 * text so the expandable help panel is reachable.
 */
const PACKS: RulePackInfo[] = [
  { id: 'security-properties', name: 'Security properties', count: 2 },
  { id: 'identity-access', name: 'Identity & access', count: 1 },
];

const RULES: RuleInfo[] = [
  {
    id: 'TM1008',
    pack: 'security-properties',
    severity: 'error',
    description: 'A flow crosses a trust boundary in cleartext. Anyone on the path can read it.',
    helpText: 'Set Protocol to HTTPS.',
  },
  {
    id: 'TM1013',
    pack: 'security-properties',
    severity: 'warning',
    description: 'A data store holds credentials without stating an algorithm.',
    helpText: '',
  },
  {
    id: 'TM1021',
    pack: 'identity-access',
    severity: 'error',
    description: 'A process accepts requests without authenticating them.',
    helpText: 'Set AuthenticationScheme.',
  },
];

const EMPTY_BUNDLE: RuleBundle = { rulePacks: [], diagnostics: [] };

function handlers() {
  return {
    onTogglePack: vi.fn(),
    onToggleRule: vi.fn(),
    onLoadRuleFile: vi.fn(),
    onClearRuleFile: vi.fn(),
  };
}

function renderSettings(overrides: {
  disabledPacks?: string[];
  disabledRuleIds?: string[];
  ruleBundle?: RuleBundle;
  expectedPacks?: TmForgeExpectedRulePack[];
  packs?: RulePackInfo[];
} = {}) {
  const h = handlers();
  render(
    <AnalysisSettings
      rules={RULES}
      packs={overrides.packs ?? PACKS}
      disabledPacks={overrides.disabledPacks ?? []}
      disabledRuleIds={overrides.disabledRuleIds ?? []}
      ruleBundle={overrides.ruleBundle ?? EMPTY_BUNDLE}
      expectedPacks={overrides.expectedPacks ?? []}
      {...h}
    />,
  );
  return h;
}

/** The row for a rule, as an element the query helpers can scope to. */
function ruleRow(ruleId: string): HTMLElement {
  return screen.getByText(ruleId).closest('.val-rule') as HTMLElement;
}

/** The checkbox that reflects whether a rule will run. */
function ruleCheckbox(ruleId: string): HTMLInputElement {
  return within(ruleRow(ruleId)).getByRole('checkbox') as HTMLInputElement;
}

describe('AnalysisSettings — which rules will run', () => {
  it('shows every rule as enabled when nothing is disabled', () => {
    renderSettings();

    for (const id of ['TM1008', 'TM1013', 'TM1021']) {
      expect(ruleCheckbox(id).checked).toBe(true);
    }
  });

  it('reports an individually disabled rule as off, and leaves its siblings alone', () => {
    renderSettings({ disabledRuleIds: ['TM1013'] });

    expect(ruleCheckbox('TM1013').checked).toBe(false);
    expect(ruleCheckbox('TM1008').checked).toBe(true);
    expect(ruleCheckbox('TM1021').checked).toBe(true);
  });

  it('shows every rule in a disabled pack as off, including ones not individually disabled', () => {
    // The consequence of getting this wrong is silent: an author believes these rules ran.
    renderSettings({ disabledPacks: ['security-properties'] });

    expect(ruleCheckbox('TM1008').checked).toBe(false);
    expect(ruleCheckbox('TM1013').checked).toBe(false);
    // A rule in a different pack is unaffected.
    expect(ruleCheckbox('TM1021').checked).toBe(true);
  });

  it('toggles a pack when its chip is clicked', () => {
    const h = renderSettings();

    fireEvent.click(screen.getByTitle('Disable the Security properties rule pack'));
    expect(h.onTogglePack).toHaveBeenCalledWith('security-properties');
  });

  it('offers to re-enable a pack that is off', () => {
    const h = renderSettings({ disabledPacks: ['identity-access'] });

    fireEvent.click(screen.getByTitle('Enable the Identity & access rule pack'));
    expect(h.onTogglePack).toHaveBeenCalledWith('identity-access');
  });

  it('toggles a rule when its row is clicked', () => {
    const h = renderSettings();

    fireEvent.click(ruleRow('TM1008'));
    expect(h.onToggleRule).toHaveBeenCalledWith('TM1008');
  });

  it('ignores clicks on a rule whose pack is off, so the row cannot be toggled invisibly', () => {
    const h = renderSettings({ disabledPacks: ['security-properties'] });

    fireEvent.click(ruleRow('TM1008'));

    expect(h.onToggleRule).not.toHaveBeenCalled();
    // And it is out of the tab order, so it cannot be reached by keyboard either.
    expect(ruleRow('TM1008')).toHaveAttribute('tabindex', '-1');
  });

  it('toggles a rule from the keyboard', () => {
    const h = renderSettings();
    const row = ruleRow('TM1013');

    fireEvent.keyDown(row, { key: 'Enter' });
    fireEvent.keyDown(row, { key: ' ' });

    expect(h.onToggleRule).toHaveBeenCalledTimes(2);
    expect(h.onToggleRule).toHaveBeenCalledWith('TM1013');
  });

  it('ignores other keys on a rule row', () => {
    const h = renderSettings();

    fireEvent.keyDown(ruleRow('TM1013'), { key: 'a' });

    expect(h.onToggleRule).not.toHaveBeenCalled();
  });
});

describe('AnalysisSettings — rule help', () => {
  it('opens the help panel without also toggling the rule', () => {
    // The help button sits inside the clickable row, so this is the interaction most likely to
    // silently start disabling a rule every time someone asks what it checks.
    const h = renderSettings();

    fireEvent.click(within(ruleRow('TM1008')).getByTitle('What does this rule check?'));

    expect(h.onToggleRule).not.toHaveBeenCalled();
    expect(screen.getByText('What it checks')).toBeInTheDocument();
  });

  it('shows the full description and the fix, not the truncated summary', () => {
    renderSettings();

    fireEvent.click(within(ruleRow('TM1008')).getByRole('button', { expanded: false }));

    expect(screen.getByText(RULES[0].description)).toBeInTheDocument();
    expect(screen.getByText('How to fix')).toBeInTheDocument();
    expect(screen.getByText('Set Protocol to HTTPS.')).toBeInTheDocument();
  });

  it('omits the fix section for a rule that has no help text', () => {
    renderSettings();

    fireEvent.click(within(ruleRow('TM1013')).getByRole('button', { expanded: false }));

    expect(screen.getByText('What it checks')).toBeInTheDocument();
    expect(screen.queryByText('How to fix')).not.toBeInTheDocument();
  });

  it('keeps only one help panel open at a time', () => {
    renderSettings();
    const open = (ruleId: string) =>
      fireEvent.click(within(ruleRow(ruleId)).getByRole('button', { expanded: false }));

    open('TM1008');
    open('TM1021');

    expect(screen.getAllByText('What it checks')).toHaveLength(1);
  });

  it('closes the panel when the same rule is asked again', () => {
    renderSettings();

    fireEvent.click(within(ruleRow('TM1008')).getByRole('button', { expanded: false }));
    fireEvent.click(within(ruleRow('TM1008')).getByRole('button', { expanded: true }));

    expect(screen.queryByText('What it checks')).not.toBeInTheDocument();
  });
});

describe('AnalysisSettings — custom rule packs', () => {
  it('identifies each loaded pack by id, version, rule count and fingerprint', () => {
    renderSettings({
      ruleBundle: {
        rulePacks: [
          {
            id: 'corporate',
            name: 'Corporate policy',
            version: '1.2.0',
            fingerprint: 'abcdef0123456789',
            dialect: 'urn:tmforge:rules:flat-v1',
            ruleCount: 4,
          },
        ],
        diagnostics: [],
      },
    });

    expect(screen.getByText('Corporate policy')).toBeInTheDocument();
    expect(screen.getByText(/corporate · 1\.2\.0 · 4 rule\(s\)/)).toBeInTheDocument();
  });

  it('warns that findings are incomplete when a pack the model expects is not loaded', () => {
    // This is the whole point of pinning a pack: analyzing without it under-reports, and saying
    // nothing would let a review pass while the rules it was signed off against never ran.
    renderSettings({ expectedPacks: [{ id: 'corporate' }] });

    expect(screen.getByText(/expects rule pack .*corporate.*not loaded/)).toBeInTheDocument();
    expect(screen.getByText(/Findings will be incomplete/)).toBeInTheDocument();
  });

  it('does not warn when the expected pack is loaded', () => {
    renderSettings({
      expectedPacks: [{ id: 'corporate' }],
      ruleBundle: {
        rulePacks: [
          {
            id: 'corporate',
            name: 'Corporate policy',
            fingerprint: 'abcdef0123456789',
            dialect: 'urn:tmforge:rules:flat-v1',
            ruleCount: 4,
          },
        ],
        diagnostics: [],
      },
    });

    expect(screen.queryByText(/Findings will be incomplete/)).not.toBeInTheDocument();
  });

  it('surfaces loader diagnostics rather than failing quietly', () => {
    renderSettings({
      ruleBundle: { rulePacks: [], diagnostics: ['corporate.tmrules.json: rule CORP-2 has no assert.'] },
    });

    expect(screen.getByText(/rule CORP-2 has no assert/)).toBeInTheDocument();
  });

  it('offers a way back to the built-in rules only when something is loaded or expected', () => {
    renderSettings();
    expect(screen.queryByText('Use built-in rules')).not.toBeInTheDocument();
  });

  it('offers a way back to the built-in rules when a pack is expected but missing', () => {
    const h = renderSettings({ expectedPacks: [{ id: 'corporate' }] });

    fireEvent.click(screen.getByText('Use built-in rules'));
    expect(h.onClearRuleFile).toHaveBeenCalled();
  });

  it('hands a picked file to the loader', () => {
    // The component also resets the input's value so re-picking the same file fires change again.
    // That cannot be asserted here: jsdom never populates value for a file input, so a check on it
    // passes whether or not the reset happens. Asserting it anyway would look like a guarantee and
    // provide none — a mutation removing the reset goes undetected either way.
    const h = renderSettings();
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File(['{}'], 'corporate.tmrules.json', { type: 'application/json' });

    fireEvent.change(input, { target: { files: [file] } });

    expect(h.onLoadRuleFile).toHaveBeenCalledWith(file);
  });

  it('does nothing when the file picker is dismissed', () => {
    const h = renderSettings();

    fireEvent.change(document.querySelector('input[type="file"]') as HTMLInputElement, {
      target: { files: [] },
    });

    expect(h.onLoadRuleFile).not.toHaveBeenCalled();
  });
});

describe('AnalysisSettings — offline', () => {
  it('explains that the engine is needed instead of showing an empty picker', () => {
    renderSettings({ packs: [] });

    expect(screen.getByText(/Connect the engine/)).toBeInTheDocument();
  });
});

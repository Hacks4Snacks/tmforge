import '@testing-library/jest-dom/vitest';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ThreatsPanel, type ThreatsPanelProps } from './ThreatsPanel';
import type { Finding, Threat } from './engineClient';

function threat(overrides: Partial<Threat> = {}): Threat {
  return {
    id: 'element:TM1000',
    ruleId: 'TM1000',
    category: 'Spoofing',
    title: 'A representative threat.',
    mitigation: 'Do the mitigating thing.',
    severity: 'warning',
    priority: 'Medium',
    references: [],
    elementIds: ['e1'],
    interaction: 'Client',
    state: 'Open',
    ...overrides,
  };
}

const THREATS: Threat[] = [
  threat({
    id: 'client:TM1023',
    ruleId: 'TM1023',
    category: 'Spoofing',
    title: 'External entity does not authenticate itself.',
    references: ['CWE-287', 'CAPEC-151'],
    elementIds: ['client'],
    interaction: 'Client',
  }),
  threat({
    id: 'edge1:TM1013',
    ruleId: 'TM1013',
    category: 'InformationDisclosure',
    title: 'Edge lacks a data classification tag.',
    references: ['CWE-200'],
    elementIds: ['edge1'],
    interaction: 'Client -> Gateway [HTTPS]',
  }),
  threat({
    id: 'edge2:TM1013',
    ruleId: 'TM1013',
    category: 'InformationDisclosure',
    title: 'Another edge lacks a data classification tag.',
    references: [],
    elementIds: ['edge2'],
    interaction: 'Gateway -> Auth [HTTPS]',
  }),
];

const SCOPE_OPTIONS = [
  { id: 'client', label: 'Client · external' },
  { id: 'edge1', label: 'Client -> Gateway · flow' },
];

/** Renders the panel with sensible defaults; pass overrides for the props a test cares about. */
function renderPanel(overrides: Partial<ThreatsPanelProps> = {}): ThreatsPanelProps {
  const props: ThreatsPanelProps = {
    threats: THREATS,
    findings: [],
    onSelect: vi.fn(),
    offPageLabel: () => undefined,
    onEditThreat: vi.fn(),
    onAddThreat: vi.fn(),
    onDeleteThreat: vi.fn(),
    scopeOptions: SCOPE_OPTIONS,
    ...overrides,
  };
  render(<ThreatsPanel {...props} />);
  return props;
}

describe('ThreatsPanel', () => {
  it('groups threats by STRIDE category, in canonical order, with per-group counts', () => {
    renderPanel();

    expect(screen.getByText('3 open threats')).toBeInTheDocument();

    const heads = [...document.querySelectorAll('.threat-group-head')].map((h) => h.textContent);
    // Spoofing (1) precedes Information disclosure (2) — the canonical STRIDE order, not input order.
    expect(heads).toEqual(['Spoofing1', 'Information disclosure2']);
  });

  it('links CWE / CAPEC references to their MITRE catalog pages', () => {
    renderPanel();

    expect(screen.getByRole('link', { name: 'CWE-287' })).toHaveAttribute(
      'href',
      'https://cwe.mitre.org/data/definitions/287.html',
    );
    expect(screen.getByRole('link', { name: 'CAPEC-151' })).toHaveAttribute(
      'href',
      'https://capec.mitre.org/data/definitions/151.html',
    );
  });

  it('renders the rule id, interaction scope, and singular "1 open threat" heading', () => {
    renderPanel({ threats: [THREATS[0]] });

    expect(screen.getByText('1 open threat')).toBeInTheDocument();
    expect(screen.getByText('TM1023')).toBeInTheDocument();
    expect(screen.getByText('Client')).toBeInTheDocument();
  });

  it('shows an off-page badge when a threat lives on another page', () => {
    renderPanel({ threats: [THREATS[1]], offPageLabel: () => 'Page 2' });

    expect(screen.getByText('Page 2')).toBeInTheDocument();
  });

  it("labels a rule threat's editable priority explicitly, distinct from its rule severity", () => {
    renderPanel({ threats: [threat({ priority: 'High' })] });

    expect(screen.getByText('Priority: High')).toBeInTheDocument();
  });

  it("calls onSelect with the clicked threat's element ids", () => {
    const { onSelect } = renderPanel();

    fireEvent.click(screen.getByText('External entity does not authenticate itself.'));

    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect).toHaveBeenCalledWith(THREATS[0].elementIds);
  });

  it('does not select the threat when a reference link is clicked (stops propagation)', () => {
    const { onSelect } = renderPanel();

    fireEvent.click(screen.getByRole('link', { name: 'CWE-287' }));

    expect(onSelect).not.toHaveBeenCalled();
  });

  it('edits a threat: accepting it via the state control emits the new state and justification', () => {
    const { onEditThreat } = renderPanel({ threats: [THREATS[0]] });

    fireEvent.click(screen.getByRole('button', { name: 'Edit' }));
    fireEvent.change(screen.getByLabelText('State'), { target: { value: 'Accepted' } });
    fireEvent.change(screen.getByLabelText('Justification'), { target: { value: 'Compensating control X is in place.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onEditThreat).toHaveBeenCalledWith(
      THREATS[0],
      expect.objectContaining({ state: 'Accepted', justification: 'Compensating control X is in place.' }),
    );
  });

  it('edits a threat: changing priority and description emits just those changed fields', () => {
    const { onEditThreat } = renderPanel({ threats: [THREATS[0]] });

    fireEvent.click(screen.getByRole('button', { name: 'Edit' }));
    fireEvent.change(screen.getByLabelText('Priority'), { target: { value: 'High' } });
    fireEvent.change(screen.getByLabelText('Description'), { target: { value: 'Exploitable from the internet.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onEditThreat).toHaveBeenCalledWith(THREATS[0], {
      state: 'Open',
      priority: 'High',
      description: 'Exploitable from the internet.',
    });
  });

  it('shows a non-open state badge and the justification for an accepted threat', () => {
    const accepted = threat({
      id: 'client:TM1023',
      ruleId: 'TM1023',
      title: 'External entity does not authenticate itself.',
      state: 'Accepted',
      justification: 'Accepted for the pilot.',
      elementIds: ['client'],
    });
    renderPanel({ threats: [accepted] });

    expect(screen.getByText(/0 open threats/)).toBeInTheDocument();
    expect(screen.getByText(/1 triaged/)).toBeInTheDocument();
    expect(screen.getByText('Accepted')).toBeInTheDocument();
    expect(screen.getByText('“Accepted for the pilot.”')).toBeInTheDocument();
  });

  it('authors a manual threat from the add form with a title, category, and scope', () => {
    const { onAddThreat } = renderPanel({ threats: [] });

    fireEvent.click(screen.getByRole('button', { name: '+ Add threat' }));
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Replay of a captured token' } });
    fireEvent.change(screen.getByLabelText('Category'), { target: { value: 'Spoofing' } });
    fireEvent.change(screen.getByLabelText('Scope'), { target: { value: 'client' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add threat' }));

    expect(onAddThreat).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Replay of a captured token', category: 'Spoofing', scopeId: 'client' }),
    );
  });

  it('marks manual threats and offers delete, which rule threats do not', () => {
    const manual = threat({ id: 'manual:x', ruleId: '', title: 'Hand-authored threat.', manual: true });
    const { onDeleteThreat } = renderPanel({ threats: [manual] });

    expect(screen.getByText('Manual')).toBeInTheDocument();
    // A manual threat has no rule severity, so its leading badge shows the author's priority, not 'warning'.
    expect(screen.getByText('Medium')).toBeInTheDocument();
    expect(screen.queryByText('warning')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Delete' }));
    expect(onDeleteThreat).toHaveBeenCalledWith(manual);
  });

  it('does not offer delete for a rule-derived threat', () => {
    renderPanel({ threats: [THREATS[0]] });

    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
  });

  it('renders non-threat hygiene findings in an "Other findings" section', () => {
    const findings: Finding[] = [
      { id: 'TM1003:0', severity: 'warning', ruleId: 'TM1003', message: 'The model has too few components.', elementIds: [] },
    ];
    renderPanel({ threats: [], findings });

    expect(screen.getByText('Other findings')).toBeInTheDocument();
    expect(screen.getByText('The model has too few components.')).toBeInTheDocument();
  });
});

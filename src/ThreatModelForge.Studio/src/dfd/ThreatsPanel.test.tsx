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

/** Renders the panel with sensible defaults; pass overrides for the props a test cares about. */
function renderPanel(overrides: Partial<ThreatsPanelProps> = {}): ThreatsPanelProps {
  const props: ThreatsPanelProps = {
    threats: THREATS,
    findings: [],
    onSelect: vi.fn(),
    offPageLabel: () => undefined,
    onAccept: vi.fn(),
    onUndoAccept: vi.fn(),
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

  it('accepts a threat with a required justification', () => {
    const { onAccept } = renderPanel({ threats: [THREATS[0]] });

    fireEvent.click(screen.getByRole('button', { name: /^Accept/ }));
    const input = screen.getByPlaceholderText('Why is this risk accepted? (required)');
    // The confirm button stays disabled until a justification is entered.
    expect(screen.getByRole('button', { name: 'Accept risk' })).toBeDisabled();

    fireEvent.change(input, { target: { value: 'Compensating control X is in place.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Accept risk' }));

    expect(onAccept).toHaveBeenCalledWith(THREATS[0], 'Compensating control X is in place.');
  });

  it('shows accepted state (badge + justification) and can undo', () => {
    const accepted = threat({
      id: 'client:TM1023',
      ruleId: 'TM1023',
      title: 'External entity does not authenticate itself.',
      state: 'Accepted',
      justification: 'Accepted for the pilot.',
      elementIds: ['client'],
    });
    const { onUndoAccept } = renderPanel({ threats: [accepted] });

    expect(screen.getByText(/0 open threats/)).toBeInTheDocument();
    expect(screen.getByText(/1 accepted/)).toBeInTheDocument();
    expect(screen.getByText('Accepted')).toBeInTheDocument();
    expect(screen.getByText('“Accepted for the pilot.”')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Undo' }));
    expect(onUndoAccept).toHaveBeenCalledWith(accepted);
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

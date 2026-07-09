import '@testing-library/jest-dom/vitest';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { Inspector } from './Inspector';
import type { PropertyDescriptorInfo } from './engineClient';
import type { DfdEdge, DfdNode } from './types';

/**
 * A representative slice of the engine property schema. It deliberately includes the flow
 * properties (Port, Algorithm) that the old hard-coded inspector could NOT expose — the bug this
 * suite guards against — alongside a process property.
 */
const SCHEMA: PropertyDescriptorInfo[] = [
  { appliesTo: 'flow', name: 'Protocol', kind: 'enum', values: ['HTTPS', 'HTTP', 'FTP'], default: 'HTTPS' },
  { appliesTo: 'flow', name: 'Port', kind: 'string', values: [], default: '' },
  { appliesTo: 'flow', name: 'DataType', kind: 'enum', values: ['Customer Content', 'EUII'], default: 'Customer Content' },
  { appliesTo: 'flow', name: 'Algorithm', kind: 'enum', values: ['None', 'AES-GCM', 'RC4'], default: 'None' },
  { appliesTo: 'flow', name: 'Cached', kind: 'bool', values: ['Yes', 'No'], default: 'No' },
  { appliesTo: 'process', name: 'AuthenticationScheme', kind: 'enum', values: ['None', 'OAuth'], default: 'None' },
];

function handlers() {
  return {
    onBeginNameEdit: vi.fn(),
    onRenameNode: vi.fn(),
    onRenameEdge: vi.fn(),
    onSetEdgeProperty: vi.fn(),
    onSetNodeProperty: vi.fn(),
    onRemoveNodeProperty: vi.fn(),
    onDelete: vi.fn(),
  };
}

function edge(properties: Record<string, string> = {}): DfdEdge {
  return { id: 'f1', source: 'n1', target: 'n2', label: 'request', data: { properties } };
}

function processNode(properties: Record<string, string> = {}): DfdNode {
  return { id: 'n1', type: 'process', position: { x: 0, y: 0 }, data: { label: 'API', properties } };
}

describe('Inspector — data flow', () => {
  it('renders a typed control for every flow schema property (incl. the previously-missing Port/Algorithm)', () => {
    const h = handlers();
    render(<Inspector node={null} edge={edge()} stencils={[]} propertySchema={SCHEMA} {...h} />);

    // Every flow property is now reachable — this is the core regression guard.
    for (const name of ['Protocol', 'Port', 'DataType', 'Algorithm', 'Cached']) {
      expect(screen.getByLabelText(name)).toBeInTheDocument();
    }
    // A process-only property must NOT leak onto a flow.
    expect(screen.queryByLabelText('AuthenticationScheme')).not.toBeInTheDocument();
  });

  it('renders enum properties as dropdowns of the schema values and strings as text inputs', () => {
    const h = handlers();
    render(<Inspector node={null} edge={edge()} stencils={[]} propertySchema={SCHEMA} {...h} />);

    const protocol = screen.getByLabelText('Protocol');
    expect(protocol.tagName).toBe('SELECT');
    const options = within(protocol).getAllByRole('option').map((o) => o.textContent);
    expect(options).toEqual(['(none)', 'HTTPS', 'HTTP', 'FTP']);

    expect(screen.getByLabelText('Port').tagName).toBe('INPUT');
  });

  it('setting a flow property emits the canonical value via onSetEdgeProperty', () => {
    const h = handlers();
    render(<Inspector node={null} edge={edge()} stencils={[]} propertySchema={SCHEMA} {...h} />);

    fireEvent.change(screen.getByLabelText('Protocol'), { target: { value: 'HTTPS' } });
    expect(h.onSetEdgeProperty).toHaveBeenCalledWith('f1', 'Protocol', 'HTTPS');

    fireEvent.change(screen.getByLabelText('Port'), { target: { value: '443' } });
    expect(h.onSetEdgeProperty).toHaveBeenCalledWith('f1', 'Port', '443');
  });

  it('clearing a flow property to "(none)" removes it', () => {
    const h = handlers();
    render(<Inspector node={null} edge={edge({ Protocol: 'HTTPS' })} stencils={[]} propertySchema={SCHEMA} {...h} />);

    fireEvent.change(screen.getByLabelText('Protocol'), { target: { value: '' } });
    // The edge's remove path is onSetEdgeProperty(id, key, '') — empty clears the property.
    expect(h.onSetEdgeProperty).toHaveBeenCalledWith('f1', 'Protocol', '');
  });

  it('shows a non-schema property under Custom properties with a remove control', () => {
    const h = handlers();
    render(<Inspector node={null} edge={edge({ Legacy: 'x' })} stencils={[]} propertySchema={SCHEMA} {...h} />);

    expect(screen.getByText('Custom properties')).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Remove Legacy'));
    expect(h.onSetEdgeProperty).toHaveBeenCalledWith('f1', 'Legacy', '');
  });
});

describe('Inspector — element', () => {
  it('renders the selected kind\'s schema controls and writes via onSetNodeProperty', () => {
    const h = handlers();
    render(<Inspector node={processNode()} edge={null} stencils={[]} propertySchema={SCHEMA} {...h} />);

    const scheme = screen.getByLabelText('AuthenticationScheme');
    expect(scheme).toBeInTheDocument();
    // Flow-only properties must not appear on a process.
    expect(screen.queryByLabelText('Protocol')).not.toBeInTheDocument();

    fireEvent.change(scheme, { target: { value: 'OAuth' } });
    expect(h.onSetNodeProperty).toHaveBeenCalledWith('n1', 'AuthenticationScheme', 'OAuth');
  });

  it('falls back to a free-text add row when the engine schema is unavailable (offline)', () => {
    const h = handlers();
    render(<Inspector node={processNode()} edge={null} stencils={[]} propertySchema={[]} {...h} />);

    // No typed controls, but the author can still add properties by name.
    expect(screen.queryByLabelText('AuthenticationScheme')).not.toBeInTheDocument();
    expect(screen.getByPlaceholderText('key')).toBeInTheDocument();
  });
});

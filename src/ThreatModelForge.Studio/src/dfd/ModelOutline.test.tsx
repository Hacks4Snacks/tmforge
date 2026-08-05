import '@testing-library/jest-dom/vitest';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { ModelOutline } from './ModelOutline';
import { buildOutline, type OutlineOrder } from './outline';
import type { DfdEdge, DfdKind, DfdNode } from './types';

function node(id: string, type: DfdKind, label: string, x: number, y: number, w: number, h: number): DfdNode {
  return { id, type, position: { x, y }, width: w, height: h, data: { label } };
}

function edge(id: string, source: string, target: string, label: string): DfdEdge {
  return { id, source, target, label, type: 'flow', data: {} };
}

const NODES: DfdNode[] = [
  node('tb1', 'boundary', 'TB1: Azure control plane', 0, 0, 400, 200),
  node('tb2', 'boundary', 'TB2: Cluster', 0, 300, 400, 200),
  node('x1', 'external', 'X1: Resource Manager', 40, 40, 100, 60),
  node('p1', 'process', 'P1: API server', 40, 340, 100, 60),
  node('p2', 'process', 'P2: controller', 200, 340, 100, 60),
];

const EDGES: DfdEdge[] = [
  edge('f1', 'x1', 'p1', 'F1: create resources'),
  edge('f2', 'p1', 'p2', 'F2: reconcile'),
];

function renderOutline(
  overrides: { order?: OutlineOrder; crossingOnly?: boolean; selectedFlowId?: string | null; selectedObjectId?: string | null } = {},
) {
  const handlers = {
    onOrderChange: vi.fn(),
    onCrossingOnlyChange: vi.fn(),
    onSelectFlow: vi.fn(),
    onSelectObject: vi.fn(),
    onStep: vi.fn(),
  };
  const outline = buildOutline(NODES, EDGES, overrides.order ?? 'model');
  const crossingOnly = overrides.crossingOnly ?? false;
  const flows = crossingOnly ? outline.flows.filter((flow) => flow.crossings.length > 0) : outline.flows;
  render(
    <ModelOutline
      outline={outline}
      flows={flows}
      order={overrides.order ?? 'model'}
      crossingOnly={crossingOnly}
      selectedFlowId={overrides.selectedFlowId ?? null}
      selectedObjectId={overrides.selectedObjectId ?? null}
      {...handlers}
    />,
  );
  return handlers;
}

/** The clickable row carrying the given label. */
function row(label: string | RegExp): HTMLElement {
  return screen.getByText(label).closest('button') as HTMLElement;
}

describe('ModelOutline — flows', () => {
  it('lists every flow with its position, its endpoints, and the boundaries it crosses', () => {
    renderOutline();

    const first = row('F1: create resources');
    expect(within(first).getByText('1')).toBeInTheDocument();
    expect(within(first).getByText('X1: Resource Manager → P1: API server')).toBeInTheDocument();
    expect(within(first).getByText(/crosses TB1: Azure control plane/)).toBeInTheDocument();

    // A flow with both ends in one boundary carries no crossing note.
    expect(within(row('F2: reconcile')).queryByText(/crosses/)).toBeNull();
  });

  it('jumps to a flow when its row is clicked', () => {
    const handlers = renderOutline();

    fireEvent.click(row('F2: reconcile'));

    expect(handlers.onSelectFlow).toHaveBeenCalledWith('f2');
  });

  it('marks the selected flow so the list and the canvas agree', () => {
    renderOutline({ selectedFlowId: 'f2' });

    expect(row('F2: reconcile')).toHaveClass('selected');
    expect(row('F1: create resources')).not.toHaveClass('selected');
  });

  it('steps forwards and backwards through the flow order', () => {
    const handlers = renderOutline({ selectedFlowId: 'f2' });

    expect(screen.getByText('2/2')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Next flow' }));
    fireEvent.click(screen.getByRole('button', { name: 'Previous flow' }));

    expect(handlers.onStep).toHaveBeenNthCalledWith(1, 1);
    expect(handlers.onStep).toHaveBeenNthCalledWith(2, -1);
  });

  it('shows no position in the stepper until a flow is selected', () => {
    renderOutline();

    expect(screen.getByText('–/2')).toBeInTheDocument();
  });

  it('reports how many flows the boundary-crossing filter is showing', () => {
    const handlers = renderOutline({ crossingOnly: true });

    expect(screen.getByText('1 of 2')).toBeInTheDocument();
    expect(screen.queryByText('F2: reconcile')).toBeNull();

    fireEvent.click(screen.getByLabelText('Boundary-crossing only'));
    expect(handlers.onCrossingOnlyChange).toHaveBeenCalledWith(false);
  });

  it('changes the review order', () => {
    const handlers = renderOutline();

    fireEvent.change(screen.getByLabelText('Outline order'), { target: { value: 'name' } });

    expect(handlers.onOrderChange).toHaveBeenCalledWith('name');
  });
});

describe('ModelOutline — objects', () => {
  it('groups objects under their trust boundary with their kind and flow count', () => {
    renderOutline();

    expect(within(row('P1: API server')).getByText('Process · 2 flows')).toBeInTheDocument();
    expect(within(row('X1: Resource Manager')).getByText('External · 1 flow')).toBeInTheDocument();
    expect(within(row('TB2: Cluster')).getByText('2')).toBeInTheDocument();
  });

  it('jumps to an object, and to a trust boundary, when its row is clicked', () => {
    const handlers = renderOutline();

    fireEvent.click(row('P2: controller'));
    fireEvent.click(row('TB1: Azure control plane'));

    expect(handlers.onSelectObject).toHaveBeenNthCalledWith(1, 'p2');
    expect(handlers.onSelectObject).toHaveBeenNthCalledWith(2, 'tb1');
  });
});

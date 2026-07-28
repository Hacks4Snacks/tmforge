import '@testing-library/jest-dom/vitest';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { PageTabs } from './PageTabs';

const PAGES = [
  { id: 'p1', name: 'Context' },
  { id: 'p2', name: 'Payments' },
  { id: 'p3', name: 'Identity' },
];

function handlers() {
  return {
    onSwitch: vi.fn(),
    onAdd: vi.fn(),
    onRename: vi.fn(),
    onDelete: vi.fn(),
    onReorder: vi.fn(),
  };
}

function renderTabs(overrides: {
  pages?: { id: string; name: string }[];
  activePageId?: string;
  findingPageIds?: ReadonlySet<string>;
} = {}) {
  const h = handlers();
  render(
    <PageTabs
      pages={overrides.pages ?? PAGES}
      activePageId={overrides.activePageId ?? 'p1'}
      findingPageIds={overrides.findingPageIds}
      {...h}
    />,
  );
  return h;
}

/** The tab element carrying the given label. */
function tab(name: string): HTMLElement {
  return screen.getByText(name).closest('.page-tab') as HTMLElement;
}

describe('PageTabs — switching', () => {
  it('marks the active page and only the active page', () => {
    renderTabs({ activePageId: 'p2' });

    expect(tab('Payments')).toHaveAttribute('aria-selected', 'true');
    expect(tab('Context')).toHaveAttribute('aria-selected', 'false');
  });

  it('switches on click and from the keyboard', () => {
    const h = renderTabs();

    fireEvent.click(tab('Payments'));
    fireEvent.keyDown(tab('Identity'), { key: 'Enter' });
    fireEvent.keyDown(tab('Identity'), { key: ' ' });

    expect(h.onSwitch).toHaveBeenNthCalledWith(1, 'p2');
    expect(h.onSwitch).toHaveBeenNthCalledWith(2, 'p3');
    expect(h.onSwitch).toHaveBeenCalledTimes(3);
  });

  it('badges only the pages that carry findings', () => {
    renderTabs({ findingPageIds: new Set(['p2']) });

    expect(tab('Payments').querySelector('.page-tab-dot')).not.toBeNull();
    expect(tab('Context').querySelector('.page-tab-dot')).toBeNull();
  });

  it('adds a page', () => {
    const h = renderTabs();

    fireEvent.click(screen.getByLabelText('Add page'));

    expect(h.onAdd).toHaveBeenCalled();
  });
});

describe('PageTabs — deleting', () => {
  it('deletes the page whose close button was clicked, without switching to it first', () => {
    // The close button sits inside the tab's own click handler. Without stopPropagation, deleting a
    // page would also make it active on the way out — a switch to a page that no longer exists.
    const h = renderTabs();

    fireEvent.click(screen.getByLabelText('Delete Payments'));

    expect(h.onDelete).toHaveBeenCalledWith('p2');
    expect(h.onSwitch).not.toHaveBeenCalled();
  });

  it('offers no way to delete the last remaining page', () => {
    // Deleting a page destroys its contents; leaving a model with no pages at all is not a state
    // the editor should be able to reach from the UI.
    renderTabs({ pages: [{ id: 'only', name: 'Page 1' }], activePageId: 'only' });

    expect(screen.queryByLabelText('Delete Page 1')).not.toBeInTheDocument();
  });

  it('restores the delete affordance once there is more than one page', () => {
    renderTabs({ pages: PAGES.slice(0, 2) });

    expect(screen.getByLabelText('Delete Context')).toBeInTheDocument();
    expect(screen.getByLabelText('Delete Payments')).toBeInTheDocument();
  });
});

describe('PageTabs — renaming', () => {
  it('commits a new name on Enter', () => {
    const h = renderTabs();

    fireEvent.doubleClick(tab('Context'));
    const input = screen.getByDisplayValue('Context');
    fireEvent.change(input, { target: { value: 'Overview' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(h.onRename).toHaveBeenCalledWith('p1', 'Overview');
  });

  it('starts a rename from F2 as well as a double-click', () => {
    renderTabs();

    fireEvent.keyDown(tab('Payments'), { key: 'F2' });

    expect(screen.getByDisplayValue('Payments')).toBeInTheDocument();
  });

  it('commits when focus leaves the field', () => {
    const h = renderTabs();

    fireEvent.doubleClick(tab('Context'));
    const input = screen.getByDisplayValue('Context');
    fireEvent.change(input, { target: { value: 'Overview' } });
    fireEvent.blur(input);

    expect(h.onRename).toHaveBeenCalledWith('p1', 'Overview');
  });

  it('keeps the original name when the rename is cancelled', () => {
    const h = renderTabs();

    fireEvent.doubleClick(tab('Context'));
    const input = screen.getByDisplayValue('Context');
    fireEvent.change(input, { target: { value: 'Discarded' } });
    fireEvent.keyDown(input, { key: 'Escape' });

    expect(h.onRename).not.toHaveBeenCalled();
    expect(screen.getByText('Context')).toBeInTheDocument();
  });

  it('refuses a blank name rather than leaving a page with no label', () => {
    const h = renderTabs();

    fireEvent.doubleClick(tab('Context'));
    const input = screen.getByDisplayValue('Context');
    fireEvent.change(input, { target: { value: '   ' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(h.onRename).not.toHaveBeenCalled();
    expect(screen.getByText('Context')).toBeInTheDocument();
  });

  it('trims the committed name', () => {
    const h = renderTabs();

    fireEvent.doubleClick(tab('Context'));
    const input = screen.getByDisplayValue('Context');
    fireEvent.change(input, { target: { value: '  Overview  ' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(h.onRename).toHaveBeenCalledWith('p1', 'Overview');
  });

  it('does not switch pages while the name is being edited', () => {
    const h = renderTabs({ activePageId: 'p2' });

    fireEvent.doubleClick(tab('Context'));
    fireEvent.click(screen.getByDisplayValue('Context'));

    expect(h.onSwitch).not.toHaveBeenCalled();
  });

  it('hides the delete button while renaming, so a stray click cannot destroy the page', () => {
    renderTabs();

    fireEvent.doubleClick(tab('Context'));

    expect(screen.queryByLabelText('Delete Context')).not.toBeInTheDocument();
  });

  it('stops the tab being draggable while renaming, so the text can be selected', () => {
    renderTabs();

    fireEvent.doubleClick(tab('Context'));

    const editing = screen.getByDisplayValue('Context').closest('.page-tab')!;
    expect(editing).toHaveAttribute('draggable', 'false');
  });
});

describe('PageTabs — reordering', () => {
  it('reports the move when a tab is dropped on another', () => {
    const h = renderTabs();

    fireEvent.dragStart(tab('Context'));
    fireEvent.dragOver(tab('Identity'));
    fireEvent.drop(tab('Identity'));

    expect(h.onReorder).toHaveBeenCalledWith(0, 2);
  });

  it('ignores a tab dropped back onto itself', () => {
    const h = renderTabs();

    fireEvent.dragStart(tab('Payments'));
    fireEvent.drop(tab('Payments'));

    expect(h.onReorder).not.toHaveBeenCalled();
  });

  it('ignores a drop that did not start on a tab', () => {
    const h = renderTabs();

    fireEvent.drop(tab('Payments'));

    expect(h.onReorder).not.toHaveBeenCalled();
  });
});

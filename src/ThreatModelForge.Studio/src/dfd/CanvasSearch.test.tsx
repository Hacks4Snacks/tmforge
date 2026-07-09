import '@testing-library/jest-dom/vitest';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { CanvasSearch, type SearchItem } from './CanvasSearch';

const ITEMS: SearchItem[] = [
  { id: 'p1', name: 'Gateway', kind: 'process', pageId: 'pg1', pageName: 'Context' },
  { id: 's1', name: 'User DB', kind: 'datastore', pageId: 'pg1', pageName: 'Context' },
  { id: 'f1', name: 'login request', kind: 'flow', pageId: 'pg2', pageName: 'Auth' },
];

const LABEL = 'Find an element or flow on the canvas';

function setup(onJump: (item: SearchItem) => void = vi.fn()) {
  render(<CanvasSearch items={ITEMS} onJump={onJump} />);
  return screen.getByLabelText(LABEL) as HTMLInputElement;
}

describe('CanvasSearch', () => {
  it('shows no results dropdown until the user types', () => {
    setup();
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('filters by name (case-insensitive) and by kind label', () => {
    const input = setup();
    fireEvent.change(input, { target: { value: 'GATE' } });
    expect(screen.getByText('Gateway')).toBeInTheDocument();
    expect(screen.queryByText('User DB')).toBeNull();

    fireEvent.change(input, { target: { value: 'flow' } });
    expect(screen.getByText('login request')).toBeInTheDocument();
    expect(screen.queryByText('Gateway')).toBeNull();
  });

  it('jumps on click and clears the query', () => {
    const onJump = vi.fn();
    const input = setup(onJump);
    fireEvent.change(input, { target: { value: 'user' } });
    fireEvent.click(screen.getByText('User DB'));
    expect(onJump).toHaveBeenCalledWith(expect.objectContaining({ id: 's1' }));
    expect(input.value).toBe('');
  });

  it('jumps to the first match on Enter', () => {
    const onJump = vi.fn();
    const input = setup(onJump);
    fireEvent.change(input, { target: { value: 'gateway' } });
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(onJump).toHaveBeenCalledWith(expect.objectContaining({ id: 'p1' }));
  });

  it('shows a no-matches message when nothing matches', () => {
    const input = setup();
    fireEvent.change(input, { target: { value: 'zzz' } });
    expect(screen.getByText('No matches')).toBeInTheDocument();
  });
});

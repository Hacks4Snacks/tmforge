import { describe, expect, it, vi } from 'vitest';
import { StudioBridge } from './vscodeHost';
import type { TmForgeModel } from './dfd/types';

const model: TmForgeModel = { schema: 'tmforge-json', version: '0.1', elements: [], flows: [] };

describe('VS Code document bridge', () => {
  it('serializes edits using acknowledged versions and saves after pending edits', async () => {
    const messages: Record<string, unknown>[] = [];
    const bridge = new StudioBridge(message => messages.push(message as Record<string, unknown>));
    bridge.receive({ type: 'document', version: 3, model });
    bridge.edit(model, model);
    bridge.edit(model, model);
    const save = bridge.command('save');
    await vi.waitFor(() => expect(messages).toHaveLength(1));
    expect(messages[0].params).toMatchObject({ version: 3 });
    bridge.receive({ type: 'response', id: messages[0].id, result: { version: 4, dirty: true } });
    await vi.waitFor(() => expect(messages).toHaveLength(2));
    expect(messages[1].params).toMatchObject({ version: 4 });
    bridge.receive({ type: 'response', id: messages[1].id, result: { version: 5, dirty: true } });
    await vi.waitFor(() => expect(messages).toHaveLength(3));
    expect(messages[2].method).toBe('save');
    bridge.receive({ type: 'response', id: messages[2].id, result: null });
    await save;
    bridge.dispose();
  });

  it('discards queued stale edits after a source change', async () => {
    const post = vi.fn();
    const bridge = new StudioBridge(post);
    bridge.receive({ type: 'document', version: 1, model });
    bridge.edit(model, model);
    bridge.receive({ type: 'document', version: 2, model });
    await Promise.resolve();
    expect(post).not.toHaveBeenCalled();
    bridge.dispose();
  });

  it('surfaces rejected edits and asks for the current source', async () => {
    const post = vi.fn();
    const bridge = new StudioBridge(post);
    bridge.onError = vi.fn();
    bridge.edit(model, model);
    await vi.waitFor(() => expect(post).toHaveBeenCalledOnce());
    bridge.receive({ type: 'response', id: post.mock.calls[0][0].id, error: 'Source changed' });
    await vi.waitFor(() => expect(bridge.onError).toHaveBeenCalledWith('Source changed'));
    expect(post).toHaveBeenLastCalledWith({ type: 'ready' });
    bridge.dispose();
  });
});

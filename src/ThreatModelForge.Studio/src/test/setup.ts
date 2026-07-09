// Vitest global setup: register jest-dom matchers (toBeInTheDocument, toHaveValue, ...) and
// clean the DOM between tests so component trees never leak across cases.
import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';

// jsdom in this environment runs on an opaque origin and does not expose the Web Storage API, so
// window.localStorage is undefined. The editor persists the workspace, theme, and palette
// preferences there (behind try/catch), so provide a minimal in-memory implementation for tests.
function createMemoryStorage(): Storage {
  const store = new Map<string, string>();
  return {
    get length(): number {
      return store.size;
    },
    clear(): void {
      store.clear();
    },
    getItem(key: string): string | null {
      return store.has(key) ? store.get(key)! : null;
    },
    key(index: number): string | null {
      return Array.from(store.keys())[index] ?? null;
    },
    removeItem(key: string): void {
      store.delete(key);
    },
    setItem(key: string, value: string): void {
      store.set(key, String(value));
    },
  } as Storage;
}

if (typeof window !== 'undefined' && !window.localStorage) {
  Object.defineProperty(window, 'localStorage', {
    value: createMemoryStorage(),
    configurable: true,
    writable: true,
  });
}

afterEach(() => {
  cleanup();
});


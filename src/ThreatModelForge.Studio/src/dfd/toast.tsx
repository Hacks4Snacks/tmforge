import { useEffect, useState } from 'react';

export type ToastKind = 'error' | 'info' | 'success';

interface ToastItem {
  id: number;
  message: string;
  kind: ToastKind;
}

const DISMISS_MS = 5000;

let counter = 0;
let items: ToastItem[] = [];
const listeners = new Set<(items: ToastItem[]) => void>();

function emit(): void {
  for (const listener of listeners) {
    listener(items);
  }
}

function dismiss(id: number): void {
  items = items.filter((t) => t.id !== id);
  emit();
}

/** Show a transient, non-blocking notification. Replaces window.alert for recoverable errors. */
export function toast(message: string, kind: ToastKind = 'info'): void {
  const id = ++counter;
  items = [...items, { id, message, kind }];
  emit();
  window.setTimeout(() => dismiss(id), DISMISS_MS);
}

/** Renders the active toasts. Mount once near the app root. */
export function Toaster() {
  const [list, setList] = useState<ToastItem[]>(items);

  useEffect(() => {
    listeners.add(setList);
    setList(items);
    return () => {
      listeners.delete(setList);
    };
  }, []);

  if (list.length === 0) {
    return null;
  }

  return (
    <div className="toaster" role="status" aria-live="polite">
      {list.map((t) => (
        <div key={t.id} className={`toast toast-${t.kind}`}>
          <span className="toast-msg">{t.message}</span>
          <button className="toast-close" aria-label="Dismiss" onClick={() => dismiss(t.id)}>
            ×
          </button>
        </div>
      ))}
    </div>
  );
}

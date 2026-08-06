import { useStore } from '../lib/useStore.js';
import { toasts } from '../lib/store.js';

export function Toasts() {
  useStore();
  return (
    <div className="toast-stack">
      {toasts.items.map(t => <div className="toast" key={t.id}>{t.message}</div>)}
    </div>
  );
}

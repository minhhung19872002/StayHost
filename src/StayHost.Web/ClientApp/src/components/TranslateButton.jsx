import { useEffect, useState } from 'react';
import { useStore } from '../lib/useStore.js';
import { api } from '../lib/api.js';

// docs/01 TĐ-03, TN-06 — the config is the same for everyone, so fetch it once
// per page load and share it. Null while unknown; {enabled} once loaded.
let configPromise = null;
function loadConfig() {
  configPromise ||= api.translateConfig().catch(() => ({ enabled: false, targets: [] }));
  return configPromise;
}

/**
 * A "Dịch" toggle that only appears when translation is switched on. Translates
 * the given text into the viewer's current language and shows it inline; a second
 * click hides it again. Off by default, so with no provider configured nothing
 * renders — the same rule the social-login buttons follow.
 */
export function TranslateButton({ text, className = 'text-btn', style }) {
  const state = useStore();
  const [enabled, setEnabled] = useState(false);
  const [shown, setShown] = useState(false);
  const [translated, setTranslated] = useState(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => { loadConfig().then(c => setEnabled(!!c.enabled)); }, []);

  if (!enabled || !text?.trim()) return null;

  const target = state.language?.code || 'vi';

  const toggle = async () => {
    if (shown) { setShown(false); return; }
    if (translated) { setShown(true); return; }
    setBusy(true);
    try {
      const res = await api.translate(text, target);
      setTranslated(res.text);
      setShown(true);
    } catch { /* leave the original showing */ }
    finally { setBusy(false); }
  };

  return (
    <>
      <button type="button" className={className} style={style} onClick={toggle} disabled={busy}>
        {busy ? 'Đang dịch…' : shown ? 'Xem bản gốc' : 'Dịch'}
      </button>
      {shown && translated && (
        <div className="translated-text" style={{ marginTop: 6, whiteSpace: 'pre-wrap' }}>{translated}</div>
      )}
    </>
  );
}

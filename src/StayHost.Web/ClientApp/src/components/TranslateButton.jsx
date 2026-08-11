import { useEffect, useState } from 'react';
import { useStore } from '../lib/useStore.js';
import { api } from '../lib/api.js';
import { t } from '../lib/i18n.js';

// docs/01 TĐ-03, TN-06 — the config is the same for everyone, so fetch it once
// per page load and share it. Null while unknown; {enabled} once loaded.
let configPromise = null;
function loadConfig() {
  configPromise ||= api.translateConfig().catch(() => ({ enabled: false, targets: [] }));
  return configPromise;
}

/** Whether a translation provider is configured at all. Shared with TranslatedText. */
export const translationEnabled = () => loadConfig().then(c => !!c.enabled);

/**
 * A "Dịch" toggle that only appears when translation is switched on. Translates
 * the given text into the viewer's current language and shows it inline; a second
 * click hides it again. Off by default, so with no provider configured nothing
 * renders — the same rule the social-login buttons follow.
 */
export function TranslateButton({ text, className = 'translate-btn', style }) {
  const state = useStore();
  const [config, setConfig] = useState(null);
  const [shown, setShown] = useState(false);
  const [translated, setTranslated] = useState(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => { loadConfig().then(setConfig); }, []);

  if (!config?.enabled || !text?.trim()) return null;

  // Translate into the viewer's language. When the interface is Vietnamese the
  // content usually is too, so a vi→vi call would show nothing — fall back to
  // English there so a tap always produces a visible result.
  const ui = state.language?.code || 'vi';
  const target = ui === 'vi' ? 'en' : ui;
  // The label comes from the server's own list rather than a copy kept here: a
  // second list is a second thing to forget, and forgetting it shows the reader
  // a raw language code.
  const targetLabel = config.targets?.find(x => x.code === target)?.label || target;

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
        <span aria-hidden="true">🌐</span>
        {' '}
        {busy ? t('Đang dịch…') : shown ? t('Xem bản gốc') : `${t('Dịch sang')} ${targetLabel}`}
      </button>
      {shown && translated && (
        <div className="translated-text" style={{ marginTop: 6, whiteSpace: 'pre-wrap' }}>{translated}</div>
      )}
    </>
  );
}

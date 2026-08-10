import { useEffect } from 'react';
import { closeOverlay } from '../../lib/store.js';
import { t } from '../../lib/i18n.js';

/**
 * Shared chrome for every overlay: backdrop click and Escape close it, and the
 * page behind stops scrolling while it is open.
 */
export function Modal({ title, size = '', children, foot }) {
  useEffect(() => {
    const onKey = e => { if (e.key === 'Escape') closeOverlay(); };
    document.addEventListener('keydown', onKey);
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = '';
    };
  }, []);

  return (
    <div className="overlay" onMouseDown={e => { if (e.target === e.currentTarget) closeOverlay(); }}>
      <div className={`modal ${size}`} role="dialog" aria-modal="true" aria-label={title}>
        <div className="modal-head">
          <button className="modal-close" onClick={closeOverlay} aria-label={t('Đóng')}>✕</button>
          <h2>{title}</h2>
          <span style={{ width: 32 }} />
        </div>
        <div className="modal-body">{children}</div>
        {foot && <div className="modal-foot">{foot}</div>}
      </div>
    </div>
  );
}

/** The ± row used by the filter and guest pickers. */
export function CountRow({ label, hint, value, display, onDec, onInc, decDisabled }) {
  return (
    <div className="count-row">
      <div className="tx"><b>{label}</b>{hint && <span>{hint}</span>}</div>
      <div className="count-ctl">
        <button type="button" className="round-btn" onClick={onDec}
                disabled={decDisabled} aria-label={`${t('Giảm')} ${label}`}>−</button>
        <span className="num">{display ?? value}</span>
        <button type="button" className="round-btn" onClick={onInc} aria-label={`${t('Tăng')} ${label}`}>+</button>
      </div>
    </div>
  );
}

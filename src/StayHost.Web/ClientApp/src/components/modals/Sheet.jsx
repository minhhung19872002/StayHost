import { useEffect } from 'react';
import { t } from '../../lib/i18n.js';

/**
 * The same chrome as <Modal>, but closed by a callback instead of the global
 * overlay slot. A booking dialog belongs to the page that opened it — it carries
 * the slot, the quantity and the quote the page is already holding — so routing
 * it through state.overlay would mean lifting all of that into the store for no
 * gain.
 */
export function Sheet({ title, size = '', onClose, children, foot }) {
  useEffect(() => {
    const onKey = e => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('keydown', onKey);
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = '';
    };
  }, [onClose]);

  return (
    <div className="overlay" onMouseDown={e => { if (e.target === e.currentTarget) onClose(); }}>
      <div className={`modal ${size}`} role="dialog" aria-modal="true" aria-label={title}>
        <div className="modal-head">
          <button className="modal-close" onClick={onClose} aria-label={t('Đóng')}>✕</button>
          <h2>{title}</h2>
          <span style={{ width: 32 }} />
        </div>
        <div className="modal-body">{children}</div>
        {foot && <div className="modal-foot">{foot}</div>}
      </div>
    </div>
  );
}

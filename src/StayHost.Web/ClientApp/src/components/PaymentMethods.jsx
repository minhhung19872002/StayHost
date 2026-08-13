import { useEffect, useState } from 'react';
import { useStore } from '../lib/useStore.js';
import { set } from '../lib/store.js';
import { api } from '../lib/api.js';
import { FALLBACK_METHODS } from '../lib/payments.js';
import { Icon } from './Icon.jsx';
import { t } from '../lib/i18n.js';

/**
 * docs/07 §2 and §4 — picking how to pay: the methods the server offers, the
 * cards already saved, and the fields for a new one.
 *
 * One component for all three checkouts (a stay, a service, a ticket) rather
 * than three copies of the same list. They were three, and the two newer ones
 * were already a paragraph behind: four unlabelled rows of text where the
 * difference between "Thẻ tín dụng" and "Thẻ ATM nội địa" was a sentence you
 * had to read rather than a picture you could see.
 */
const ICONS = { card: 'card', napas: 'bank', momo: 'wallet', zalopay: 'wallet' };

export function PaymentMethods({ idPrefix = 'card' }) {
  const state = useStore();
  const [offered, setOffered] = useState(FALLBACK_METHODS);
  const [cards, setCards] = useState([]);

  useEffect(() => {
    // The list is the server's, so a checkout and the saved-methods screen
    // cannot disagree about what StayHost takes. The balance has a control of
    // its own on a stay, so it is not a method to pick here.
    api.paymentCatalogue()
      .then(d => {
        const methods = d.methods.filter(m => m.key !== 'balance');
        setOffered(methods);
        // Kept on the store as well, because the review step has to name the
        // method the guest picked and the fallback list cannot: it is the §2.1
        // group by definition, so anything added later — VietQR, PayPal — would
        // come out as "Thẻ" there.
        set({ paymentMethods: methods });
      })
      .catch(() => { /* the §2.1 group is the fallback either way */ });
    api.savedCards().then(setCards).catch(() => setCards([]));
  }, []);

  const usable = cards.filter(c => !c.isExpired);

  return <>
    <div style={{ display: 'grid', gap: 10 }}>
      {offered.map(m => (
        <button type="button" key={m.key} className={`opt opt-row ${state.payMethod === m.key ? 'is-on' : ''}`}
                onClick={() => set({ payMethod: m.key })}>
          <span className="opt-ic"><Icon name={ICONS[m.key] ?? 'card'} size={22} /></span>
          <span className="opt-tx"><b>{t(m.label)}</b><span>{t(m.hint)}</span></span>
        </button>
      ))}
    </div>

    {/* docs/07 §4 — a guest who has saved a card should not retype it. */}
    {state.payMethod === 'card' && !!usable.length && (
      <div style={{ display: 'grid', gap: 8, marginTop: 16 }}>
        <span className="cap">{t('Thẻ đã lưu')}</span>
        {usable.map(c => (
          <button type="button" key={c.id}
                  className={`opt opt-row ${state.payCardId === c.id ? 'is-on' : ''}`}
                  onClick={() => set({ payCardId: c.id, payCardLast4: c.last4 })}>
            <span className="opt-ic"><Icon name="card" size={22} /></span>
            <span className="opt-tx">
              <b>{c.brandLabel} •••• {c.last4}</b><span>{t('Hết hạn')} {c.expiry}</span>
            </span>
          </button>
        ))}
        <button type="button" className={`opt opt-row ${state.payCardId ? '' : 'is-on'}`}
                onClick={() => set({ payCardId: null, payCardLast4: null })}>
          <span className="opt-ic"><Icon name="card" size={22} /></span>
          <span className="opt-tx">
            <b>{t('Dùng thẻ khác')}</b><span>{t('Nhập số thẻ bên dưới')}</span>
          </span>
        </button>
      </div>
    )}

    {state.payMethod === 'card' && !state.payCardId && <>
      <div className="field-grid" style={{ marginTop: 18 }}>
        <label className="form-field" style={{ gridColumn: '1/-1' }}>
          <span className="cap">{t('Số thẻ')}</span>
          <input id={`${idPrefix}-number`} inputMode="numeric" placeholder="4242 4242 4242 4242"
                 defaultValue="4242 4242 4242 4242" />
        </label>
        <label className="form-field"><span className="cap">{t('Hết hạn')}</span>
          <input id={`${idPrefix}-exp`} placeholder="12/28" defaultValue="12/28" /></label>
        <label className="form-field"><span className="cap">CVV</span>
          <input id={`${idPrefix}-cvv`} inputMode="numeric" placeholder="123" defaultValue="123" /></label>
      </div>
      <p className="pay-demo">
        <Icon name="shield" size={16} />
        {t('Bản demo dùng thẻ thử nghiệm, không có giao dịch thật nào được thực hiện.')}
      </p>
    </>}
  </>;
}

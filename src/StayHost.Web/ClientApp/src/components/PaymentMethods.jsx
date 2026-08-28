import { Fragment, useEffect, useState } from 'react';
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

/**
 * docs/07 §13 — whose page the guest is about to land on. A brand name, so it is
 * not translated; it is only ever shown, never matched against.
 *
 * Keyed by the *gateway* the server names, not by the payment method. It used to
 * be keyed by method, with card and napas both spelled "VNPay" — and the day
 * those two rows were pointed at OnePay the checkout began promising a page it
 * was not about to open. Which gateway serves which method is configuration, so
 * only the server knows it.
 */
const GATEWAY_NAME = { vnpay: 'VNPay', onepay: 'OnePay', momo: 'MoMo', zalopay: 'ZaloPay' };

export function PaymentMethods({ idPrefix = 'card', listingId = null }) {
  const state = useStore();
  const [offered, setOffered] = useState(FALLBACK_METHODS);
  const [cards, setCards] = useState([]);

  useEffect(() => {
    // The list is the server's, so a checkout and the saved-methods screen
    // cannot disagree about what Staylio takes. The balance has a control of
    // its own on a stay, so it is not a method to pick here.
    // docs/07 §2.5 — with a listing in hand the catalogue can also answer
    // whether this host takes the money at the door.
    api.paymentCatalogue(listingId)
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
    // docs/07 §2.5 — a guest with no account has no saved cards, so asking is a
    // round trip that can only answer 401. It was caught and ignored, which is
    // why nothing broke; it still put an error in the console of the most
    // important page in the funnel, for somebody to chase later.
    if (state.user) api.savedCards().then(setCards).catch(() => setCards([]));
    else setCards([]);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [listingId, state.user]);

  // The method the guest picked, if a licensed gateway is wired behind it.
  const live = offered.find(m => m.key === state.payMethod && m.live) ?? null;

  // A card typed into the stand-in cannot be charged at VNPay and the other way
  // round — the gateway holds a token, this platform holds nothing. So the list
  // only offers what the method the guest picked can actually use.
  const usable = cards.filter(c => !c.isExpired && (live ? c.gatewayHeld : !c.gatewayHeld));

  return <>
    <div style={{ display: 'grid', gap: 10 }}>
      {offered.map(m => (
        <Fragment key={m.key}>
          <button type="button" className={`opt opt-row ${state.payMethod === m.key ? 'is-on' : ''}`}
                  onClick={() => set({ payMethod: m.key })}>
            <span className="opt-ic"><Icon name={ICONS[m.key] ?? 'card'} size={22} /></span>
            <span className="opt-tx"><b>{t(m.label)}</b><span>{t(m.hint)}</span></span>
          </button>

          {/* docs/07 §13 — a method with a licensed gateway behind it is paid for
              on that gateway's own page. It sits under the row the guest just
              pressed rather than under the whole list: below the list it falls
              past the bottom of the modal, so the guest picks "Thẻ tín dụng",
              sees no card fields, and reads the checkout as broken. The one
              line that explains why has to be where the choice was made. */}
          {state.payMethod === m.key && m.live && (
            <p className="pay-demo" style={{ margin: 0 }}>
              <Icon name="shield" size={16} />
              {t('Bạn sẽ được chuyển sang trang thanh toán của {} để hoàn tất. Staylio không nhìn thấy số thẻ của bạn.')
                .replace('{}', GATEWAY_NAME[m.provider] ?? t('cổng thanh toán'))}
            </p>
          )}
        </Fragment>
      ))}
    </div>

    {/* docs/07 §4 — the guest chooses whether the gateway keeps this card. It is
        not only a convenience: with a live gateway this is also the only way
        Staylio ever learns the card's last four digits (§14.2 means the number
        is typed on their page), and §10's closed-card refund rule reads exactly
        that field. So the wording says what it is for. */}
    {live?.tokens && !state.payCardId && (
      <label className="opt opt-row" style={{ marginTop: 12, cursor: 'pointer' }}>
        <input type="checkbox" checked={!!state.paySaveCard} style={{ width: 18, height: 18 }}
               onChange={e => set({ paySaveCard: e.target.checked })} />
        <span className="opt-tx">
          <b>{t('Lưu thẻ này cho lần sau')}</b>
          <span>{t('Thẻ do cổng thanh toán giữ. Staylio chỉ thấy 4 số cuối, dùng để hoàn tiền và nhắc khi thẻ hết hạn.')}</span>
        </span>
      </label>
    )}

    {/* docs/07 §4 — a guest who has saved a card should not retype it. With a
        live gateway the saved card is a token there, so picking one sends the
        guest to that gateway's token page instead of its card form. */}
    {state.payMethod === 'card' && (!live || live.tokens) && !!usable.length && (
      <div style={{ display: 'grid', gap: 8, marginTop: 16 }}>
        <span className="cap">{t('Thẻ đã lưu')}</span>
        {usable.map(c => (
          <button type="button" key={c.id}
                  className={`opt opt-row ${state.payCardId === c.id ? 'is-on' : ''}`}
                  onClick={() => set({ payCardId: c.id, payCardLast4: c.last4 })}>
            <span className="opt-ic"><Icon name="card" size={22} /></span>
            <span className="opt-tx">
              <b>{c.brandLabel} •••• {c.last4}</b>
              {/* A gateway-held card has no expiry here to print — the server
                  says so in words, and those words need translating like any
                  other server-generated string. */}
              <span>{c.gatewayHeld ? t(c.expiry) : `${t('Hết hạn')} ${c.expiry}`}</span>
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

    {/* docs/07 §14.2 — the card fields only exist for the built-in stand-in.
        With a real gateway wired the number is typed on their page and this form
        must not appear at all, let alone collect anything. */}
    {state.payMethod === 'card' && !state.payCardId && !live && <>
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

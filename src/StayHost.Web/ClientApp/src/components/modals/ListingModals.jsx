import { useStore } from '../../lib/useStore.js';
import { useEffect, useRef, useState } from 'react';
import {
  set, holdDates, payHeld, releaseHold, openSplit, openOverlay, closeOverlay,
  shareListing, toggleFavorite
} from '../../lib/store.js';
import { money, longDate, parseIso, isoOf } from '../../lib/format.js';
import { AmenityIcon } from '../Icon.jsx';
import { HostReply, StarDistribution } from '../../pages/Detail.jsx';
import { api } from '../../lib/api.js';
import { useSlideshow } from '../../lib/useSlideshow.js';
import { Modal } from './Modal.jsx';

const PHOTO_CAPTIONS = ['Ảnh chính', 'Phòng khách', 'Phòng ngủ', 'Không gian ngoài trời', 'Phòng tắm'];

export function PhotosModal() {
  const state = useStore();
  const c = state.detail?.card;
  if (!c) return null;

  const index = state.photoIndex;

  // A focused index turns the grid into a single-photo viewer with arrows.
  if (index != null) return <PhotoLightbox card={c} index={index} />;

  return (
    <Modal title={`${c.title} — ${c.images.length} ảnh`} size="wide">
      <div className="lightbox-grid">
        {c.images.map((src, i) => (
          <figure key={i}>
            <button className="lightbox-open" onClick={() => set({ photoIndex: i })} aria-label={`Phóng to ảnh ${i + 1}`}>
              <img src={src} alt={`${c.title} — ảnh ${i + 1}`} loading="lazy" decoding="async" />
            </button>
            <figcaption>{PHOTO_CAPTIONS[i] ?? `Ảnh ${i + 1}`}</figcaption>
          </figure>
        ))}
      </div>
    </Modal>
  );
}

/**
 * The photo viewer: the whole screen, black, with the photo and nothing else
 * competing for attention. Moving between photos uses the slide-and-zoom of
 * codepen.io/daniesy/pen/JoWOpR — the one arriving grows from half size while
 * the one being replaced slides out the side you came from.
 *
 * This one does not use Modal: a white card with a header around a photograph
 * is exactly what a viewer is meant to get out of the way of.
 */
function PhotoLightbox({ card, index }) {
  const total = card.images.length;
  const slides = useSlideshow(index, i => set({ photoIndex: i }), total);
  const { idx } = slides;

  // Arrow keys are how anybody looks through photos full screen, and Escape is
  // how they leave. Re-bound every render so the handler sees the current index.
  useEffect(() => {
    const onKey = e => {
      if (e.key === 'ArrowLeft') slides.step(-1);
      else if (e.key === 'ArrowRight') slides.step(1);
      else if (e.key === 'Escape') closeOverlay();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  });

  // The page behind must not scroll while the viewer has the screen.
  useEffect(() => {
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = ''; };
  }, []);

  return (
    <div className="viewer" role="dialog" aria-modal="true" aria-label={`${card.title} — ảnh`}>
      <header className="viewer-bar">
        <button className="viewer-btn" onClick={closeOverlay}>✕ <span>Đóng</span></button>
        <span className="viewer-count">{idx + 1} / {total}</span>
        <div className="viewer-actions">
          <button className="viewer-btn" onClick={() => shareListing(card)} aria-label="Chia sẻ">⤴</button>
          <button className={`viewer-btn ${card.isFavorite ? 'is-on' : ''}`}
                  onClick={() => toggleFavorite(card.id)}
                  aria-label={card.isFavorite ? 'Bỏ lưu' : 'Lưu chỗ nghỉ'}
                  aria-pressed={!!card.isFavorite}>♥</button>
        </div>
      </header>

      <div className="viewer-stage">
        {total > 1 && (
          <button className="viewer-nav prev" onClick={() => slides.step(-1)} aria-label="Ảnh trước">‹</button>
        )}

        {card.images.map((src, i) =>
          // Only the photo on screen, its two neighbours and the one still
          // sliding away are worth downloading.
          slides.isMounted(i) || Math.abs(i - idx) === 1 || Math.abs(i - idx) === total - 1
            ? <img key={i} src={src} alt={`${card.title} — ảnh ${i + 1}`}
                   className={slides.frameClass(i)} decoding="async" />
            : <img key={i} alt="" aria-hidden="true" className="is-deferred" />
        )}

        {total > 1 && (
          <button className="viewer-nav next" onClick={() => slides.step(1)} aria-label="Ảnh tiếp theo">›</button>
        )}
      </div>

      <footer className="viewer-foot">
        <p className="viewer-caption">{PHOTO_CAPTIONS[idx] ?? `Ảnh ${idx + 1}`}</p>
        <button className="viewer-grid-link" onClick={() => set({ photoIndex: null })}>
          ⊞ Xem tất cả {total} ảnh
        </button>
      </footer>
    </div>
  );
}

/**
 * docs/01 TĐ-04 — grouped, with the amenities this place does not have kept in
 * the list and struck through rather than quietly omitted.
 */
export function AmenitiesModal() {
  const state = useStore();
  const d = state.detail;
  if (!d) return null;

  const groups = d.allAmenities.reduce((acc, a) => {
    (acc[a.group] ||= []).push(a);
    return acc;
  }, {});

  return (
    <Modal title="Nơi này có những gì">
      {Object.entries(groups).map(([group, items]) => (
        <section className="modal-section" key={group}>
          <h3>{group}</h3>
          <div style={{ display: 'grid', gap: 2, marginTop: 12 }}>
            {[...items].sort((a, b) => Number(b.available) - Number(a.available)).map(a => (
              <div className={`amenity ${a.available ? '' : 'is-missing'}`} key={a.key}
                   style={{ padding: '14px 0', borderBottom: '1px solid #f0f0f0' }}>
                <span className="ic"><AmenityIcon name={a.key} /></span><span>{a.label}</span>
              </div>
            ))}
          </div>
        </section>
      ))}
    </Modal>
  );
}

const REVIEW_SORTS = [['recent', 'Mới nhất'], ['high', 'Điểm cao nhất'], ['low', 'Điểm thấp nhất']];

export function ReviewsModal() {
  const state = useStore();
  const d = state.detail;
  if (!d) return null;

  const term = state.reviewQuery.trim().toLowerCase();
  const list = (term
    ? d.reviews.filter(r => r.text.toLowerCase().includes(term) || r.authorName.toLowerCase().includes(term))
    : d.reviews.slice()
  ).sort((a, b) =>
    state.reviewSort === 'high' ? b.rating - a.rating
      : state.reviewSort === 'low' ? a.rating - b.rating
        : 0);

  return (
    <Modal title={`★ ${d.card.rating.toFixed(2)} · ${d.reviews.length} đánh giá`} size="wide">
      <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 20 }}>
        <input type="search" className="field" style={{ flex: '1 1 220px' }} placeholder="Tìm trong đánh giá"
               value={state.reviewQuery} onChange={e => set({ reviewQuery: e.target.value })} />
        <select className="field" style={{ flex: '0 0 200px', width: 'auto' }}
                value={state.reviewSort} onChange={e => set({ reviewSort: e.target.value })}>
          {REVIEW_SORTS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
        </select>
      </div>
      <StarDistribution counts={d.ratingBreakdown.starCounts} total={d.reviews.length} />

      {!list.length && <p style={{ fontSize: 14, color: 'var(--ink-muted)' }}>Không có đánh giá nào khớp từ khoá.</p>}
      <div className="review-grid">
        {list.map(r => (
          <article className="review" key={r.id ?? `${r.authorName}-${r.when}`}>
            <div className="review-head">
              <span className="avatar" aria-hidden="true">{r.authorInitials}</span>
              <div style={{ minWidth: 0 }}>
                <div className="review-name">{r.authorName}</div>
                <div className="review-when">{r.authorLocation ? `${r.authorLocation} · ` : ''}{r.when}</div>
              </div>
              <span style={{ marginLeft: 'auto', fontSize: 13, fontWeight: 700 }}>★ {r.rating.toFixed(1)}</span>
            </div>
            <p>{r.text}</p>
            <HostReply review={r} />
          </article>
        ))}
      </div>
    </Modal>
  );
}

/* ---------------------------------------------------------------- checkout */

const CHECKOUT_STEPS = ['Chuyến đi', 'Thanh toán', 'Xác nhận'];

/**
 * docs/07 §2 — the list comes from the server so the payment page and the
 * saved-methods screen cannot disagree about what StayHost takes. The fallback
 * is the §2.1 group, which is what has to be there for the platform to work at
 * all. Manual bank transfer used to be offered here; §2.4 refuses it.
 */
const FALLBACK_METHODS = [
  { key: 'card', label: 'Thẻ tín dụng / ghi nợ', hint: 'Visa, Mastercard, JCB, American Express' },
  { key: 'napas', label: 'Thẻ ATM nội địa', hint: 'Qua NAPAS, cần đăng ký thanh toán trực tuyến' },
  { key: 'momo', label: 'Ví MoMo', hint: 'Mở ứng dụng MoMo để xác nhận' },
  { key: 'zalopay', label: 'ZaloPay', hint: 'Mở ứng dụng ZaloPay để xác nhận' }
];

export function CheckoutModal() {
  const state = useStore();
  const d = state.detail;
  const q = state.quote;
  const [busy, setBusy] = useState(false);
  /* docs/07 §7 — one key per attempt the guest makes, reused by every retry. */
  const attemptKey = useRef(null);

  // docs/01 ĐP-02 — moving past the trip step takes the dates off the market
  // for 15 minutes; walking away puts them straight back.
  useEffect(() => () => { releaseHold(); }, []);

  if (!d || !q) return null;

  const step = state.checkoutStep;
  const blocked = q.guestsExceeded || q.belowMinNights;
  const isRequest = !d.card.instantBook;

  const next = async () => {
    if (step === 0 && !isRequest && !state.held) {
      setBusy(true);
      const held = await holdDates({
        guestName: state.checkoutName || state.user?.fullName || null,
        guestEmail: state.checkoutEmail || state.user?.email || null,
        guestNote: state.checkoutNote || null
      });
      setBusy(false);
      if (!held) return;
    }
    set({ checkoutStep: step + 1 });
  };

  const confirm = async () => {
    const card = document.getElementById('card-number')?.value?.replace(/\D/g, '') ?? '';
    attemptKey.current ??= `pay-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
    const payment = {
      paymentMethod: state.payMethod,
      // docs/07 §4 — a saved card is picked, not retyped.
      cardLast4: state.payCardLast4 ?? (card.length >= 4 ? card.slice(-4) : null),
      // docs/01 ĐP-06 — a deposit now, the rest taken 14 days before check-in.
      payDeposit: state.payDeposit,
      depositAmount: state.payDeposit ? Math.ceil(q.total / 2) : null,
      // docs/07 §7 — the same key on a retry, so a lost reply cannot become a
      // second charge. New for each fresh attempt the guest makes themselves.
      idempotencyKey: attemptKey.current
    };

    setBusy(true);

    // docs/01 ĐP-07 — a split does not charge anyone here: it holds the dates
    // for a day and sends everybody, the organiser included, their own link.
    if (state.splitBill && !isRequest) {
      const emails = (state.splitEmails ?? '').split(/[,\s]+/).map(e => e.trim()).filter(e => e.includes('@'));
      const opened = await openSplit(emails);
      setBusy(false);
      if (opened) closeOverlay();
      return;
    }

    // A request to book has nothing to pay for yet — it goes to the host first.
    const result = isRequest
      ? await holdDates({
          guestName: state.checkoutName || state.user?.fullName || null,
          guestEmail: state.checkoutEmail || state.user?.email || null,
          guestNote: state.checkoutNote || null,
          ...payment
        })
      : await payHeld(payment);
    setBusy(false);

    if (result) { set({ bookingResult: result, held: null }); closeOverlay(); }
  };

  return (
    <Modal title="Đặt chỗ" foot={<>
      <div style={{ minWidth: 0 }}>
        <div style={{ fontSize: 16, fontWeight: 800 }}>
          {money(state.payDeposit ? Math.ceil(q.total / 2) : q.total)}
        </div>
        <div style={{ fontSize: 12, color: 'var(--ink-muted)' }}>
          {state.payDeposit ? `trả trước · tổng ${money(q.total)}` : `${q.nights} đêm · đã gồm thuế`}
        </div>
      </div>
      <div style={{ display: 'flex', gap: 10 }}>
        {step > 0 && <button className="btn btn-outline btn-sm" onClick={() => set({ checkoutStep: step - 1 })}>Quay lại</button>}
        {step < 2
          ? <button className="btn btn-primary btn-sm" disabled={blocked || busy} onClick={next}>
              {busy ? 'Đang giữ chỗ…' : 'Tiếp tục'}
            </button>
          : <button className="btn btn-primary btn-sm" disabled={blocked || busy} onClick={confirm}>
              {busy ? 'Đang xử lý…' : isRequest ? 'Gửi yêu cầu đặt' : 'Xác nhận và thanh toán'}
            </button>}
      </div>
    </>}>
      <HoldCountdown held={state.held} />
      <div className="stepper-bar">
        {CHECKOUT_STEPS.map((label, i) => (
          <div key={label} className={`step-dot ${i === step ? 'is-active' : ''} ${i < step ? 'is-done' : ''}`}>
            <span className="n">{i < step ? '✓' : i + 1}</span>{label}
          </div>
        ))}
      </div>

      <div style={{ display: 'flex', gap: 14, alignItems: 'center', padding: '18px 0', borderBottom: '1px solid var(--divider)' }}>
        <img src={d.card.images[0]} alt="" style={{ width: 96, height: 72, objectFit: 'cover', borderRadius: 12 }} />
        <div style={{ minWidth: 0 }}>
          <div style={{ fontSize: 15, fontWeight: 700 }}>{d.card.title}</div>
          <div style={{ fontSize: 13.5, color: 'var(--ink-muted)' }}>
            {d.card.city} · ★ {d.card.rating.toFixed(2)} ({d.card.reviewCount})
          </div>
        </div>
      </div>

      {step === 0 && <StepTrip q={q} />}
      {step === 1 && <StepPayment q={q} />}
      {step === 2 && <StepReview q={q} />}

      {blocked && (
        <div className="book-alert is-error">
          <b>Chưa đặt được</b>
          <span>{q.guestsExceeded
            ? `Chỗ nghỉ này nhận tối đa ${q.maxGuests} khách.`
            : `Chỗ nghỉ này yêu cầu tối thiểu ${q.minNights} đêm.`}</span>
        </div>
      )}

      {state.bookingError && (
        <div className="book-alert is-error"><b>Không đặt được</b><span>{state.bookingError}</span></div>
      )}
    </Modal>
  );
}

function StepTrip({ q }) {
  const state = useStore();

  return <>
    <section className="modal-section">
      <h3>Chuyến đi của bạn</h3>
      <div style={{ display: 'grid', gap: 12, marginTop: 14, fontSize: 14.5 }}>
        <div className="book-line">
          <span><b>Ngày</b><br />{longDate(state.checkIn)} – {longDate(state.checkOut)}</span>
          <button className="text-btn" onClick={() => openOverlay('dates')}>Chỉnh sửa</button>
        </div>
        <div className="book-line">
          <span><b>Khách</b><br />{q.guests} khách</span>
          <button className="text-btn" onClick={() => openOverlay('guests')}>Chỉnh sửa</button>
        </div>
      </div>
    </section>

    <section className="modal-section">
      <h3>Thông tin liên hệ</h3>
      <div style={{ marginTop: 14 }}>
        <label className="form-field"><span className="cap">Họ tên</span>
          <input type="text" placeholder="Nguyễn Văn A"
                 value={state.checkoutName || state.user?.fullName || ''}
                 onChange={e => set({ checkoutName: e.target.value })} /></label>
        <label className="form-field"><span className="cap">Email</span>
          <input type="email" placeholder="ban@email.com"
                 value={state.checkoutEmail || state.user?.email || ''}
                 onChange={e => set({ checkoutEmail: e.target.value })} /></label>
        <label className="form-field">
          <span className="cap">Lời nhắn cho chủ nhà <span style={{ fontWeight: 400 }}>(không bắt buộc)</span></span>
          <textarea rows={3} placeholder="Chúng mình đến khoảng 20:00…"
                    value={state.checkoutNote} onChange={e => set({ checkoutNote: e.target.value })}
                    style={{ width: '100%', padding: '12px 14px', border: '1px solid var(--line)', borderRadius: 12, fontSize: 14 }} />
        </label>
      </div>
    </section>

    <section className="modal-section">
      <h3>Chính sách huỷ</h3>
      <p style={{ fontSize: 14, lineHeight: 1.6, color: 'var(--ink-body)', margin: '10px 0 0' }}>
        <b>{q.cancellationTier}</b> — {q.cancellationSummary}
      </p>
    </section>
  </>;
}

/** Spend the guest's balance on this booking, against the room charge only. */
function CreditChoice({ q }) {
  const state = useStore();
  const [balance, setBalance] = useState(null);

  useEffect(() => {
    if (!state.user) return;
    api.wallet().then(w => setBalance(w.balance)).catch(() => setBalance(0));
  }, [state.user]);

  if (!balance) return null;

  const room = q.roomBeforeDiscount - q.roomDiscount;
  const usable = Math.min(balance, room);

  return (
    <button type="button" className={`opt ${state.useCredit ? 'is-on' : ''}`} style={{ marginTop: 18 }}
            onClick={() => set({ useCredit: !state.useCredit })}>
      <b>Dùng số dư {money(usable)}</b>
      <span>Bạn đang có {money(balance)}. Số dư chỉ trừ vào tiền phòng.</span>
    </button>
  );
}

/**
 * docs/01 ĐP-06 — half now and the rest automatically, but only when there is
 * still runway for a second charge. Inside two weeks of check-in the option is
 * not offered at all, which is what the rule says.
 */
function DepositChoice({ q }) {
  const state = useStore();
  const days = Math.round((parseIso(state.checkIn) - new Date()) / 86400000);
  if (days <= 14) return null;

  const half = Math.ceil(q.total / 2);
  const dueOn = parseIso(state.checkIn);
  dueOn.setDate(dueOn.getDate() - 14);

  return (
    <div style={{ display: 'grid', gap: 10, marginTop: 18 }}>
      <button type="button" className={`opt ${!state.payDeposit ? 'is-on' : ''}`}
              onClick={() => set({ payDeposit: false })}>
        <b>Trả toàn bộ {money(q.total)}</b><span>Xong luôn, không phải nhớ gì thêm</span>
      </button>
      <button type="button" className={`opt ${state.payDeposit ? 'is-on' : ''}`}
              onClick={() => set({ payDeposit: true })}>
        <b>Trả trước {money(half)}</b>
        <span>Phần còn lại {money(q.total - half)} tự động thu ngày {longDate(isoOf(dueOn))}</span>
      </button>
    </div>
  );
}

/**
 * docs/01 ĐP-07 — invite up to fifteen other people to pay their share. The
 * booking is only confirmed when the last one does, so this replaces paying
 * rather than sitting alongside it.
 */
function SplitChoice({ q }) {
  const state = useStore();
  const [emails, setEmails] = useState('');

  const people = emails.split(/[,\s]+/).map(e => e.trim()).filter(e => e.includes('@'));
  const each = people.length ? Math.floor(q.total / (people.length + 1)) : 0;

  return (
    <div style={{ marginTop: 18 }}>
      <button type="button" className={`opt ${state.splitBill ? 'is-on' : ''}`}
              onClick={() => set({ splitBill: !state.splitBill })}>
        <b>Chia hoá đơn với người khác</b>
        <span>Mỗi người nhận một liên kết và trả phần của mình</span>
      </button>

      {state.splitBill && <div style={{ marginTop: 12 }}>
        <label className="form-field">
          <span className="cap">Email những người cùng trả (cách nhau bằng dấu phẩy)</span>
          <input value={emails} placeholder="an@vidu.vn, binh@vidu.vn"
                 onChange={e => { setEmails(e.target.value); set({ splitEmails: e.target.value }); }} />
        </label>
        <p style={{ fontSize: 13, color: 'var(--ink-muted)', margin: 0, lineHeight: 1.6 }}>
          {people.length
            ? `${people.length + 1} người · mỗi người khoảng ${money(each)} · bạn trả phần lẻ`
            : `Tối đa ${16} người kể cả bạn.`}
          <br />Đơn chỉ được xác nhận khi tất cả đã trả, trong vòng 24 giờ.
        </p>
      </div>}
    </div>
  );
}

function StepPayment({ q }) {
  const state = useStore();
  const method = state.payMethod;
  const [offered, setOffered] = useState(FALLBACK_METHODS);
  const [refused, setRefused] = useState(null);
  const [cards, setCards] = useState([]);

  useEffect(() => {
    // The balance has its own control below, so it is not a method to pick here.
    api.paymentCatalogue()
      .then(d => {
        setOffered(d.methods.filter(m => m.key !== 'balance'));
        setRefused(d);
      })
      .catch(() => { /* the fallback list is the §2.1 group either way */ });

    api.savedCards().then(setCards).catch(() => setCards([]));
  }, []);

  const usable = cards.filter(c => !c.isExpired);

  return (
    <section className="modal-section">
      <h3>Chọn cách thanh toán</h3>
      <div style={{ display: 'grid', gap: 10, marginTop: 14 }}>
        {offered.map(m => (
          <button type="button" key={m.key} className={`opt ${method === m.key ? 'is-on' : ''}`}
                  onClick={() => set({ payMethod: m.key })}>
            <b>{m.label}</b><span>{m.hint}</span>
          </button>
        ))}
      </div>

      {/* docs/07 §4 — a guest who has saved a card should not retype it. */}
      {method === 'card' && !!usable.length && (
        <div style={{ display: 'grid', gap: 8, marginTop: 16 }}>
          <span className="cap">Thẻ đã lưu</span>
          {usable.map(c => (
            <button type="button" key={c.id}
                    className={`opt ${state.payCardId === c.id ? 'is-on' : ''}`}
                    onClick={() => set({ payCardId: c.id, payCardLast4: c.last4 })}>
              <b>{c.brandLabel} •••• {c.last4}</b><span>Hết hạn {c.expiry}</span>
            </button>
          ))}
          <button type="button" className={`opt ${state.payCardId ? '' : 'is-on'}`}
                  onClick={() => set({ payCardId: null, payCardLast4: null })}>
            <b>Dùng thẻ khác</b><span>Nhập số thẻ bên dưới</span>
          </button>
        </div>
      )}

      {method === 'card' && !state.payCardId && <>
        <div className="field-grid" style={{ marginTop: 18 }}>
          <label className="form-field" style={{ gridColumn: '1/-1' }}><span className="cap">Số thẻ</span>
            <input id="card-number" inputMode="numeric" placeholder="4242 4242 4242 4242" defaultValue="4242 4242 4242 4242" /></label>
          <label className="form-field"><span className="cap">Hết hạn</span>
            <input id="card-exp" placeholder="12/28" defaultValue="12/28" /></label>
          <label className="form-field"><span className="cap">CVV</span>
            <input id="card-cvv" inputMode="numeric" placeholder="123" defaultValue="123" /></label>
        </div>
        <p style={{ fontSize: 12.5, color: 'var(--ink-muted)', lineHeight: 1.5 }}>
          Bản demo dùng thẻ thử nghiệm, không có giao dịch thật nào được thực hiện.
        </p>
      </>}

      <CreditChoice q={q} />
      <DepositChoice q={q} />
      <SplitChoice q={q} />

      {method === 'momo' && (
        <p style={{ marginTop: 18, fontSize: 14, color: 'var(--ink-body)', lineHeight: 1.6 }}>
          Sau khi xác nhận, bạn sẽ được chuyển sang ứng dụng MoMo để hoàn tất thanh toán {money(q.total)}.
        </p>
      )}

      {method === 'zalopay' && (
        <p style={{ marginTop: 18, fontSize: 14, color: 'var(--ink-body)', lineHeight: 1.6 }}>
          Sau khi xác nhận, bạn sẽ được chuyển sang ứng dụng ZaloPay để hoàn tất thanh toán {money(q.total)}.
        </p>
      )}

      {/* docs/07 §2.4 — the ways StayHost does not take, with the one reason. */}
      {!!refused?.notAccepted?.length && (
        <p style={{ marginTop: 18, fontSize: 12.5, color: 'var(--ink-muted)', lineHeight: 1.6 }}>
          StayHost không nhận {refused.notAccepted.join(', ').toLowerCase()}. {refused.refusalReason}
        </p>
      )}
    </section>
  );
}

function StepReview({ q }) {
  const state = useStore();
  const method = FALLBACK_METHODS.find(m => m.key === state.payMethod);

  return <>
    <section className="modal-section">
      <h3>Kiểm tra lần cuối</h3>
      <div style={{ display: 'grid', gap: 12, marginTop: 14, fontSize: 14.5 }}>
        <div className="book-line"><span>Ngày</span><span>{longDate(state.checkIn)} – {longDate(state.checkOut)}</span></div>
        <div className="book-line"><span>Khách</span><span>{q.guests} khách</span></div>
        <div className="book-line"><span>Thanh toán bằng</span><span>{method?.label ?? 'Thẻ'}</span></div>
      </div>
    </section>

    <section className="modal-section">
      <h3>Chi tiết giá</h3>
      <PriceLines q={q} />
    </section>

    <section className="modal-section">
      <h3>Chính sách huỷ</h3>
      <p style={{ fontSize: 14, lineHeight: 1.6, color: 'var(--ink-body)', margin: '10px 0 0' }}>
        <b>{q.cancellationTier}</b> — {q.cancellationSummary}
      </p>
    </section>
  </>;
}

/**
 * docs/01 ĐP-02 — the guest can see exactly how long the dates are theirs.
 * Ticking every second is cheap and makes the deadline feel real.
 */
function HoldCountdown({ held }) {
  const [left, setLeft] = useState(() => remaining(held));

  useEffect(() => {
    if (!held?.holdExpiresAt) return undefined;
    const id = setInterval(() => setLeft(remaining(held)), 1000);
    return () => clearInterval(id);
  }, [held]);

  if (!held?.holdExpiresAt || left <= 0) return null;

  const mm = String(Math.floor(left / 60)).padStart(2, '0');
  const ss = String(left % 60).padStart(2, '0');

  return (
    <div className="book-alert" style={{ marginBottom: 4 }}>
      <b>Đang giữ chỗ cho bạn · {mm}:{ss}</b>
      <span>Hết giờ mà chưa thanh toán xong thì ngày sẽ được mở lại cho khách khác.</span>
    </div>
  );
}

const remaining = held =>
  held?.holdExpiresAt ? Math.max(0, Math.round((new Date(held.holdExpiresAt) - Date.now()) / 1000)) : 0;

/**
 * Renders whatever line items the quote carries. The server owns the running
 * order and the labels (docs/00 §6.8), so adding a rule there shows up here
 * without a front-end change.
 */
export function PriceLines({ q, className = '' }) {
  const state = useStore();
  const lines = q.lines ?? legacyLines(q);

  return (
    <div className={`book-lines ${className}`} style={{ marginTop: 14 }}>
      {lines.map((l, i) => (
        <div className="book-line" key={i} style={l.amount < 0 ? { color: 'var(--brand-dark)' } : undefined}>
          <u>{l.label}</u><span>{l.amount < 0 ? `−${money(-l.amount)}` : money(l.amount)}</span>
        </div>
      ))}
      <div className="book-rule" />
      <div className="book-total"><span>Tổng ({state.currency.code})</span><span>{money(q.total)}</span></div>
    </div>
  );
}

/** Fallback for quotes from before the server started sending `lines`. */
function legacyLines(q) {
  const out = [
    { label: `${money(q.pricePerNight)} × ${q.nights} đêm`, amount: q.subtotal + (q.lengthDiscount ?? 0) }
  ];
  if (q.lengthDiscount > 0) out.push({ label: `Giảm giá ở dài ngày (${q.lengthDiscountPercent}%)`, amount: -q.lengthDiscount });
  out.push({ label: 'Phí dọn dẹp', amount: q.cleaningFee });
  out.push({ label: 'Phí dịch vụ StayHost', amount: q.serviceFee });
  if (q.tax) out.push({ label: 'Thuế', amount: q.tax });
  return out;
}

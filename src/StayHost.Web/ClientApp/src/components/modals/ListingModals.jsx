import { useStore } from '../../lib/useStore.js';
import { useEffect, useState } from 'react';
import { set, holdDates, payHeld, releaseHold, openOverlay, closeOverlay } from '../../lib/store.js';
import { money, longDate } from '../../lib/format.js';
import { AmenityIcon } from '../Icon.jsx';
import { HostReply, StarDistribution } from '../../pages/Detail.jsx';
import { Modal } from './Modal.jsx';

const PHOTO_CAPTIONS = ['Ảnh chính', 'Phòng khách', 'Phòng ngủ', 'Không gian ngoài trời', 'Phòng tắm'];

export function PhotosModal() {
  const state = useStore();
  const c = state.detail?.card;
  if (!c) return null;

  const index = state.photoIndex;

  // A focused index turns the grid into a single-photo viewer with arrows.
  if (index != null) {
    const total = c.images.length;
    const step = dir => set({ photoIndex: Math.min(total - 1, Math.max(0, index + dir)) });

    return (
      <Modal title={`${index + 1} / ${total}`} size="wide" foot={<>
        <button className="text-btn" onClick={() => set({ photoIndex: null })}>← Xem dạng lưới</button>
        <span style={{ fontSize: 13, color: 'var(--ink-muted)' }}>{index + 1} trong {total} ảnh</span>
      </>}>
        <div className="lightbox-stage">
          <button className="lightbox-nav prev" onClick={() => step(-1)} aria-label="Ảnh trước" disabled={index === 0}>‹</button>
          <img src={c.images[index]} alt={`${c.title} — ảnh ${index + 1}`} />
          <button className="lightbox-nav next" onClick={() => step(1)} aria-label="Ảnh tiếp theo" disabled={index === total - 1}>›</button>
        </div>
        <p className="lightbox-caption">{PHOTO_CAPTIONS[index] ?? `Ảnh ${index + 1}`}</p>
        <div className="lightbox-strip">
          {c.images.map((src, i) => (
            <button key={i} className={`strip-thumb ${i === index ? 'is-on' : ''}`}
                    onClick={() => set({ photoIndex: i })} aria-label={`Xem ảnh ${i + 1}`}>
              <img src={src} alt="" loading="lazy" />
            </button>
          ))}
        </div>
      </Modal>
    );
  }

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

const PAY_METHODS = [
  ['card', 'Thẻ tín dụng / ghi nợ', 'Visa, Mastercard, JCB'],
  ['momo', 'Ví MoMo', 'Thanh toán qua ứng dụng MoMo'],
  ['bank', 'Chuyển khoản ngân hàng', 'Chuyển khoản nhanh 24/7']
];

export function CheckoutModal() {
  const state = useStore();
  const d = state.detail;
  const q = state.quote;
  const [busy, setBusy] = useState(false);

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
    const payment = { paymentMethod: state.payMethod, cardLast4: card.length >= 4 ? card.slice(-4) : null };

    setBusy(true);
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
        <div style={{ fontSize: 16, fontWeight: 800 }}>{money(q.total)}</div>
        <div style={{ fontSize: 12, color: 'var(--ink-muted)' }}>{q.nights} đêm · đã gồm thuế</div>
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

function StepPayment({ q }) {
  const state = useStore();
  const method = state.payMethod;

  return (
    <section className="modal-section">
      <h3>Chọn cách thanh toán</h3>
      <div style={{ display: 'grid', gap: 10, marginTop: 14 }}>
        {PAY_METHODS.map(([key, label, hint]) => (
          <button type="button" key={key} className={`opt ${method === key ? 'is-on' : ''}`}
                  onClick={() => set({ payMethod: key })}>
            <b>{label}</b><span>{hint}</span>
          </button>
        ))}
      </div>

      {method === 'card' && <>
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

      {method === 'momo' && (
        <p style={{ marginTop: 18, fontSize: 14, color: 'var(--ink-body)', lineHeight: 1.6 }}>
          Sau khi xác nhận, bạn sẽ được chuyển sang ứng dụng MoMo để hoàn tất thanh toán {money(q.total)}.
        </p>
      )}

      {method === 'bank' && (
        <div style={{ marginTop: 18, padding: 16, background: 'var(--surface-soft)', borderRadius: 12, fontSize: 14, lineHeight: 1.7 }}>
          <div>Ngân hàng: <b>Vietcombank</b></div>
          <div>Số tài khoản: <b>0071 0009 8765</b></div>
          <div>Chủ tài khoản: <b>CONG TY STAYHOST</b></div>
          <div>Nội dung: <b>{state.user?.initials ?? 'SH'} {q.listingId}</b></div>
        </div>
      )}
    </section>
  );
}

function StepReview({ q }) {
  const state = useStore();
  const method = PAY_METHODS.find(m => m[0] === state.payMethod);

  return <>
    <section className="modal-section">
      <h3>Kiểm tra lần cuối</h3>
      <div style={{ display: 'grid', gap: 12, marginTop: 14, fontSize: 14.5 }}>
        <div className="book-line"><span>Ngày</span><span>{longDate(state.checkIn)} – {longDate(state.checkOut)}</span></div>
        <div className="book-line"><span>Khách</span><span>{q.guests} khách</span></div>
        <div className="book-line"><span>Thanh toán bằng</span><span>{method?.[1] ?? 'Thẻ'}</span></div>
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

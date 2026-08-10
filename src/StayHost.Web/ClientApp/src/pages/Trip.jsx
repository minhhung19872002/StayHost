import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { loadTrip, set, requireAuth, toast, payBalance } from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate, dateTime } from '../lib/format.js';
import { Icon } from '../components/Icon.jsx';
import { CardCarousel } from '../components/CardCarousel.jsx';
import { Deadline, previewCancel, openReview } from './Trips.jsx';
import { duration } from './Experiences.jsx';
import { t } from '../lib/i18n.js';

const PAYMENT = {
  Pending: 'Đang chờ',
  Authorized: 'Đã giữ tiền',
  Captured: 'Đã thanh toán',
  Refunded: 'Đã hoàn tiền',
  Failed: 'Thất bại'
};

export function Trip() {
  const state = useStore();
  const { id } = useParams();
  const navigate = useNavigate();

  useEffect(() => { loadTrip(Number(id)); }, [id]);

  const b = state.trip;

  if (state.tripLoading || !b) {
    return (
      <div className="shell" style={{ paddingBlock: '32px 90px' }}>
        <div className="sk-line skeleton" style={{ width: 280, height: 26 }} />
        <div className="skeleton" style={{ height: 220, borderRadius: 16, marginTop: 20 }} />
      </div>
    );
  }

  const messageHost = async () => {
    if (!requireAuth()) return;
    try {
      const thread = await api.sendMessage({
        listingId: b.listingId,
        body: 'Chào bạn, mình muốn hỏi thêm về chỗ nghỉ.'
      });
      set({ activeThread: thread });
      navigate('/messages');
    } catch (err) { toast(err.message); }
  };

  return (
    <div className="shell" style={{ paddingBlock: '24px 90px' }}>
      <button className="back-link" onClick={() => navigate('/trips')}>← {t('Tất cả chuyến đi')}</button>

      <div className="page-head" style={{ marginTop: 10 }}>
        <div>
          <h1 className="section-title">{b.listingTitle}</h1>
          <p className="section-sub">{b.listingCity} · {t('mã đặt chỗ')} <b>{b.reference}</b></p>
        </div>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <span className={`badge ${b.statusBadge}`}>{b.statusLabel}</span>
          <Deadline booking={b} />
        </div>
      </div>

      <Countdown booking={b} />

      <div className="trip-layout">
        <div style={{ minWidth: 0 }}>
          <section className="detail-section" style={{ paddingTop: 0 }}>
            <h2>{t('Chuyến đi của bạn')}</h2>
            <div className="kv-grid">
              {/* docs/01 CĐ-03 — the listing's own hours, not a hardcoded pair. */}
              <Kv label={t('Nhận phòng')} value={longDate(b.checkIn)} hint={arrivalHint(b, 'in')} />
              <Kv label={t('Trả phòng')} value={longDate(b.checkOut)} hint={arrivalHint(b, 'out')} />
              <Kv label={t('Số đêm')} value={`${b.nights} ${t('đêm')}`} />
              <Kv label={t('Khách')} value={`${b.guests} ${t('khách')}`} />
              <Kv label={t('Chủ nhà')} value={b.hostName} />
              <Kv label={t('Đặt lúc')} value={longDate(b.createdAt.slice(0, 10))} />
            </div>
            {b.guestNote && (
              <p style={{ margin: '16px 0 0', fontSize: 14, color: 'var(--ink-body)' }}>
                <b>{t('Lời nhắn:')}</b> {b.guestNote}
              </p>
            )}
          </section>

          <CheckInSection booking={b} />

          <CrossSell booking={b} />

          <section className="detail-section">
            <h2>{t('Chính sách huỷ')}</h2>
            <p style={{ margin: 0, fontSize: 14.5, lineHeight: 1.6, color: 'var(--ink-body)' }}>
              <b>{b.cancellationTier}</b> — {b.cancellationSummary}
            </p>
            {b.refundedAmount > 0 && (
              <p style={{ margin: '12px 0 0', fontSize: 14, color: 'var(--brand-dark)' }}>
                {t('Đã hoàn')} {money(b.refundedAmount)} {t('về phương thức thanh toán ban đầu.')}
              </p>
            )}
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 16 }}>
              {b.canCancel && (
                <button className="btn btn-outline btn-sm"
                        onClick={() => previewCancel(b.id)}>{t('Huỷ chuyến đi')}</button>
              )}
              {/* docs/01 CĐ-12 — get help scoped to this exact booking. */}
              <button className="btn btn-outline btn-sm"
                      onClick={() => navigate('/resolutions', { state: { bookingId: b.id } })}>
                {t('Cần trợ giúp với đơn này')}
              </button>
            </div>
          </section>

          <ChangeTrip booking={b} />

          <ShieldPanel booking={b} />

          <Balance booking={b} />

          <History events={b.history} />

          <section className="detail-section">
            <h2>{t('Hỗ trợ')}</h2>
            <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
              <button className="btn btn-outline btn-sm" onClick={messageHost}>{t('Nhắn tin cho chủ nhà')}</button>
              <button className="btn btn-outline btn-sm" onClick={() => navigate(`/rooms/${b.listingSlug}`)}>{t('Xem chỗ nghỉ')}</button>
              {b.canReview && <button className="btn btn-primary btn-sm" onClick={() => openReview(b)}>{t('Viết đánh giá')}</button>}
            </div>
          </section>
        </div>

        <Receipt booking={b} />
      </div>
    </div>
  );
}

/**
 * docs/01 CĐ-02 — how long until they are standing at the door. Counted from
 * the listing's own check-in hour when the guide has told us one, because a
 * countdown that says "2 days" while the door opens at 14:00 is off by half a
 * day for anybody catching a morning flight.
 */
function Countdown({ booking }) {
  const [now, setNow] = useState(() => Date.now());

  const live = booking.status === 'Confirmed';
  useEffect(() => {
    if (!live) return undefined;
    const timer = setInterval(() => setNow(Date.now()), 60_000);
    return () => clearInterval(timer);
  }, [live]);

  if (!live) return null;

  const hour = booking.checkInGuide?.windowLabel?.match(/(\d{2}):(\d{2})/);
  const opensAt = new Date(`${booking.checkIn}T${hour ? `${hour[1]}:${hour[2]}` : '14:00'}:00`);
  const minutes = Math.round((opensAt - now) / 60_000);

  if (minutes <= 0) return null;

  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);

  const left = days > 0
    ? `${days} ${t('ngày')}${hours ? ` ${hours} ${t('giờ')}` : ''}`
    : hours > 0 ? `${hours} ${t('giờ')} ${minutes % 60} ${t('phút')}` : `${minutes} ${t('phút')}`;

  return (
    <div className="countdown">
      <span className="cap">{t('Còn')}</span>
      <b>{left}</b>
      <span>{t('nữa là tới ngày nhận phòng')}</span>
    </div>
  );
}

/**
 * docs/01 CĐ-03 — "Nhận phòng 14:00 – 22:00 · Trả phòng trước 12:00" split back
 * into the two halves the summary grid shows. Falls back to nothing rather than
 * to an invented hour when the guide is still withheld.
 */
function arrivalHint(booking, side) {
  const label = booking.checkInGuide?.windowLabel;
  if (!label) return undefined;
  const [arrive, leave] = label.split(' · ');
  return side === 'in' ? arrive : leave;
}

/**
 * docs/01 CĐ-03 — how to get in, what the wifi is, how the appliances work.
 * docs/03 §10 keeps the whole section off an unconfirmed booking, and the door
 * code off it until 48 hours before check-in (CĐ-04) — both decided by the
 * server, so this component renders what it is given and withholds nothing
 * itself.
 */
function CheckInSection({ booking }) {
  const g = booking.checkInGuide;
  if (!g) return null;

  return (
    <section className="detail-section">
      <h2>{t('Hướng dẫn nhận phòng')}</h2>

      <p className="guide-window">{g.windowLabel}</p>

      <div className="kv-grid">
        <Kv label={t('Cách vào nhà')} value={g.methodLabel} />
        {g.addressLine && <Kv label={t('Địa chỉ')} value={g.addressLine} />}
        {g.hostPhone && <Kv label={t('Điện thoại chủ nhà')} value={g.hostPhone} />}
        {g.wifiName && <Kv label="Wifi" value={g.wifiName} hint={g.wifiPassword ? `${t('Mật khẩu:')} ${g.wifiPassword}` : undefined} />}
      </div>

      {/* docs/01 CĐ-04 — either the code, or when it will be here. */}
      {g.doorCodeExpected && (
        <div className={`door-code ${g.doorCode ? 'is-ready' : ''}`}>
          <span className="cap">{t('Mã cửa')}</span>
          {g.doorCode
            ? <b>{g.doorCode}</b>
            : <span className="door-code-wait">{g.doorCodeNote}</span>}
        </div>
      )}

      {g.addressLine && (
        <a className="btn btn-outline btn-sm" style={{ marginTop: 14 }} target="_blank" rel="noreferrer"
           href={`https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(g.addressLine)}`}>
          {t('Chỉ đường')}
        </a>
      )}

      {g.directions && <p className="guide-note">{g.directions}</p>}

      {!!g.applianceNotes.length && <>
        <h3 className="guide-sub">{t('Hướng dẫn thiết bị')}</h3>
        <ul className="guide-list">
          {g.applianceNotes.map(n => <li key={n}>{n}</li>)}
        </ul>
      </>}
    </section>
  );
}

/** A card that is really a link, the way the browse grids build theirs. */
const CARD_BUTTON = { textAlign: 'left', border: 0, background: 'none', padding: 0, cursor: 'pointer' };

const stars = x => (x.reviewCount ? `★ ${x.rating.toFixed(2)} (${x.reviewCount})` : `★ ${t('Mới')}`);

/**
 * docs/09 §4 (MR-C-02) — the cross-sell, which §4 calls the main revenue channel
 * for experiences and services: somebody who has already booked a stay is told
 * what is on in that city on the days they are there.
 *
 * Which ones are relevant is the server's decision (city, dates, seats left,
 * moderation) — this renders what it is handed and shows nothing at all when the
 * answer is empty, rather than an apologetic box on every trip page.
 */
function CrossSell({ booking }) {
  const navigate = useNavigate();
  const [data, setData] = useState(null);

  useEffect(() => {
    let live = true;
    setData(null);
    api.tripSuggestions(booking.id)
      .then(r => { if (live) setData(r); })
      // A failed suggestion is not worth a toast on somebody's trip page.
      .catch(() => { if (live) setData(null); });
    return () => { live = false; };
  }, [booking.id]);

  const experiences = data?.experiences ?? [];
  const services = data?.services ?? [];
  if (!experiences.length && !services.length) return null;

  return (
    <>
      {!!experiences.length && (
        <section className="detail-section">
          <h2>{t('Trải nghiệm quanh chỗ bạn ở')}</h2>
          <p className="section-sub">
            {t('Còn chỗ đúng những ngày bạn ở')} {booking.listingCity}.
          </p>
          <div className="card-grid" style={{ marginTop: 18 }}>
            {experiences.map(x => (
              <button className="card" key={x.id} style={CARD_BUTTON}
                      onClick={() => navigate(`/experiences/${x.slug}`)}>
                <CardCarousel images={x.images} alt={x.title} />
                <div className="card-body">
                  <div className="card-row">
                    <h3 className="card-title">{x.title}</h3>
                    <div className="card-rating">{stars(x)}</div>
                  </div>
                  <div className="card-sub card-line">{x.city} · {duration(x.durationMinutes)}</div>
                  <div className="card-price">
                    <b>{money(x.pricePerPerson)}</b> <span>/ {t('người')}</span>
                  </div>
                  <div className="card-perks card-line">
                    {x.openSlots
                      ? `${t('Còn')} ${x.openSlots} ${t('suất')} ${t('trong chuyến của bạn')}`
                      : t('Tạm hết suất')}
                  </div>
                </div>
              </button>
            ))}
          </div>
        </section>
      )}

      {!!services.length && (
        <section className="detail-section">
          <h2>{t('Dịch vụ tới tận chỗ bạn ở')}</h2>
          <p className="section-sub">
            {t('Đầu bếp, chụp ảnh, massage — chọn khung giờ trong những ngày bạn ở đây.')}
          </p>
          <div className="card-grid" style={{ marginTop: 18 }}>
            {services.map(s => (
              <button className="card" key={s.id} style={CARD_BUTTON}
                      onClick={() => navigate(`/services/${s.slug}`)}>
                <CardCarousel images={s.images} alt={s.title} />
                <div className="card-body">
                  <div className="card-row">
                    <h3 className="card-title">{s.title}</h3>
                    <div className="card-rating">{stars(s)}</div>
                  </div>
                  <div className="card-sub card-line">{s.city} · {s.pricingLabel}</div>
                  <div className="card-price">
                    <b>{money(s.basePrice)}</b> <span>/ {s.unit}</span>
                  </div>
                  <div className="card-perks card-line">
                    {s.travelsToGuest
                      ? `${t('Tới tận nơi trong')} ${s.serviceRadiusKm} km`
                      : t('Khách tới chỗ cung cấp')}
                    {s.isPartner ? ` · ${t('qua')} ${s.partnerName}` : ''}
                  </div>
                </div>
              </button>
            ))}
          </div>
        </section>
      )}
    </>
  );
}

const ACTOR = { system: 'Hệ thống', guest: 'Bạn', host: 'Chủ nhà', admin: 'StayHost' };

/**
 * docs/00 §6.2 — every state change is a row, never an overwrite. Showing it
 * is what makes that guarantee worth anything to the guest.
 */
/**
 * docs/06 AT-06-02 — the way in to StayShield, and only while it can be used:
 * from check-in until 72 hours after it. Outside that window the guest still
 * has the resolution centre, so the button says so rather than vanishing.
 */
/**
 * docs/01 CĐ-06, docs/04 QT-4 — ask the host to move the stay to new dates or a
 * new guest count. The request shows the price difference; the old dates stay
 * held until the host accepts.
 */
function ChangeTrip({ booking }) {
  const [open, setOpen] = useState(false);
  const [checkIn, setCheckIn] = useState(booking.checkIn);
  const [checkOut, setCheckOut] = useState(booking.checkOut);
  const [guests, setGuests] = useState(booking.guests);
  const [pending, setPending] = useState(null);
  const [busy, setBusy] = useState(false);

  const canChange = booking.status === 'Confirmed' || booking.status === 'PendingHostApproval';
  if (!canChange) return null;

  const submit = async () => {
    setBusy(true);
    try {
      const r = await api.requestChange(booking.id, {
        checkIn, checkOut, guests: Number(guests), adults: Number(guests)
      });
      setPending(r);
      toast('Đã gửi yêu cầu đổi lịch, chờ chủ nhà duyệt trong 24 giờ.');
    } catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  const withdraw = async () => {
    try { await api.withdrawChange(booking.id, pending.id); setPending(null); toast('Đã rút yêu cầu.'); }
    catch (err) { toast(err.message); }
  };

  return (
    <section className="trip-section">
      <h2>{t('Đổi ngày hoặc số khách')}</h2>
      {pending ? (
        <div>
          <p style={{ fontSize: 14.5 }}>
            {t('Đã gửi:')} {longDate(pending.newCheckIn)} → {longDate(pending.newCheckOut)} · {pending.newGuests} {t('khách')}.
            <br />{pending.differenceLabel}
          </p>
          <button className="btn btn-outline btn-sm" style={{ marginTop: 10 }} onClick={withdraw}>{t('Rút yêu cầu')}</button>
        </div>
      ) : !open ? (
        <button className="btn btn-outline btn-sm" onClick={() => setOpen(true)}>{t('Yêu cầu đổi lịch')}</button>
      ) : (
        <div style={{ display: 'grid', gap: 10, maxWidth: 360 }}>
          <label className="form-field"><span className="cap">{t('Nhận phòng')}</span>
            <input type="date" value={checkIn} onChange={e => setCheckIn(e.target.value)} /></label>
          <label className="form-field"><span className="cap">{t('Trả phòng')}</span>
            <input type="date" value={checkOut} onChange={e => setCheckOut(e.target.value)} /></label>
          <label className="form-field"><span className="cap">{t('Số khách')}</span>
            <input type="number" min="1" value={guests} onChange={e => setGuests(e.target.value)} /></label>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
            <button className="btn btn-outline btn-sm" onClick={() => setOpen(false)}>{t('Huỷ')}</button>
            <button className="btn btn-primary btn-sm" disabled={busy} onClick={submit}>{t('Gửi yêu cầu')}</button>
          </div>
        </div>
      )}
    </section>
  );
}

function ShieldPanel({ booking }) {
  const navigate = useNavigate();
  const [claims, setClaims] = useState(null);

  useEffect(() => {
    api.shieldClaims()
      .then(rows => setClaims(rows.filter(c => c.bookingId === booking.id)))
      .catch(() => setClaims([]));
  }, [booking.id]);

  const checkIn = new Date(`${booking.checkIn}T14:00:00Z`);
  const hoursIn = (Date.now() - checkIn.getTime()) / 3_600_000;
  const live = booking.status === 'InProgress' || booking.status === 'Completed'
    || booking.status === 'Confirmed';
  const open = live && hoursIn >= 0 && hoursIn <= 72;

  if (!live) return null;

  return (
    <section className="detail-section">
      <h2>{t('Chỗ ở có vấn đề?')}</h2>

      {claims?.length ? (
        <>
          <p style={{ margin: '0 0 12px', fontSize: 14.5, color: 'var(--ink-body)' }}>
            {t('Bạn đã mở hồ sơ')} {claims[0].reference} — {claims[0].statusLabel}.
          </p>
          <button className="btn btn-outline btn-sm"
                  onClick={() => navigate(`/shield/${claims[0].id}`)}>{t('Xem hồ sơ')}</button>
        </>
      ) : open ? (
        <>
          <p style={{ margin: '0 0 12px', fontSize: 14.5, lineHeight: 1.7, color: 'var(--ink-body)' }}>
            {t('Không vào được, chỗ ở khác xa mô tả hoặc không ở được? StayHost tìm chỗ khác hoặc hoàn tiền cho bạn. Báo trong 72 giờ đầu kể từ giờ nhận phòng.')}
          </p>
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
            <button className="btn btn-primary btn-sm"
                    onClick={() => set({ shieldBooking: booking, shieldSide: 'guest', overlay: 'shield' })}>
              {t('Báo vấn đề')}
            </button>
            <button className="btn btn-outline btn-sm"
                    onClick={() => navigate('/shield/terms')}>{t('StayShield là gì')}</button>
          </div>
        </>
      ) : (
        <p style={{ margin: 0, fontSize: 14.5, lineHeight: 1.7, color: 'var(--ink-muted)' }}>
          {t('Cửa sổ 72 giờ của StayShield đã khép. Bạn vẫn mở được yêu cầu ở')}{' '}
          <button className="text-btn" onClick={() => navigate('/resolutions')}>{t('Trung tâm giải quyết')}</button>.
        </p>
      )}
    </section>
  );
}

/** docs/01 ĐP-06 — what is still owed on a part-paid booking, and when. */
function Balance({ booking }) {
  const [busy, setBusy] = useState(false);
  if (booking.balanceStatus === 'None') return null;

  const settled = booking.balanceDue <= 0;

  return (
    <section className="detail-section">
      <h2>{t('Thanh toán')}</h2>
      <div className="kv-grid">
        <Kv label={t('Đã trả')} value={money(booking.depositPaid)} />
        <Kv label={t('Còn phải trả')} value={money(booking.balanceDue)}
            hint={booking.balanceDueOn ? `${t('Thu tự động ngày')} ${longDate(booking.balanceDueOn)}` : undefined} />
      </div>
      <p style={{ margin: '12px 0 0', fontSize: 14, color: 'var(--ink-body)' }}>{booking.balanceLabel}</p>

      {!settled && (
        <button className="btn btn-primary btn-sm" style={{ marginTop: 16 }} disabled={busy}
                onClick={async () => { setBusy(true); await payBalance(booking.id); setBusy(false); }}>
          {busy ? t('Đang xử lý…') : `${t('Trả nốt')} ${money(booking.balanceDue)} ${t('ngay')}`}
        </button>
      )}
    </section>
  );
}

function History({ events }) {
  if (!events?.length) return null;

  return (
    <section className="detail-section">
      <h2>{t('Lịch sử đơn')}</h2>
      <div style={{ display: 'grid', gap: 10 }}>
        {events.map((e, i) => (
          <div className="cal-row" key={i}>
            <span className="badge pending">{dateTime(e.at)}</span>
            <div style={{ flex: 1, minWidth: 0, fontSize: 13.5 }}>
              <b>{e.fromLabel ? `${e.fromLabel} → ${e.toLabel}` : e.toLabel}</b>
              {e.reason && <span style={{ color: 'var(--ink-muted)' }}> · {e.reason}</span>}
            </div>
            <span style={{ fontSize: 12.5, color: 'var(--ink-muted)' }}>
              {t(ACTOR[e.actor.split(':')[0]] ?? e.actor)}
            </span>
          </div>
        ))}
      </div>
    </section>
  );
}

function Kv({ label, value, hint }) {
  return (
    <div className="kv">
      <span className="kv-label">{label}</span>
      <b>{value}</b>
      {hint && <span className="kv-hint">{hint}</span>}
    </div>
  );
}

function Receipt({ booking: b }) {
  // The receipt mirrors whatever line items the booking was priced with, so a
  // change in Pricing.cs shows up on old and new receipts alike.
  const lines = b.lines ?? [
    { label: `${t('Tiền phòng')} · ${b.nights} ${t('đêm')}`, amount: b.subtotal },
    { label: t('Phí dọn dẹp'), amount: b.cleaningFee },
    { label: t('Phí dịch vụ StayHost'), amount: b.serviceFee },
    ...(b.tax ? [{ label: t('Thuế'), amount: b.tax }] : [])
  ];

  return (
    <aside className="receipt" id="receipt">
      <div className="receipt-head">
        <span className="brand-mark" aria-hidden="true">S</span>
        <div><b>{t('Hoá đơn StayHost')}</b><span>{b.reference}</span></div>
      </div>

      <img src={b.listingImage} alt="" className="receipt-image" />

      <div className="book-lines" style={{ marginTop: 16 }}>
        {lines.map((l, i) => (
          <div className="book-line" key={i} style={l.amount < 0 ? { color: 'var(--brand-dark)' } : undefined}>
            <span>{l.label}</span><span>{l.amount < 0 ? `−${money(-l.amount)}` : money(l.amount)}</span>
          </div>
        ))}
        <div className="book-rule" />
        <div className="book-total"><span>{t('Tổng cộng')}</span><span>{money(b.total)}</span></div>
        {b.refundedAmount > 0 && (
          <div className="book-line" style={{ color: 'var(--brand-dark)' }}>
            <span>{t('Đã hoàn')}</span><span>−{money(b.refundedAmount)}</span>
          </div>
        )}
      </div>

      <div className="receipt-meta">
        <div><Icon name="star" size={15} /> {t(PAYMENT[b.paymentStatus] ?? b.paymentStatus)}</div>
        {b.paymentReference && <div>{t('Mã giao dịch')} {b.paymentReference}</div>}
        {b.cardLast4 && <div>{t('Thẻ')} •••• {b.cardLast4}</div>}
      </div>

      {/* docs/01 ĐP-14 — a proper invoice document, opened in its own tab so it
          can be saved or printed on its own rather than the whole trip page. */}
      <a className="btn btn-dark btn-block btn-sm" style={{ marginTop: 18 }}
         href={`/api/bookings/${b.id}/invoice`} target="_blank" rel="noreferrer">
        {t('Tải hoá đơn')}
      </a>
    </aside>
  );
}

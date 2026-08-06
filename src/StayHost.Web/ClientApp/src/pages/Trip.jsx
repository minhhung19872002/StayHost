import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { loadTrip, set, requireAuth, toast, payBalance } from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate, dateTime } from '../lib/format.js';
import { Icon } from '../components/Icon.jsx';
import { Deadline, previewCancel, openReview } from './Trips.jsx';

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
      <button className="back-link" onClick={() => navigate('/trips')}>← Tất cả chuyến đi</button>

      <div className="page-head" style={{ marginTop: 10 }}>
        <div>
          <h1 className="section-title">{b.listingTitle}</h1>
          <p className="section-sub">{b.listingCity} · mã đặt chỗ <b>{b.reference}</b></p>
        </div>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <span className={`badge ${b.statusBadge}`}>{b.statusLabel}</span>
          <Deadline booking={b} />
        </div>
      </div>

      <div className="trip-layout">
        <div style={{ minWidth: 0 }}>
          <section className="detail-section" style={{ paddingTop: 0 }}>
            <h2>Chuyến đi của bạn</h2>
            <div className="kv-grid">
              <Kv label="Nhận phòng" value={longDate(b.checkIn)} hint="Sau 14:00" />
              <Kv label="Trả phòng" value={longDate(b.checkOut)} hint="Trước 12:00" />
              <Kv label="Số đêm" value={`${b.nights} đêm`} />
              <Kv label="Khách" value={`${b.guests} khách`} />
              <Kv label="Chủ nhà" value={b.hostName} />
              <Kv label="Đặt lúc" value={longDate(b.createdAt.slice(0, 10))} />
            </div>
            {b.guestNote && (
              <p style={{ margin: '16px 0 0', fontSize: 14, color: 'var(--ink-body)' }}>
                <b>Lời nhắn:</b> {b.guestNote}
              </p>
            )}
          </section>

          <section className="detail-section">
            <h2>Chính sách huỷ</h2>
            <p style={{ margin: 0, fontSize: 14.5, lineHeight: 1.6, color: 'var(--ink-body)' }}>
              <b>{b.cancellationTier}</b> — {b.cancellationSummary}
            </p>
            {b.refundedAmount > 0 && (
              <p style={{ margin: '12px 0 0', fontSize: 14, color: 'var(--brand-dark)' }}>
                Đã hoàn {money(b.refundedAmount)} về phương thức thanh toán ban đầu.
              </p>
            )}
            {b.canCancel && (
              <button className="btn btn-outline btn-sm" style={{ marginTop: 16 }}
                      onClick={() => previewCancel(b.id)}>Huỷ chuyến đi</button>
            )}
          </section>

          <Balance booking={b} />

          <History events={b.history} />

          <section className="detail-section">
            <h2>Hỗ trợ</h2>
            <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
              <button className="btn btn-outline btn-sm" onClick={messageHost}>Nhắn tin cho chủ nhà</button>
              <button className="btn btn-outline btn-sm" onClick={() => navigate(`/rooms/${b.listingSlug}`)}>Xem chỗ nghỉ</button>
              {b.canReview && <button className="btn btn-primary btn-sm" onClick={() => openReview(b)}>Viết đánh giá</button>}
            </div>
          </section>
        </div>

        <Receipt booking={b} />
      </div>
    </div>
  );
}

const ACTOR = { system: 'Hệ thống', guest: 'Bạn', host: 'Chủ nhà', admin: 'StayHost' };

/**
 * docs/00 §6.2 — every state change is a row, never an overwrite. Showing it
 * is what makes that guarantee worth anything to the guest.
 */
/** docs/01 ĐP-06 — what is still owed on a part-paid booking, and when. */
function Balance({ booking }) {
  const [busy, setBusy] = useState(false);
  if (booking.balanceStatus === 'None') return null;

  const settled = booking.balanceDue <= 0;

  return (
    <section className="detail-section">
      <h2>Thanh toán</h2>
      <div className="kv-grid">
        <Kv label="Đã trả" value={money(booking.depositPaid)} />
        <Kv label="Còn phải trả" value={money(booking.balanceDue)}
            hint={booking.balanceDueOn ? `Thu tự động ngày ${longDate(booking.balanceDueOn)}` : undefined} />
      </div>
      <p style={{ margin: '12px 0 0', fontSize: 14, color: 'var(--ink-body)' }}>{booking.balanceLabel}</p>

      {!settled && (
        <button className="btn btn-primary btn-sm" style={{ marginTop: 16 }} disabled={busy}
                onClick={async () => { setBusy(true); await payBalance(booking.id); setBusy(false); }}>
          {busy ? 'Đang xử lý…' : `Trả nốt ${money(booking.balanceDue)} ngay`}
        </button>
      )}
    </section>
  );
}

function History({ events }) {
  if (!events?.length) return null;

  return (
    <section className="detail-section">
      <h2>Lịch sử đơn</h2>
      <div style={{ display: 'grid', gap: 10 }}>
        {events.map((e, i) => (
          <div className="cal-row" key={i}>
            <span className="badge pending">{dateTime(e.at)}</span>
            <div style={{ flex: 1, minWidth: 0, fontSize: 13.5 }}>
              <b>{e.fromLabel ? `${e.fromLabel} → ${e.toLabel}` : e.toLabel}</b>
              {e.reason && <span style={{ color: 'var(--ink-muted)' }}> · {e.reason}</span>}
            </div>
            <span style={{ fontSize: 12.5, color: 'var(--ink-muted)' }}>
              {ACTOR[e.actor.split(':')[0]] ?? e.actor}
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
    { label: `Tiền phòng · ${b.nights} đêm`, amount: b.subtotal },
    { label: 'Phí dọn dẹp', amount: b.cleaningFee },
    { label: 'Phí dịch vụ StayHost', amount: b.serviceFee },
    ...(b.tax ? [{ label: 'Thuế', amount: b.tax }] : [])
  ];

  return (
    <aside className="receipt" id="receipt">
      <div className="receipt-head">
        <span className="brand-mark" aria-hidden="true">S</span>
        <div><b>Hoá đơn StayHost</b><span>{b.reference}</span></div>
      </div>

      <img src={b.listingImage} alt="" className="receipt-image" />

      <div className="book-lines" style={{ marginTop: 16 }}>
        {lines.map((l, i) => (
          <div className="book-line" key={i} style={l.amount < 0 ? { color: 'var(--brand-dark)' } : undefined}>
            <span>{l.label}</span><span>{l.amount < 0 ? `−${money(-l.amount)}` : money(l.amount)}</span>
          </div>
        ))}
        <div className="book-rule" />
        <div className="book-total"><span>Tổng cộng</span><span>{money(b.total)}</span></div>
        {b.refundedAmount > 0 && (
          <div className="book-line" style={{ color: 'var(--brand-dark)' }}>
            <span>Đã hoàn</span><span>−{money(b.refundedAmount)}</span>
          </div>
        )}
      </div>

      <div className="receipt-meta">
        <div><Icon name="star" size={15} /> {PAYMENT[b.paymentStatus] ?? b.paymentStatus}</div>
        {b.paymentReference && <div>Mã giao dịch {b.paymentReference}</div>}
        {b.cardLast4 && <div>Thẻ •••• {b.cardLast4}</div>}
      </div>

      <button className="btn btn-dark btn-block btn-sm" style={{ marginTop: 18 }} onClick={() => window.print()}>
        In hoặc lưu PDF
      </button>
    </aside>
  );
}

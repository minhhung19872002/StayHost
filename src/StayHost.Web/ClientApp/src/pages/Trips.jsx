import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, loadBookings, requireAuth, toast } from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate } from '../lib/format.js';

// Status wording and badge colour come from the server (BookingLifecycle), so
// the ten states of docs/03 §3 read the same everywhere.
export const PAYMENT = {
  Pending: 'đang chờ',
  Authorized: 'đã giữ tiền',
  Captured: 'đã thanh toán',
  Refunded: 'đã hoàn tiền',
  Failed: 'thất bại'
};

/** Loads the refund preview, then opens the confirm dialog with real numbers. */
export async function previewCancel(id) {
  try {
    const preview = await api.refundPreview(id);
    set({ cancelPreview: { ...preview, bookingId: id }, overlay: 'cancel-trip' });
  } catch (err) { toast(err.message); }
}

export function openReview(booking) {
  if (!requireAuth()) return;
  set({ reviewBooking: booking, reviewDraft: null, overlay: 'review' });
}

/**
 * The two timers of docs/03 §2–§3, shown as a countdown so the guest knows how
 * long they actually have rather than being surprised by an expiry.
 */
export function Deadline({ booking }) {
  const at = booking.holdExpiresAt ?? booking.requestExpiresAt;
  if (!at) return null;

  const minutes = Math.round((new Date(at) - Date.now()) / 60000);
  if (minutes <= 0) return null;

  const label = booking.holdExpiresAt
    ? `Giữ chỗ còn ${minutes} phút`
    : minutes < 60
      ? `Chủ nhà còn ${minutes} phút để trả lời`
      : `Chủ nhà còn ${Math.round(minutes / 60)} giờ để trả lời`;

  return <span className="badge pending">{label}</span>;
}

export function Trips() {
  const state = useStore();
  const navigate = useNavigate();

  useEffect(() => { loadBookings(); }, []);

  const items = state.bookings;

  return (
    <div className="shell" style={{ paddingBlock: '30px 90px' }}>
      <h1 className="section-title">Chuyến đi của tôi</h1>
      <p className="section-sub">{items.length} lượt đặt chỗ</p>

      {items.length ? items.map(b => (
          <article className="trip" key={b.id}>
            <img src={b.listingImage} alt={b.listingTitle} loading="lazy" decoding="async" />
            <div style={{ minWidth: 0 }}>
              <h3>{b.listingTitle}</h3>
              <div className="meta">{b.listingCity} · Mã đặt chỗ {b.reference}</div>
              <div className="meta">
                {longDate(b.checkIn)} → {longDate(b.checkOut)} · {b.nights} đêm · {b.guests} khách
              </div>
              <div className="meta">
                <b style={{ color: 'var(--ink)' }}>{money(b.total)}</b> tổng cộng ·
                thanh toán {PAYMENT[b.paymentStatus] ?? b.paymentStatus}
              </div>
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 8 }}>
                <span className={`badge ${b.statusBadge}`}>{b.statusLabel}</span>
                {b.hasReview && <span className="badge confirmed">Đã đánh giá</span>}
                <Deadline booking={b} />
              </div>
            </div>
            <div style={{ display: 'grid', gap: 8 }}>
              <button className="btn btn-dark btn-sm" onClick={() => navigate(`/trips/${b.id}`)}>Chi tiết &amp; hoá đơn</button>
              {b.canReview && <button className="btn btn-primary btn-sm" onClick={() => openReview(b)}>Viết đánh giá</button>}
              {b.canCancel && <button className="btn btn-outline btn-sm" onClick={() => previewCancel(b.id)}>Huỷ đặt chỗ</button>}
            </div>
          </article>
      )) : (
        <div className="empty-state" style={{ marginTop: 24 }}>
          <h3>Chưa có chuyến đi nào</h3>
          <p>Khi bạn đặt chỗ, thông tin chuyến đi sẽ hiện ở đây.</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }} onClick={() => navigate('/')}>Tìm chỗ nghỉ</button>
        </div>
      )}
    </div>
  );
}

import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, loadBookings, requireAuth, toast } from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate } from '../lib/format.js';
import { t } from '../lib/i18n.js';

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
    ? `${t('Giữ chỗ còn')} ${minutes} ${t('phút')}`
    : minutes < 60
      ? `${t('Chủ nhà còn')} ${minutes} ${t('phút để trả lời')}`
      : `${t('Chủ nhà còn')} ${Math.round(minutes / 60)} ${t('giờ để trả lời')}`;

  return <span className="badge pending">{label}</span>;
}

export function Trips() {
  const state = useStore();
  const navigate = useNavigate();

  useEffect(() => { loadBookings(); }, []);

  const items = state.bookings;

  return (
    <div className="shell" style={{ paddingBlock: '30px 90px' }}>
      <h1 className="section-title">{t('Chuyến đi của tôi')}</h1>
      <p className="section-sub">{items.length} {t('lượt đặt chỗ')}</p>

      {items.length ? items.map(b => (
          <article className="trip" key={b.id}>
            <img src={b.listingImage} alt={b.listingTitle} loading="lazy" decoding="async" />
            <div style={{ minWidth: 0 }}>
              <h3>{b.listingTitle}</h3>
              <div className="meta">{b.listingCity} · {t('Mã đặt chỗ')} {b.reference}</div>
              <div className="meta">
                {longDate(b.checkIn)} → {longDate(b.checkOut)} · {b.nights} {t('đêm')} · {b.guests} {t('khách')}
              </div>
              <div className="meta">
                <b style={{ color: 'var(--ink)' }}>{money(b.total)}</b> {t('tổng cộng')} ·
                {t('thanh toán')} {t(PAYMENT[b.paymentStatus] ?? b.paymentStatus)}
              </div>
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 8 }}>
                <span className={`badge ${b.statusBadge}`}>{t(b.statusLabel, 'status')}</span>
                {b.hasReview && <span className="badge confirmed">{t('Đã đánh giá')}</span>}
                <Deadline booking={b} />
              </div>
            </div>
            <div style={{ display: 'grid', gap: 8 }}>
              <button className="btn btn-dark btn-sm" onClick={() => navigate(`/trips/${b.id}`)}>{t('Chi tiết & hoá đơn')}</button>
              {b.canReview && <button className="btn btn-primary btn-sm" onClick={() => openReview(b)}>{t('Viết đánh giá')}</button>}
              {b.canCancel && <button className="btn btn-outline btn-sm" onClick={() => previewCancel(b.id)}>{t('Huỷ đặt chỗ')}</button>}
            </div>
          </article>
      )) : (
        <div className="empty-state" style={{ marginTop: 24 }}>
          <h3>{t('Chưa có chuyến đi nào')}</h3>
          <p>{t('Khi bạn đặt chỗ, thông tin chuyến đi sẽ hiện ở đây.')}</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }} onClick={() => navigate('/')}>{t('Tìm chỗ nghỉ')}</button>
        </div>
      )}
    </div>
  );
}

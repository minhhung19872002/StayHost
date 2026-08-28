import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, loadBookings, requireAuth, toast } from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate, todayIso } from '../lib/format.js';
import { t } from '../lib/i18n.js';

/** docs/02 D4 — the quick "nhắn tin" button, same door as the trip page. */
async function messageHost(booking, navigate) {
  if (!requireAuth()) return;
  try {
    const thread = await api.sendMessage({
      listingId: booking.listingId,
      body: 'Chào bạn, mình muốn hỏi thêm về chỗ nghỉ.'
    });
    set({ activeThread: thread });
    navigate('/messages');
  } catch (err) { toast(err.message); }
}

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
  set({ reviewBooking: booking, reviewDraft: null, reviewEditing: false, overlay: 'review' });
}

/** docs/01 ĐG-08 — the same modal, opened over what the guest already wrote. */
export function openReviewEdit(booking) {
  if (!requireAuth()) return;
  set({ reviewBooking: booking, reviewDraft: null, reviewEditing: true, overlay: 'review' });
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

/**
 * docs/02 D4 — bốn nhóm, không phải một danh sách phẳng. The grouping is read
 * from the server's own status wherever it says enough (a cancelled booking is
 * cancelled whatever the dates say); the dates only separate a stay still ahead
 * from one being lived in right now, which is the one thing the status of a
 * confirmed booking cannot say on its own until the sweep moves it on.
 */
const TRIP_GROUPS = [
  ['upcoming', 'Sắp tới'],
  ['current', 'Đang diễn ra'],
  ['past', 'Đã đi'],
  ['cancelled', 'Đã huỷ']
];

const CANCELLED = ['Declined', 'Expired', 'PaymentFailed', 'CancelledByGuest', 'CancelledByHost'];

export function groupOf(b, today) {
  if (CANCELLED.includes(b.status)) return 'cancelled';
  if (b.status === 'Completed' || b.checkOut <= today) return 'past';
  if (b.status === 'InProgress' || (b.checkIn <= today && today < b.checkOut)) return 'current';
  return 'upcoming';
}

export function Trips() {
  const state = useStore();
  const navigate = useNavigate();
  const [group, setGroup] = useState(null);

  useEffect(() => { loadBookings(); }, []);

  const today = todayIso();
  const all = state.bookings;
  const counts = Object.fromEntries(TRIP_GROUPS.map(([key]) => [key, 0]));
  for (const b of all) counts[groupOf(b, today)] += 1;

  // Open on the group that has something in it, so a guest with one upcoming
  // stay does not land on an empty tab and think the booking was lost.
  const active = group ?? TRIP_GROUPS.find(([key]) => counts[key] > 0)?.[0] ?? 'upcoming';
  const items = all.filter(b => groupOf(b, today) === active);

  return (
    <div className="shell" style={{ paddingBlock: '30px 90px' }}>
      <h1 className="section-title">{t('Chuyến đi của tôi')}</h1>
      <p className="section-sub">{all.length} {t('lượt đặt chỗ')}</p>

      <div className="seg-tabs" role="tablist" style={{ marginTop: 18 }}>
        {TRIP_GROUPS.map(([key, label]) => (
          <button role="tab" key={key} aria-selected={active === key}
                  className={`seg-tab ${active === key ? 'is-active' : ''}`}
                  onClick={() => setGroup(key)}>
            {t(label)} ({counts[key]})
          </button>
        ))}
      </div>

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
              {/* docs/07 §2.3 — a booking waiting on a transfer is the one case
                  where the guest still has something to do here: a host who has
                  just accepted a request leaves it exactly like this, and without
                  a way back to the code there is nowhere to pay from. */}
              {b.status === 'PendingPayment' && b.paymentMethod === 'vietqr' && (
                <button className="btn btn-primary btn-sm"
                        onClick={() => navigate(`/chuyen-khoan/${b.reference}`)}>{t('Chuyển khoản')}</button>
              )}
              {/* docs/02 D4 — "mỗi chuyến có nút nhanh: nhắn tin, chỉ đường,
                  xem chi tiết". Directions are pointed at the city while the
                  stay is still ahead: the full address only exists on the trip
                  page and only after the booking is confirmed (docs/01 CĐ-02). */}
              <button className="btn btn-outline btn-sm" onClick={() => messageHost(b, navigate)}>
                {t('Nhắn tin')}
              </button>
              <a className="btn btn-outline btn-sm" target="_blank" rel="noreferrer"
                 href={`https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(`${b.listingTitle}, ${b.listingCity}`)}`}>
                {t('Chỉ đường')}
              </a>
              {b.canReview && <button className="btn btn-primary btn-sm" onClick={() => openReview(b)}>{t('Viết đánh giá')}</button>}
              {/* docs/01 ĐG-08 — sửa được trong 48 giờ, và chỉ khi bên kia chưa
                  gửi. Nút vẫn hiện sau đó: modal nói rõ vì sao không sửa được
                  nữa, đỡ hơn là biến mất không lời giải thích. */}
              {b.hasReview && <button className="btn btn-outline btn-sm" onClick={() => openReviewEdit(b)}>{t('Sửa đánh giá')}</button>}
              {b.canCancel && <button className="btn btn-outline btn-sm" onClick={() => previewCancel(b.id)}>{t('Huỷ đặt chỗ')}</button>}
            </div>
          </article>
      )) : (
        <div className="empty-state" style={{ marginTop: 24 }}>
          <h3>{all.length ? t('Không có chuyến nào trong nhóm này') : t('Chưa có chuyến đi nào')}</h3>
          <p>{t('Khi bạn đặt chỗ, thông tin chuyến đi sẽ hiện ở đây.')}</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }} onClick={() => navigate('/')}>{t('Tìm chỗ nghỉ')}</button>
        </div>
      )}
    </div>
  );
}

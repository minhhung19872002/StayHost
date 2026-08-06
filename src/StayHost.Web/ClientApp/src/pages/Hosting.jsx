import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import {
  set, loadHosting, loadHostCalendar, respondBooking, requireAuth
} from '../lib/store.js';
import { money, longDate } from '../lib/format.js';
import { Icon } from '../components/Icon.jsx';
import { Today } from './hosting/Today.jsx';
import { Payout, SuperhostProgress } from './hosting/Payout.jsx';
import { MultiCalendar } from './hosting/MultiCalendar.jsx';
import { Team } from './hosting/Team.jsx';

const TABS = [
  ['today', 'Hôm nay'], ['overview', 'Tổng quan'], ['listings', 'Chỗ nghỉ'],
  ['calendar', 'Lịch'], ['bookings', 'Đơn đặt'], ['earnings', 'Doanh thu'],
  ['payout', 'Nhận tiền'], ['team', 'Đồng quản lý']
];

export function Hosting() {
  const state = useStore();
  const navigate = useNavigate();

  useEffect(() => { if (state.user) loadHosting(); }, [state.user]);

  if (!state.user) {
    return (
      <div className="shell" style={{ paddingBlock: '60px 90px' }}>
        <div className="empty-state">
          <h3>Đăng nhập để quản lý chỗ nghỉ</h3>
          <p>Trang chủ nhà cho bạn xem đơn đặt, lịch và doanh thu.</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }}
                  onClick={() => set({ authMode: 'login', authError: null, overlay: 'login' })}>Đăng nhập</button>
        </div>
      </div>
    );
  }

  const d = state.hosting;

  if (state.hostingLoading || !d) {
    return (
      <div className="shell" style={{ paddingBlock: '34px 90px' }}>
        <div className="sk-line skeleton" style={{ width: 260, height: 26 }} />
        <div className="stat-grid" style={{ marginTop: 24 }}>
          {Array.from({ length: 4 }, (_, i) => <div className="stat skeleton" key={i} style={{ height: 112, border: 0 }} />)}
        </div>
      </div>
    );
  }

  const newListing = () => {
    if (!requireAuth()) return;
    set({ editingListing: null, overlay: 'listing-editor' });
  };

  if (d.listingCount === 0) {
    return (
      <div className="shell" style={{ paddingBlock: '40px 90px' }}>
        <h1 className="section-title">Trang chủ nhà</h1>
        <p className="section-sub">Bạn chưa có chỗ nghỉ nào. Đăng chỗ đầu tiên để bắt đầu nhận đặt.</p>
        <div className="empty-state" style={{ marginTop: 22 }}>
          <h3>Đăng chỗ nghỉ đầu tiên</h3>
          <p>Mất khoảng 5 phút: mô tả không gian, thêm ảnh và đặt giá.</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }} onClick={newListing}>+ Đăng chỗ nghỉ</button>
        </div>
      </div>
    );
  }

  const tab = state.hostingTab;

  return (
    <div className="shell" style={{ paddingBlock: '30px 90px' }}>
      <div className="page-head">
        <div>
          <h1 className="section-title">Trang chủ nhà</h1>
          <p className="section-sub">
            Xin chào {state.user.fullName} — {d.publishedCount}/{d.listingCount} chỗ nghỉ đang hiển thị
          </p>
        </div>
        <button className="btn btn-primary btn-sm" onClick={newListing}>+ Đăng chỗ nghỉ</button>
      </div>

      <nav className="seg-tabs" role="tablist">
        {TABS.map(([key, label]) => (
          <button role="tab" key={key} aria-selected={tab === key}
                  className={`seg-tab ${tab === key ? 'is-active' : ''}`}
                  onClick={() => set({ hostingTab: key })}>{label}</button>
        ))}
      </nav>

      {tab === 'today' && <Today />}
      {tab === 'overview' && <Overview d={d} navigate={navigate} />}
      {tab === 'listings' && (
        <div className="host-listing-grid" style={{ marginTop: 24 }}>
          {d.listings.map(l => <ListingCard key={l.id} listing={l} navigate={navigate} />)}
        </div>
      )}
      {tab === 'calendar' && <MultiCalendar />}
      {tab === 'bookings' && <Bookings d={d} navigate={navigate} />}
      {tab === 'earnings' && <Earnings d={d} />}
      {tab === 'payout' && <><Payout /><SuperhostProgress /></>}
      {tab === 'team' && <Team />}
    </div>
  );
}

function Overview({ d, navigate }) {
  const cards = [
    ['Chỗ nghỉ đang hiển thị', `${d.publishedCount}/${d.listingCount}`, 'Bản nháp không hiện với khách'],
    ['Lượt đặt sắp tới', String(d.upcomingBookings), `${money(d.earningsUpcoming)} sẽ nhận`],
    ['Đã nhận đến nay', money(d.earningsToDate), 'Sau phí dịch vụ StayHost'],
    ['Điểm đánh giá', d.totalReviews ? `★ ${d.averageRating.toFixed(2)}` : 'Chưa có', `${d.totalReviews} đánh giá`]
  ];

  const pending = d.bookings.filter(b => b.status === 'PendingHostApproval');

  return <>
    <div className="stat-grid" style={{ marginTop: 24 }}>
      {cards.map(([label, value, note]) => (
        <div className="stat" key={label}>
          <div className="value" style={{ fontSize: 'clamp(22px,2.6vw,28px)' }}>{value}</div>
          <div className="label">{label}</div>
          <div className="note">{note}</div>
        </div>
      ))}
    </div>

    {!!pending.length && (
      <section style={{ marginTop: 38 }}>
        <h2 className="section-title" style={{ fontSize: 20 }}>Cần bạn xử lý ({pending.length})</h2>
        <p className="section-sub">Khách đang chờ bạn xác nhận.</p>
        {pending.map(b => <BookingRow key={b.id} booking={b} navigate={navigate} />)}
      </section>
    )}

    <section style={{ marginTop: 38 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Chỗ nghỉ của bạn</h2>
      <div className="host-listing-grid" style={{ marginTop: 16 }}>
        {d.listings.slice(0, 4).map(l => <ListingCard key={l.id} listing={l} navigate={navigate} />)}
      </div>
    </section>
  </>;
}

function ListingCard({ listing: l, navigate }) {
  const openCalendar = async () => {
    await loadHostCalendar(l.id);
    set({ overlay: 'host-block', hostMonthOffset: 0 });
  };

  return (
    <article className="host-listing">
      <div className="host-listing-media">
        {l.images.length
          ? <img src={l.images[0]} alt={l.title} loading="lazy" decoding="async" />
          : <div className="skeleton" style={{ width: '100%', height: '100%' }} />}
        <span className={`badge ${l.isPublished ? 'confirmed' : 'pending'} host-listing-state`}>
          {l.isPublished ? 'Đang hiển thị' : 'Bản nháp'}
        </span>
      </div>
      <div className="host-listing-body">
        <h3>{l.title}</h3>
        <div className="meta">{l.city} · {l.bedrooms} phòng ngủ · {l.maxGuests} khách</div>
        <div className="meta">
          <b style={{ color: 'var(--ink)' }}>{money(l.pricePerNight)}</b> / đêm
          {l.reviewCount ? ` · ★ ${l.rating.toFixed(2)} (${l.reviewCount})` : ' · Chưa có đánh giá'}
        </div>
        <div className="meta">{l.upcomingBookings} lượt đặt sắp tới · đã nhận {money(l.earningsToDate)}</div>
        <div className="host-listing-actions">
          <button className="btn btn-outline btn-sm"
                  onClick={() => set({ editingListing: l, overlay: 'listing-editor' })}>Chỉnh sửa</button>
          <button className="btn btn-outline btn-sm" onClick={openCalendar}>Lịch</button>
          <button className="btn btn-outline btn-sm" onClick={() => navigate(`/rooms/${l.slug}`)}>Xem trang</button>
        </div>
      </div>
    </article>
  );
}

function Bookings({ d, navigate }) {
  if (!d.bookings.length) {
    return (
      <div className="empty-state" style={{ marginTop: 24 }}>
        <h3>Chưa có lượt đặt nào</h3>
        <p>Khi có khách đặt, đơn sẽ hiện ở đây.</p>
      </div>
    );
  }
  return <div style={{ marginTop: 24 }}>{d.bookings.map(b => <BookingRow key={b.id} booking={b} navigate={navigate} />)}</div>;
}

/** docs/03 §3 — the host has 24 hours before the request expires by itself. */
function RespondDeadline({ at }) {
  const minutes = Math.round((new Date(at) - Date.now()) / 60000);
  if (minutes <= 0) return null;

  return (
    <span className="badge pending">
      {minutes < 60 ? `Còn ${minutes} phút để trả lời` : `Còn ${Math.round(minutes / 60)} giờ để trả lời`}
    </span>
  );
}

function BookingRow({ booking: b, navigate }) {
  const awaitingHost = b.status === 'PendingHostApproval';

  return (
    <article className="host-booking">
      <div style={{ minWidth: 0 }}>
        <h3>{b.listingTitle}</h3>
        <div className="meta">{b.guestName}{b.guestEmail ? ` · ${b.guestEmail}` : ''} · mã {b.reference}</div>
        <div className="meta">
          {longDate(b.checkIn)} → {longDate(b.checkOut)} · {b.nights} đêm · {b.guests} khách
        </div>
        <div className="meta">
          Khách trả <b style={{ color: 'var(--ink)' }}>{money(b.total)}</b> ·
          bạn nhận <b style={{ color: 'var(--brand)' }}>{money(b.hostPayout)}</b>
        </div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 8 }}>
          <span className={`badge ${b.statusBadge}`}>{b.statusLabel}</span>
          {awaitingHost && b.requestExpiresAt && <RespondDeadline at={b.requestExpiresAt} />}
        </div>
      </div>
      <div className="host-booking-actions">
        {awaitingHost && <>
          <button className="btn btn-primary btn-sm" onClick={() => respondBooking(b.id, 'confirm')}>Xác nhận</button>
          <button className="btn btn-outline btn-sm" onClick={() => respondBooking(b.id, 'decline')}>Từ chối</button>
        </>}
        {b.status === 'Completed' && (
          <button className="btn btn-outline btn-sm"
                  onClick={() => set({ guestReviewBooking: b, overlay: 'guest-review' })}>Đánh giá khách</button>
        )}
        <button className="btn btn-outline btn-sm" onClick={() => navigate('/messages')}>Nhắn khách</button>
      </div>
    </article>
  );
}

function Earnings({ d }) {
  const state = useStore();
  const hostRate = state.meta?.fees?.hostServiceFeeRate ?? 0.03;

  if (!d.earningsByMonth.length) {
    return (
      <div className="empty-state" style={{ marginTop: 24 }}>
        <h3>Chưa có doanh thu</h3>
        <p>Biểu đồ sẽ hiện khi bạn có lượt đặt đầu tiên.</p>
      </div>
    );
  }

  const max = Math.max(...d.earningsByMonth.map(m => Number(m.amount)), 1);
  const pct = Math.round(hostRate * 100);

  return (
    <div style={{ marginTop: 24 }}>
      <div className="stat-grid">
        <div className="stat">
          <div className="value">{money(d.earningsToDate)}</div>
          <div className="label">Đã nhận</div>
          <div className="note">Các kỳ nghỉ đã hoàn tất</div>
        </div>
        <div className="stat">
          <div className="value">{money(d.earningsUpcoming)}</div>
          <div className="label">Sắp nhận</div>
          <div className="note">{d.upcomingBookings} lượt đặt sắp tới</div>
        </div>
        <div className="stat">
          <div className="value">{money(d.earningsToDate + d.earningsUpcoming)}</div>
          <div className="label">Tổng cộng</div>
          <div className="note">Sau phí dịch vụ chủ nhà {pct}%</div>
        </div>
      </div>

      <section style={{ marginTop: 34 }}>
        <div className="page-head" style={{ marginBottom: 0 }}>
          <h2 className="section-title" style={{ fontSize: 20 }}>Theo tháng nhận phòng</h2>
          <a className="btn btn-outline btn-sm" href="/api/host/earnings.csv" download>Tải file doanh thu</a>
        </div>
        <div className="bar-chart">
          {d.earningsByMonth.map(m => (
            <div className="bar-col" key={m.month} title={`${m.month}: ${money(m.amount)} · ${m.nights} đêm`}>
              <div className="bar" style={{ height: `${Math.max(4, (Number(m.amount) / max) * 100)}%` }} />
              <span className="bar-label">{m.month}</span>
              <span className="bar-value">{money(m.amount)}</span>
            </div>
          ))}
        </div>
      </section>

      <section style={{ marginTop: 34 }}>
        <h2 className="section-title" style={{ fontSize: 20 }}>Cách StayHost tính tiền</h2>
        <div className="know-grid" style={{ marginTop: 16 }}>
          <div className="know">
            <h3><Icon name="star" size={18} /> Phí dịch vụ</h3>
            <ul><li>StayHost giữ {pct}% trên tạm tính của mỗi lượt đặt thành công.</li></ul>
          </div>
          <div className="know">
            <h3><Icon name="heart" size={18} /> Bạn nhận</h3>
            <ul><li>Toàn bộ tiền phòng và phí dọn dẹp trừ phí dịch vụ, chuyển 24 giờ sau khi khách nhận phòng.</li></ul>
          </div>
          <div className="know">
            <h3><Icon name="globe" size={18} /> Huỷ &amp; hoàn</h3>
            <ul><li>Phí vệ sinh luôn được hoàn 100% cho khách ở mọi chính sách huỷ.</li></ul>
          </div>
        </div>
      </section>
    </div>
  );
}

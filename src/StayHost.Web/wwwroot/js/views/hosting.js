import { esc, money, longDate } from '../util.js';
import { state } from '../store.js';
import { icon } from '../components/icons.js';

const TABS = [
  ['overview', 'Tổng quan'],
  ['listings', 'Chỗ nghỉ'],
  ['bookings', 'Đơn đặt'],
  ['earnings', 'Doanh thu']
];

const BOOKING_STATUS = {
  Pending: ['pending', 'Chờ xác nhận'],
  Confirmed: ['confirmed', 'Đã xác nhận'],
  Cancelled: ['cancelled', 'Đã huỷ']
};

export function renderHosting() {
  if (!state.user) return signedOut();

  const d = state.hosting;
  if (state.hostingLoading || !d) {
    return `<div class="shell" style="padding-block:34px 90px">
      <div class="sk-line skeleton" style="width:260px;height:26px"></div>
      <div class="stat-grid" style="margin-top:24px">
        ${Array.from({ length: 4 }, () => '<div class="stat skeleton" style="height:112px;border:0"></div>').join('')}
      </div>
    </div>`;
  }

  if (d.listingCount === 0) return emptyHost();

  const tab = state.hostingTab;

  return `
    <div class="shell" style="padding-block:30px 90px">
      <div class="page-head">
        <div>
          <h1 class="section-title">Trang chủ nhà</h1>
          <p class="section-sub">Xin chào ${esc(state.user.fullName)} — ${esc(d.publishedCount)}/${esc(d.listingCount)} chỗ nghỉ đang hiển thị</p>
        </div>
        <button class="btn btn-primary btn-sm" data-act="new-listing">+ Đăng chỗ nghỉ</button>
      </div>

      <nav class="seg-tabs" role="tablist">
        ${TABS.map(([key, label]) => `
          <button role="tab" class="seg-tab ${tab === key ? 'is-active' : ''}"
                  data-act="host-tab" data-key="${key}" aria-selected="${tab === key}">${esc(label)}</button>
        `).join('')}
      </nav>

      ${tab === 'overview' ? overview(d) : ''}
      ${tab === 'listings' ? listings(d) : ''}
      ${tab === 'bookings' ? bookings(d) : ''}
      ${tab === 'earnings' ? earnings(d) : ''}
    </div>
  `;
}

function signedOut() {
  return `
    <div class="shell" style="padding-block:60px 90px">
      <div class="empty-state">
        <h3>Đăng nhập để quản lý chỗ nghỉ</h3>
        <p>Trang chủ nhà cho bạn xem đơn đặt, lịch và doanh thu.</p>
        <button class="btn btn-primary" style="margin-top:18px" data-act="open-auth" data-mode="login">Đăng nhập</button>
      </div>
    </div>
  `;
}

function emptyHost() {
  return `
    <div class="shell" style="padding-block:40px 90px">
      <h1 class="section-title">Trang chủ nhà</h1>
      <p class="section-sub">Bạn chưa có chỗ nghỉ nào. Đăng chỗ đầu tiên để bắt đầu nhận đặt.</p>
      <div class="empty-state" style="margin-top:22px">
        <h3>Đăng chỗ nghỉ đầu tiên</h3>
        <p>Mất khoảng 5 phút: mô tả không gian, thêm ảnh và đặt giá.</p>
        <button class="btn btn-primary" style="margin-top:18px" data-act="new-listing">+ Đăng chỗ nghỉ</button>
      </div>
    </div>
  `;
}

function overview(d) {
  const cards = [
    ['Chỗ nghỉ đang hiển thị', `${d.publishedCount}/${d.listingCount}`, 'Bản nháp không hiện với khách'],
    ['Lượt đặt sắp tới', String(d.upcomingBookings), money(d.earningsUpcoming) + ' sẽ nhận'],
    ['Đã nhận đến nay', money(d.earningsToDate), 'Sau phí dịch vụ StayHost'],
    ['Điểm đánh giá', d.totalReviews ? `★ ${d.averageRating.toFixed(2)}` : 'Chưa có', `${d.totalReviews} đánh giá`]
  ];

  const pending = d.bookings.filter(b => b.status === 'Pending');

  return `
    <div class="stat-grid" style="margin-top:24px">
      ${cards.map(([label, value, note]) => `
        <div class="stat">
          <div class="value" style="font-size:clamp(22px,2.6vw,28px)">${esc(value)}</div>
          <div class="label">${esc(label)}</div>
          <div class="note">${esc(note)}</div>
        </div>`).join('')}
    </div>

    ${pending.length ? `
      <section style="margin-top:38px">
        <h2 class="section-title" style="font-size:20px">Cần bạn xử lý (${esc(pending.length)})</h2>
        <p class="section-sub">Khách đang chờ bạn xác nhận.</p>
        ${pending.map(bookingRow).join('')}
      </section>` : ''}

    <section style="margin-top:38px">
      <h2 class="section-title" style="font-size:20px">Chỗ nghỉ của bạn</h2>
      <div class="host-listing-grid" style="margin-top:16px">
        ${d.listings.slice(0, 4).map(listingCard).join('')}
      </div>
    </section>
  `;
}

function listings(d) {
  return `
    <div class="host-listing-grid" style="margin-top:24px">
      ${d.listings.map(listingCard).join('')}
    </div>
  `;
}

function listingCard(l) {
  return `
    <article class="host-listing">
      <div class="host-listing-media">
        ${l.images.length
          ? `<img src="${esc(l.images[0])}" alt="${esc(l.title)}" loading="lazy" decoding="async">`
          : '<div class="skeleton" style="width:100%;height:100%"></div>'}
        <span class="badge ${l.isPublished ? 'confirmed' : 'pending'} host-listing-state">
          ${l.isPublished ? 'Đang hiển thị' : 'Bản nháp'}
        </span>
      </div>
      <div class="host-listing-body">
        <h3>${esc(l.title)}</h3>
        <div class="meta">${esc(l.city)} · ${esc(l.bedrooms)} phòng ngủ · ${esc(l.maxGuests)} khách</div>
        <div class="meta"><b style="color:var(--ink)">${money(l.pricePerNight)}</b> / đêm
          ${l.reviewCount ? ` · ★ ${esc(l.rating.toFixed(2))} (${esc(l.reviewCount)})` : ' · Chưa có đánh giá'}</div>
        <div class="meta">${esc(l.upcomingBookings)} lượt đặt sắp tới · đã nhận ${money(l.earningsToDate)}</div>
        <div class="host-listing-actions">
          <button class="btn btn-outline btn-sm" data-act="edit-listing" data-id="${esc(l.id)}">Chỉnh sửa</button>
          <button class="btn btn-outline btn-sm" data-act="open-host-calendar" data-id="${esc(l.id)}">Lịch</button>
          <button class="btn btn-outline btn-sm" data-act="open-listing" data-slug="${esc(l.slug)}">Xem trang</button>
        </div>
      </div>
    </article>
  `;
}

function bookings(d) {
  if (!d.bookings.length) {
    return `<div class="empty-state" style="margin-top:24px">
      <h3>Chưa có lượt đặt nào</h3>
      <p>Khi có khách đặt, đơn sẽ hiện ở đây.</p>
    </div>`;
  }
  return `<div style="margin-top:24px">${d.bookings.map(bookingRow).join('')}</div>`;
}

function bookingRow(b) {
  const [cls, label] = BOOKING_STATUS[b.status] ?? BOOKING_STATUS.Pending;
  return `
    <article class="host-booking">
      <div style="min-width:0">
        <h3>${esc(b.listingTitle)}</h3>
        <div class="meta">${esc(b.guestName)}${b.guestEmail ? ` · ${esc(b.guestEmail)}` : ''} · mã ${esc(b.reference)}</div>
        <div class="meta">${esc(longDate(b.checkIn))} → ${esc(longDate(b.checkOut))} · ${esc(b.nights)} đêm · ${esc(b.guests)} khách</div>
        <div class="meta">Khách trả <b style="color:var(--ink)">${money(b.total)}</b> · bạn nhận <b style="color:var(--brand)">${money(b.hostPayout)}</b></div>
        <span class="badge ${cls}" style="margin-top:8px">${esc(label)}</span>
      </div>
      <div class="host-booking-actions">
        ${b.status === 'Pending' ? `
          <button class="btn btn-primary btn-sm" data-act="respond-booking" data-id="${esc(b.id)}" data-mode="confirm">Xác nhận</button>
          <button class="btn btn-outline btn-sm" data-act="respond-booking" data-id="${esc(b.id)}" data-mode="decline">Từ chối</button>
        ` : ''}
        ${b.status !== 'Cancelled' && new Date(b.checkOut) < new Date()
          ? `<button class="btn btn-outline btn-sm" data-act="open-guest-review" data-id="${esc(b.id)}">Đánh giá khách</button>`
          : ''}
        <button class="btn btn-outline btn-sm" data-act="go" data-href="/messages">Nhắn khách</button>
      </div>
    </article>
  `;
}

function earnings(d) {
  if (!d.earningsByMonth.length) {
    return `<div class="empty-state" style="margin-top:24px">
      <h3>Chưa có doanh thu</h3>
      <p>Biểu đồ sẽ hiện khi bạn có lượt đặt đầu tiên.</p>
    </div>`;
  }

  const max = Math.max(...d.earningsByMonth.map(m => Number(m.amount)), 1);

  return `
    <div style="margin-top:24px">
      <div class="stat-grid">
        <div class="stat">
          <div class="value">${money(d.earningsToDate)}</div>
          <div class="label">Đã nhận</div>
          <div class="note">Các kỳ nghỉ đã hoàn tất</div>
        </div>
        <div class="stat">
          <div class="value">${money(d.earningsUpcoming)}</div>
          <div class="label">Sắp nhận</div>
          <div class="note">${esc(d.upcomingBookings)} lượt đặt sắp tới</div>
        </div>
        <div class="stat">
          <div class="value">${money(d.earningsToDate + d.earningsUpcoming)}</div>
          <div class="label">Tổng cộng</div>
          <div class="note">Sau phí dịch vụ 9%</div>
        </div>
      </div>

      <section style="margin-top:34px">
        <h2 class="section-title" style="font-size:20px">Theo tháng nhận phòng</h2>
        <div class="bar-chart">
          ${d.earningsByMonth.map(m => `
            <div class="bar-col" title="${esc(m.month)}: ${money(m.amount)} · ${esc(m.nights)} đêm">
              <div class="bar" style="height:${Math.max(4, (Number(m.amount) / max) * 100)}%"></div>
              <span class="bar-label">${esc(m.month)}</span>
              <span class="bar-value">${money(m.amount)}</span>
            </div>`).join('')}
        </div>
      </section>

      <section style="margin-top:34px">
        <h2 class="section-title" style="font-size:20px">Cách StayHost tính tiền</h2>
        <div class="know-grid" style="margin-top:16px">
          <div class="know">
            <h3>${icon('star', 18)} Phí dịch vụ</h3>
            <ul><li>StayHost giữ 9% trên tiền phòng của mỗi lượt đặt thành công.</li></ul>
          </div>
          <div class="know">
            <h3>${icon('heart', 18)} Bạn nhận</h3>
            <ul><li>Toàn bộ tiền phòng và phí dọn dẹp, chuyển 24 giờ sau khi khách nhận phòng.</li></ul>
          </div>
          <div class="know">
            <h3>${icon('globe', 18)} Huỷ &amp; hoàn</h3>
            <ul><li>Khách huỷ trước 48 giờ được hoàn tiền phòng; phí dịch vụ được hoàn lại đầy đủ.</li></ul>
          </div>
        </div>
      </section>
    </div>
  `;
}

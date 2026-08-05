import { esc, money, longDate } from '../util.js';
import { state } from '../store.js';

const STATUS = {
  Pending: ['pending', 'Chờ chủ nhà xác nhận'],
  Confirmed: ['confirmed', 'Đã xác nhận'],
  Cancelled: ['cancelled', 'Đã huỷ']
};

const PAYMENT = {
  Pending: 'đang chờ',
  Authorized: 'đã giữ tiền',
  Captured: 'đã thanh toán',
  Refunded: 'đã hoàn tiền',
  Failed: 'thất bại'
};

export function renderTrips() {
  const items = state.bookings;

  return `
    <div class="shell" style="padding-block:30px 90px">
      <h1 class="section-title">Chuyến đi của tôi</h1>
      <p class="section-sub">${esc(items.length)} lượt đặt chỗ</p>

      ${items.length ? items.map(renderTrip).join('') : `
        <div class="empty-state" style="margin-top:24px">
          <h3>Chưa có chuyến đi nào</h3>
          <p>Khi bạn đặt chỗ, thông tin chuyến đi sẽ hiện ở đây.</p>
          <button class="btn btn-primary" style="margin-top:18px" data-act="go" data-href="/">Tìm chỗ nghỉ</button>
        </div>
      `}
    </div>
  `;
}

function renderTrip(b) {
  const [cls, label] = STATUS[b.status] ?? STATUS.Pending;

  return `
    <article class="trip">
      <img src="${esc(b.listingImage)}" alt="${esc(b.listingTitle)}" loading="lazy" decoding="async">
      <div style="min-width:0">
        <h3>${esc(b.listingTitle)}</h3>
        <div class="meta">${esc(b.listingCity)} · Mã đặt chỗ ${esc(b.reference)}</div>
        <div class="meta">${esc(longDate(b.checkIn))} → ${esc(longDate(b.checkOut))} · ${esc(b.nights)} đêm · ${esc(b.guests)} khách</div>
        <div class="meta"><b style="color:var(--ink)">${money(b.total)}</b> tổng cộng · thanh toán ${esc(PAYMENT[b.paymentStatus] ?? b.paymentStatus)}</div>
        <div style="display:flex;gap:8px;flex-wrap:wrap;margin-top:8px">
          <span class="badge ${cls}">${esc(label)}</span>
          ${b.hasReview ? '<span class="badge confirmed">Đã đánh giá</span>' : ''}
        </div>
      </div>
      <div style="display:grid;gap:8px">
        <button class="btn btn-dark btn-sm" data-act="go" data-href="/trips/${esc(b.id)}">Chi tiết &amp; hoá đơn</button>
        ${b.canReview
          ? `<button class="btn btn-primary btn-sm" data-act="open-review" data-id="${esc(b.id)}">Viết đánh giá</button>`
          : ''}
        ${b.canCancel
          ? `<button class="btn btn-outline btn-sm" data-act="preview-cancel" data-id="${esc(b.id)}">Huỷ đặt chỗ</button>`
          : ''}
      </div>
    </article>
  `;
}

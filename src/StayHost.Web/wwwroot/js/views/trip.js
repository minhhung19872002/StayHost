import { esc, money, longDate } from '../util.js';
import { state } from '../store.js';
import { icon } from '../components/icons.js';

const STATUS = {
  Pending: ['pending', 'Chờ chủ nhà xác nhận'],
  Confirmed: ['confirmed', 'Đã xác nhận'],
  Cancelled: ['cancelled', 'Đã huỷ'],
  Completed: ['confirmed', 'Đã hoàn tất']
};

const PAYMENT = {
  Pending: 'Đang chờ',
  Authorized: 'Đã giữ tiền',
  Captured: 'Đã thanh toán',
  Refunded: 'Đã hoàn tiền',
  Failed: 'Thất bại'
};

export function renderTrip() {
  const b = state.trip;

  if (state.tripLoading || !b) {
    return `<div class="shell" style="padding-block:32px 90px">
      <div class="sk-line skeleton" style="width:280px;height:26px"></div>
      <div class="skeleton" style="height:220px;border-radius:16px;margin-top:20px"></div>
    </div>`;
  }

  const [cls, label] = STATUS[b.status] ?? STATUS.Pending;

  return `
    <div class="shell" style="padding-block:24px 90px">
      <button class="back-link" data-act="go" data-href="/trips">← Tất cả chuyến đi</button>

      <div class="page-head" style="margin-top:10px">
        <div>
          <h1 class="section-title">${esc(b.listingTitle)}</h1>
          <p class="section-sub">${esc(b.listingCity)} · mã đặt chỗ <b>${esc(b.reference)}</b></p>
        </div>
        <span class="badge ${cls}">${esc(label)}</span>
      </div>

      <div class="trip-layout">
        <div style="min-width:0">
          <section class="detail-section" style="padding-top:0">
            <h2>Chuyến đi của bạn</h2>
            <div class="kv-grid">
              ${kv('Nhận phòng', longDate(b.checkIn), 'Sau 14:00')}
              ${kv('Trả phòng', longDate(b.checkOut), 'Trước 12:00')}
              ${kv('Số đêm', `${b.nights} đêm`, '')}
              ${kv('Khách', `${b.guests} khách`, '')}
              ${kv('Chủ nhà', b.hostName, '')}
              ${kv('Đặt lúc', longDate(b.createdAt.slice(0, 10)), '')}
            </div>
            ${b.guestNote ? `<p style="margin:16px 0 0;font-size:14px;color:var(--ink-body)"><b>Lời nhắn:</b> ${esc(b.guestNote)}</p>` : ''}
          </section>

          <section class="detail-section">
            <h2>Chính sách huỷ</h2>
            <p style="margin:0;font-size:14.5px;line-height:1.6;color:var(--ink-body)">
              <b>${esc(b.cancellationTier)}</b> — ${esc(b.cancellationSummary)}
            </p>
            ${b.refundedAmount > 0 ? `
              <p style="margin:12px 0 0;font-size:14px;color:var(--brand-dark)">
                Đã hoàn ${money(b.refundedAmount)} về phương thức thanh toán ban đầu.
              </p>` : ''}
            ${b.canCancel ? `
              <button class="btn btn-outline btn-sm" style="margin-top:16px"
                      data-act="preview-cancel" data-id="${esc(b.id)}">Huỷ chuyến đi</button>` : ''}
          </section>

          <section class="detail-section">
            <h2>Hỗ trợ</h2>
            <div style="display:flex;gap:10px;flex-wrap:wrap">
              <button class="btn btn-outline btn-sm" data-act="message-host" data-id="${esc(b.listingId)}">
                Nhắn tin cho chủ nhà
              </button>
              <button class="btn btn-outline btn-sm" data-act="open-listing" data-slug="${esc(b.listingSlug)}">
                Xem chỗ nghỉ
              </button>
              ${b.canReview
                ? `<button class="btn btn-primary btn-sm" data-act="open-review" data-id="${esc(b.id)}">Viết đánh giá</button>`
                : ''}
            </div>
          </section>
        </div>

        ${renderReceipt(b)}
      </div>
    </div>
  `;
}

function kv(label, value, hint) {
  return `
    <div class="kv">
      <span class="kv-label">${esc(label)}</span>
      <b>${esc(value)}</b>
      ${hint ? `<span class="kv-hint">${esc(hint)}</span>` : ''}
    </div>
  `;
}

function renderReceipt(b) {
  return `
    <aside class="receipt" id="receipt">
      <div class="receipt-head">
        <span class="brand-mark" aria-hidden="true">S</span>
        <div>
          <b>Hoá đơn StayHost</b>
          <span>${esc(b.reference)}</span>
        </div>
      </div>

      <img src="${esc(b.listingImage)}" alt="" class="receipt-image">

      <div class="book-lines" style="margin-top:16px">
        <div class="book-line"><span>Tiền phòng · ${esc(b.nights)} đêm</span><span>${money(b.subtotal)}</span></div>
        <div class="book-line"><span>Phí dọn dẹp</span><span>${money(b.cleaningFee)}</span></div>
        <div class="book-line"><span>Phí dịch vụ StayHost</span><span>${money(b.serviceFee)}</span></div>
        <div class="book-line"><span>Thuế VAT 8%</span><span>${money(b.tax)}</span></div>
        <div class="book-rule"></div>
        <div class="book-total"><span>Tổng cộng</span><span>${money(b.total)}</span></div>
        ${b.refundedAmount > 0 ? `
          <div class="book-line" style="color:var(--brand-dark)"><span>Đã hoàn</span><span>−${money(b.refundedAmount)}</span></div>` : ''}
      </div>

      <div class="receipt-meta">
        <div>${icon('star', 15)} ${esc(PAYMENT[b.paymentStatus] ?? b.paymentStatus)}</div>
        ${b.paymentReference ? `<div>Mã giao dịch ${esc(b.paymentReference)}</div>` : ''}
        ${b.cardLast4 ? `<div>Thẻ •••• ${esc(b.cardLast4)}</div>` : ''}
      </div>

      <button class="btn btn-dark btn-block btn-sm" style="margin-top:18px" data-act="print-receipt">
        In hoặc lưu PDF
      </button>
    </aside>
  `;
}

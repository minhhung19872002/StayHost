import { esc, money, longDate } from '../util.js';
import { state } from '../store.js';

const REPORT_STATUS = {
  Open: ['pending', 'Mới'],
  Reviewing: ['pending', 'Đang xem xét'],
  Resolved: ['confirmed', 'Đã xử lý'],
  Dismissed: ['cancelled', 'Đã bỏ qua']
};

export function renderAdmin() {
  if (!state.user) return gate('Đăng nhập để vào trang quản trị');
  if (state.user.role !== 'Admin') return gate('Tài khoản của bạn không có quyền quản trị');

  const d = state.admin;
  if (!d) {
    return `<div class="shell" style="padding-block:34px 90px">
      <div class="sk-line skeleton" style="width:240px;height:26px"></div>
      <div class="stat-grid" style="margin-top:24px">
        ${Array.from({ length: 4 }, () => '<div class="stat skeleton" style="height:110px;border:0"></div>').join('')}
      </div>
    </div>`;
  }

  return `
    <div class="shell" style="padding-block:30px 90px">
      <h1 class="section-title">Quản trị StayHost</h1>
      <p class="section-sub">Tổng quan nền tảng, kiểm duyệt chỗ nghỉ và xử lý báo cáo</p>

      <div class="stat-grid" style="margin-top:22px">
        ${stat('Doanh thu nền tảng', money(d.platformRevenue), `Tổng giao dịch ${money(d.grossVolume)}`)}
        ${stat('Chỗ nghỉ', `${d.publishedListings}/${d.listings}`, `${d.drafts} bản nháp`)}
        ${stat('Lượt đặt', String(d.bookings), `${d.activeBookings} đang hiệu lực`)}
        ${stat('Người dùng', String(d.users), `${d.hosts} chủ nhà`)}
      </div>

      <div class="stat-grid" style="margin-top:16px">
        ${stat('Báo cáo đang mở', String(d.openReports), 'Cần xử lý')}
        ${stat('Email chờ gửi', String(d.queuedEmails), 'Hàng đợi giao dịch')}
      </div>

      <section style="margin-top:40px">
        <h2 class="section-title" style="font-size:20px">Báo cáo chỗ nghỉ</h2>
        ${d.reports.length ? `
          <div style="margin-top:16px;display:grid;gap:12px">
            ${d.reports.map(renderReport).join('')}
          </div>`
          : '<p class="section-sub">Chưa có báo cáo nào.</p>'}
      </section>

      <section style="margin-top:40px">
        <h2 class="section-title" style="font-size:20px">Chỗ nghỉ mới nhất</h2>
        <div class="table-wrap">
          <table class="admin-table">
            <thead>
              <tr><th>Chỗ nghỉ</th><th>Chủ nhà</th><th>Giá</th><th>Đánh giá</th><th>Trạng thái</th><th></th></tr>
            </thead>
            <tbody>
              ${d.recentListings.map(l => `
                <tr>
                  <td>
                    <b>${esc(l.title)}</b>
                    <span>${esc(l.city)} · ${esc(longDate(l.createdAt.slice(0, 10)))}</span>
                  </td>
                  <td>${esc(l.hostName)}</td>
                  <td>${money(l.pricePerNight)}</td>
                  <td>${l.reviewCount ? `★ ${esc(l.rating.toFixed(2))} (${esc(l.reviewCount)})` : '—'}</td>
                  <td><span class="badge ${l.isPublished ? 'confirmed' : 'pending'}">
                    ${l.isPublished ? 'Hiển thị' : 'Nháp'}</span></td>
                  <td style="white-space:nowrap">
                    <button class="btn btn-outline btn-sm" data-act="admin-publish"
                            data-id="${esc(l.id)}" data-published="${!l.isPublished}">
                      ${l.isPublished ? 'Gỡ hiển thị' : 'Duyệt'}
                    </button>
                    <button class="btn btn-outline btn-sm" data-act="open-listing" data-slug="${esc(l.slug)}">Xem</button>
                  </td>
                </tr>`).join('')}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  `;
}

function gate(message) {
  return `
    <div class="shell" style="padding-block:60px 90px">
      <div class="empty-state">
        <h3>${esc(message)}</h3>
        <p>Trang này dành cho đội vận hành StayHost.</p>
        ${state.user ? '' : '<button class="btn btn-primary" style="margin-top:18px" data-act="open-auth" data-mode="login">Đăng nhập</button>'}
      </div>
    </div>
  `;
}

function stat(label, value, note) {
  return `
    <div class="stat">
      <div class="value" style="font-size:clamp(20px,2.4vw,26px)">${esc(value)}</div>
      <div class="label">${esc(label)}</div>
      <div class="note">${esc(note)}</div>
    </div>
  `;
}

function renderReport(r) {
  const [cls, label] = REPORT_STATUS[r.status] ?? REPORT_STATUS.Open;
  const open = r.status === 'Open' || r.status === 'Reviewing';

  return `
    <article class="host-booking">
      <div style="min-width:0">
        <h3>${esc(r.listingTitle)}</h3>
        <div class="meta">${esc(r.reason)}${r.detail ? ` — ${esc(r.detail)}` : ''}</div>
        <div class="meta">Báo cáo bởi ${esc(r.reporterName)} · ${esc(longDate(r.createdAt.slice(0, 10)))}</div>
        ${r.resolution ? `<div class="meta">Kết luận: ${esc(r.resolution)}</div>` : ''}
        <span class="badge ${cls}" style="margin-top:8px">${esc(label)}</span>
      </div>
      <div class="host-booking-actions">
        ${open ? `
          <button class="btn btn-primary btn-sm" data-act="admin-resolve" data-id="${esc(r.id)}" data-status="Resolved">Đã xử lý</button>
          <button class="btn btn-outline btn-sm" data-act="admin-resolve" data-id="${esc(r.id)}" data-status="Dismissed">Bỏ qua</button>
        ` : ''}
        <button class="btn btn-outline btn-sm" data-act="admin-publish" data-id="${esc(r.listingId)}" data-published="false">
          Gỡ chỗ nghỉ
        </button>
      </div>
    </article>
  `;
}

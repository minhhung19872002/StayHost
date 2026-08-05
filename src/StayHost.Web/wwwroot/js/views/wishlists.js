import { esc } from '../util.js';
import { state } from '../store.js';
import { renderCard } from '../components/card.js';
import { icon } from '../components/icons.js';

export function renderWishlists() {
  // A selected list shows its contents; otherwise we show the list of lists.
  return state.activeWishlist ? renderOne() : renderIndex();
}

function renderIndex() {
  const lists = state.wishlists ?? [];

  return `
    <div class="shell" style="padding-block:30px 90px">
      <div class="page-head">
        <div>
          <h1 class="section-title">Danh sách yêu thích</h1>
          <p class="section-sub">${esc(lists.length)} danh sách · ${esc(lists.reduce((n, l) => n + l.count, 0))} chỗ nghỉ đã lưu</p>
        </div>
        <button class="btn btn-outline btn-sm" data-act="new-wishlist">+ Tạo danh sách</button>
      </div>

      ${lists.length ? `
        <div class="wl-grid">
          ${lists.map(renderListCard).join('')}
        </div>
      ` : `
        <div class="empty-state" style="margin-top:24px">
          <h3>Chưa lưu chỗ nghỉ nào</h3>
          <p>Nhấn ♥ trên bất kỳ chỗ nghỉ để lưu lại đây.</p>
          <button class="btn btn-primary" style="margin-top:18px" data-act="go" data-href="/">Khám phá chỗ nghỉ</button>
        </div>
      `}
    </div>
  `;
}

function renderListCard(list) {
  const covers = list.coverImages ?? [];

  return `
    <article class="wl-card" data-act="open-wishlist" data-id="${esc(list.id)}">
      <div class="wl-cover">
        ${covers.length
          ? covers.slice(0, 4).map(url => `<img src="${esc(url)}" alt="" loading="lazy" decoding="async">`).join('')
          : `<div class="wl-empty">${icon('heart', 28)}</div>`}
      </div>
      <div class="wl-body">
        <b>${esc(list.name)}</b>
        <span>${esc(list.count)} chỗ nghỉ${list.isDefault ? ' · mặc định' : ''}</span>
      </div>
    </article>
  `;
}

function renderOne() {
  const detail = state.activeWishlist;
  const list = detail.list;

  return `
    <div class="shell" style="padding-block:26px 90px">
      <button class="back-link" data-act="close-wishlist">← Tất cả danh sách</button>

      <div class="page-head" style="margin-top:8px">
        <div>
          <h1 class="section-title">${esc(list.name)}</h1>
          <p class="section-sub">${esc(list.count)} chỗ nghỉ</p>
        </div>
        <div style="display:flex;gap:8px;flex-wrap:wrap">
          <button class="btn btn-outline btn-sm" data-act="rename-wishlist" data-id="${esc(list.id)}">Đổi tên</button>
          ${list.isDefault
            ? ''
            : `<button class="btn btn-outline btn-sm" data-act="delete-wishlist" data-id="${esc(list.id)}">Xoá danh sách</button>`}
        </div>
      </div>

      ${detail.items.length ? `
        <div class="card-grid" style="margin-top:20px">
          ${detail.items.map(c => renderCard(c, { lazy: true })).join('')}
        </div>
      ` : `
        <div class="empty-state" style="margin-top:24px">
          <h3>Danh sách này còn trống</h3>
          <p>Nhấn ♥ trên chỗ nghỉ bạn thích để thêm vào đây.</p>
          <button class="btn btn-primary" style="margin-top:18px" data-act="go" data-href="/">Khám phá chỗ nghỉ</button>
        </div>
      `}
    </div>
  `;
}

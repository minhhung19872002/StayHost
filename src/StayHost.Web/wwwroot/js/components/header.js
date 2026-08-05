import { esc, dateRangeLabel } from '../util.js';
import { state, activeFilterCount, guestLabel, totalGuests, isDiscovery } from '../store.js';
import { icon, CATEGORY_ICON } from './icons.js';

const TABS = [
  { key: 'homes', label: 'Chỗ ở', icon: 'house' },
  { key: 'experiences', label: 'Trải nghiệm', icon: 'boutique' },
  { key: 'services', label: 'Dịch vụ', icon: 'pool' }
];

export function renderHeader() {
  return `
    ${renderTopBar()}
    ${state.route.name === 'browse' && !isDiscovery() ? renderCategoryBar() : ''}
  `;
}

function renderTopBar() {
  return `
    <div class="header-main">
      <button class="brand" data-act="go" data-href="/" aria-label="StayHost OS — về trang chủ">
        <span class="brand-mark" aria-hidden="true">S</span>
        <span class="brand-text">StayHost<span> OS</span></span>
      </button>

      <nav class="nav-tabs" aria-label="Loại dịch vụ">
        ${TABS.map(t => `
          <button class="nav-tab ${state.tab === t.key ? 'is-active' : ''}"
                  data-act="set-tab" data-key="${esc(t.key)}" aria-pressed="${state.tab === t.key}">
            <span class="ic">${icon(t.icon, 22)}</span>
            <span>${esc(t.label)}</span>
          </button>
        `).join('')}
      </nav>

      <div class="header-actions">
        <button class="ghost-btn host-link" data-act="go" data-href="${state.user?.isHost ? '/hosting' : '/host'}">
          ${state.user?.isHost ? 'Trang chủ nhà' : 'Cho thuê nhà'}
        </button>
        <button class="icon-btn" data-act="open" data-overlay="language"
                aria-label="Chọn ngôn ngữ và tiền tệ" title="Ngôn ngữ &amp; tiền tệ">${icon('globe', 18)}</button>
        <div class="menu-anchor">
          <button class="account-btn" data-act="toggle-menu" aria-haspopup="true" aria-expanded="${state.menu === 'account'}">
            <span class="ic">${icon('menu', 17)}</span>
            ${unreadBadge()}
            <span class="avatar" aria-hidden="true">${esc(state.user?.initials ?? '')}</span>
          </button>
          ${state.menu === 'account' ? renderAccountMenu() : ''}
        </div>
      </div>
    </div>

    ${renderSearchBar()}
  `;
}

function unreadBadge() {
  const unread = state.user?.unreadMessages ?? 0;
  if (unread > 0) return `<span class="fav-count" title="Tin nhắn chưa đọc">${esc(unread)}</span>`;
  if (state.favCount > 0) return `<span class="fav-count" title="Chỗ nghỉ đã lưu">${esc(state.favCount)}</span>`;
  return '';
}

/** Segmented "Địa điểm · Ngày · Khách" pill, expanded on the landing page. */
function renderSearchBar() {
  const wide = isDiscovery() && state.route.name === 'browse';

  return `
    <div class="search-row ${wide ? 'is-wide' : ''}">
      <form class="searchbar" data-act="submit-search" role="search">
        <label class="seg seg-where">
          <span class="seg-cap">Địa điểm</span>
          <input id="q" type="text" value="${esc(state.q)}" placeholder="Tìm điểm đến" autocomplete="off" data-act="input-q">
        </label>

        <span class="seg-div"></span>

        <button type="button" class="seg seg-when" data-act="open" data-overlay="dates">
          <span class="seg-cap">Ngày</span>
          <span class="seg-val">${esc(dateRangeLabel(state.checkIn, state.checkOut))}</span>
        </button>

        <span class="seg-div"></span>

        <button type="button" class="seg seg-who" data-act="open" data-overlay="guests">
          <span class="seg-cap">Khách</span>
          <span class="seg-val">${esc(guestLabel())}</span>
        </button>

        <button type="submit" class="search-go" aria-label="Tìm kiếm">${icon('search', 17)}</button>
      </form>
    </div>
  `;
}

function renderAccountMenu() {
  const u = state.user;

  if (!u) {
    return `
      <div class="menu" role="menu">
        <button class="bold" data-act="open-auth" data-mode="register" role="menuitem">Đăng ký</button>
        <button class="bold" data-act="open-auth" data-mode="login" role="menuitem">Đăng nhập</button>
        <hr>
        <button data-act="go" data-href="/wishlists" role="menuitem">Danh sách yêu thích (${esc(state.favCount)})</button>
        <button data-act="go" data-href="/trips" role="menuitem">Chuyến đi của tôi</button>
        <button data-act="go" data-href="/host" role="menuitem">Cho thuê nhà trên StayHost</button>
        <hr>
        <button data-act="open" data-overlay="language" role="menuitem">Ngôn ngữ &amp; tiền tệ</button>
        <button data-act="open" data-overlay="help" role="menuitem">Trung tâm trợ giúp</button>
      </div>
    `;
  }

  return `
    <div class="menu" role="menu">
      <div class="menu-user">
        <span class="avatar">${esc(u.initials)}</span>
        <div style="min-width:0">
          <b>${esc(u.fullName)}</b>
          <span>${esc(u.email)}</span>
        </div>
      </div>
      <hr>
      <button class="bold" data-act="go" data-href="/messages" role="menuitem">
        Tin nhắn ${u.unreadMessages ? `<span class="fav-count" style="margin-left:auto">${esc(u.unreadMessages)}</span>` : ''}
      </button>
      <button data-act="go" data-href="/trips" role="menuitem">Chuyến đi của tôi</button>
      <button data-act="go" data-href="/wishlists" role="menuitem">Danh sách yêu thích (${esc(state.favCount)})</button>
      <hr>
      ${u.isHost
        ? `<button class="bold" data-act="go" data-href="/hosting" role="menuitem">Trang chủ nhà (${esc(u.listingCount)} chỗ nghỉ)</button>`
        : `<button class="bold" data-act="become-host" role="menuitem">Cho thuê nhà trên StayHost</button>`}
      <button data-act="open" data-overlay="profile" role="menuitem">Tài khoản</button>
      <hr>
      <button data-act="open" data-overlay="language" role="menuitem">Ngôn ngữ &amp; tiền tệ</button>
      <button data-act="open" data-overlay="help" role="menuitem">Trung tâm trợ giúp</button>
      <button data-act="logout" role="menuitem">Đăng xuất</button>
    </div>
  `;
}

function renderCategoryBar() {
  const cats = state.meta?.categories ?? [];
  const filters = activeFilterCount();

  return `
    <div class="catbar-outer">
      <div class="catbar">
        <button class="cat-arrow left" data-act="cat-scroll" data-dir="-1" aria-label="Cuộn trái">${icon('chevronLeft', 14)}</button>
        <div class="cat-scroll" id="cat-scroll">
          ${cats.map(c => `
            <button class="cat ${state.category === c.key ? 'is-active' : ''}"
                    data-act="pick-category" data-key="${esc(c.key)}"
                    aria-pressed="${state.category === c.key}"
                    title="${esc(c.label)} · ${esc(c.count)} chỗ nghỉ">
              <span class="icon">${icon(CATEGORY_ICON[c.key] ?? 'all', 24)}</span>
              <span class="label">${esc(c.label)}</span>
            </button>
          `).join('')}
        </div>
        <button class="cat-arrow right" data-act="cat-scroll" data-dir="1" aria-label="Cuộn phải">${icon('chevronRight', 14)}</button>

        <button class="total-toggle ${state.showTotalPrice ? 'is-on' : ''}" data-act="toggle-total"
                aria-pressed="${state.showTotalPrice}" title="Hiển thị giá trọn kỳ nghỉ thay vì giá mỗi đêm">
          <span class="switch" aria-hidden="true"></span> Hiển thị tổng giá
        </button>

        <button class="chip-btn" data-act="open" data-overlay="filters">
          ${icon('filter', 15)} Bộ lọc ${filters ? `<span class="count">${esc(filters)}</span>` : ''}
        </button>
      </div>
    </div>
  `;
}

export { guestLabel };

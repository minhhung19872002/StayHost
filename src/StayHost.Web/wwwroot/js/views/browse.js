import { esc, dateRangeLabel } from '../util.js';
import { state, activeFilterCount, isDiscovery } from '../store.js';
import { renderCard, renderCardSkeleton } from '../components/card.js';
import { icon } from '../components/icons.js';

export function renderBrowse() {
  return isDiscovery() ? renderDiscovery() : renderResults();
}

/* --------------------------------------------------- landing (carousel rows) */

function renderDiscovery() {
  const home = state.home;

  if (state.homeLoading || !home) {
    return `<div class="shell" style="padding-block:32px 90px">
      ${[0, 1].map(() => `
        <div style="margin-bottom:40px">
          <div class="sk-line skeleton" style="width:280px;height:22px;margin-bottom:18px"></div>
          <div class="card-grid">${Array.from({ length: 4 }, renderCardSkeleton).join('')}</div>
        </div>`).join('')}
    </div>`;
  }

  return `
    <div class="shell" style="padding-block:26px 60px">
      ${home.sections.map(renderSection).join('')}
    </div>
    ${renderInspiration(home.inspiration)}
  `;
}

function renderSection(section) {
  return `
    <section class="rail" data-rail="${esc(section.key)}">
      <div class="rail-head">
        <button class="rail-title" data-act="go" data-href="${esc(section.href)}">
          <span>${esc(section.title)}</span>${icon('arrowRight', 18)}
        </button>
        ${section.subtitle ? `<p class="rail-sub">${esc(section.subtitle)}</p>` : ''}
        <div class="rail-nav">
          <button class="round-btn" data-act="rail-scroll" data-key="${esc(section.key)}" data-dir="-1"
                  aria-label="Cuộn trái">${icon('chevronLeft', 14)}</button>
          <button class="round-btn" data-act="rail-scroll" data-key="${esc(section.key)}" data-dir="1"
                  aria-label="Cuộn phải">${icon('chevronRight', 14)}</button>
        </div>
      </div>
      <div class="rail-track" data-rail-track="${esc(section.key)}">
        ${section.items.map(c => `<div class="rail-item">${renderCard(c, { lazy: true })}</div>`).join('')}
      </div>
    </section>
  `;
}

function renderInspiration(groups) {
  if (!groups?.length) return '';
  const active = state.inspirationTab ?? groups[0].tab;
  const current = groups.find(g => g.tab === active) ?? groups[0];

  return `
    <section class="inspiration">
      <div class="shell">
        <h2 class="section-title">Gợi ý cho chuyến đi sắp tới</h2>
        <div class="insp-tabs" role="tablist">
          ${groups.map(g => `
            <button role="tab" class="insp-tab ${g.tab === current.tab ? 'is-active' : ''}"
                    data-act="set-inspiration" data-key="${esc(g.tab)}"
                    aria-selected="${g.tab === current.tab}">${esc(g.tab)}</button>
          `).join('')}
        </div>
        <div class="insp-grid">
          ${current.links.map(l => `
            <button class="insp-link" data-act="go" data-href="${esc(l.href)}">
              <b>${esc(l.title)}</b><span>${esc(l.subtitle)}</span>
            </button>
          `).join('')}
        </div>
      </div>
    </section>
  `;
}

/* ------------------------------------------------------- search result grid */

function renderResults() {
  const { results, loading } = state;

  const title = state.q.trim()
    ? `Chỗ nghỉ tại "${esc(state.q.trim())}"`
    : state.category !== 'all'
      ? `${esc(state.meta?.categories.find(c => c.key === state.category)?.label ?? '')} được yêu thích`
      : 'Chỗ nghỉ được yêu thích ở Việt Nam';

  const grid = loading
    ? `<div class="card-grid">${Array.from({ length: 8 }, renderCardSkeleton).join('')}</div>`
    : results.items.length
      ? `<div class="card-grid">${results.items.map(c => renderCard(c)).join('')}</div>`
      : renderEmpty();

  const hasMore = !loading && results.items.length < results.total;

  const body = `
    <div class="results-head">
      <h1 class="results-title">${title}</h1>
      <div class="results-meta">${esc(loading ? '…' : results.total)} chỗ nghỉ · ${esc(dateRangeLabel(state.checkIn, state.checkOut))}</div>
    </div>
    ${grid}
    ${hasMore ? `
      <div class="load-more">
        <p>Đang hiển thị ${esc(results.items.length)} trong ${esc(results.total)} chỗ nghỉ</p>
        <button class="btn btn-dark" data-act="load-more" ${state.loadingMore ? 'disabled' : ''}>
          ${state.loadingMore ? 'Đang tải…' : 'Xem thêm chỗ nghỉ'}
        </button>
      </div>` : ''}
  `;

  if (state.showMap) {
    return `
      <div class="split">
        <div class="split-list shell" style="padding-block:22px 90px">${body}</div>
        <div class="split-map"><div id="map"></div></div>
      </div>
      ${renderMapToggle()}
    `;
  }

  return `
    <div class="shell" style="padding-block:22px 90px">${body}</div>
    ${renderMapToggle()}
  `;
}

function renderMapToggle() {
  return `
    <button class="map-toggle" data-act="toggle-map">
      ${state.showMap ? 'Hiện danh sách' : 'Hiện bản đồ'} ${icon(state.showMap ? 'filter' : 'map', 16)}
    </button>
  `;
}

function renderEmpty() {
  const filters = activeFilterCount();
  return `
    <div class="empty-state">
      <h3>Không tìm thấy chỗ nghỉ phù hợp</h3>
      <p>${filters
        ? 'Thử nới giá hoặc bỏ bớt tiện nghi trong bộ lọc.'
        : 'Thử một điểm đến khác hoặc đổi ngày.'}</p>
      <button class="btn btn-primary" style="margin-top:18px" data-act="reset-filters">Xoá bộ lọc</button>
    </div>
  `;
}

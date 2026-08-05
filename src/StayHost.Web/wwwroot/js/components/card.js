import { esc, money, nightsBetween } from '../util.js';
import { state } from '../store.js';

/** Card price respects the "Hiển thị tổng giá" switch, like Airbnb's total-price toggle. */
export function cardPrice(card) {
  if (!state.showTotalPrice) {
    return `<b>${money(card.pricePerNight)}</b> <span>/ đêm</span>`;
  }
  const nights = nightsBetween(state.checkIn, state.checkOut);
  const total = card.pricePerNight * nights + 350000 + Math.round(card.pricePerNight * nights * 0.09);
  return `<b>${money(total)}</b> <span>tổng ${nights} đêm</span>`;
}

export function renderCard(card, opts = {}) {
  const images = card.images?.length ? card.images : [''];
  const idx = Math.min(state.carousel[card.id] ?? 0, images.length - 1);

  const badge = card.isGuestFavorite
    ? 'KHÁCH YÊU THÍCH'
    : card.isSuperhost ? 'SIÊU CHỦ NHÀ' : null;

  return `
    <article class="card" data-listing="${esc(card.id)}">
      <div class="card-media" data-act="open-listing" data-slug="${esc(card.slug)}">
        ${images.map((src, i) => `
          <img src="${esc(src)}" alt="${esc(card.title)} — ảnh ${i + 1}"
               class="${i === idx ? 'is-current' : ''}"
               loading="${i === 0 && !opts.lazy ? 'eager' : 'lazy'}"
               decoding="async">
        `).join('')}

        ${badge ? `<span class="card-badge">${esc(badge)}</span>` : ''}

        <button class="card-fav ${card.isFavorite ? 'is-on' : ''}"
                data-act="toggle-fav" data-id="${esc(card.id)}"
                aria-label="${card.isFavorite ? 'Bỏ lưu' : 'Lưu'} ${esc(card.title)}"
                aria-pressed="${!!card.isFavorite}">♥</button>

        ${images.length > 1 ? `
          <button class="carousel-nav prev" data-act="carousel" data-id="${esc(card.id)}" data-dir="-1"
                  aria-label="Ảnh trước" ${idx === 0 ? 'disabled' : ''}>‹</button>
          <button class="carousel-nav next" data-act="carousel" data-id="${esc(card.id)}" data-dir="1"
                  aria-label="Ảnh tiếp theo" ${idx === images.length - 1 ? 'disabled' : ''}>›</button>
          <div class="carousel-dots" aria-hidden="true">
            ${images.map((_, i) => `<i class="${i === idx ? 'is-on' : ''}"></i>`).join('')}
          </div>
        ` : ''}
      </div>

      <div class="card-body" data-act="open-listing" data-slug="${esc(card.slug)}">
        <div class="card-row">
          <h3 class="card-title">${esc(card.title)}</h3>
          <div class="card-rating">${card.reviewCount ? `★ ${esc(card.rating.toFixed(2))}` : 'Mới'}</div>
        </div>
        <div class="card-sub">${esc(card.city)} · ${esc(card.bedrooms)} phòng ngủ</div>
        <div class="card-sub">${esc(card.typeLabel)} · ${esc(card.roomTypeLabel)}</div>
        <div class="card-price">${cardPrice(card)}</div>
      </div>
    </article>
  `;
}

export function renderCardSkeleton() {
  return `
    <div class="card" aria-hidden="true">
      <div class="card-media skeleton"></div>
      <div class="card-body">
        <div class="sk-line skeleton" style="width:80%"></div>
        <div class="sk-line skeleton" style="width:55%"></div>
        <div class="sk-line skeleton" style="width:40%"></div>
      </div>
    </div>
  `;
}

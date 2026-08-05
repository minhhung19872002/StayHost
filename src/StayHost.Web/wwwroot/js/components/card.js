import { esc, money, nightsBetween } from '../util.js';
import { state } from '../store.js';

/** Total the guest actually pays for the selected dates, fees included. */
export function stayTotal(card) {
  const nights = nightsBetween(state.checkIn, state.checkOut);
  const subtotal = card.pricePerNight * nights;
  return { nights, total: subtotal + 350000 + Math.round(subtotal * 0.09) };
}

/** Card price respects the "Hiển thị tổng giá" switch, like Airbnb's total-price toggle. */
export function cardPrice(card) {
  if (!state.showTotalPrice) {
    return `<b>${money(card.pricePerNight)}</b> <span>/ đêm</span>`;
  }
  const { nights, total } = stayTotal(card);
  return `<b>${money(total)}</b> <span>tổng ${nights} đêm</span>`;
}

/**
 * `variant: 'search'` renders the denser layout airbnb.com uses on /s/ results:
 * "<loại> tại <thành phố>" as the heading, the listing name underneath, the
 * bed/bath line, the selected dates and the all-in total for the stay.
 */
export function renderCard(card, opts = {}) {
  const images = card.images?.length ? card.images : [''];
  const idx = Math.min(state.carousel[card.id] ?? 0, images.length - 1);
  const search = opts.variant === 'search';

  const badge = card.isGuestFavorite
    ? 'KHÁCH YÊU THÍCH'
    : card.isSuperhost ? 'SIÊU CHỦ NHÀ' : null;

  return `
    <article class="card ${search ? 'card-search' : ''}" data-listing="${esc(card.id)}">
      <div class="card-media" data-act="open-listing" data-slug="${esc(card.slug)}">
        ${images.map((src, i) => {
          // Only the visible frame and its neighbours are in the DOM; the rest are
          // placeholders so a grid of cards costs a handful of images, not hundreds.
          const near = Math.abs(i - idx) <= 1;
          if (!near) return `<img data-src="${esc(src)}" alt="" aria-hidden="true" class="is-deferred">`;
          return `<img src="${esc(src)}" alt="${esc(card.title)} — ảnh ${i + 1}"
                       class="${i === idx ? 'is-current' : ''}"
                       loading="${i === 0 && !opts.lazy ? 'eager' : 'lazy'}"
                       decoding="async">`;
        }).join('')}

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

      ${search ? searchBody(card) : opts.variant === 'rail' ? railBody(card) : browseBody(card)}
    </article>
  `;
}

/**
 * Compact two-line body used in the landing-page rails, matching how
 * airbnb.com renders its carousel cards: name, then price · rating on one line.
 */
function railBody(card) {
  const { nights, total } = stayTotal(card);

  return `
    <div class="card-body" data-act="open-listing" data-slug="${esc(card.slug)}">
      <h3 class="card-title">${esc(card.typeLabel)} tại ${esc(card.city)}</h3>
      <div class="card-sub card-inline">
        <b>${money(total)}</b> cho ${nights} đêm
        ${card.reviewCount ? ` · ★ ${esc(card.rating.toFixed(2))}` : ' · Mới'}
      </div>
    </div>
  `;
}

function browseBody(card) {
  return `
    <div class="card-body" data-act="open-listing" data-slug="${esc(card.slug)}">
      <div class="card-row">
        <h3 class="card-title">${esc(card.title)}</h3>
        <div class="card-rating">${card.reviewCount ? `★ ${esc(card.rating.toFixed(2))}` : 'Mới'}</div>
      </div>
      <div class="card-sub">${esc(card.city)} · ${esc(card.bedrooms)} phòng ngủ</div>
      <div class="card-sub">${esc(card.typeLabel)} · ${esc(card.roomTypeLabel)}</div>
      <div class="card-price">${cardPrice(card)}</div>
    </div>
  `;
}

function searchBody(card) {
  const { nights, total } = stayTotal(card);

  // Same fee model as the discounted total, applied to the pre-discount rate.
  const originalTotal = card.originalPricePerNight
    ? (() => {
        const sub = card.originalPricePerNight * nights;
        return sub + 350000 + Math.round(sub * 0.09);
      })()
    : null;

  return `
    <div class="card-body" data-act="open-listing" data-slug="${esc(card.slug)}">
      <div class="card-row">
        <h3 class="card-title">${esc(card.typeLabel)} tại ${esc(card.city)}</h3>
        <div class="card-rating">
          ${card.reviewCount ? `★ ${esc(card.rating.toFixed(2))} (${esc(card.reviewCount)})` : '★ Mới'}
        </div>
      </div>
      <div class="card-sub card-name">${esc(card.title)}</div>
      <div class="card-sub">${esc(card.bedrooms)} phòng ngủ · ${esc(card.beds)} giường · ${esc(card.bathrooms)} phòng tắm</div>
      <div class="card-price">
        ${originalTotal ? `<s>${money(originalTotal)}</s> ` : ''}
        <b>${money(total)}</b> <span>cho ${nights} đêm</span>
      </div>
      <div class="card-perks">Đã gồm phí · Huỷ miễn phí</div>
    </div>
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

import { esc, money, longDate, nightsBetween } from '../util.js';
import { state, totalGuests, guestLabel } from '../store.js';
import { renderCard } from '../components/card.js';
import { renderCalendar } from '../components/calendar.js';
import { icon, amenityIcon } from '../components/icons.js';

const RATING_LABELS = {
  cleanliness: 'Mức độ sạch sẽ',
  accuracy: 'Độ chính xác',
  checkIn: 'Nhận phòng',
  communication: 'Giao tiếp',
  location: 'Vị trí',
  value: 'Giá trị'
};

export function renderDetail() {
  if (state.detailLoading || !state.detail) {
    return `<div class="shell" style="padding-block:32px 90px">
      <div class="skeleton" style="height:44vw;max-height:420px;border-radius:16px"></div>
      <div class="sk-line skeleton" style="width:40%;height:22px;margin-top:24px"></div>
      <div class="sk-line skeleton" style="width:60%"></div>
    </div>`;
  }

  const d = state.detail;
  const c = d.card;
  const nights = nightsBetween(state.checkIn, state.checkOut);

  return `
    <div class="shell" style="padding-block:20px 110px">
      <button class="back-link" data-act="go" data-href="/">← Tất cả chỗ nghỉ</button>

      <div class="detail-head">
        <div style="min-width:0">
          <h1>${esc(c.title)}</h1>
          <div class="detail-sub">
            <span>★ ${esc(c.rating.toFixed(2))}</span>
            <span class="dot">·</span>
            <u data-act="open" data-overlay="reviews">${esc(d.reviews.length)} đánh giá</u>
            ${c.isSuperhost ? '<span class="dot">·</span><span>Siêu chủ nhà</span>' : ''}
            <span class="dot">·</span>
            <u data-act="scroll-to" data-target="section-location">${esc(c.city)}, ${esc(c.country)}</u>
          </div>
        </div>
        <div class="detail-actions">
          <button class="text-btn" data-act="share">⤴ Chia sẻ</button>
          <button class="text-btn ${c.isFavorite ? 'is-on' : ''}" data-act="toggle-fav" data-id="${esc(c.id)}">
            ♥ ${c.isFavorite ? 'Đã lưu' : 'Lưu chỗ nghỉ'}
          </button>
        </div>
      </div>

      ${renderGallery(c)}

      <div class="detail-body">
        <div class="detail-main">
          ${renderSummary(d, c)}
          ${renderHostRow(d)}
          ${renderHighlights(d, c)}
          ${renderSleeping(c)}
          ${renderAmenities(d)}
          ${renderCalendarSection(nights)}
          ${renderReviews(d, c)}
          ${renderLocation(c)}
          ${renderHostProfile(d)}
          ${renderThingsToKnow(d)}
        </div>

        ${renderBookPanel(d, c, nights)}
      </div>

      ${d.similar.length ? `
        <section style="margin-top:44px;border-top:1px solid var(--divider);padding-top:32px">
          <h2 class="section-title" style="font-size:22px">Chỗ nghỉ tương tự</h2>
          <p class="section-sub">Cùng khu vực hoặc cùng loại hình với ${esc(c.title)}</p>
          <div class="card-grid">${d.similar.map(s => renderCard(s, { lazy: true })).join('')}</div>
        </section>` : ''}
    </div>

    ${renderMobileBar(nights)}
  `;
}

function renderGallery(c) {
  const imgs = c.images.slice(0, 5);
  while (imgs.length < 5) imgs.push(imgs[imgs.length - 1] ?? '');

  return `
    <div class="gallery" style="grid-template-columns:2fr 1fr 1fr;grid-template-rows:repeat(2,clamp(110px,16vw,215px))">
      ${imgs.map((src, i) => `
        <figure class="gallery-tile" style="${i === 0 ? 'grid-row:span 2;' : ''}margin:0"
                data-act="open" data-overlay="photos">
          <img src="${esc(src)}" alt="${esc(c.title)} — ảnh ${i + 1}" loading="${i === 0 ? 'eager' : 'lazy'}" decoding="async">
        </figure>
      `).join('')}
      <button class="gallery-all" data-act="open" data-overlay="photos">⊞ Hiện tất cả ảnh</button>
    </div>
  `;
}

function renderSummary(d, c) {
  return `
    <div class="summary-block">
      <div class="summary-title">${esc(c.typeLabel)} tại ${esc(c.city)} do ${esc(d.host.name)} cho thuê</div>
      <div class="summary-meta">${esc(c.maxGuests)} khách · ${esc(c.bedrooms)} phòng ngủ · ${esc(c.beds)} giường · ${esc(c.bathrooms)} phòng tắm</div>
      <p class="summary-desc">${esc(d.description)}</p>
      ${c.highlight ? `<p class="summary-desc" style="margin-top:10px"><b>Điểm nổi bật:</b> ${esc(c.highlight)}</p>` : ''}
    </div>
  `;
}

function renderHostRow(d) {
  const h = d.host;
  return `
    <div class="host-row">
      <div class="host-avatar" aria-hidden="true">${esc(h.initials)}</div>
      <div>
        <div class="host-name">Chủ nhà: ${esc(h.name)}</div>
        <div class="host-meta">${h.isSuperhost ? 'Siêu chủ nhà · ' : ''}${esc(h.yearsHosting)} năm kinh nghiệm đón khách</div>
      </div>
    </div>
  `;
}

function renderHighlights(d, c) {
  const items = [
    c.isSuperhost && {
      ic: 'star', title: `${d.host.name} là Siêu chủ nhà`,
      sub: 'Siêu chủ nhà là những chủ nhà giàu kinh nghiệm, được đánh giá cao.'
    },
    c.amenityKeys.includes('selfcheckin') && {
      ic: 'selfcheckin', title: 'Tự nhận phòng',
      sub: 'Bạn có thể tự nhận phòng bằng khoá số ở cửa.'
    },
    {
      ic: 'heart', title: 'Huỷ miễn phí trước 48 giờ',
      sub: d.cancellationPolicy
    },
    c.amenityKeys.includes('workspace') && {
      ic: 'workspace', title: 'Không gian riêng để làm việc',
      sub: 'Phòng có bàn làm việc và wifi tốc độ cao, phù hợp làm việc từ xa.'
    }
  ].filter(Boolean);

  return `
    <div class="highlights">
      ${items.map(i => `
        <div class="highlight">
          <span class="ic">${icon(i.ic, 24)}</span>
          <span class="tx"><b>${esc(i.title)}</b><span>${esc(i.sub)}</span></span>
        </div>
      `).join('')}
    </div>
  `;
}

/** Airbnb's "Where you'll sleep" strip — one card per bedroom. */
function renderSleeping(c) {
  const rooms = Array.from({ length: Math.max(1, c.bedrooms) }, (_, i) => {
    const perRoom = Math.max(1, Math.round(c.beds / Math.max(1, c.bedrooms)));
    const extra = i === 0 ? c.beds - perRoom * c.bedrooms : 0;
    return { name: `Phòng ngủ ${i + 1}`, beds: Math.max(1, perRoom + extra) };
  });

  return `
    <section class="detail-section" id="section-sleeping">
      <h2>Nơi bạn sẽ ngủ</h2>
      <div class="sleep-grid">
        ${rooms.map(r => `
          <div class="sleep-card">
            <span class="ic">${icon('house', 26)}</span>
            <b>${esc(r.name)}</b>
            <span>${esc(r.beds)} giường đôi</span>
          </div>
        `).join('')}
      </div>
    </section>
  `;
}

function renderAmenities(d) {
  const all = d.amenityGroups.flatMap(g => g.items);
  const preview = all.slice(0, 8);

  return `
    <section class="detail-section" id="section-amenities">
      <h2>Tiện nghi tại đây</h2>
      <div class="amenity-grid">
        ${preview.map(a => `
          <div class="amenity"><span class="ic">${amenityIcon(a.key)}</span><span>${esc(a.label)}</span></div>
        `).join('')}
      </div>
      ${all.length > preview.length ? `
        <button class="btn btn-outline btn-sm" style="margin-top:18px" data-act="open" data-overlay="amenities">
          Hiện tất cả ${esc(all.length)} tiện nghi
        </button>` : ''}
    </section>
  `;
}

function renderCalendarSection(nights) {
  return `
    <section class="detail-section" id="section-calendar">
      <h2>${esc(nights)} đêm tại ${esc(state.detail.card.city)}</h2>
      <p style="margin:-8px 0 18px;font-size:14px;color:var(--ink-muted)">
        ${esc(longDate(state.checkIn))} – ${esc(longDate(state.checkOut))}
      </p>
      ${renderCalendar(state.checkIn)}
      <div style="display:flex;justify-content:flex-end;margin-top:14px">
        <button class="text-btn" data-act="clear-dates">Xoá ngày</button>
      </div>
    </section>
  `;
}

function renderReviews(d, c) {
  const rb = d.ratingBreakdown;
  const shown = d.reviews.slice(0, 6);

  return `
    <section class="detail-section" id="section-reviews">
      <div class="rating-summary">
        <span class="rating-big">★ ${esc(c.rating.toFixed(2))}</span>
        <span style="font-size:15px;color:var(--ink-muted)">· ${esc(d.reviews.length)} đánh giá</span>
        ${c.isGuestFavorite ? '<span class="badge confirmed" style="margin-left:6px">Khách yêu thích</span>' : ''}
      </div>

      <div class="rating-bars">
        ${Object.entries(RATING_LABELS).map(([key, label]) => `
          <div class="rating-bar">
            <span>${esc(label)}</span>
            <span class="track"><span class="fill" style="width:${(rb[key] / 5) * 100}%"></span></span>
            <span class="val">${esc(rb[key].toFixed(1))}</span>
          </div>
        `).join('')}
      </div>

      <div class="review-grid">
        ${shown.map(r => `
          <article class="review">
            <div class="review-head">
              <span class="avatar" aria-hidden="true">${esc(r.authorInitials)}</span>
              <div style="min-width:0">
                <div class="review-name">${esc(r.authorName)}</div>
                <div class="review-when">${esc(r.authorLocation ? r.authorLocation + ' · ' : '')}${esc(r.when)}</div>
              </div>
            </div>
            <p>${esc(r.text)}</p>
          </article>
        `).join('')}
      </div>

      ${d.reviews.length > shown.length ? `
        <button class="btn btn-outline btn-sm" style="margin-top:20px" data-act="open" data-overlay="reviews">
          Hiện tất cả ${esc(d.reviews.length)} đánh giá
        </button>` : ''}
    </section>
  `;
}

function renderLocation(c) {
  return `
    <section class="detail-section" id="section-location">
      <h2>Vị trí chỗ nghỉ</h2>
      <p style="margin:-8px 0 16px;font-size:14.5px;color:var(--ink-muted)">${esc(c.city)}, ${esc(c.country)}</p>
      <div class="detail-map" id="detail-map"></div>
      <p style="margin:14px 0 0;font-size:13.5px;color:var(--ink-muted)">
        Vị trí chính xác được cung cấp sau khi đặt chỗ thành công.
      </p>
    </section>
  `;
}

function renderHostProfile(d) {
  const h = d.host;
  return `
    <section class="detail-section" id="section-host">
      <h2>Gặp gỡ chủ nhà</h2>
      <div style="display:flex;gap:20px;flex-wrap:wrap;align-items:flex-start">
        <div style="display:flex;align-items:center;gap:14px;min-width:220px">
          <div class="host-avatar" style="width:64px;height:64px;font-size:20px">${esc(h.initials)}</div>
          <div>
            <div style="font-size:20px;font-weight:800">${esc(h.name)}</div>
            <div class="host-meta">${h.isSuperhost ? 'Siêu chủ nhà' : 'Chủ nhà'}</div>
          </div>
        </div>
        <div style="display:grid;gap:8px;font-size:14px;color:var(--ink-body);min-width:200px">
          <div><b>${esc(h.totalReviews)}</b> đánh giá</div>
          <div><b>★ ${esc(h.averageRating.toFixed(2))}</b> điểm trung bình</div>
          <div><b>${esc(h.listingCount)}</b> chỗ nghỉ đang cho thuê</div>
          <div>${esc(h.joinedLabel)}</div>
        </div>
      </div>
      ${h.bio ? `<p class="summary-desc" style="margin-top:18px">${esc(h.bio)}</p>` : ''}
      <div style="display:grid;gap:6px;margin-top:16px;font-size:14px;color:var(--ink-body)">
        <div>Tỉ lệ phản hồi: <b>${esc(h.responseRate)}</b></div>
        <div>Thời gian phản hồi: <b>${esc(h.responseTime)}</b></div>
      </div>
      ${h.userId ? `
        <button class="btn btn-outline btn-sm" style="margin-top:18px"
                data-act="message-host" data-id="${esc(state.detail.card.id)}">
          Nhắn tin cho ${esc(h.name)}
        </button>`
      : `<button class="btn btn-outline btn-sm" style="margin-top:18px" data-act="open" data-overlay="contact-host">
          Nhắn tin cho chủ nhà
        </button>`}
    </section>
  `;
}

function renderThingsToKnow(d) {
  return `
    <section class="detail-section" id="section-know">
      <h2>Những điều cần biết</h2>
      <div class="know-grid">
        <div class="know">
          <h3>Nội quy nhà</h3>
          <ul>${d.houseRules.map(r => `<li>${esc(r)}</li>`).join('')}</ul>
        </div>
        <div class="know">
          <h3>An toàn &amp; chỗ ở</h3>
          <ul>${d.safetyInfo.map(r => `<li>${esc(r)}</li>`).join('')}</ul>
        </div>
        <div class="know">
          <h3>Chính sách huỷ</h3>
          <ul><li>${esc(d.cancellationPolicy)}</li></ul>
        </div>
      </div>
    </section>
  `;
}

function renderBookPanel(d, c, nights) {
  const q = state.quote;
  const result = state.bookingResult;

  return `
    <aside class="book-panel">
      <div class="book-price">
        <span class="amount">${money(c.pricePerNight)}</span>
        <span class="per">/ đêm</span>
        ${c.isGuestFavorite ? '<span class="per" style="margin-left:auto">★ ' + esc(c.rating.toFixed(2)) + '</span>' : ''}
      </div>

      <div class="book-fields">
        <div class="book-dates">
          <button class="book-field" data-act="open" data-overlay="dates">
            <span class="cap">NHẬN PHÒNG</span>
            <span class="val">${esc(longDate(state.checkIn))}</span>
          </button>
          <button class="book-field" data-act="open" data-overlay="dates">
            <span class="cap">TRẢ PHÒNG</span>
            <span class="val">${esc(longDate(state.checkOut))}</span>
          </button>
        </div>
        <div class="book-guests">
          <button class="book-guests-open" data-act="open" data-overlay="guests">
            <span class="cap">KHÁCH</span>
            <span class="val">${esc(guestLabel())}</span>
          </button>
          <div style="display:flex;align-items:center;gap:10px">
            <button class="round-btn lg" data-act="guests-dec" aria-label="Giảm số khách" ${totalGuests() <= 1 ? 'disabled' : ''}>−</button>
            <button class="round-btn lg" data-act="guests-inc" aria-label="Tăng số khách" ${totalGuests() >= c.maxGuests ? 'disabled' : ''}>+</button>
          </div>
        </div>
      </div>

      <button class="btn btn-primary btn-block" style="margin-top:16px" data-act="open" data-overlay="checkout">
        Đặt chỗ ngay
      </button>
      <p class="book-note">Bạn chưa bị trừ tiền ở bước này</p>

      ${q ? `
        <div class="book-lines">
          <div class="book-line"><u>${money(q.pricePerNight)} × ${esc(q.nights)} đêm</u><span>${money(q.subtotal)}</span></div>
          <div class="book-line"><u>Phí dọn dẹp</u><span>${money(q.cleaningFee)}</span></div>
          <div class="book-line"><u>Phí dịch vụ StayHost</u><span>${money(q.serviceFee)}</span></div>
          <div class="book-rule"></div>
          <div class="book-total"><span>Tổng cộng</span><span>${money(q.total)}</span></div>
        </div>
        ${q.guestsExceeded ? `
          <div class="book-alert is-error">
            <b>Vượt quá sức chứa</b>
            <span>Chỗ nghỉ này nhận tối đa ${esc(q.maxGuests)} khách.</span>
          </div>` : ''}
      ` : `<div class="book-lines"><div class="book-line"><span>Đang tính giá…</span></div></div>`}

      ${result ? `
        <div class="book-alert">
          <b>Đã giữ chỗ cho bạn · ${esc(result.reference)}</b>
          <span>${esc(result.nights)} đêm · ${esc(result.guests)} khách · ${money(result.total)}. Chủ nhà sẽ xác nhận trong 12 giờ.</span>
        </div>` : ''}

      ${state.bookingError ? `
        <div class="book-alert is-error">
          <b>Không đặt được</b><span>${esc(state.bookingError)}</span>
        </div>` : ''}

      <button class="text-btn" style="margin-top:14px;justify-content:center" data-act="open" data-overlay="report">
        ⚑ Báo cáo chỗ nghỉ này
      </button>
    </aside>
  `;
}

function renderMobileBar(nights) {
  const q = state.quote;
  return `
    <div class="mobile-bar">
      <div style="min-width:0">
        <div class="amount">${q ? money(q.total) : money(state.detail.card.pricePerNight)}</div>
        <div class="meta">${esc(nights)} đêm · ${esc(totalGuests())} khách</div>
      </div>
      <button class="btn btn-primary" data-act="open" data-overlay="checkout">Đặt chỗ ngay</button>
    </div>
  `;
}

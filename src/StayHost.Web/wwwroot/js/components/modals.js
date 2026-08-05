import { esc, money, longDate, nightsBetween } from '../util.js';
import { state, activeFilterCount, totalGuests } from '../store.js';
import { renderCalendar } from './calendar.js';
import { icon, amenityIcon, CATEGORY_ICON } from './icons.js';

export function renderOverlay() {
  const kind = state.overlay;
  if (!kind) return '';

  const body = {
    filters: filtersModal,
    login: loginModal,
    language: languageModal,
    photos: photosModal,
    amenities: amenitiesModal,
    reviews: reviewsModal,
    checkout: checkoutModal,
    help: helpModal,
    'contact-host': contactHostModal,
    report: reportModal,
    guests: guestsModal,
    dates: datesModal
  }[kind];

  if (!body) return '';
  return `<div class="overlay" data-act="close-overlay-bg">${body()}</div>`;
}

function shell({ title, body, foot, size = '' }) {
  return `
    <div class="modal ${size}" role="dialog" aria-modal="true" aria-label="${esc(title)}">
      <div class="modal-head">
        <button class="modal-close" data-act="close-overlay" aria-label="Đóng">✕</button>
        <h2>${esc(title)}</h2>
        <span style="width:32px"></span>
      </div>
      <div class="modal-body">${body}</div>
      ${foot ? `<div class="modal-foot">${foot}</div>` : ''}
    </div>
  `;
}

/* ----------------------------------------------------------------- filters */

function filtersModal() {
  const meta = state.meta;
  if (!meta) return shell({ title: 'Bộ lọc', body: '<p>Đang tải…</p>' });

  const span = Math.max(1, meta.maxPrice - meta.minPrice);
  const lowPct = ((state.minPrice - meta.minPrice) / span) * 100;
  const highPct = ((state.maxPrice - meta.minPrice) / span) * 100;

  const bars = meta.priceHistogram.map((h, i) => {
    const max = Math.max(...meta.priceHistogram, 1);
    const at = meta.minPrice + (span * i) / (meta.priceHistogram.length - 1);
    const inRange = at >= state.minPrice && at <= state.maxPrice;
    return `<i class="${inRange ? 'in' : ''}" style="height:${Math.max(6, (h / max) * 100)}%"></i>`;
  }).join('');

  const groups = groupBy(meta.amenities, a => a.group);

  return shell({
    title: 'Bộ lọc',
    body: `
      <section class="modal-section">
        <h3>Khoảng giá</h3>
        <span class="hint">Giá mỗi đêm, đã gồm phí và thuế</span>
        <div class="histogram">${bars}</div>
        <div class="range-wrap">
          <span class="range-track"></span>
          <span class="range-fill" style="left:${lowPct}%;right:${100 - highPct}%"></span>
          <input type="range" min="${meta.minPrice}" max="${meta.maxPrice}" step="100000"
                 value="${state.minPrice}" data-act="set-min-price" aria-label="Giá tối thiểu">
          <input type="range" min="${meta.minPrice}" max="${meta.maxPrice}" step="100000"
                 value="${state.maxPrice}" data-act="set-max-price" aria-label="Giá tối đa">
        </div>
        <div class="range-vals">
          <label><span class="cap">Tối thiểu</span><div class="amt">${money(state.minPrice)}</div></label>
          <label><span class="cap">Tối đa</span><div class="amt">${money(state.maxPrice)}${state.maxPrice >= meta.maxPrice ? '+' : ''}</div></label>
        </div>
      </section>

      <section class="modal-section">
        <h3>Loại nơi ở</h3>
        <span class="hint">Bạn muốn ở trọn chỗ nghỉ hay chia sẻ với người khác?</span>
        <div class="opt-grid">
          ${meta.roomTypes.map(r => `
            <button class="opt ${state.roomType === r.key ? 'is-on' : ''}" data-act="set-room-type" data-key="${esc(r.key)}">
              <b>${esc(r.label)}</b><span>${esc(r.hint)}</span>
            </button>
          `).join('')}
        </div>
      </section>

      <section class="modal-section">
        <h3>Phòng và giường</h3>
        ${counter('Phòng ngủ', state.bedrooms, 'bedrooms')}
        ${counter('Giường', state.beds, 'beds')}
        ${counter('Phòng tắm', state.bathrooms, 'bathrooms')}
      </section>

      <section class="modal-section">
        <h3>Loại chỗ ở</h3>
        <div class="pill-row" style="margin-top:14px">
          ${meta.categories.map(c => `
            <button class="pill ${state.category === c.key ? 'is-on' : ''}" data-act="pick-category" data-key="${esc(c.key)}">
              ${icon(CATEGORY_ICON[c.key] ?? 'all', 17)} ${esc(c.label)} (${esc(c.count)})
            </button>
          `).join('')}
        </div>
      </section>

      ${Object.entries(groups).map(([group, items]) => `
        <section class="modal-section">
          <h3>${esc(group)}</h3>
          <div class="pill-row" style="margin-top:14px">
            ${items.map(a => `
              <button class="pill ${state.amenities.includes(a.key) ? 'is-on' : ''}"
                      data-act="toggle-amenity" data-key="${esc(a.key)}"
                      aria-pressed="${state.amenities.includes(a.key)}">
                ${amenityIcon(a.key, 17)} ${esc(a.label)}
              </button>
            `).join('')}
          </div>
        </section>
      `).join('')}

      <section class="modal-section">
        <h3>Lựa chọn nổi bật</h3>
        <div class="pill-row" style="margin-top:14px">
          <button class="pill ${state.superhostOnly ? 'is-on' : ''}" data-act="toggle-superhost">◈ Siêu chủ nhà</button>
          <button class="pill ${state.guestFavoriteOnly ? 'is-on' : ''}" data-act="toggle-guest-fav">♥ Khách yêu thích</button>
        </div>
      </section>

      <section class="modal-section">
        <h3>Sắp xếp kết quả</h3>
        <select class="field" data-act="set-sort" style="margin-top:14px">
          ${[['reco', 'Đề xuất cho bạn'], ['low', 'Giá thấp đến cao'], ['high', 'Giá cao đến thấp'],
             ['rating', 'Đánh giá cao nhất'], ['reviews', 'Nhiều đánh giá nhất']]
            .map(([v, l]) => `<option value="${v}" ${state.sort === v ? 'selected' : ''}>${esc(l)}</option>`).join('')}
        </select>
      </section>
    `,
    foot: `
      <button class="text-btn" data-act="reset-filters">Xoá tất cả</button>
      <button class="btn btn-dark btn-sm" data-act="close-overlay">
        Hiện ${esc(state.results.total)} chỗ nghỉ${activeFilterCount() ? ` (${activeFilterCount()} bộ lọc)` : ''}
      </button>
    `
  });
}

function counter(label, value, key) {
  return `
    <div class="count-row">
      <div class="tx"><b>${esc(label)}</b></div>
      <div class="count-ctl">
        <button class="round-btn" data-act="count-dec" data-key="${esc(key)}" aria-label="Giảm ${esc(label)}" ${value <= 0 ? 'disabled' : ''}>−</button>
        <span class="num">${value ? value + '+' : 'Bất kỳ'}</span>
        <button class="round-btn" data-act="count-inc" data-key="${esc(key)}" aria-label="Tăng ${esc(label)}">+</button>
      </div>
    </div>
  `;
}

function groupBy(items, keyFn) {
  return items.reduce((acc, item) => {
    const k = keyFn(item);
    (acc[k] ||= []).push(item);
    return acc;
  }, {});
}

/* ------------------------------------------------------------------- dates */

function datesModal() {
  const nights = nightsBetween(state.checkIn, state.checkOut);
  return shell({
    title: 'Chọn ngày',
    body: `
      <div style="margin-bottom:20px">
        <h3 style="margin:0;font-size:20px;font-weight:800">${esc(nights)} đêm</h3>
        <p style="margin:4px 0 0;font-size:14px;color:var(--ink-muted)">
          ${esc(longDate(state.checkIn))} – ${esc(longDate(state.checkOut))}
        </p>
      </div>
      ${renderCalendar(state.checkIn)}
      <div style="display:flex;gap:8px;flex-wrap:wrap;margin-top:20px">
        ${[['Cuối tuần này', 'weekend'], ['1 tuần', 'week'], ['2 tuần', 'fortnight'], ['1 tháng', 'month']]
          .map(([label, key]) => `<button class="pill" data-act="date-preset" data-key="${key}">${esc(label)}</button>`).join('')}
      </div>
    `,
    foot: `
      <button class="text-btn" data-act="clear-dates">Xoá ngày</button>
      <button class="btn btn-dark btn-sm" data-act="close-overlay">Xong</button>
    `
  });
}

/* ------------------------------------------------------------------ guests */

function guestsModal() {
  const g = state.guests;
  const rows = [
    ['adults', 'Người lớn', 'Từ 13 tuổi trở lên'],
    ['children', 'Trẻ em', 'Độ tuổi 2 – 12'],
    ['infants', 'Em bé', 'Dưới 2 tuổi'],
    ['pets', 'Thú cưng', 'Bạn mang theo thú hỗ trợ?']
  ];

  return shell({
    title: 'Khách',
    size: 'narrow',
    body: rows.map(([key, label, hint]) => `
      <div class="count-row">
        <div class="tx"><b>${esc(label)}</b><span>${esc(hint)}</span></div>
        <div class="count-ctl">
          <button class="round-btn" data-act="guest-dec" data-key="${key}" ${g[key] <= (key === 'adults' ? 1 : 0) ? 'disabled' : ''}>−</button>
          <span class="num">${esc(g[key])}</span>
          <button class="round-btn" data-act="guest-inc" data-key="${key}">+</button>
        </div>
      </div>
    `).join(''),
    foot: `<span style="font-size:13px;color:var(--ink-muted)">Tổng ${esc(totalGuests())} khách</span>
           <button class="btn btn-dark btn-sm" data-act="close-overlay">Xong</button>`
  });
}

/* ------------------------------------------------------------------- login */

function loginModal() {
  return shell({
    title: 'Đăng nhập hoặc đăng ký',
    size: 'narrow',
    body: `
      <h3 style="margin:0 0 18px;font-size:20px;font-weight:800">Chào mừng đến StayHost</h3>
      <form data-act="submit-login">
        <label class="form-field">
          <span class="cap">Quốc gia / Khu vực</span>
          <select><option>Việt Nam (+84)</option><option>United States (+1)</option><option>Japan (+81)</option></select>
        </label>
        <label class="form-field">
          <span class="cap">Số điện thoại</span>
          <input type="tel" placeholder="912 345 678" required>
        </label>
        <p style="font-size:12.5px;color:var(--ink-muted);line-height:1.5">
          Chúng tôi sẽ gọi hoặc nhắn tin để xác nhận số của bạn. Có thể áp dụng phí tin nhắn và dữ liệu.
        </p>
        <button type="submit" class="btn btn-primary btn-block" style="margin-top:14px">Tiếp tục</button>
      </form>
      <div style="display:flex;align-items:center;gap:12px;margin:20px 0">
        <span style="flex:1;height:1px;background:var(--divider)"></span>
        <span style="font-size:12px;color:var(--ink-muted)">hoặc</span>
        <span style="flex:1;height:1px;background:var(--divider)"></span>
      </div>
      <div style="display:grid;gap:10px">
        ${[['✉', 'Tiếp tục với email'], ['◉', 'Tiếp tục với Google'], ['', 'Tiếp tục với Apple'], ['ⓕ', 'Tiếp tục với Facebook']]
          .map(([ic, label]) => `
            <button class="btn btn-outline btn-block btn-sm" style="text-align:left;display:flex;gap:12px;align-items:center" data-act="demo-auth">
              <span aria-hidden="true">${esc(ic)}</span> ${esc(label)}
            </button>`).join('')}
      </div>
    `
  });
}

/* ---------------------------------------------------------------- language */

function languageModal() {
  const meta = state.meta;
  if (!meta) return shell({ title: 'Ngôn ngữ', body: '<p>Đang tải…</p>' });

  return shell({
    title: 'Ngôn ngữ & tiền tệ',
    body: `
      <section class="modal-section">
        <h3>Ngôn ngữ đề xuất</h3>
        <div class="lang-grid" style="margin-top:14px">
          ${meta.languages.map(l => `
            <button class="lang ${state.language.code === l.code ? 'is-on' : ''}" data-act="set-language" data-key="${esc(l.code)}">
              <b>${esc(l.label)}</b><span>${esc(l.region)}</span>
            </button>
          `).join('')}
        </div>
      </section>
      <section class="modal-section">
        <h3>Chọn loại tiền tệ</h3>
        <div class="lang-grid" style="margin-top:14px">
          ${meta.currencies.map(c => `
            <button class="lang ${state.currency.code === c.code ? 'is-on' : ''}" data-act="set-currency" data-key="${esc(c.code)}">
              <b>${esc(c.label)}</b><span>${esc(c.code)} — ${esc(c.symbol)}</span>
            </button>
          `).join('')}
        </div>
      </section>
    `
  });
}

/* ------------------------------------------------------------------ photos */

function photosModal() {
  const c = state.detail?.card;
  if (!c) return '';

  const captions = ['Ảnh chính', 'Phòng khách', 'Phòng ngủ', 'Không gian ngoài trời', 'Phòng tắm'];
  return shell({
    title: `${c.title} — ${c.images.length} ảnh`,
    size: 'wide',
    body: `
      <div class="lightbox-grid">
        ${c.images.map((src, i) => `
          <figure>
            <img src="${esc(src)}" alt="${esc(c.title)} — ảnh ${i + 1}" loading="lazy" decoding="async">
            <figcaption>${esc(captions[i] ?? `Ảnh ${i + 1}`)}</figcaption>
          </figure>
        `).join('')}
      </div>
    `
  });
}

/* --------------------------------------------------------------- amenities */

function amenitiesModal() {
  const d = state.detail;
  if (!d) return '';

  return shell({
    title: 'Nơi này có những gì',
    body: d.amenityGroups.map(g => `
      <section class="modal-section">
        <h3>${esc(g.group)}</h3>
        <div style="display:grid;gap:2px;margin-top:12px">
          ${g.items.map(a => `
            <div class="amenity" style="padding:14px 0;border-bottom:1px solid #f0f0f0">
              <span class="ic">${amenityIcon(a.key)}</span><span>${esc(a.label)}</span>
            </div>
          `).join('')}
        </div>
      </section>
    `).join('')
  });
}

/* ----------------------------------------------------------------- reviews */

function reviewsModal() {
  const d = state.detail;
  if (!d) return '';

  const term = (state.reviewQuery ?? '').trim().toLowerCase();
  let list = term
    ? d.reviews.filter(r => r.text.toLowerCase().includes(term) || r.authorName.toLowerCase().includes(term))
    : d.reviews.slice();

  if (state.reviewSort === 'high') list.sort((a, b) => b.rating - a.rating);
  else if (state.reviewSort === 'low') list.sort((a, b) => a.rating - b.rating);

  return shell({
    title: `★ ${d.card.rating.toFixed(2)} · ${d.reviews.length} đánh giá`,
    size: 'wide',
    body: `
      <div style="display:flex;gap:12px;flex-wrap:wrap;margin-bottom:20px">
        <input type="search" class="field" style="flex:1 1 220px" placeholder="Tìm trong đánh giá"
               value="${esc(state.reviewQuery ?? '')}" data-act="review-search">
        <select class="field" style="flex:0 0 200px;width:auto" data-act="review-sort">
          ${[['recent', 'Mới nhất'], ['high', 'Điểm cao nhất'], ['low', 'Điểm thấp nhất']]
            .map(([v, l]) => `<option value="${v}" ${(state.reviewSort ?? 'recent') === v ? 'selected' : ''}>${esc(l)}</option>`).join('')}
        </select>
      </div>
      ${list.length ? '' : '<p style="font-size:14px;color:var(--ink-muted)">Không có đánh giá nào khớp từ khoá.</p>'}
      <div class="review-grid">
        ${list.map(r => `
          <article class="review">
            <div class="review-head">
              <span class="avatar" aria-hidden="true">${esc(r.authorInitials)}</span>
              <div style="min-width:0">
                <div class="review-name">${esc(r.authorName)}</div>
                <div class="review-when">${esc(r.authorLocation ? r.authorLocation + ' · ' : '')}${esc(r.when)}</div>
              </div>
              <span style="margin-left:auto;font-size:13px;font-weight:700">★ ${esc(r.rating.toFixed(1))}</span>
            </div>
            <p>${esc(r.text)}</p>
          </article>
        `).join('')}
      </div>
    `
  });
}

/* ---------------------------------------------------------------- checkout */

function checkoutModal() {
  const d = state.detail;
  const q = state.quote;
  if (!d || !q) return '';

  return shell({
    title: 'Xác nhận và thanh toán',
    body: `
      <div style="display:flex;gap:14px;align-items:center;padding-bottom:20px;border-bottom:1px solid var(--divider)">
        <img src="${esc(d.card.images[0])}" alt="" style="width:96px;height:72px;object-fit:cover;border-radius:12px">
        <div style="min-width:0">
          <div style="font-size:15px;font-weight:700">${esc(d.card.title)}</div>
          <div style="font-size:13.5px;color:var(--ink-muted)">${esc(d.card.city)} · ★ ${esc(d.card.rating.toFixed(2))} (${esc(d.card.reviewCount)})</div>
        </div>
      </div>

      <section class="modal-section">
        <h3>Chuyến đi của bạn</h3>
        <div style="display:grid;gap:12px;margin-top:14px;font-size:14.5px">
          <div class="book-line"><span><b>Ngày</b><br>${esc(longDate(state.checkIn))} – ${esc(longDate(state.checkOut))}</span>
            <button class="text-btn" data-act="close-overlay">Chỉnh sửa</button></div>
          <div class="book-line"><span><b>Khách</b><br>${esc(q.guests)} khách</span>
            <button class="text-btn" data-act="open" data-overlay="guests">Chỉnh sửa</button></div>
        </div>
      </section>

      <section class="modal-section">
        <h3>Thông tin liên hệ</h3>
        <div style="margin-top:14px">
          <label class="form-field"><span class="cap">Họ tên</span><input type="text" id="guest-name" placeholder="Nguyễn Văn A"></label>
          <label class="form-field"><span class="cap">Email</span><input type="email" id="guest-email" placeholder="ban@email.com"></label>
        </div>
      </section>

      <section class="modal-section">
        <h3>Chi tiết giá</h3>
        <div class="book-lines" style="margin-top:14px">
          <div class="book-line"><u>${money(q.pricePerNight)} × ${esc(q.nights)} đêm</u><span>${money(q.subtotal)}</span></div>
          <div class="book-line"><u>Phí dọn dẹp</u><span>${money(q.cleaningFee)}</span></div>
          <div class="book-line"><u>Phí dịch vụ StayHost</u><span>${money(q.serviceFee)}</span></div>
          <div class="book-rule"></div>
          <div class="book-total"><span>Tổng (${esc(state.currency.code)})</span><span>${money(q.total)}</span></div>
        </div>
      </section>

      <section class="modal-section">
        <h3>Chính sách huỷ</h3>
        <p style="font-size:14px;line-height:1.6;color:var(--ink-body);margin:10px 0 0">${esc(d.cancellationPolicy)}</p>
      </section>

      ${state.bookingError ? `<div class="book-alert is-error"><b>Không đặt được</b><span>${esc(state.bookingError)}</span></div>` : ''}
    `,
    foot: `
      <span style="font-size:15px;font-weight:800">${money(q.total)}</span>
      <button class="btn btn-primary btn-sm" data-act="confirm-booking" ${q.guestsExceeded ? 'disabled' : ''}>
        Xác nhận đặt chỗ
      </button>
    `
  });
}

/* ------------------------------------------------------------------- misc */

function helpModal() {
  return shell({
    title: 'Trung tâm trợ giúp',
    size: 'narrow',
    body: `
      <div style="display:grid;gap:14px">
        ${[
          ['Tôi cần thay đổi ngày đặt chỗ', 'Vào Chuyến đi của tôi → chọn đặt chỗ → Chỉnh sửa ngày.'],
          ['Chủ nhà chưa phản hồi', 'Sau 24 giờ, StayHost sẽ tự huỷ và hoàn tiền toàn bộ.'],
          ['Tôi muốn được hoàn tiền', 'Huỷ trước 48 giờ nhận phòng để được hoàn 100% tiền phòng.'],
          ['Liên hệ hỗ trợ 24/7', 'Hotline 1900 1234 hoặc chat trực tiếp trong ứng dụng.']
        ].map(([q, a]) => `
          <div style="border:1px solid var(--divider);border-radius:12px;padding:16px">
            <b style="font-size:14.5px">${esc(q)}</b>
            <p style="margin:6px 0 0;font-size:13.5px;color:var(--ink-muted);line-height:1.55">${esc(a)}</p>
          </div>`).join('')}
      </div>
    `
  });
}

function contactHostModal() {
  const h = state.detail?.host;
  if (!h) return '';
  return shell({
    title: `Nhắn tin cho ${h.name}`,
    size: 'narrow',
    body: `
      <p style="font-size:14px;color:var(--ink-muted);line-height:1.6;margin:0 0 16px">
        ${esc(h.name)} thường phản hồi ${esc(h.responseTime)} · tỉ lệ phản hồi ${esc(h.responseRate)}.
      </p>
      <label class="form-field">
        <span class="cap">Tin nhắn</span>
        <textarea rows="5" style="width:100%;padding:12px 14px;border:1px solid var(--line);border-radius:12px;font-size:14px"
                  placeholder="Chào ${esc(h.name)}, mình muốn hỏi về..."></textarea>
      </label>
      <button class="btn btn-primary btn-block" data-act="demo-auth">Gửi tin nhắn</button>
    `
  });
}

function reportModal() {
  return shell({
    title: 'Báo cáo chỗ nghỉ này',
    size: 'narrow',
    body: `
      <div style="display:grid;gap:8px">
        ${['Thông tin không chính xác', 'Không phải chỗ nghỉ thật', 'Lừa đảo', 'Nội dung xúc phạm', 'Lý do khác']
          .map(r => `<button class="opt" data-act="demo-auth"><b>${esc(r)}</b></button>`).join('')}
      </div>
    `
  });
}

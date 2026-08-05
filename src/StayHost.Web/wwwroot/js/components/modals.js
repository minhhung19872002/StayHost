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
    dates: datesModal,
    profile: profileModal,
    review: reviewModal,
    'listing-editor': listingEditorModal,
    'host-block': hostBlockModal,
    'cancel-trip': cancelTripModal
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
  if (state.authMode === 'forgot') return forgotModal();
  if (state.authMode === 'reset') return resetModal();

  const isRegister = state.authMode === 'register';

  return shell({
    title: isRegister ? 'Đăng ký' : 'Đăng nhập',
    size: 'narrow',
    body: `
      <h3 style="margin:0 0 4px;font-size:20px;font-weight:800">Chào mừng đến StayHost</h3>
      <p style="margin:0 0 18px;font-size:13.5px;color:var(--ink-muted)">
        ${isRegister ? 'Tạo tài khoản để đặt chỗ, lưu yêu thích và cho thuê nhà.' : 'Đăng nhập để tiếp tục.'}
      </p>

      <form data-act="submit-auth" novalidate>
        ${isRegister ? `
          <label class="form-field">
            <span class="cap">Họ và tên</span>
            <input type="text" name="fullName" autocomplete="name" placeholder="Nguyễn Văn A" required>
          </label>` : ''}

        <label class="form-field">
          <span class="cap">Email</span>
          <input type="email" name="email" autocomplete="email" placeholder="ban@email.com" required>
        </label>

        <label class="form-field">
          <span class="cap">Mật khẩu</span>
          <input type="password" name="password" autocomplete="${isRegister ? 'new-password' : 'current-password'}"
                 placeholder="Tối thiểu 8 ký tự" required>
        </label>

        ${isRegister ? `
          <label class="form-field">
            <span class="cap">Số điện thoại <span style="font-weight:400">(không bắt buộc)</span></span>
            <input type="tel" name="phone" autocomplete="tel" placeholder="0912 345 678">
          </label>` : ''}

        ${state.authError ? `<div class="form-error">${esc(state.authError)}</div>` : ''}

        <button type="submit" class="btn btn-primary btn-block" style="margin-top:6px" ${state.authBusy ? 'disabled' : ''}>
          ${state.authBusy ? 'Đang xử lý…' : isRegister ? 'Tạo tài khoản' : 'Đăng nhập'}
        </button>
      </form>

      ${!isRegister ? `
        <p style="text-align:right;margin:-6px 0 0">
          <button class="link-btn" style="font-weight:600;font-size:13px" data-act="switch-auth" data-mode="forgot">
            Quên mật khẩu?
          </button>
        </p>` : ''}

      <p style="text-align:center;font-size:13.5px;color:var(--ink-muted);margin:18px 0 0">
        ${isRegister ? 'Đã có tài khoản?' : 'Chưa có tài khoản?'}
        <button class="link-btn" data-act="switch-auth" data-mode="${isRegister ? 'login' : 'register'}">
          ${isRegister ? 'Đăng nhập' : 'Đăng ký ngay'}
        </button>
      </p>

      <div style="margin-top:22px;padding:14px;background:var(--surface-soft);border-radius:12px">
        <b style="font-size:12.5px">Tài khoản dùng thử</b>
        <div style="font-size:12.5px;color:var(--ink-muted);margin-top:6px;line-height:1.6">
          Khách: <code>guest@stayhost.vn</code><br>
          Chủ nhà: <code>host1@stayhost.vn</code><br>
          Mật khẩu: <code>stayhost123</code>
        </div>
        <button class="btn btn-outline btn-sm btn-block" style="margin-top:10px" data-act="fill-demo">Điền tài khoản chủ nhà</button>
      </div>
    `
  });
}

function forgotModal() {
  return shell({
    title: 'Quên mật khẩu',
    size: 'narrow',
    body: `
      <p style="margin:0 0 18px;font-size:14px;color:var(--ink-muted);line-height:1.6">
        Nhập email của bạn, chúng tôi sẽ gửi liên kết đặt lại mật khẩu.
      </p>
      <form data-act="submit-forgot">
        <label class="form-field">
          <span class="cap">Email</span>
          <input type="email" name="email" autocomplete="email" placeholder="ban@email.com" required>
        </label>
        ${state.authError ? `<div class="form-error">${esc(state.authError)}</div>` : ''}
        ${state.resetLink ? `
          <div class="book-alert">
            <b>Liên kết đặt lại của bạn</b>
            <span>Bản demo không gửi email thật — bấm nút dưới để tiếp tục.</span>
          </div>
          <button type="button" class="btn btn-dark btn-block" style="margin-top:12px" data-act="use-reset-link">
            Mở trang đặt lại mật khẩu
          </button>` : `
          <button type="submit" class="btn btn-primary btn-block" ${state.authBusy ? 'disabled' : ''}>
            ${state.authBusy ? 'Đang gửi…' : 'Gửi liên kết'}
          </button>`}
      </form>
      <p style="text-align:center;margin:18px 0 0">
        <button class="link-btn" data-act="switch-auth" data-mode="login">Quay lại đăng nhập</button>
      </p>
    `
  });
}

function resetModal() {
  return shell({
    title: 'Đặt mật khẩu mới',
    size: 'narrow',
    body: `
      <form data-act="submit-reset">
        <label class="form-field">
          <span class="cap">Mật khẩu mới</span>
          <input type="password" name="newPassword" autocomplete="new-password" placeholder="Tối thiểu 8 ký tự" required>
        </label>
        <label class="form-field">
          <span class="cap">Nhập lại mật khẩu</span>
          <input type="password" name="confirmPassword" autocomplete="new-password" required>
        </label>
        ${state.authError ? `<div class="form-error">${esc(state.authError)}</div>` : ''}
        <button type="submit" class="btn btn-primary btn-block" ${state.authBusy ? 'disabled' : ''}>
          ${state.authBusy ? 'Đang lưu…' : 'Đặt mật khẩu mới'}
        </button>
      </form>
    `
  });
}

/* ----------------------------------------------------------------- profile */

const PROFILE_TABS = [['profile', 'Hồ sơ'], ['security', 'Bảo mật'], ['devices', 'Thiết bị']];

function profileModal() {
  const u = state.user;
  if (!u) return '';
  const tab = state.profileTab ?? 'profile';

  return shell({
    title: 'Tài khoản',
    body: `
      <div style="display:flex;align-items:center;gap:14px;margin-bottom:18px">
        <span class="avatar" style="width:56px;height:56px;font-size:18px">${esc(u.initials)}</span>
        <div style="min-width:0">
          <div style="font-size:17px;font-weight:800">${esc(u.fullName)}</div>
          <div style="font-size:13px;color:var(--ink-muted)">${esc(u.email)} · ${esc(u.joinedLabel)}</div>
          <div style="margin-top:6px">
            ${u.emailConfirmed
              ? '<span class="badge confirmed">Email đã xác minh</span>'
              : `<span class="badge pending">Chưa xác minh email</span>
                 <button class="link-btn" style="margin-left:8px;font-size:12.5px" data-act="send-verification">Gửi liên kết</button>`}
          </div>
        </div>
      </div>

      <nav class="seg-tabs" style="margin-bottom:18px">
        ${PROFILE_TABS.map(([key, label]) => `
          <button class="seg-tab ${tab === key ? 'is-active' : ''}" data-act="profile-tab" data-key="${key}">${esc(label)}</button>
        `).join('')}
      </nav>

      ${tab === 'profile' ? `
        <form data-act="submit-profile">
          <label class="form-field"><span class="cap">Họ và tên</span>
            <input type="text" name="fullName" value="${esc(u.fullName)}" required></label>
          <label class="form-field"><span class="cap">Số điện thoại</span>
            <input type="tel" name="phone" value="${esc(u.phone ?? '')}"></label>
          <label class="form-field"><span class="cap">Giới thiệu</span>
            <textarea name="bio" rows="4"
              style="width:100%;padding:12px 14px;border:1px solid var(--line);border-radius:12px;font-size:14px">${esc(u.bio ?? '')}</textarea></label>
          <button type="submit" class="btn btn-primary btn-block">Lưu thay đổi</button>
        </form>` : ''}

      ${tab === 'security' ? `
        <form data-act="submit-change-password">
          <label class="form-field"><span class="cap">Mật khẩu hiện tại</span>
            <input type="password" name="currentPassword" autocomplete="current-password" required></label>
          <label class="form-field"><span class="cap">Mật khẩu mới</span>
            <input type="password" name="newPassword" autocomplete="new-password" placeholder="Tối thiểu 8 ký tự" required></label>
          <label class="form-field"><span class="cap">Nhập lại mật khẩu mới</span>
            <input type="password" name="confirmPassword" autocomplete="new-password" required></label>
          ${state.authError ? `<div class="form-error">${esc(state.authError)}</div>` : ''}
          <p style="font-size:12.5px;color:var(--ink-muted);line-height:1.5;margin:0 0 12px">
            Đổi mật khẩu sẽ đăng xuất mọi thiết bị khác.
          </p>
          <button type="submit" class="btn btn-primary btn-block">Đổi mật khẩu</button>
        </form>` : ''}

      ${tab === 'devices' ? `
        <div style="display:grid;gap:10px">
          ${(state.sessions ?? []).map(s => `
            <div class="cal-row">
              <div style="flex:1;min-width:0">
                <b style="font-size:14px">${esc(s.device)}</b>
                ${s.isCurrent ? '<span class="badge confirmed" style="margin-left:8px">Thiết bị này</span>' : ''}
                <div style="font-size:12.5px;color:var(--ink-muted)">
                  Đăng nhập ${esc(longDate(s.createdAt.slice(0, 10)))}
                </div>
              </div>
              ${s.isCurrent ? '' : `<button class="text-btn" data-act="revoke-session" data-id="${esc(s.id)}">Đăng xuất</button>`}
            </div>`).join('') || '<p style="font-size:14px;color:var(--ink-muted)">Đang tải phiên đăng nhập…</p>'}
        </div>` : ''}
    `
  });
}

/* ------------------------------------------------------------ guest review */

const REVIEW_FIELDS = [
  ['cleanliness', 'Mức độ sạch sẽ'],
  ['accuracy', 'Độ chính xác'],
  ['checkIn', 'Nhận phòng'],
  ['communication', 'Giao tiếp'],
  ['location', 'Vị trí'],
  ['value', 'Giá trị']
];

function reviewModal() {
  const b = state.reviewBooking;
  if (!b) return '';

  return shell({
    title: 'Đánh giá chuyến đi',
    body: `
      <div style="display:flex;gap:14px;align-items:center;padding-bottom:18px;border-bottom:1px solid var(--divider)">
        <img src="${esc(b.listingImage)}" alt="" style="width:88px;height:66px;object-fit:cover;border-radius:12px">
        <div style="min-width:0">
          <div style="font-size:15px;font-weight:700">${esc(b.listingTitle)}</div>
          <div style="font-size:13px;color:var(--ink-muted)">${esc(b.listingCity)} · ${esc(b.nights)} đêm</div>
        </div>
      </div>

      <form data-act="submit-review" data-booking="${esc(b.id)}">
        <div style="padding:20px 0;border-bottom:1px solid var(--divider)">
          <b style="font-size:15px">Điểm tổng thể</b>
          <div class="star-row" data-field="rating" style="margin-top:10px">
            ${[1, 2, 3, 4, 5].map(n => `
              <button type="button" class="star ${n <= (state.reviewDraft?.rating ?? 5) ? 'is-on' : ''}"
                      data-act="set-star" data-field="rating" data-value="${n}" aria-label="${n} sao">★</button>
            `).join('')}
          </div>
        </div>

        ${REVIEW_FIELDS.map(([key, label]) => `
          <div class="count-row">
            <div class="tx"><b>${esc(label)}</b></div>
            <div class="star-row" data-field="${key}">
              ${[1, 2, 3, 4, 5].map(n => `
                <button type="button" class="star sm ${n <= (state.reviewDraft?.[key] ?? 5) ? 'is-on' : ''}"
                        data-act="set-star" data-field="${key}" data-value="${n}" aria-label="${n} sao">★</button>
              `).join('')}
            </div>
          </div>
        `).join('')}

        <label class="form-field" style="margin-top:20px">
          <span class="cap">Cảm nhận của bạn</span>
          <textarea name="text" rows="5" required minlength="10"
            style="width:100%;padding:12px 14px;border:1px solid var(--line);border-radius:12px;font-size:14px"
            placeholder="Chỗ nghỉ thế nào? Chủ nhà hỗ trợ ra sao?">${esc(state.reviewDraft?.text ?? '')}</textarea>
        </label>

        <button type="submit" class="btn btn-primary btn-block">Gửi đánh giá</button>
      </form>
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

const PHOTO_CAPTIONS = ['Ảnh chính', 'Phòng khách', 'Phòng ngủ', 'Không gian ngoài trời', 'Phòng tắm'];

function photosModal() {
  const c = state.detail?.card;
  if (!c) return '';

  // A focused index turns the grid into a single-photo viewer with arrows.
  const index = state.photoIndex;
  if (index !== null && index !== undefined) {
    const total = c.images.length;
    return shell({
      title: `${index + 1} / ${total}`,
      size: 'wide',
      body: `
        <div class="lightbox-stage">
          <button class="lightbox-nav prev" data-act="photo-step" data-dir="-1"
                  aria-label="Ảnh trước" ${index === 0 ? 'disabled' : ''}>‹</button>
          <img src="${esc(c.images[index])}" alt="${esc(c.title)} — ảnh ${index + 1}">
          <button class="lightbox-nav next" data-act="photo-step" data-dir="1"
                  aria-label="Ảnh tiếp theo" ${index === total - 1 ? 'disabled' : ''}>›</button>
        </div>
        <p class="lightbox-caption">${esc(PHOTO_CAPTIONS[index] ?? `Ảnh ${index + 1}`)}</p>
        <div class="lightbox-strip">
          ${c.images.map((src, i) => `
            <button class="strip-thumb ${i === index ? 'is-on' : ''}" data-act="photo-open" data-index="${i}"
                    aria-label="Xem ảnh ${i + 1}">
              <img src="${esc(src)}" alt="" loading="lazy">
            </button>`).join('')}
        </div>
      `,
      foot: `
        <button class="text-btn" data-act="photo-grid">← Xem dạng lưới</button>
        <span style="font-size:13px;color:var(--ink-muted)">${esc(index + 1)} trong ${esc(total)} ảnh</span>
      `
    });
  }

  return shell({
    title: `${c.title} — ${c.images.length} ảnh`,
    size: 'wide',
    body: `
      <div class="lightbox-grid">
        ${c.images.map((src, i) => `
          <figure>
            <button class="lightbox-open" data-act="photo-open" data-index="${i}" aria-label="Phóng to ảnh ${i + 1}">
              <img src="${esc(src)}" alt="${esc(c.title)} — ảnh ${i + 1}" loading="lazy" decoding="async">
            </button>
            <figcaption>${esc(PHOTO_CAPTIONS[i] ?? `Ảnh ${i + 1}`)}</figcaption>
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

const CHECKOUT_STEPS = ['Chuyến đi', 'Thanh toán', 'Xác nhận'];

const PAY_METHODS = [
  ['card', 'Thẻ tín dụng / ghi nợ', 'Visa, Mastercard, JCB'],
  ['momo', 'Ví MoMo', 'Thanh toán qua ứng dụng MoMo'],
  ['bank', 'Chuyển khoản ngân hàng', 'Chuyển khoản nhanh 24/7']
];

function checkoutModal() {
  const d = state.detail;
  const q = state.quote;
  if (!d || !q) return '';

  const step = state.checkoutStep ?? 0;
  const blocked = q.guestsExceeded || q.belowMinNights;

  return shell({
    title: 'Đặt chỗ',
    body: `
      <div class="stepper-bar">
        ${CHECKOUT_STEPS.map((label, i) => `
          <div class="step-dot ${i === step ? 'is-active' : ''} ${i < step ? 'is-done' : ''}">
            <span class="n">${i < step ? '✓' : i + 1}</span>${esc(label)}
          </div>`).join('')}
      </div>

      <div style="display:flex;gap:14px;align-items:center;padding:18px 0;border-bottom:1px solid var(--divider)">
        <img src="${esc(d.card.images[0])}" alt="" style="width:96px;height:72px;object-fit:cover;border-radius:12px">
        <div style="min-width:0">
          <div style="font-size:15px;font-weight:700">${esc(d.card.title)}</div>
          <div style="font-size:13.5px;color:var(--ink-muted)">${esc(d.card.city)} · ★ ${esc(d.card.rating.toFixed(2))} (${esc(d.card.reviewCount)})</div>
        </div>
      </div>

      ${step === 0 ? checkoutStepTrip(q, d) : ''}
      ${step === 1 ? checkoutStepPayment(q) : ''}
      ${step === 2 ? checkoutStepReview(q, d) : ''}

      ${blocked ? `
        <div class="book-alert is-error">
          <b>Chưa đặt được</b>
          <span>${q.guestsExceeded
            ? `Chỗ nghỉ này nhận tối đa ${esc(q.maxGuests)} khách.`
            : `Chỗ nghỉ này yêu cầu tối thiểu ${esc(q.minNights)} đêm.`}</span>
        </div>` : ''}

      ${state.bookingError ? `<div class="book-alert is-error"><b>Không đặt được</b><span>${esc(state.bookingError)}</span></div>` : ''}
    `,
    foot: `
      <div style="min-width:0">
        <div style="font-size:16px;font-weight:800">${money(q.total)}</div>
        <div style="font-size:12px;color:var(--ink-muted)">${esc(q.nights)} đêm · đã gồm thuế</div>
      </div>
      <div style="display:flex;gap:10px">
        ${step > 0 ? '<button class="btn btn-outline btn-sm" data-act="checkout-back">Quay lại</button>' : ''}
        ${step < 2
          ? `<button class="btn btn-primary btn-sm" data-act="checkout-next" ${blocked ? 'disabled' : ''}>Tiếp tục</button>`
          : `<button class="btn btn-primary btn-sm" data-act="confirm-booking" ${blocked ? 'disabled' : ''}>Xác nhận và thanh toán</button>`}
      </div>
    `
  });
}

function checkoutStepTrip(q, d) {
  return `
    <section class="modal-section">
      <h3>Chuyến đi của bạn</h3>
      <div style="display:grid;gap:12px;margin-top:14px;font-size:14.5px">
        <div class="book-line"><span><b>Ngày</b><br>${esc(longDate(state.checkIn))} – ${esc(longDate(state.checkOut))}</span>
          <button class="text-btn" data-act="open" data-overlay="dates">Chỉnh sửa</button></div>
        <div class="book-line"><span><b>Khách</b><br>${esc(q.guests)} khách</span>
          <button class="text-btn" data-act="open" data-overlay="guests">Chỉnh sửa</button></div>
      </div>
    </section>

    <section class="modal-section">
      <h3>Thông tin liên hệ</h3>
      <div style="margin-top:14px">
        <label class="form-field"><span class="cap">Họ tên</span>
          <input type="text" id="guest-name" value="${esc(state.user?.fullName ?? '')}" placeholder="Nguyễn Văn A"></label>
        <label class="form-field"><span class="cap">Email</span>
          <input type="email" id="guest-email" value="${esc(state.user?.email ?? '')}" placeholder="ban@email.com"></label>
        <label class="form-field"><span class="cap">Lời nhắn cho chủ nhà <span style="font-weight:400">(không bắt buộc)</span></span>
          <textarea id="guest-note" rows="3"
            style="width:100%;padding:12px 14px;border:1px solid var(--line);border-radius:12px;font-size:14px"
            placeholder="Chúng mình đến khoảng 20:00…">${esc(state.checkoutNote ?? '')}</textarea></label>
      </div>
    </section>

    <section class="modal-section">
      <h3>Chính sách huỷ</h3>
      <p style="font-size:14px;line-height:1.6;color:var(--ink-body);margin:10px 0 0">
        <b>${esc(q.cancellationTier)}</b> — ${esc(q.cancellationSummary)}
      </p>
    </section>
  `;
}

function checkoutStepPayment(q) {
  const method = state.payMethod ?? 'card';

  return `
    <section class="modal-section">
      <h3>Chọn cách thanh toán</h3>
      <div style="display:grid;gap:10px;margin-top:14px">
        ${PAY_METHODS.map(([key, label, hint]) => `
          <button type="button" class="opt ${method === key ? 'is-on' : ''}" data-act="set-pay-method" data-key="${key}">
            <b>${esc(label)}</b><span>${esc(hint)}</span>
          </button>`).join('')}
      </div>

      ${method === 'card' ? `
        <div class="field-grid" style="margin-top:18px">
          <label class="form-field" style="grid-column:1/-1"><span class="cap">Số thẻ</span>
            <input id="card-number" inputmode="numeric" placeholder="4242 4242 4242 4242" value="4242 4242 4242 4242"></label>
          <label class="form-field"><span class="cap">Hết hạn</span>
            <input id="card-exp" placeholder="12/28" value="12/28"></label>
          <label class="form-field"><span class="cap">CVV</span>
            <input id="card-cvv" inputmode="numeric" placeholder="123" value="123"></label>
        </div>
        <p style="font-size:12.5px;color:var(--ink-muted);line-height:1.5">
          Bản demo dùng thẻ thử nghiệm, không có giao dịch thật nào được thực hiện.
        </p>` : ''}

      ${method === 'momo' ? `
        <p style="margin-top:18px;font-size:14px;color:var(--ink-body);line-height:1.6">
          Sau khi xác nhận, bạn sẽ được chuyển sang ứng dụng MoMo để hoàn tất thanh toán ${money(q.total)}.
        </p>` : ''}

      ${method === 'bank' ? `
        <div style="margin-top:18px;padding:16px;background:var(--surface-soft);border-radius:12px;font-size:14px;line-height:1.7">
          <div>Ngân hàng: <b>Vietcombank</b></div>
          <div>Số tài khoản: <b>0071 0009 8765</b></div>
          <div>Chủ tài khoản: <b>CONG TY STAYHOST</b></div>
          <div>Nội dung: <b>${esc(state.user?.initials ?? 'SH')} ${esc(q.listingId)}</b></div>
        </div>` : ''}
    </section>
  `;
}

function checkoutStepReview(q, d) {
  const method = PAY_METHODS.find(m => m[0] === (state.payMethod ?? 'card'));

  return `
    <section class="modal-section">
      <h3>Kiểm tra lần cuối</h3>
      <div style="display:grid;gap:12px;margin-top:14px;font-size:14.5px">
        <div class="book-line"><span>Ngày</span><span>${esc(longDate(state.checkIn))} – ${esc(longDate(state.checkOut))}</span></div>
        <div class="book-line"><span>Khách</span><span>${esc(q.guests)} khách</span></div>
        <div class="book-line"><span>Thanh toán bằng</span><span>${esc(method?.[1] ?? 'Thẻ')}</span></div>
      </div>
    </section>

    <section class="modal-section">
      <h3>Chi tiết giá</h3>
      <div class="book-lines" style="margin-top:14px">
        <div class="book-line"><u>${money(q.pricePerNight)} × ${esc(q.nights)} đêm</u><span>${money(q.subtotal + q.lengthDiscount)}</span></div>
        ${q.weekendSurcharge > 0
          ? `<div class="book-line"><u>Phụ thu cuối tuần</u><span>đã gồm</span></div>` : ''}
        ${q.lengthDiscount > 0
          ? `<div class="book-line" style="color:var(--brand-dark)"><u>Giảm giá ở dài ngày (${esc(q.lengthDiscountPercent)}%)</u><span>−${money(q.lengthDiscount)}</span></div>` : ''}
        <div class="book-line"><u>Phí dọn dẹp</u><span>${money(q.cleaningFee)}</span></div>
        <div class="book-line"><u>Phí dịch vụ StayHost</u><span>${money(q.serviceFee)}</span></div>
        <div class="book-line"><u>Thuế VAT 8%</u><span>${money(q.tax)}</span></div>
        <div class="book-rule"></div>
        <div class="book-total"><span>Tổng (${esc(state.currency.code)})</span><span>${money(q.total)}</span></div>
      </div>
    </section>

    <section class="modal-section">
      <h3>Chính sách huỷ</h3>
      <p style="font-size:14px;line-height:1.6;color:var(--ink-body);margin:10px 0 0">
        <b>${esc(q.cancellationTier)}</b> — ${esc(q.cancellationSummary)}
      </p>
    </section>
  `;
}

/* ------------------------------------------------------------ cancel trip */

function cancelTripModal() {
  const preview = state.cancelPreview;
  if (!preview) return '';

  return shell({
    title: 'Huỷ chuyến đi',
    size: 'narrow',
    body: `
      <p style="margin:0 0 18px;font-size:14.5px;line-height:1.6;color:var(--ink-body)">
        ${esc(preview.explanation)}
      </p>
      <div class="book-lines">
        <div class="book-line"><span>Đã thanh toán</span><span>${money(preview.total)}</span></div>
        <div class="book-line" style="color:var(--brand-dark)"><span>Sẽ hoàn lại</span><span>${money(preview.refund)}</span></div>
        <div class="book-rule"></div>
        <div class="book-total"><span>Không hoàn</span><span>${money(preview.penalty)}</span></div>
      </div>
    `,
    foot: `
      <button class="text-btn" data-act="close-overlay">Giữ chuyến đi</button>
      <button class="btn btn-primary btn-sm" data-act="confirm-cancel" data-id="${esc(preview.bookingId)}">
        Xác nhận huỷ
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

/* ---------------------------------------------------------- listing editor */

const BLANK_LISTING = {
  id: 0, title: '', city: '', typeKey: 'house', roomTypeKey: 'entire',
  bedrooms: 1, beds: 1, bathrooms: 1, maxGuests: 2,
  pricePerNight: 800000, cleaningFee: 200000, minNights: 1,
  instantBook: true, isPublished: true,
  description: '', highlight: '', images: [], amenityKeys: []
};

function listingEditorModal() {
  const l = { ...BLANK_LISTING, ...(state.editingListing ?? {}) };
  const meta = state.meta;
  const isNew = !l.id;

  return shell({
    title: isNew ? 'Đăng chỗ nghỉ mới' : 'Chỉnh sửa chỗ nghỉ',
    size: 'wide',
    body: `
      <form id="listing-form" data-act="submit-listing" data-id="${esc(l.id)}">
        <section class="modal-section">
          <h3>Thông tin cơ bản</h3>
          <div class="field-grid">
            <label class="form-field" style="grid-column:1/-1">
              <span class="cap">Tiêu đề *</span>
              <input name="title" value="${esc(l.title)}" placeholder="Villa hồ bơi riêng gần biển Mỹ Khê" required>
            </label>
            <label class="form-field">
              <span class="cap">Thành phố *</span>
              <input name="city" value="${esc(l.city)}" placeholder="Đà Nẵng" list="city-list" required>
              <datalist id="city-list">
                ${(meta?.cities ?? []).map(c => `<option value="${esc(c)}">`).join('')}
              </datalist>
            </label>
            <label class="form-field">
              <span class="cap">Loại chỗ ở</span>
              <select name="typeKey">
                ${(meta?.categories ?? []).filter(c => c.key !== 'all').map(c =>
                  `<option value="${esc(c.key)}" ${l.typeKey === c.key ? 'selected' : ''}>${esc(c.label)}</option>`).join('')}
              </select>
            </label>
            <label class="form-field">
              <span class="cap">Loại nơi ở</span>
              <select name="roomTypeKey">
                ${[['entire', 'Nguyên căn'], ['private', 'Phòng riêng'], ['shared', 'Phòng chung']].map(([v, t]) =>
                  `<option value="${v}" ${l.roomTypeKey === v ? 'selected' : ''}>${esc(t)}</option>`).join('')}
              </select>
            </label>
            <label class="form-field">
              <span class="cap">Số khách tối đa</span>
              <input type="number" name="maxGuests" min="1" max="30" value="${esc(l.maxGuests)}" required>
            </label>
          </div>
        </section>

        <section class="modal-section">
          <h3>Bố trí</h3>
          <div class="field-grid">
            <label class="form-field"><span class="cap">Phòng ngủ</span>
              <input type="number" name="bedrooms" min="0" max="20" value="${esc(l.bedrooms)}"></label>
            <label class="form-field"><span class="cap">Giường</span>
              <input type="number" name="beds" min="1" max="40" value="${esc(l.beds)}"></label>
            <label class="form-field"><span class="cap">Phòng tắm</span>
              <input type="number" name="bathrooms" min="0" max="20" value="${esc(l.bathrooms)}"></label>
          </div>
        </section>

        <section class="modal-section">
          <h3>Giá &amp; quy tắc</h3>
          <div class="field-grid">
            <label class="form-field"><span class="cap">Giá mỗi đêm (₫) *</span>
              <input type="number" name="pricePerNight" min="50000" step="10000" value="${esc(l.pricePerNight)}" required></label>
            <label class="form-field"><span class="cap">Phí dọn dẹp (₫)</span>
              <input type="number" name="cleaningFee" min="0" step="10000" value="${esc(l.cleaningFee)}"></label>
            <label class="form-field"><span class="cap">Số đêm tối thiểu</span>
              <input type="number" name="minNights" min="1" max="90" value="${esc(l.minNights)}"></label>
          </div>
          <div class="pill-row" style="margin-top:14px">
            <button type="button" class="pill ${l.instantBook ? 'is-on' : ''}" data-act="toggle-listing-flag" data-key="instantBook">
              Đặt ngay không cần duyệt
            </button>
            <button type="button" class="pill ${l.isPublished ? 'is-on' : ''}" data-act="toggle-listing-flag" data-key="isPublished">
              ${l.isPublished ? 'Đang hiển thị công khai' : 'Đang là bản nháp'}
            </button>
          </div>
        </section>

        <section class="modal-section">
          <h3>Chính sách huỷ</h3>
          <span class="hint">Chính sách càng linh hoạt thì càng nhiều khách đặt.</span>
          <div class="opt-grid">
            ${[['Flexible', 'Linh hoạt', 'Hoàn 100% tiền phòng nếu huỷ trước 24 giờ.'],
               ['Moderate', 'Trung bình', 'Hoàn 100% nếu huỷ trước 5 ngày, sau đó 50%.'],
               ['Strict', 'Nghiêm ngặt', 'Hoàn 50% nếu huỷ trước 7 ngày, sau đó không hoàn.']]
              .map(([key, label, hint]) => `
                <button type="button" class="opt ${(l.cancellationTier ?? 'Moderate') === key ? 'is-on' : ''}"
                        data-act="set-listing-tier" data-key="${key}">
                  <b>${esc(label)}</b><span>${esc(hint)}</span>
                </button>`).join('')}
          </div>
        </section>

        <section class="modal-section">
          <h3>Mô tả</h3>
          <label class="form-field">
            <span class="cap">Giới thiệu chỗ nghỉ * <span style="font-weight:400">(tối thiểu 40 ký tự)</span></span>
            <textarea name="description" rows="5" required
              style="width:100%;padding:12px 14px;border:1px solid var(--line);border-radius:12px;font-size:14px"
              placeholder="Kể về không gian, vị trí và điều khiến chỗ nghỉ của bạn đặc biệt.">${esc(l.description)}</textarea>
          </label>
          <label class="form-field">
            <span class="cap">Điểm nổi bật</span>
            <input name="highlight" value="${esc(l.highlight ?? '')}" placeholder="Hồ bơi riêng nhìn ra vườn dừa">
          </label>
        </section>

        <section class="modal-section">
          <h3>Ảnh</h3>
          <span class="hint">Tải ảnh từ máy hoặc dán liên kết. Ảnh đầu tiên là ảnh bìa.</span>

          <div class="dropzone" data-act="pick-images">
            <input type="file" id="image-input" accept="image/jpeg,image/png,image/webp,image/avif" multiple hidden>
            <b>${state.uploading ? 'Đang tải ảnh lên…' : 'Chọn ảnh từ máy'}</b>
            <span>JPG, PNG, WebP hoặc AVIF · tối đa 8MB mỗi ảnh</span>
          </div>

          ${(l.images ?? []).length ? `
            <div class="thumb-grid">
              ${l.images.map((url, i) => `
                <figure class="thumb">
                  <img src="${esc(url)}" alt="Ảnh ${i + 1}" loading="lazy">
                  ${i === 0 ? '<figcaption>Ảnh bìa</figcaption>' : ''}
                  <button type="button" class="thumb-remove" data-act="remove-listing-image" data-index="${i}"
                          aria-label="Xoá ảnh ${i + 1}">✕</button>
                </figure>`).join('')}
            </div>` : ''}

          <details style="margin-top:14px">
            <summary style="font-size:13px;font-weight:600;cursor:pointer">Hoặc dán liên kết ảnh</summary>
            <textarea name="images" rows="4" style="width:100%;margin-top:10px;padding:12px 14px;border:1px solid var(--line);border-radius:12px;font-size:13px;font-family:ui-monospace,monospace"
              placeholder="https://…">${esc((l.images ?? []).join('\n'))}</textarea>
          </details>
        </section>

        <section class="modal-section">
          <h3>Tiện nghi</h3>
          <div class="pill-row" style="margin-top:14px">
            ${(meta?.amenities ?? []).map(a => `
              <button type="button" class="pill ${(l.amenityKeys ?? []).includes(a.key) ? 'is-on' : ''}"
                      data-act="toggle-listing-amenity" data-key="${esc(a.key)}">
                ${amenityIcon(a.key, 16)} ${esc(a.label)}
              </button>`).join('')}
          </div>
        </section>

        ${state.authError ? `<div class="form-error">${esc(state.authError)}</div>` : ''}
      </form>
    `,
    foot: `
      ${l.id ? `<button class="text-btn" data-act="delete-listing" data-id="${esc(l.id)}">Xoá chỗ nghỉ</button>` : '<span></span>'}
      <button class="btn btn-primary btn-sm" data-act="save-listing">${isNew ? 'Đăng chỗ nghỉ' : 'Lưu thay đổi'}</button>
    `
  });
}

/* ------------------------------------------------------------- host block */

function hostBlockModal() {
  const cal = state.hostCalendar;
  if (!cal) return '';

  return shell({
    title: 'Khoá lịch',
    size: 'narrow',
    body: `
      <p style="margin:0 0 16px;font-size:13.5px;color:var(--ink-muted)">
        Chặn khoảng ngày bạn không muốn nhận khách (bảo trì, gia đình dùng…).
      </p>
      <form data-act="submit-block" data-listing="${esc(cal.listingId)}">
        <div class="field-grid">
          <label class="form-field"><span class="cap">Từ ngày</span>
            <input type="date" name="from" required></label>
          <label class="form-field"><span class="cap">Đến ngày</span>
            <input type="date" name="to" required></label>
        </div>
        <label class="form-field"><span class="cap">Ghi chú</span>
          <input name="note" placeholder="Bảo trì hồ bơi"></label>
        <button type="submit" class="btn btn-dark btn-block">Khoá lịch</button>
      </form>

      ${cal.bookings?.length ? `
        <div style="margin-top:24px">
          <b style="font-size:13px">Lịch khách đã đặt</b>
          <div style="display:grid;gap:8px;margin-top:10px">
            ${cal.bookings.map(b => `
              <div class="cal-row">
                <span class="badge confirmed">Đã đặt</span>
                <div style="flex:1;min-width:0;font-size:13.5px">
                  ${esc(b.checkIn)} → ${esc(b.checkOut)}
                  <span style="color:var(--ink-muted)"> · ${esc(b.guests)} khách · ${esc(b.reference)}</span>
                </div>
              </div>`).join('')}
          </div>
        </div>` : `
        <p style="margin-top:24px;font-size:13px;color:var(--ink-muted)">Chưa có lượt đặt nào sắp tới.</p>`}

      ${cal.blocks?.length ? `
        <div style="margin-top:22px">
          <b style="font-size:13px">Bạn đang khoá</b>
          <div style="display:grid;gap:8px;margin-top:10px">
            ${cal.blocks.map(b => `
              <div class="cal-row">
                <span class="badge cancelled">Khoá</span>
                <div style="flex:1;min-width:0;font-size:13.5px">
                  ${esc(b.from)} → ${esc(b.to)}
                  ${b.note ? `<span style="color:var(--ink-muted)"> · ${esc(b.note)}</span>` : ''}
                </div>
                <button class="text-btn" data-act="remove-block" data-id="${esc(b.id)}">Bỏ</button>
              </div>`).join('')}
          </div>
        </div>` : ''}
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

import { esc } from '../util.js';
import { state } from '../store.js';

const COLUMNS = [
  {
    title: 'Hỗ trợ',
    links: ['Trung tâm trợ giúp', 'AirCover cho khách', 'Chống phân biệt đối xử',
            'Hỗ trợ người khuyết tật', 'Tuỳ chọn huỷ', 'Báo cáo lo ngại khu dân cư']
  },
  {
    title: 'Đón tiếp khách',
    links: ['Cho thuê nhà trên StayHost', 'AirCover cho Chủ nhà', 'Tài nguyên cho Chủ nhà',
            'Diễn đàn cộng đồng', 'Đón tiếp khách có trách nhiệm', 'Tham gia khoá học miễn phí']
  },
  {
    title: 'StayHost',
    links: ['Trang tin tức', 'Tính năng mới', 'Cơ hội nghề nghiệp',
            'Nhà đầu tư', 'Chỗ ở khẩn cấp StayHost.org', 'Thẻ quà tặng']
  },
  {
    title: 'Khám phá',
    links: ['Chỗ nghỉ ven biển', 'Villa có hồ bơi', 'Homestay vùng cao',
            'Cabin gỗ Đà Lạt', 'Căn hộ dài hạn', 'Chỗ nghỉ cho thú cưng']
  }
];

const LEGAL = ['© 2026 StayHost OS, Inc.', 'Quyền riêng tư', 'Điều khoản', 'Sơ đồ trang web', 'Thông tin công ty'];

export function renderFooter() {
  return `
    <div class="footer-cols">
      ${COLUMNS.map(col => `
        <div class="footer-col">
          <h4>${esc(col.title)}</h4>
          <ul>
            ${col.links.map(l => `<li><a href="#" data-act="noop">${esc(l)}</a></li>`).join('')}
          </ul>
        </div>
      `).join('')}
    </div>
    <div class="footer-bottom">
      <div class="footer-bottom-inner">
        <div class="footer-legal">
          ${LEGAL.map(l => `<span>${esc(l)}</span>`).join('')}
        </div>
        <div class="footer-prefs">
          <button data-act="open" data-overlay="language">⊕ ${esc(state.language.label)}</button>
          <button data-act="open" data-overlay="language">${esc(state.currency.symbol)} ${esc(state.currency.code)}</button>
          <button data-act="noop" aria-label="Facebook">ⓕ</button>
          <button data-act="noop" aria-label="X">✕</button>
          <button data-act="noop" aria-label="Instagram">◉</button>
        </div>
      </div>
    </div>
  `;
}

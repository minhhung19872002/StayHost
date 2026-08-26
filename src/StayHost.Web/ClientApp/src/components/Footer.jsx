import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { openOverlay, toast } from '../lib/store.js';
import { t } from '../lib/i18n.js';

const COLUMNS = [
  {
    title: 'Hỗ trợ',
    links: ['Trung tâm trợ giúp', 'StayShield cho khách', 'Chống phân biệt đối xử',
            'Hỗ trợ người khuyết tật', 'Tuỳ chọn huỷ', 'Báo cáo lo ngại khu dân cư']
  },
  {
    title: 'Đón tiếp khách',
    links: ['Cho thuê nhà trên StayHost', 'StayShield cho Chủ nhà', 'Tài nguyên cho Chủ nhà',
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

// docs/01 TM-26 — the city landing pages, from the server rather than from a
// list typed here. The hard-coded six covered a third of the cities that existed,
// so twelve landing pages had no link pointing at them from anywhere on the site
// and a crawler could not reach them at all. The server also decides the slug, so
// it cannot drift from the address CitiesController actually routes on.
//
// Capped, because this is a footer on every page and the catalogue keeps growing.
// The ones left out are still in sitemap.xml — the cap costs discovery speed, not
// discovery. Ordered by how much each city has to show.
const FOOTER_CITY_LIMIT = 12;

// Used only until meta arrives on first paint, so the column is never empty.
const FALLBACK_CITIES = [
  { name: 'Đà Lạt', slug: 'da-lat' }, { name: 'Đà Nẵng', slug: 'da-nang' },
  { name: 'Hội An', slug: 'hoi-an' }, { name: 'Hà Nội', slug: 'ha-noi' },
  { name: 'TP. Hồ Chí Minh', slug: 'ho-chi-minh' }, { name: 'Nha Trang', slug: 'nha-trang' }
];

const demo = e => { e.preventDefault(); toast('Bản demo — chức năng này chưa kết nối dịch vụ thật.'); };

/** The few footer links that lead somewhere real (docs/01 AT-07). */
const ROUTES = {
  'Trung tâm trợ giúp': '/help',
  'Cho thuê nhà trên StayHost': '/host',
  'Trải nghiệm': '/experiences',
  'Dịch vụ': '/services',
  'Thẻ quà tặng': '/wallet',
  'StayShield cho khách': '/shield/terms',
  'StayShield cho Chủ nhà': '/shield/terms',
  // docs/01 AT-03 — the neighbour channel.
  'Báo cáo lo ngại khu dân cư': '/neighbors'
};

export function Footer() {
  const state = useStore();
  const navigate = useNavigate();

  const cities = (state.meta?.cityLinks?.length ? state.meta.cityLinks : FALLBACK_CITIES)
    .slice(0, FOOTER_CITY_LIMIT);

  return <>
    <div className="footer-cols">
      {COLUMNS.map(col => (
        <div className="footer-col" key={col.title}>
          <h4>{t(col.title)}</h4>
          <ul>
            {col.links.map(l => (
              <li key={l}>
                <a href={ROUTES[l] ?? '#'}
                   onClick={e => { e.preventDefault(); ROUTES[l] ? navigate(ROUTES[l]) : demo(e); }}>{t(l)}</a>
              </li>
            ))}
          </ul>
        </div>
      ))}
      {/* docs/01 TM-26 — real, crawlable links into the per-city landing pages. */}
      <div className="footer-col">
        <h4>{t('Điểm đến')}</h4>
        <ul>
          {cities.map(c => (
            <li key={c.slug}>
              <a href={`/thanh-pho/${c.slug}`}
                 onClick={e => {
                   if (e.metaKey || e.ctrlKey || e.shiftKey) return;
                   e.preventDefault();
                   navigate(`/thanh-pho/${c.slug}`);
                 }}>{c.name}</a>
            </li>
          ))}
        </ul>
      </div>
    </div>
    <div className="footer-bottom">
      <div className="footer-bottom-inner">
        <div className="footer-legal">
          {LEGAL.map(l => <span key={l}>{t(l)}</span>)}
        </div>
        <div className="footer-prefs">
          <button onClick={() => openOverlay('language')}>⊕ {state.language.label}</button>
          <button onClick={() => openOverlay('language')}>{state.currency.symbol} {state.currency.code}</button>
          <button onClick={demo} aria-label="Facebook">ⓕ</button>
          <button onClick={demo} aria-label="X">✕</button>
          <button onClick={demo} aria-label="Instagram">◉</button>
        </div>
      </div>
    </div>
  </>;
}

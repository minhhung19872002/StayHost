import { useEffect, useState } from 'react';
import { useParams, useNavigate, useSearchParams } from 'react-router-dom';
import { api } from '../lib/api.js';
import { Card } from '../components/Card.jsx';
import { t } from '../lib/i18n.js';
import { setPageMeta, setStructuredData, cityJsonLd, breadcrumbJsonLd } from '../lib/seo.js';

/**
 * docs/01 TM-26 — a landing page for one city, so a visitor arriving from a
 * search engine sees real content and a way into the catalogue rather than an
 * empty search box.
 */
export function City() {
  const { city } = useParams();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const pageNo = Math.max(1, Number(params.get('trang')) || 1);
  const [page, setPage] = useState(null);
  const [missing, setMissing] = useState(false);

  useEffect(() => {
    setPage(null);
    setMissing(false);
    api.city(city, pageNo).then(setPage).catch(() => setMissing(true));
    // The whole page reloads on a page change, so the effect watches the number
    // as well — otherwise following a paging link would leave page 1 on screen.
  }, [city, pageNo]);

  // The whole point of a city landing page is the query "khách sạn Đà Lạt", and
  // a page whose title never says "Đà Lạt" cannot answer it. Until this ran, all
  // eighteen city pages carried the home page's title and competed with it and
  // with each other for the same words.
  useEffect(() => {
    if (!page) return;

    // Page 2 onwards says so in the title. Two pages of a series sharing one
    // title is a duplicate to a search engine, and the one it keeps is arbitrary.
    const suffix = page.totalPages > 1 && page.page > 1
      ? ` — trang ${page.page}/${page.totalPages}` : '';

    setPageMeta({
      title: `Khách sạn, nhà & homestay cho thuê tại ${page.city}${suffix} | StayHost OS`,
      // The count is the honest hook and it is the real number from the same
      // query the page renders, so it cannot promise more than the page shows.
      description: `${page.count} chỗ nghỉ tại ${page.city}: khách sạn, nhà nguyên căn, `
        + `căn hộ, villa và homestay. Giá trọn gói, chính sách huỷ ghi rõ trên từng tin.`,
    });

    setStructuredData({
      '@context': 'https://schema.org',
      '@graph': [
        cityJsonLd(page.city, page.listings || []),
        breadcrumbJsonLd([
          { name: 'Trang chủ', path: '/' },
          { name: page.city, path: `/thanh-pho/${city}` },
        ]),
      ],
    });
  }, [page, city]);

  if (missing) {
    return (
      <div className="shell" style={{ paddingBlock: '40px 90px' }}>
        <div className="empty-state">
          <h3>{t('Chưa có chỗ nghỉ ở nơi này')}</h3>
          <p>{t('StayHost chưa có tin đăng nào cho thành phố bạn tìm.')}</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }} onClick={() => navigate('/')}>{t('Về trang chủ')}</button>
        </div>
      </div>
    );
  }

  if (!page) {
    return <div className="shell" style={{ paddingBlock: '40px 90px' }}>
      <div className="sk-line skeleton" style={{ width: 280, height: 30 }} />
    </div>;
  }

  return (
    <div className="shell" style={{ paddingBlock: '28px 80px' }}>
      <h1 className="section-title" style={{ fontSize: 28 }}>{t('Chỗ nghỉ tại')} {page.city}</h1>
      <p className="section-sub" style={{ maxWidth: 720, fontSize: 15, lineHeight: 1.6 }}>{page.blurb}</p>
      <p className="section-sub">{page.count} {t('chỗ nghỉ đang mở đặt')}</p>

      <div className="card-grid" style={{ marginTop: 20 }}>
        {page.listings.map(c => <Card key={c.id} card={c} lazy />)}
      </div>

      {page.totalPages > 1 && (
        /* Real <a href> links, not buttons. This strip is the only path a crawler
           has to the places past the first page — a button that calls navigate()
           is invisible to it, which is exactly how the listings came to have no
           inbound links at all. preventDefault keeps the in-app navigation. */
        <nav className="city-pages" aria-label={t('Phân trang')} style={{ marginTop: 26 }}>
          {Array.from({ length: page.totalPages }, (_, i) => i + 1).map(n => {
            const href = n === 1 ? `/thanh-pho/${city}` : `/thanh-pho/${city}?trang=${n}`;
            const here = n === page.page;
            return (
              <a key={n} href={href}
                 aria-current={here ? 'page' : undefined}
                 className={`city-page-link ${here ? 'is-on' : ''}`}
                 onClick={e => {
                   if (e.metaKey || e.ctrlKey || e.shiftKey) return;
                   e.preventDefault();
                   navigate(href);
                 }}>{n}</a>
            );
          })}
        </nav>
      )}

      <div style={{ marginTop: 28 }}>
        <button className="btn btn-dark" onClick={() => navigate(`/?q=${encodeURIComponent(page.city)}`)}>
          {t('Xem tất cả chỗ nghỉ ở')} {page.city}
        </button>
      </div>
    </div>
  );
}

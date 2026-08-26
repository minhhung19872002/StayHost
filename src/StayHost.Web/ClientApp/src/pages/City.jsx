import { useEffect, useState } from 'react';
import { useParams, useNavigate, useSearchParams } from 'react-router-dom';
import { api } from '../lib/api.js';
import { Card } from '../components/Card.jsx';
import { t } from '../lib/i18n.js';
import { setPageMeta, setStructuredData, cityJsonLd, breadcrumbJsonLd } from '../lib/seo.js';
import { NotFound } from './NotFound.jsx';

/**
 * docs/01 TM-26 — a landing page for one city, so a visitor arriving from a
 * search engine sees real content and a way into the catalogue rather than an
 * empty search box.
 */
export function City() {
  const { city } = useParams();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const rawPage = params.get('trang');
  const pageNo = Math.max(1, Number(rawPage) || 1);
  const [page, setPage] = useState(null);
  const [missing, setMissing] = useState(false);

  useEffect(() => {
    setPage(null);
    setMissing(false);
    api.city(city, pageNo).then(setPage).catch(() => setMissing(true));
    // The whole page reloads on a page change, so the effect watches the number
    // as well — otherwise following a paging link would leave page 1 on screen.
  }, [city, pageNo]);

  /*
   * A page number outside the series is not a page.
   *
   * The server clamps ?trang=99 to the last real page and returns its contents,
   * which is the right answer to give a person. Left there, though, the address
   * bar still said 99 and canonical — which deliberately keeps `trang`, because
   * page 2 holds different places from page 1 — pointed at ?trang=99 as if it
   * were a page of its own. Every number a crawler tried became another address
   * claiming to be canonical while showing page 1's rooms. Today no city holds
   * more than twelve places, so *every* ?trang=N was such a duplicate.
   *
   * The redirect is what closes it: one address per page, and the number in it
   * is one the series really has.
   */
  useEffect(() => {
    if (!page) return;
    const want = page.page <= 1 ? null : String(page.page);
    if (rawPage === want) return;
    navigate(page.page <= 1 ? `/thanh-pho/${city}` : `/thanh-pho/${city}?trang=${page.page}`,
             { replace: true });
  }, [page, rawPage, city, navigate]);

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
      title: `Khách sạn, nhà & homestay cho thuê tại ${page.city}${suffix} | Staylio`,
      // The count is the honest hook and it is the real number from the same
      // query the page renders, so it cannot promise more than the page shows.
      description: `${page.count} chỗ nghỉ tại ${page.city}: khách sạn, nhà nguyên căn, `
        + `căn hộ, villa và homestay. Giá trọn gói, chính sách huỷ ghi rõ trên từng tin.`,
      // A real place in this city on the share card, rather than the site's
      // default one — the first listing shown is the one the page leads with.
      image: (page.listings || [])[0]?.images?.[0],
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

  // A city with nothing in it is an address with no page behind it. It used to
  // render an empty state under a 200, which reads to a crawler as a real but
  // thin page — and eighteen of those is how a site starts being distrusted.
  if (missing) {
    return (
      <NotFound
        title={t('Chưa có chỗ nghỉ ở nơi này')}
        body={t('Staylio chưa có tin đăng nào cho thành phố bạn tìm.')} />
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

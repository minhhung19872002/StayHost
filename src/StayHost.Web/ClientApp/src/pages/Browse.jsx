import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import {
  set, state as store, isDiscovery, activeFilterCount, resetFilters,
  loadHome, runSearch
} from '../lib/store.js';
import { queryToSearch } from '../lib/urlState.js';
import { applySearch } from '../lib/nav.js';
import { rememberSearch } from '../lib/history.js';
import { dateRangeLabel, money } from '../lib/format.js';
import { t } from '../lib/i18n.js';
import { Card, CardSkeleton } from '../components/Card.jsx';
import { ResultsMap } from '../components/Maps.jsx';
import { Icon } from '../components/Icon.jsx';
import { api } from '../lib/api.js';

export function Browse() {
  const state = useStore();
  const location = useLocation();

  // The query string is the source of truth: read it back into the store on
  // every change, then fetch whichever view that lands us in.
  useEffect(() => {
    queryToSearch(location.search);
    // A new destination means the map rectangle from the last search no longer applies.
    set({ searchArea: null, searchPolygon: null });

    if (isDiscovery()) { loadHome(); return; }
    runSearch();
    rememberSearch({
      q: store.q, checkIn: store.checkIn, checkOut: store.checkOut, guests: store.guests
    });
  }, [location.search, state.meta]);

  return isDiscovery() ? <Discovery /> : <Results />;
}

/* --------------------------------------------------- landing (carousel rows) */

function Discovery() {
  const state = useStore();
  const home = state.home;

  if (state.homeLoading || !home) {
    return (
      <div className="shell" style={{ paddingBlock: '32px 90px' }}>
        {[0, 1].map(i => (
          <div key={i} style={{ marginBottom: 40 }}>
            <div className="sk-line skeleton" style={{ width: 280, height: 22, marginBottom: 18 }} />
            <div className="card-grid">{Array.from({ length: 4 }, (_, n) => <CardSkeleton key={n} />)}</div>
          </div>
        ))}
      </div>
    );
  }

  return <>
    <div className="shell" style={{ paddingBlock: '26px 60px' }}>
      <h1 className="sr-only">StayHost OS — thuê nhà ngắn hạn khắp Việt Nam</h1>
      {home.sections.map(s => <Rail key={s.key} section={s} />)}
      {/* docs/01 TM-02 — the "Tất cả" row is the one that has to show more than
          places to stay, or it is the same page as "Chỗ ở" under another name. */}
      {store.tab === 'all' && <OtherLines />}
    </div>
    <Inspiration groups={home.inspiration} />
  </>;
}

/**
 * docs/01 TM-02, MR-01, MR-05 — experiences and services alongside homes. Each
 * strip loads on its own and a failure hides only itself: discovery is the first
 * page anybody sees, and it must not go blank because one extra line is down.
 */
function OtherLines() {
  const [experiences, setExperiences] = useState([]);
  const [services, setServices] = useState([]);
  const navigate = useNavigate();

  useEffect(() => {
    // Both endpoints answer with a plain array, not a paged envelope.
    api.experiences().then(d => setExperiences((d ?? []).slice(0, 4))).catch(() => {});
    api.services().then(d => setServices((d ?? []).slice(0, 4))).catch(() => {});
  }, []);

  const strip = (title, subtitle, items, to, render) => items.length ? (
    <section style={{ marginTop: 40 }}>
      <div className="page-head" style={{ marginBottom: 0 }}>
        <div>
          <h2 className="section-title" style={{ fontSize: 22 }}>{title}</h2>
          <p className="section-sub">{subtitle}</p>
        </div>
        <button className="btn btn-outline btn-sm" onClick={() => navigate(to)}>Xem tất cả</button>
      </div>
      <div className="card-grid" style={{ marginTop: 16 }}>{items.map(render)}</div>
    </section>
  ) : null;

  return <>
    {strip('Trải nghiệm', 'Hoạt động do người địa phương dẫn', experiences, '/experiences',
      x => (
        <button className="opt" key={x.id} style={{ textAlign: 'left' }}
                onClick={() => navigate(`/experiences/${x.slug}`)}>
          <b>{x.title}</b>
          <span className="meta">{x.city} · {money(x.pricePerPerson)}/khách</span>
        </button>
      ))}

    {strip('Dịch vụ', 'Đầu bếp, dọn dẹp, hướng dẫn viên tới tận nơi', services, '/services',
      x => (
        <button className="opt" key={x.id} style={{ textAlign: 'left' }}
                onClick={() => navigate(`/services/${x.slug}`)}>
          <b>{x.title}</b>
          <span className="meta">{x.city} · {money(x.basePrice)} {x.pricingLabel ?? ''}</span>
        </button>
      ))}
  </>;
}

function Rail({ section }) {
  const navigate = useNavigate();

  const scroll = dir => {
    const track = document.querySelector(`[data-rail-track="${section.key}"]`);
    track?.scrollBy({ left: dir * track.clientWidth * 0.8, behavior: 'smooth' });
  };

  return (
    <section className="rail" data-rail={section.key}>
      <div className="rail-head">
        <button className="rail-title" onClick={() => navigate(section.href)}>
          <span>{section.title}</span><Icon name="arrowRight" size={18} />
        </button>
        {section.subtitle && <p className="rail-sub">{section.subtitle}</p>}
        <div className="rail-nav">
          <button className="round-btn" onClick={() => scroll(-1)} aria-label="Cuộn trái"><Icon name="chevronLeft" size={14} /></button>
          <button className="round-btn" onClick={() => scroll(1)} aria-label="Cuộn phải"><Icon name="chevronRight" size={14} /></button>
        </div>
      </div>
      <div className="rail-track" data-rail-track={section.key}>
        {section.items.map(c => (
          <div className="rail-item" key={c.id}><Card card={c} variant="rail" lazy /></div>
        ))}
      </div>
    </section>
  );
}

function Inspiration({ groups }) {
  const state = useStore();
  const navigate = useNavigate();
  if (!groups?.length) return null;

  const current = groups.find(g => g.tab === state.inspirationTab) ?? groups[0];

  return (
    <section className="inspiration">
      <div className="shell">
        <h2 className="section-title">{t('Gợi ý cho chuyến đi sắp tới')}</h2>
        <div className="insp-tabs" role="tablist">
          {groups.map(g => (
            <button role="tab" key={g.tab} aria-selected={g.tab === current.tab}
                    className={`insp-tab ${g.tab === current.tab ? 'is-active' : ''}`}
                    onClick={() => set({ inspirationTab: g.tab })}>{g.tab}</button>
          ))}
        </div>
        <div className="insp-grid">
          {current.links.map(l => (
            <button className="insp-link" key={l.href} onClick={() => navigate(l.href)}>
              <b>{l.title}</b><span>{l.subtitle}</span>
            </button>
          ))}
        </div>
      </div>
    </section>
  );
}

/* ------------------------------------------------------- search result grid */

function Results() {
  const state = useStore();
  const { results, loading } = state;

  const title = state.q.trim()
    ? `${t('Chỗ ở')} · ${state.q.trim()}`
    : state.category !== 'all'
      ? `${state.meta?.categories.find(c => c.key === state.category)?.label ?? ''} ${t('được yêu thích')}`
      : t('Chỗ nghỉ được yêu thích ở Việt Nam');

  const body = <>
    <div className="results-head">
      <h1 className="results-title">
        {loading ? title : `${results.total > results.pageSize ? t('Hơn ') : ''}${results.total} ${t('chỗ nghỉ')}`}
      </h1>
      <div className="fee-note"><Icon name="star" size={15} /> {t('Giá đã gồm mọi khoản phí')}</div>
    </div>
    <p className="results-context">
      {title} · {dateRangeLabel(state.checkIn, state.checkOut)}
      {results.dates && (
        <span className="flex-note">
          {' '}· {results.dates.label}, {results.dates.nights} đêm — đã xét {results.dates.options} khoảng ngày
        </span>
      )}
    </p>

    {loading
      ? <div className="card-grid">{Array.from({ length: 8 }, (_, i) => <CardSkeleton key={i} />)}</div>
      : results.items.length
        ? <div className="card-grid">{results.items.map(c => <Card key={c.id} card={c} variant="search" />)}</div>
        : <Empty noResults={results.noResults} />}

    <Pagination results={results} />
  </>;

  return <>
    {state.hideMap
      ? <div className="shell" style={{ paddingBlock: '22px 90px' }}>{body}</div>
      : (
        <div className="split">
          <div className="split-list" style={{ padding: '22px var(--gutter) 90px' }}>{body}</div>
          <div className="split-map">
            <ResultsMap
              onSearchArea={area => { set({ searchArea: area, searchPolygon: null }); runSearch(); }}
              onDrawArea={pts => { set({ searchPolygon: pts, searchArea: null }); runSearch(); }} />
          </div>
        </div>
      )}

    <button className="map-toggle" onClick={() => set({ hideMap: !state.hideMap })}>
      {state.hideMap ? t('Hiện bản đồ') : t('Hiện danh sách')} <Icon name={state.hideMap ? 'map' : 'filter'} size={16} />
    </button>
  </>;
}

/** Numbered pagination, the way airbnb.com paginates /s/ results. */
function Pagination({ results }) {
  const pages = Math.ceil(results.total / results.pageSize);
  if (pages <= 1) return null;

  const current = results.page;
  const numbers = [...new Set([1, pages, current, current - 1, current + 1])]
    .filter(n => n >= 1 && n <= pages)
    .sort((a, b) => a - b);

  const go = page => {
    runSearch({ page });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const items = [];
  let previous = 0;
  for (const n of numbers) {
    if (n - previous > 1) items.push(<span className="page-gap" key={`gap${n}`}>…</span>);
    items.push(
      <button key={n} className={`page-btn ${n === current ? 'is-on' : ''}`}
              aria-current={n === current} onClick={() => go(n)}>{n}</button>
    );
    previous = n;
  }

  return (
    <nav className="pagination" aria-label="Phân trang">
      <button className="page-btn nav" disabled={current === 1} aria-label="Trang trước"
              onClick={() => go(current - 1)}><Icon name="chevronLeft" size={14} /></button>
      {items}
      <button className="page-btn nav" disabled={current === pages} aria-label="Trang sau"
              onClick={() => go(current + 1)}><Icon name="chevronRight" size={14} /></button>
      <span className="page-info">Trang {current} / {pages} · {results.total} chỗ nghỉ</span>
    </nav>
  );
}

/**
 * docs/01 TM-22 — an empty result set has to say which filter is doing the
 * blocking and where there is something nearby, not just "no results".
 */
function Empty({ noResults }) {
  const navigate = useNavigate();
  const filters = activeFilterCount();
  const blocking = noResults?.blockingFilters ?? [];
  const nearby = noResults?.nearbyAreas ?? [];

  const drop = key => {
    const clear = {
      price: () => set({ minPrice: store.meta?.minPrice ?? 0, maxPrice: store.meta?.maxPrice ?? 0 }),
      amenities: () => set({ amenities: [] }),
      roomType: () => set({ roomType: 'any' }),
      rooms: () => set({ bedrooms: 0, beds: 0, bathrooms: 0 }),
      guests: () => set({ guests: { ...store.guests, adults: 1, children: 0 } }),
      superhost: () => set({ superhostOnly: false }),
      guestFavorite: () => set({ guestFavoriteOnly: false }),
      instantBook: () => set({ instantBookOnly: false }),
      freeCancellation: () => set({ freeCancellationOnly: false }),
      category: () => set({ category: 'all' })
    }[key];

    clear?.();
    applySearch();
  };

  return (
    <div className="empty-state">
      <h3>Không tìm thấy chỗ nghỉ phù hợp</h3>

      {blocking.length ? <>
        <p>Bỏ một trong các bộ lọc sau là có kết quả ngay:</p>
        <div className="pill-row" style={{ justifyContent: 'center', marginTop: 14 }}>
          {blocking.map(f => (
            <button className="pill" key={f.key} onClick={() => drop(f.key)}>
              Bỏ “{f.label}” · {f.count} chỗ nghỉ
            </button>
          ))}
        </div>
      </> : (
        <p>{filters
          ? 'Thử nới giá hoặc bỏ bớt tiện nghi trong bộ lọc.'
          : 'Thử một điểm đến khác hoặc đổi ngày.'}</p>
      )}

      {!!nearby.length && <>
        <p style={{ marginTop: 22 }}>Hoặc xem khu vực lân cận:</p>
        <div className="pill-row" style={{ justifyContent: 'center', marginTop: 12 }}>
          {nearby.map(a => (
            <button className="pill" key={a.city}
                    onClick={() => { set({ q: a.city, searchArea: null, searchPolygon: null }); applySearch({ replace: false }); }}>
              {a.city} · {a.count} chỗ · từ {money(a.fromPrice)}
            </button>
          ))}
        </div>
      </>}

      <button className="btn btn-primary" style={{ marginTop: 22 }}
              onClick={() => { resetFilters(); set({ q: '', searchArea: null, searchPolygon: null }); applySearch(); navigate('/'); }}>
        Xoá tất cả bộ lọc
      </button>
    </div>
  );
}

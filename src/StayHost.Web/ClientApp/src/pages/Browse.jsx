import { useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import {
  set, state as store, isDiscovery, activeFilterCount, resetFilters,
  loadHome, runSearch
} from '../lib/store.js';
import { queryToSearch } from '../lib/urlState.js';
import { applySearch } from '../lib/nav.js';
import { dateRangeLabel } from '../lib/format.js';
import { Card, CardSkeleton } from '../components/Card.jsx';
import { ResultsMap } from '../components/Maps.jsx';
import { Icon } from '../components/Icon.jsx';

export function Browse() {
  const state = useStore();
  const location = useLocation();

  // The query string is the source of truth: read it back into the store on
  // every change, then fetch whichever view that lands us in.
  useEffect(() => {
    queryToSearch(location.search);
    if (isDiscovery()) loadHome();
    else runSearch();
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
    </div>
    <Inspiration groups={home.inspiration} />
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
        <h2 className="section-title">Gợi ý cho chuyến đi sắp tới</h2>
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
    ? `Chỗ nghỉ tại "${state.q.trim()}"`
    : state.category !== 'all'
      ? `${state.meta?.categories.find(c => c.key === state.category)?.label ?? ''} được yêu thích`
      : 'Chỗ nghỉ được yêu thích ở Việt Nam';

  const body = <>
    <div className="results-head">
      <h1 className="results-title">
        {loading ? title : `${results.total > results.pageSize ? 'Hơn ' : ''}${results.total} chỗ nghỉ`}
      </h1>
      <div className="fee-note"><Icon name="star" size={15} /> Giá đã gồm mọi khoản phí</div>
    </div>
    <p className="results-context">{title} · {dateRangeLabel(state.checkIn, state.checkOut)}</p>

    {loading
      ? <div className="card-grid">{Array.from({ length: 8 }, (_, i) => <CardSkeleton key={i} />)}</div>
      : results.items.length
        ? <div className="card-grid">{results.items.map(c => <Card key={c.id} card={c} variant="search" />)}</div>
        : <Empty />}

    <Pagination results={results} />
  </>;

  return <>
    {state.hideMap
      ? <div className="shell" style={{ paddingBlock: '22px 90px' }}>{body}</div>
      : (
        <div className="split">
          <div className="split-list" style={{ padding: '22px var(--gutter) 90px' }}>{body}</div>
          <div className="split-map"><ResultsMap /></div>
        </div>
      )}

    <button className="map-toggle" onClick={() => set({ hideMap: !state.hideMap })}>
      {state.hideMap ? 'Hiện bản đồ' : 'Hiện danh sách'} <Icon name={state.hideMap ? 'map' : 'filter'} size={16} />
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

function Empty() {
  const filters = activeFilterCount();

  return (
    <div className="empty-state">
      <h3>Không tìm thấy chỗ nghỉ phù hợp</h3>
      <p>{filters
        ? 'Thử nới giá hoặc bỏ bớt tiện nghi trong bộ lọc.'
        : 'Thử một điểm đến khác hoặc đổi ngày.'}</p>
      <button className="btn btn-primary" style={{ marginTop: 18 }}
              onClick={() => { resetFilters(); store.q = ''; applySearch(); }}>Xoá bộ lọc</button>
    </div>
  );
}

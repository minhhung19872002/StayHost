import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { toggleFavorite } from '../lib/store.js';
import { money } from '../lib/format.js';
import { stayTotal, originalStayTotal } from '../lib/pricing.js';
import { setHoveredListing } from './Maps.jsx';

/**
 * `variant`:
 *   'search' — the denser /s/ layout: "<loại> tại <thành phố>" heading, the
 *              listing name underneath, bed/bath line and the all-in total.
 *   'rail'   — the two-line landing-page carousel card.
 *   default  — the browse grid card.
 */
export function Card({ card, variant, lazy = false }) {
  const state = useStore();
  const navigate = useNavigate();
  const [index, setIndex] = useState(0);

  const images = card.images?.length ? card.images : [''];
  const idx = Math.min(index, images.length - 1);
  const badge = card.isGuestFavorite ? 'KHÁCH YÊU THÍCH' : card.isSuperhost ? 'SIÊU CHỦ NHÀ' : null;

  const open = () => navigate(`/rooms/${card.slug}`);

  const step = (event, dir) => {
    event.preventDefault();
    event.stopPropagation();
    setIndex(i => Math.min(images.length - 1, Math.max(0, i + dir)));
  };

  return (
    <article
      className={`card ${variant === 'search' ? 'card-search' : ''}`}
      data-listing={card.id}
      onMouseEnter={() => setHoveredListing(card.id, true)}
      onMouseLeave={() => setHoveredListing(card.id, false)}
    >
      <div className="card-media" onClick={open} role="link" tabIndex={-1}>
        {images.map((src, i) =>
          // Only the visible frame and its neighbours carry a real src, so a
          // grid of cards costs a handful of images rather than hundreds.
          Math.abs(i - idx) <= 1
            ? <img key={i} src={src} alt={`${card.title} — ảnh ${i + 1}`}
                   className={i === idx ? 'is-current' : ''}
                   loading={i === 0 && !lazy ? 'eager' : 'lazy'} decoding="async" />
            : <img key={i} alt="" aria-hidden="true" className="is-deferred" />
        )}

        {badge && <span className="card-badge">{badge}</span>}

        <button className={`card-fav ${card.isFavorite ? 'is-on' : ''}`}
                onClick={e => { e.preventDefault(); e.stopPropagation(); toggleFavorite(card.id); }}
                aria-label={`${card.isFavorite ? 'Bỏ lưu' : 'Lưu'} ${card.title}`}
                aria-pressed={!!card.isFavorite}>♥</button>

        {images.length > 1 && <>
          <button className="carousel-nav prev" onClick={e => step(e, -1)}
                  aria-label="Ảnh trước" disabled={idx === 0}>‹</button>
          <button className="carousel-nav next" onClick={e => step(e, 1)}
                  aria-label="Ảnh tiếp theo" disabled={idx === images.length - 1}>›</button>
          <div className="carousel-dots" aria-hidden="true">
            {images.map((_, i) => <i key={i} className={i === idx ? 'is-on' : ''} />)}
          </div>
        </>}
      </div>

      <div className="card-body" onClick={open}>
        {variant === 'search' ? <SearchBody card={card} />
          : variant === 'rail' ? <RailBody card={card} />
            : <BrowseBody card={card} showTotal={state.showTotalPrice} />}
      </div>
    </article>
  );
}

function RailBody({ card }) {
  const { nights, total } = stayTotal(card);
  return <>
    <h3 className="card-title">{card.typeLabel} tại {card.city}</h3>
    <div className="card-sub card-inline">
      <b>{money(total)}</b> cho {nights} đêm
      {card.reviewCount ? ` · ★ ${card.rating.toFixed(2)}` : ' · Mới'}
    </div>
  </>;
}

function BrowseBody({ card, showTotal }) {
  const { nights, total } = stayTotal(card);
  return <>
    <div className="card-row">
      <h3 className="card-title">{card.title}</h3>
      <div className="card-rating">{card.reviewCount ? `★ ${card.rating.toFixed(2)}` : 'Mới'}</div>
    </div>
    <div className="card-sub">{card.city} · {card.bedrooms} phòng ngủ</div>
    <div className="card-sub">{card.typeLabel} · {card.roomTypeLabel}</div>
    <div className="card-price">
      {showTotal
        ? <><b>{money(total)}</b> <span>tổng {nights} đêm</span></>
        : <><b>{money(card.pricePerNight)}</b> <span>/ đêm</span></>}
    </div>
  </>;
}

function SearchBody({ card }) {
  const { nights, total } = stayTotal(card);
  const original = originalStayTotal(card);

  return <>
    <div className="card-row">
      <h3 className="card-title">{card.typeLabel} tại {card.city}</h3>
      <div className="card-rating">
        {card.reviewCount ? `★ ${card.rating.toFixed(2)} (${card.reviewCount})` : '★ Mới'}
      </div>
    </div>
    <div className="card-sub card-name">{card.title}</div>
    <div className="card-sub">{card.bedrooms} phòng ngủ · {card.beds} giường · {card.bathrooms} phòng tắm</div>
    <div className="card-price">
      {original && <><s>{money(original)}</s> </>}
      <b>{money(total)}</b> <span>cho {nights} đêm</span>
    </div>
    <div className="card-perks">Đã gồm phí · Huỷ miễn phí</div>
  </>;
}

export function CardSkeleton() {
  return (
    <div className="card" aria-hidden="true">
      <div className="card-media skeleton" />
      <div className="card-body">
        <div className="sk-line skeleton" style={{ width: '80%' }} />
        <div className="sk-line skeleton" style={{ width: '55%' }} />
        <div className="sk-line skeleton" style={{ width: '40%' }} />
      </div>
    </div>
  );
}

import { useEffect, useLayoutEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import {
  loadDetail, toggleFavorite, totalGuests, guestLabel, bumpTotalGuests,
  clearDates, openOverlay, requireAuth, toast, set, setRoom, shareListing
} from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate, nightsBetween } from '../lib/format.js';
import { Avatar } from '../components/Avatar.jsx';
import { Card } from '../components/Card.jsx';
import { Calendar } from '../components/Calendar.jsx';
import { DetailMap } from '../components/Maps.jsx';
import { Icon, AmenityIcon } from '../components/Icon.jsx';
import { PriceLines } from '../components/modals/ListingModals.jsx';

const RATING_LABELS = {
  cleanliness: 'Mức độ sạch sẽ',
  accuracy: 'Độ chính xác',
  checkIn: 'Nhận phòng',
  communication: 'Giao tiếp',
  location: 'Vị trí',
  value: 'Giá trị'
};

/**
 * The photos are the first thing on the page, so going back to them means going
 * back to the top — which is also how the site header, let go on this page,
 * comes back. Anything below scrolls to itself.
 */
const scrollTo = id => {
  if (id === 'section-photos') { window.scrollTo({ top: 0, behavior: 'smooth' }); return; }
  document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
};

export function Detail() {
  const state = useStore();
  const { slug } = useParams();
  const navigate = useNavigate();

  useEffect(() => { loadDetail(slug); }, [slug]);

  // On a listing page the site header scrolls away and the section nav takes the
  // top of the screen instead. You have already chosen the place by this point;
  // what is worth a permanent strip of the window is moving around inside it.
  // The class goes on the body because the header lives outside this page.
  //
  // Before paint, or the first frame of the page is drawn with the header still
  // sticky and everything below it 99px lower, and you watch it jump.
  useLayoutEffect(() => {
    document.body.classList.add('page-detail');
    return () => document.body.classList.remove('page-detail');
  }, []);

  if (state.detailLoading || !state.detail) return <DetailSkeleton />;

  const d = state.detail;
  const c = d.card;
  const nights = nightsBetween(state.checkIn, state.checkOut);

  const share = () => shareListing(c);

  return <>
    <div className="shell" style={{ paddingBlock: '20px 110px' }}>
      <button className="back-link" onClick={() => navigate('/')}>← Tất cả chỗ nghỉ</button>

      <div className="detail-head">
        <div style={{ minWidth: 0 }}>
          <h1>{c.title}</h1>
          <div className="detail-sub">
            <span>★ {c.rating.toFixed(2)}</span>
            <span className="dot">·</span>
            <u onClick={() => openOverlay('reviews')}>{d.reviews.length} đánh giá</u>
            {c.isSuperhost && <><span className="dot">·</span><span>Siêu chủ nhà</span></>}
            <span className="dot">·</span>
            <u onClick={() => scrollTo('section-location')}>{c.city}, {c.country}</u>
          </div>
        </div>
        <div className="detail-actions">
          <button className="text-btn" onClick={share}>⤴ Chia sẻ</button>
          <button className={`text-btn ${c.isFavorite ? 'is-on' : ''}`} onClick={() => toggleFavorite(c.id)}>
            ♥ {c.isFavorite ? 'Đã lưu' : 'Lưu chỗ nghỉ'}
          </button>
        </div>
      </div>

      <Gallery card={c} />
      <SubNav />

      <div className="detail-body">
        <div className="detail-main">
          <GuestFavoriteBanner detail={d} card={c} />
          <Summary detail={d} card={c} />
          <HostRow host={d.host} />
          <Highlights detail={d} card={c} />
          <Sleeping bedrooms={d.bedrooms} />
          <Amenities detail={d} />
          <CalendarSection nights={nights} city={c.city} />
          <Reviews detail={d} card={c} />
          <Location card={c} />
          <HostProfile detail={d} />
          <ThingsToKnow detail={d} />
        </div>

        <BookPanel detail={d} card={c} />
      </div>

      {!!d.similar.length && (
        <section style={{ marginTop: 44, borderTop: '1px solid var(--divider)', paddingTop: 32 }}>
          <h2 className="section-title" style={{ fontSize: 22 }}>Chỗ nghỉ tương tự</h2>
          <p className="section-sub">Cùng khu vực hoặc cùng loại hình với {c.title}</p>
          <div className="card-grid">{d.similar.map(s => <Card key={s.id} card={s} lazy />)}</div>
        </section>
      )}
    </div>

    <MobileBar nights={nights} card={c} />
  </>;
}

/*
 * The stand-in shown while the listing loads. It has to be laid out exactly like
 * the page it stands in for, or the swap moves everything: the old one put the
 * photos first and the title underneath, so when the real page arrived — title
 * first, photos below — the heading jumped 418px up the screen. That jump is
 * what a click on a card felt like.
 *
 * Every measurement below is the real page's, taken off it: 20px above, the back
 * link, 8px, the title block, 16px, then the same photo mosaic.
 */
function DetailSkeleton() {
  const tile = { borderRadius: 12 };

  return (
    <div className="shell" style={{ paddingBlock: '20px 110px' }} aria-busy="true" aria-label="Đang tải chỗ nghỉ">
      <div className="skeleton" style={{ width: 150, height: 30, borderRadius: 8 }} />

      <div style={{ marginTop: 8, height: 61 }}>
        <div className="sk-line skeleton" style={{ width: 'min(420px, 70%)', height: 30, marginBottom: 9 }} />
        <div className="sk-line skeleton" style={{ width: 'min(320px, 55%)', height: 16 }} />
      </div>

      <div className="gallery" style={{
        marginTop: 16,
        gridTemplateColumns: '2fr 1fr 1fr',
        gridTemplateRows: 'repeat(2, clamp(110px, 16vw, 215px))'
      }}>
        <div className="skeleton" style={{ ...tile, gridRow: 'span 2' }} />
        {[1, 2, 3, 4].map(i => <div key={i} className="skeleton" style={tile} />)}
      </div>
    </div>
  );
}

function Gallery({ card }) {
  const imgs = card.images.slice(0, 5);
  // A place with fewer than five photos still gets the five-tile mosaic; the
  // padding repeats the last one, so a click on it means that photo.
  while (imgs.length < 5) imgs.push(imgs[imgs.length - 1] ?? '');

  // Clicking a photo goes straight to it in the viewer. The grid is what the
  // button is for — nobody who clicked a particular picture wanted a contact
  // sheet of all of them first.
  const openAt = i => {
    set({ photoIndex: Math.min(i, card.images.length - 1) });
    openOverlay('photos');
  };

  return (
    <div className="gallery" id="section-photos"
         style={{ gridTemplateColumns: '2fr 1fr 1fr', gridTemplateRows: 'repeat(2,clamp(110px,16vw,215px))' }}>
      {imgs.map((src, i) => (
        <figure className="gallery-tile" key={i} onClick={() => openAt(i)}
                style={{ margin: 0, ...(i === 0 ? { gridRow: 'span 2' } : {}) }}>
          <img src={src} alt={`${card.title} — ảnh ${i + 1}`} loading={i === 0 ? 'eager' : 'lazy'} decoding="async" />
        </figure>
      ))}
      <button className="gallery-all"
              onClick={() => { set({ photoIndex: null }); openOverlay('photos'); }}>⊞ Hiện tất cả ảnh</button>
    </div>
  );
}

/** Sticky in-page nav, mirroring airbnb.com's NAV_DEFAULT section. */
function SubNav() {
  const links = [
    ['section-photos', 'Ảnh'], ['section-amenities', 'Tiện nghi'],
    ['section-reviews', 'Đánh giá'], ['section-location', 'Vị trí']
  ];
  return (
    <nav className="detail-nav" aria-label="Mục trong trang">
      {links.map(([target, label]) => <button key={target} onClick={() => scrollTo(target)}>{label}</button>)}
    </nav>
  );
}

function GuestFavoriteBanner({ detail, card }) {
  if (!card.isGuestFavorite || !card.reviewCount) return null;

  return (
    <div className="gf-banner">
      <div className="gf-title">
        <span className="gf-laurel"><Icon name="star" size={30} filled weight={0} /></span>
        <div>
          <b>Khách yêu thích</b>
          <span>Một trong những chỗ nghỉ được yêu thích nhất trên StayHost</span>
        </div>
      </div>
      <div className="gf-metric"><b>{card.rating.toFixed(2)}</b><span>★★★★★</span></div>
      <div className="gf-metric"><b>{detail.reviews.length}</b><span>đánh giá</span></div>
    </div>
  );
}

function Summary({ detail, card }) {
  return (
    <div className="summary-block">
      <div className="summary-title">{card.typeLabel} tại {card.city} do {detail.host.name} cho thuê</div>
      <div className="summary-meta">
        {card.maxGuests} khách · {card.bedrooms} phòng ngủ · {card.beds} giường · {card.bathrooms} phòng tắm
      </div>
      <p className="summary-desc">{detail.description}</p>
      {card.highlight && (
        <p className="summary-desc" style={{ marginTop: 10 }}><b>Điểm nổi bật:</b> {card.highlight}</p>
      )}
    </div>
  );
}

function HostRow({ host }) {
  return (
    <div className="host-row">
      <Avatar url={host.avatarUrl} initials={host.initials} className="host-avatar" />
      <div>
        <div className="host-name">Chủ nhà: {host.name}</div>
        <div className="host-meta">
          {host.isSuperhost ? 'Siêu chủ nhà · ' : ''}{host.yearsHosting} năm kinh nghiệm đón khách
        </div>
      </div>
    </div>
  );
}

function Highlights({ detail, card }) {
  const items = [
    card.isSuperhost && {
      ic: 'star', title: `${detail.host.name} là Siêu chủ nhà`,
      sub: 'Siêu chủ nhà là những chủ nhà giàu kinh nghiệm, được đánh giá cao.'
    },
    card.amenityKeys.includes('selfcheckin') && {
      ic: 'selfcheckin', title: 'Tự nhận phòng',
      sub: 'Bạn có thể tự nhận phòng bằng khoá số ở cửa.'
    },
    { ic: 'heart', title: 'Huỷ miễn phí trước 48 giờ', sub: detail.cancellationPolicy },
    card.amenityKeys.includes('workspace') && {
      ic: 'workspace', title: 'Không gian riêng để làm việc',
      sub: 'Phòng có bàn làm việc và wifi tốc độ cao, phù hợp làm việc từ xa.'
    }
  ].filter(Boolean);

  return (
    <div className="highlights">
      {items.map(i => (
        <div className="highlight" key={i.title}>
          <span className="ic"><Icon name={i.ic} size={24} /></span>
          <span className="tx"><b>{i.title}</b><span>{i.sub}</span></span>
        </div>
      ))}
    </div>
  );
}

/** docs/01 TĐ-05 — which beds are in which room, as the host described them. */
function Sleeping({ bedrooms }) {
  if (!bedrooms?.length) return null;

  return (
    <section className="detail-section" id="section-sleeping">
      <h2>Nơi bạn sẽ ngủ</h2>
      <div className="sleep-grid">
        {bedrooms.map((room, i) => (
          <div className="sleep-card" key={`${room.name}-${i}`}>
            <span className="ic"><Icon name="house" size={26} /></span>
            <b>{room.name}</b>
            <span>{summariseBeds(room.beds)}</span>
          </div>
        ))}
      </div>
    </section>
  );
}

/** "2 giường đôi, 1 giường đơn" rather than a bare list. */
function summariseBeds(beds) {
  const counts = new Map();
  for (const bed of beds) counts.set(bed, (counts.get(bed) ?? 0) + 1);
  return [...counts].map(([bed, n]) => `${n} ${bed.toLowerCase()}`).join(', ');
}

/**
 * docs/01 TĐ-04 — the present amenities come first, then the notable ones this
 * place does not have, struck through so absence is stated rather than implied.
 */
function Amenities({ detail }) {
  const present = detail.allAmenities.filter(a => a.available);
  const missing = detail.allAmenities.filter(a => !a.available);
  const preview = [...present.slice(0, 8), ...missing.slice(0, 4)];

  return (
    <section className="detail-section" id="section-amenities">
      <h2>Tiện nghi tại đây</h2>
      <div className="amenity-grid">
        {preview.map(a => (
          <div className={`amenity ${a.available ? '' : 'is-missing'}`} key={a.key}>
            <span className="ic"><AmenityIcon name={a.key} /></span><span>{a.label}</span>
          </div>
        ))}
      </div>
      {detail.allAmenities.length > preview.length && (
        <button className="btn btn-outline btn-sm" style={{ marginTop: 18 }} onClick={() => openOverlay('amenities')}>
          Hiện tất cả {detail.allAmenities.length} tiện nghi
        </button>
      )}
    </section>
  );
}

function CalendarSection({ nights, city }) {
  const state = useStore();

  return (
    <section className="detail-section" id="section-calendar">
      <h2>{nights} đêm tại {city}</h2>
      <p style={{ margin: '-8px 0 18px', fontSize: 14, color: 'var(--ink-muted)' }}>
        {longDate(state.checkIn)} – {longDate(state.checkOut)}
      </p>
      <Calendar months={4} />
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 14 }}>
        <button className="text-btn" onClick={clearDates}>Xoá ngày</button>
      </div>
    </section>
  );
}

/**
 * Airbnb tags reviews with recurring topics and a count. We derive the same
 * signal from keyword hits so the chips reflect what guests actually wrote.
 */
const REVIEW_TOPICS = [
  ['Sạch sẽ', ['sạch', 'gọn gàng', 'ngăn nắp']],
  ['Vị trí', ['vị trí', 'đi bộ', 'gần', 'trung tâm', 'biển']],
  ['Chủ nhà', ['chủ nhà', 'phản hồi', 'chu đáo', 'nhiệt tình', 'tinh tế']],
  ['Tiện nghi', ['bếp', 'wifi', 'giường', 'điều hoà', 'máy lạnh', 'trái cây']],
  ['Yên tĩnh', ['yên tĩnh', 'ngủ ngon', 'thoải mái']],
  ['Đáng tiền', ['giá', 'hợp lý', 'đáng']]
];

function Reviews({ detail, card }) {
  const navigate = useNavigate();
  const rb = detail.ratingBreakdown;
  const shown = detail.reviews.slice(0, 6);

  const topics = REVIEW_TOPICS
    .map(([label, keywords]) => ({
      label,
      n: detail.reviews.filter(r => keywords.some(k => r.text.toLowerCase().includes(k))).length
    }))
    .filter(t => t.n > 0)
    .sort((a, b) => b.n - a.n);

  return (
    <section className="detail-section" id="section-reviews">
      <div className="rating-summary">
        <span className="rating-big">★ {card.rating.toFixed(2)}</span>
        <span style={{ fontSize: 15, color: 'var(--ink-muted)' }}>· {detail.reviews.length} đánh giá</span>
        {card.isGuestFavorite && <span className="badge confirmed" style={{ marginLeft: 6 }}>Khách yêu thích</span>}
      </div>

      <StarDistribution counts={rb.starCounts} total={detail.reviews.length} />

      <div className="rating-bars">
        {Object.entries(RATING_LABELS).map(([key, label]) => (
          <div className="rating-bar" key={key}>
            <span>{label}</span>
            <span className="track"><span className="fill" style={{ width: `${(rb[key] / 5) * 100}%` }} /></span>
            <span className="val">{rb[key].toFixed(1)}</span>
          </div>
        ))}
      </div>

      {!!topics.length && (
        <div className="topic-row">
          {topics.map(t => <span className="topic-chip" key={t.label}>{t.label} <b>{t.n}</b></span>)}
        </div>
      )}

      <div className="review-grid">
        {shown.map(r => (
          <article className="review" key={r.id ?? `${r.authorName}-${r.when}`}>
            <div className="review-head">
              <Avatar initials={r.authorInitials} />
              <div style={{ minWidth: 0 }}>
                {/* docs/01 TK-05 — a real guest's name opens their profile. */}
                {r.authorUserId
                  ? <button className="link-btn review-name"
                            onClick={() => navigate(`/users/${r.authorUserId}`)}>{r.authorName}</button>
                  : <div className="review-name">{r.authorName}</div>}
                <div className="review-when">{r.authorLocation ? `${r.authorLocation} · ` : ''}{r.when}</div>
              </div>
            </div>
            <p>{r.text}</p>
            <HostReply review={r} />
          </article>
        ))}
      </div>

      {detail.reviews.length > shown.length && (
        <button className="btn btn-outline btn-sm" style={{ marginTop: 20 }} onClick={() => openOverlay('reviews')}>
          Hiện tất cả {detail.reviews.length} đánh giá
        </button>
      )}
    </section>
  );
}

/** docs/01 TĐ-10 — how the reviews split across five stars, five first. */
export function StarDistribution({ counts, total }) {
  if (!counts?.length || !total) return null;

  return (
    <div className="star-dist">
      {counts.map((n, i) => (
        <div className="star-dist-row" key={i}>
          <span>{5 - i} sao</span>
          <span className="track"><span className="fill" style={{ width: `${(n / total) * 100}%` }} /></span>
          <span>{n}</span>
        </div>
      ))}
    </div>
  );
}

/** docs/01 TĐ-12 — the host's single public answer, under the review. */
export function HostReply({ review }) {
  if (!review.hostReply) return null;

  return (
    <div className="review-reply">
      <b>Phản hồi của chủ nhà{review.hostRepliedAt ? ` · ${longDate(review.hostRepliedAt.slice(0, 10))}` : ''}</b>
      <p>{review.hostReply}</p>
    </div>
  );
}

/** Neighbourhood context derived from what the listing actually offers. */
function neighbourhoodHighlights(c) {
  const out = [];
  if (c.amenityKeys.includes('beach')) out.push(['beach', 'Sát biển', 'Đi bộ vài phút là xuống tới bãi tắm.']);
  if (c.amenityKeys.includes('bike')) out.push(['bike', 'Dễ đi lại', 'Có xe đạp miễn phí để khám phá quanh khu.']);
  if (c.amenityKeys.includes('view')) out.push(['view', 'Tầm nhìn đẹp', 'Khung cảnh mở, ít bị che chắn.']);
  if (c.amenityKeys.includes('parking')) out.push(['parking', 'Đỗ xe thuận tiện', 'Có chỗ đậu xe ngay tại chỗ nghỉ.']);
  if (out.length < 2) out.push(['house', `Trung tâm ${c.city}`, 'Gần quán ăn, cà phê và điểm tham quan chính.']);
  return out.slice(0, 3);
}

function Location({ card }) {
  return (
    <section className="detail-section" id="section-location">
      <h2>Vị trí chỗ nghỉ</h2>
      <p style={{ margin: '-8px 0 16px', fontSize: 14.5, color: 'var(--ink-muted)' }}>{card.city}, {card.country}</p>
      <DetailMap latitude={card.latitude} longitude={card.longitude} />

      <h3 style={{ margin: '22px 0 12px', fontSize: 15, fontWeight: 700 }}>Điểm nổi bật của khu vực</h3>
      <div className="hood-grid">
        {neighbourhoodHighlights(card).map(([ic, title, text]) => (
          <div className="hood" key={title}>
            <span className="ic"><Icon name={ic} size={22} /></span>
            <div><b>{title}</b><span>{text}</span></div>
          </div>
        ))}
      </div>

      <p style={{ margin: '16px 0 0', fontSize: 13.5, color: 'var(--ink-muted)' }}>
        Vị trí chính xác được cung cấp sau khi đặt chỗ thành công.
      </p>
    </section>
  );
}

function HostProfile({ detail }) {
  const h = detail.host;
  const navigate = useNavigate();

  const message = async () => {
    if (!requireAuth()) return;
    try {
      const thread = await api.sendMessage({
        listingId: detail.card.id,
        body: 'Chào bạn, mình muốn hỏi thêm về chỗ nghỉ.'
      });
      set({ activeThread: thread });
      navigate('/messages');
    } catch (err) { toast(err.message); }
  };

  return (
    <section className="detail-section" id="section-host">
      <h2>Gặp gỡ chủ nhà</h2>
      <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap', alignItems: 'flex-start' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 14, minWidth: 220 }}>
          <Avatar url={h.avatarUrl} initials={h.initials} className="host-avatar" size={64} />
          <div>
            {/* docs/01 TK-05 — the name opens the profile when there is one to open. */}
            {h.userId
              ? <button className="link-btn" style={{ fontSize: 20, fontWeight: 800 }}
                        onClick={() => navigate(`/users/${h.userId}`)}>{h.name}</button>
              : <div style={{ fontSize: 20, fontWeight: 800 }}>{h.name}</div>}
            <div className="host-meta">{h.isSuperhost ? 'Siêu chủ nhà' : 'Chủ nhà'}</div>
          </div>
        </div>
        <div style={{ display: 'grid', gap: 8, fontSize: 14, color: 'var(--ink-body)', minWidth: 200 }}>
          <div><b>{h.totalReviews}</b> đánh giá</div>
          <div><b>★ {h.averageRating.toFixed(2)}</b> điểm trung bình</div>
          <div><b>{h.listingCount}</b> chỗ nghỉ đang cho thuê</div>
          <div>{h.joinedLabel}</div>
        </div>
      </div>
      {h.bio && <p className="summary-desc" style={{ marginTop: 18 }}>{h.bio}</p>}
      <div style={{ display: 'grid', gap: 6, marginTop: 16, fontSize: 14, color: 'var(--ink-body)' }}>
        <div>Tỉ lệ phản hồi: <b>{h.responseRate}</b></div>
        <div>Thời gian phản hồi: <b>{h.responseTime}</b></div>
      </div>
      {h.userId
        ? <button className="btn btn-outline btn-sm" style={{ marginTop: 18 }} onClick={message}>Nhắn tin cho {h.name}</button>
        : <button className="btn btn-outline btn-sm" style={{ marginTop: 18 }} onClick={() => openOverlay('contact-host')}>
            Nhắn tin cho chủ nhà
          </button>}
    </section>
  );
}

function ThingsToKnow({ detail }) {
  return (
    <section className="detail-section" id="section-know">
      <h2>Những điều cần biết</h2>
      <div className="know-grid">
        <div className="know">
          <h3>Nội quy nhà</h3>
          <ul>{detail.houseRules.map(r => <li key={r}>{r}</li>)}</ul>
        </div>
        <div className="know">
          <h3>An toàn &amp; chỗ ở</h3>
          <ul>{detail.safetyInfo.map(r => <li key={r}>{r}</li>)}</ul>
        </div>
        <div className="know">
          <h3>Chính sách huỷ</h3>
          <ul><li>{detail.cancellationPolicy}</li></ul>
        </div>
      </div>
    </section>
  );
}

/**
 * docs/01 MR-08 and MR-09 — a hotel sells rooms of a kind, so the guest picks
 * one before checkout and the price follows the room rather than the property.
 */
function RoomPicker({ rooms }) {
  const state = useStore();

  // Pick the first room that still has one free, so the panel is never in a
  // state where nothing is selected and the price means nothing.
  useEffect(() => {
    if (!rooms?.length) { if (state.roomTypeId) setRoom(null); return; }
    if (rooms.some(r => r.id === state.roomTypeId && r.available > 0)) return;
    setRoom(rooms.find(r => r.available > 0)?.id ?? rooms[0].id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rooms]);

  if (!rooms?.length) return null;

  return (
    <div className="room-picker">
      <p className="cap" style={{ margin: '14px 0 8px' }}>Chọn loại phòng</p>
      {rooms.map(r => (
        <button key={r.id} className={`room-option ${state.roomTypeId === r.id ? 'is-on' : ''}`}
                disabled={r.available === 0} onClick={() => setRoom(r.id)}>
          <div style={{ minWidth: 0, flex: 1 }}>
            <b>{r.name}</b>
            <span>{r.summary}</span>
            <i>{r.available > 0 ? `Còn ${r.available}/${r.inventory} phòng` : 'Hết phòng cho ngày này'}</i>
          </div>
          <div className="room-price">{money(r.pricePerNight)}<span>/ đêm</span></div>
        </button>
      ))}
    </div>
  );
}

function BookPanel({ detail, card }) {
  const state = useStore();
  const q = state.quote;
  const result = state.bookingResult;
  const rooms = detail?.roomTypes ?? null;

  const checkout = () => {
    if (!requireAuth()) return;
    set({ checkoutStep: 0, bookingError: null, overlay: 'checkout' });
  };

  return (
    <aside className="book-panel">
      <div className="book-price">
        <span className="amount">{money(card.pricePerNight)}</span>
        <span className="per">/ đêm{rooms ? ' · phòng rẻ nhất' : ''}</span>
        {card.isGuestFavorite && <span className="per" style={{ marginLeft: 'auto' }}>★ {card.rating.toFixed(2)}</span>}
      </div>

      <RoomPicker rooms={rooms} />

      <div className="book-fields">
        <div className="book-dates">
          <button className="book-field" onClick={() => openOverlay('dates')}>
            <span className="cap">NHẬN PHÒNG</span>
            <span className="val">{longDate(state.checkIn)}</span>
          </button>
          <button className="book-field" onClick={() => openOverlay('dates')}>
            <span className="cap">TRẢ PHÒNG</span>
            <span className="val">{longDate(state.checkOut)}</span>
          </button>
        </div>
        <div className="book-guests">
          <button className="book-guests-open" onClick={() => openOverlay('guests')}>
            <span className="cap">KHÁCH</span>
            <span className="val">{guestLabel()}</span>
          </button>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <button className="round-btn lg" aria-label="Giảm số khách"
                    disabled={totalGuests() <= 1} onClick={() => bumpTotalGuests(-1)}>−</button>
            <button className="round-btn lg" aria-label="Tăng số khách"
                    disabled={totalGuests() >= card.maxGuests} onClick={() => bumpTotalGuests(1)}>+</button>
          </div>
        </div>
      </div>

      <button className="btn btn-primary btn-block" style={{ marginTop: 16 }} onClick={checkout}>Đặt chỗ ngay</button>
      <p className="book-note">Bạn chưa bị trừ tiền ở bước này</p>

      {q ? <>
        <PriceLines q={q} />
        {q.guestsExceeded && (
          <div className="book-alert is-error">
            <b>Vượt quá sức chứa</b>
            <span>Chỗ nghỉ này nhận tối đa {q.maxGuests} khách.</span>
          </div>
        )}
      </> : <div className="book-lines"><div className="book-line"><span>Đang tính giá…</span></div></div>}

      {result && (
        <div className="book-alert">
          <b>Đã giữ chỗ cho bạn · {result.reference}</b>
          <span>{result.nights} đêm · {result.guests} khách · {money(result.total)}. Chủ nhà sẽ xác nhận trong 12 giờ.</span>
        </div>
      )}

      {state.bookingError && (
        <div className="book-alert is-error"><b>Không đặt được</b><span>{state.bookingError}</span></div>
      )}

      <button className="text-btn" style={{ marginTop: 14, justifyContent: 'center' }}
              onClick={() => openOverlay('report')}>⚑ Báo cáo chỗ nghỉ này</button>
    </aside>
  );
}

function MobileBar({ nights, card }) {
  const state = useStore();
  const q = state.quote;

  const checkout = () => {
    if (!requireAuth()) return;
    set({ checkoutStep: 0, bookingError: null, overlay: 'checkout' });
  };

  return (
    <div className="mobile-bar">
      <div style={{ minWidth: 0 }}>
        <div className="amount">{money(q ? q.total : card.pricePerNight)}</div>
        <div className="meta">{nights} đêm · {totalGuests()} khách</div>
      </div>
      <button className="btn btn-primary" onClick={checkout}>Đặt chỗ ngay</button>
    </div>
  );
}

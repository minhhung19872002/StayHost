import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import {
  set, state as store, isDiscovery, activeFilterCount, guestLabel, totalGuests,
  loadSuggestions, loadNotifications, becomeHost, logout, toast, openOverlay, openMenu, toggleAmenity, settleDates,
  clearDates, resetCalendarView
} from '../lib/store.js';
import { api } from '../lib/api.js';
import { applySearch } from '../lib/nav.js';
import { recentSearches, clearSearchHistory } from '../lib/history.js';
import { dateRangeLabel, shortDate, debounce } from '../lib/format.js';
import { Avatar } from './Avatar.jsx';
import { Icon } from './Icon.jsx';
import { DateFields, GuestFields } from './modals/SearchModals.jsx';

// docs/01 TM-02 — four rows, and each one goes somewhere. The last two used to
// answer with a toast saying the demo only had homes, which stopped being true
// once MR-01 and MR-05 shipped their own pages.
const TABS = [
  { key: 'all', label: 'Tất cả', icon: 'all', path: '/' },
  { key: 'homes', label: 'Chỗ ở', icon: 'house', path: '/' },
  { key: 'experiences', label: 'Trải nghiệm', icon: 'boutique', path: '/experiences' },
  { key: 'services', label: 'Dịch vụ', icon: 'pool', path: '/services' }
];

const debouncedSuggest = debounce(() => loadSuggestions(), 180);
const debouncedSearch = debounce(() => applySearch(), 320);

export function Header() {
  const state = useStore();
  const location = useLocation();
  const navigate = useNavigate();

  const onBrowse = location.pathname === '/';
  const discovery = onBrowse && isDiscovery();

  // Any click outside the menus or the destination field closes them.
  useEffect(() => {
    const onDocClick = e => {
      const patch = {};
      if (store.menu && !e.target.closest?.('.menu-anchor')) patch.menu = null;
      if (store.suggestOpen && !e.target.closest?.('.search-anchor')) patch.suggestOpen = false;
      if (Object.keys(patch).length) set(patch);
    };
    document.addEventListener('click', onDocClick);
    return () => document.removeEventListener('click', onDocClick);
  }, []);

  const submitSearch = e => {
    e.preventDefault();
    set({ suggestOpen: false });
    applySearch({ replace: false });
    if (!onBrowse) navigate(`/?${new URLSearchParams(location.search)}`);
  };

  const onQueryInput = value => {
    set({ q: value, suggestOpen: true });
    debouncedSuggest();
    if (onBrowse) debouncedSearch();
  };

  return <>
    <div className="header-main">
      <button className="brand" onClick={() => navigate('/')} aria-label="StayHost OS — về trang chủ">
        <span className="brand-mark" aria-hidden="true">S</span>
        <span className="brand-text">StayHost<span> OS</span></span>
      </button>

      <nav className="nav-tabs" aria-label="Loại dịch vụ">
        {TABS.map(t => (
          <button key={t.key} className={`nav-tab ${state.tab === t.key ? 'is-active' : ''}`}
                  aria-pressed={state.tab === t.key}
                  onClick={() => { set({ tab: t.key }); navigate(t.path); }}>
            <span className="ic"><Icon name={t.icon} size={22} /></span>
            <span>{t.label}</span>
          </button>
        ))}
      </nav>

      <div className="header-actions">
        <button className="ghost-btn host-link" onClick={() => navigate(state.user?.isHost ? '/hosting' : '/host')}>
          {state.user?.isHost ? 'Trang chủ nhà' : 'Cho thuê nhà'}
        </button>

        {state.user && (
          <div className="menu-anchor">
            <button className="icon-btn bell" aria-label="Thông báo" aria-expanded={state.menu === 'bell'}
                    onClick={() => { openMenu('bell'); if (store.menu === 'bell') loadNotifications(); }}>
              <Icon name="star" size={18} />
              {!!state.notifications?.unread && <span className="bell-dot">{state.notifications.unread}</span>}
            </button>
            {state.menu === 'bell' && <BellMenu />}
          </div>
        )}

        <button className="icon-btn" onClick={() => openOverlay('language')}
                aria-label="Chọn ngôn ngữ và tiền tệ" title="Ngôn ngữ & tiền tệ">
          <Icon name="globe" size={18} />
        </button>

        <div className="menu-anchor">
          <button className="account-btn" aria-haspopup="true" aria-expanded={state.menu === 'account'}
                  onClick={() => openMenu('account')}>
            <span className="ic"><Icon name="menu" size={17} /></span>
            <UnreadBadge />
            {state.user
              ? <Avatar url={state.user.avatarUrl} initials={state.user.initials} />
              : <span className="avatar is-anon" aria-hidden="true"><Icon name="heart" size={15} /></span>}
          </button>
          {state.menu === 'account' && <AccountMenu />}
        </div>
      </div>
    </div>

    <SearchBar wide={discovery} onSubmit={submitSearch} onQueryInput={onQueryInput} />

    {/*
      * The chip row stays mounted while browsing and collapses to nothing on the
      * landing page, rather than appearing out of nowhere. Its 52px arriving in
      * one frame was half the jolt when a rail heading turned the landing page
      * into a set of results.
      */}
    {onBrowse && (
      <div className={`quick-slot ${discovery ? 'is-collapsed' : ''}`} aria-hidden={discovery}>
        <QuickBar />
      </div>
    )}
  </>;
}

function UnreadBadge() {
  const state = useStore();
  const unread = state.user?.unreadMessages ?? 0;
  if (unread > 0) return <span className="fav-count" title="Tin nhắn chưa đọc">{unread}</span>;
  if (state.favCount > 0) return <span className="fav-count" title="Chỗ nghỉ đã lưu">{state.favCount}</span>;
  return null;
}

/**
 * Below this the calendar has nowhere to hang: two months need more room than a
 * phone has, so those screens keep the modal.
 */
const DROPDOWN_FROM = '(min-width: 900px)';

/**
 * The landing page keeps the tall "Địa điểm · Ngày · Khách" pill; every other
 * route gets the compact summary bar, the same way airbnb.com collapses it.
 */
function SearchBar({ wide, onSubmit, onQueryInput }) {
  const state = useStore();

  // Which segment is open, the way airbnb.com does it: the bar goes grey and the
  // one you are filling in lifts out of it in white.
  const [openSeg, setOpenSeg] = useState(null);
  const barRef = useRef(null);
  const [roomy, setRoomy] = useState(() => window.matchMedia(DROPDOWN_FROM).matches);

  useEffect(() => {
    const mq = window.matchMedia(DROPDOWN_FROM);
    const onChange = e => { setRoomy(e.matches); if (!e.matches) setOpenSeg(null); };
    mq.addEventListener('change', onChange);
    return () => mq.removeEventListener('change', onChange);
  }, []);

  // A click anywhere else, or Escape, puts the bar back. Pointerdown rather than
  // click so the bar closes before whatever was clicked does its own work.
  useEffect(() => {
    if (!openSeg) return undefined;

    // Clicking away and pressing Escape close the bar just as "Xong" does, so
    // they have to commit a half-picked stay the same way — otherwise the pill
    // keeps reading "25-08 – ?" over a search that is using something else.
    const away = e => {
      if (!barRef.current?.contains(e.target)) closeBar();
    };
    const key = e => { if (e.key === 'Escape') closeBar(); };

    document.addEventListener('pointerdown', away);
    document.addEventListener('keydown', key);
    return () => {
      document.removeEventListener('pointerdown', away);
      document.removeEventListener('keydown', key);
    };
  }, [openSeg]);

  // Opening the date panel should show the dates it is about, wherever the
  // calendar was last paged to.
  const toggle = seg => {
    // The suggestion list belongs to the destination field; moving to another
    // segment should take it off the screen with it.
    set({ suggestOpen: false });

    if (!roomy) { openOverlay(seg === 'when' ? 'dates' : 'guests'); return; }
    if (seg === 'when' && openSeg !== 'when') resetCalendarView();
    setOpenSeg(seg);
  };

  const closeBar = () => { setOpenSeg(null); settleDates(); set({ suggestOpen: false }); };

  /*
   * The panel's box is worked out in pixels rather than left by CSS to one of
   * three anchorings, because `left: 0` cannot animate into `right: 0`. With
   * both edges and the height as numbers, the browser can tween all three and
   * the box glides from under one segment to under the next.
   */
  const popRef = useRef(null);
  const popContentRef = useRef(null);
  const [popBox, setPopBox] = useState(null);

  useLayoutEffect(() => {
    const anchor = barRef.current?.querySelector('.search-anchor');
    const content = popContentRef.current;
    if (!openSeg || !anchor || !content) { setPopBox(null); return undefined; }

    const measure = () => {
      const room = anchor.getBoundingClientRect().width;
      const wanted = { where: 440, when: 700, who: 400 }[openSeg];
      const width = Math.min(wanted, room);

      // Each panel sits under the thing it belongs to: the destination at the
      // left edge, the guests at the right, the calendar in the middle.
      const left = openSeg === 'where' ? 0 : openSeg === 'who' ? room - width : (room - width) / 2;

      setPopBox({ left, width, height: content.offsetHeight });
    };

    measure();

    // The calendar changes height when its tab changes, and the counters when
    // the panel is first filled; the box has to follow rather than clip them.
    const observer = new ResizeObserver(measure);
    observer.observe(content);
    observer.observe(anchor);
    return () => observer.disconnect();
    // The suggestion list arrives from the server after the panel is already
    // open, so its length has to be a reason to measure again.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [openSeg, state.suggestOpen, state.suggestions?.length]);

  const placeholder = wide
    ? 'Tìm điểm đến'
    : state.q.trim()
      ? `Chỗ ở tại ${state.q.trim()}`
      : state.category !== 'all'
        ? state.meta?.categories.find(c => c.key === state.category)?.label ?? 'Chỗ ở'
        : 'Mọi nơi';

  // The per-segment class carries the widths, so it belongs on both bars. Hanging
  // it on `wide` alone left the compact bar's three fields at their natural size,
  // bunched against the left edge with a third of the pill empty behind them.
  const segClass = seg =>
    `seg seg-${seg} ${openSeg === seg ? 'is-active' : ''}`;

  const field = (
    // The destination is a segment like the other two: putting the caret in it
    // greys the bar and lifts this one, so all three behave the same way.
    <label className={segClass('where')}>
      {wide && <span className="seg-cap">Địa điểm</span>}
      <input id="q" type="text" value={state.q} placeholder={placeholder} autoComplete="off"
             onChange={e => onQueryInput(e.target.value)}
             onFocus={() => {
               setOpenSeg('where');
               set({ suggestOpen: true });
               loadSuggestions();
             }}
             aria-autocomplete="list" aria-expanded={state.suggestOpen} />
    </label>
  );

  return (
    <div className={`search-row ${wide ? 'is-wide' : ''}`} ref={barRef}>
      {/* The panels hang off this rather than off the row, so they line up with
          the bar's edges instead of the window's. */}
      <div className="search-anchor">
      <form className={`searchbar ${wide ? '' : 'is-compact'} ${openSeg ? 'is-open' : ''}`}
            onSubmit={onSubmit} role="search">
        {!wide && <span className="seg-lead" aria-hidden="true"><Icon name="house" size={20} /></span>}
        {field}
        <span className="seg-div" />
        <button type="button" className={segClass('when')} onClick={() => toggle('when')}
                aria-expanded={openSeg === 'when'}>
          {wide && <span className="seg-cap">Ngày</span>}
          <span className="seg-val">
            {state.pickingFrom
              ? `${shortDate(state.pickingFrom)} – ?`
              : dateRangeLabel(state.checkIn, state.checkOut)}
          </span>
        </button>
        <span className="seg-div" />
        <button type="button" className={segClass('who')} onClick={() => toggle('who')}
                aria-expanded={openSeg === 'who'}>
          {wide && <span className="seg-cap">Khách</span>}
          <span className="seg-val">{guestLabel()}</span>
        </button>
        <button type="submit" className="search-go" aria-label="Tìm kiếm">
          <Icon name="search" size={wide ? 17 : 15} />
        </button>
      </form>

      {/*
        * One panel, not three. Moving between segments slides and resizes this
        * box rather than tearing one down and building another — which is the
        * difference between airbnb.com's search bar and a set of dropdowns that
        * happen to share a row.
        */}
      {openSeg && (
        <div className={`search-pop is-${openSeg}`} ref={popRef}
             style={popBox ? { left: popBox.left, width: popBox.width, height: popBox.height } : undefined}>
          <div className="search-pop-inner" ref={popContentRef} key={openSeg}>
            {openSeg === 'where' && <Suggestions onDone={closeBar} />}

            {openSeg === 'when' && <>
              <DateFields />
              <div className="search-pop-foot">
                <button type="button" className="text-btn"
                        onClick={() => { clearDates(); applySearch(); }}>Xoá ngày</button>
                <button type="button" className="btn btn-dark btn-sm" onClick={closeBar}>Xong</button>
              </div>
            </>}

            {openSeg === 'who' && <>
              <GuestFields />
              <div className="search-pop-foot">
                <span style={{ fontSize: 13, color: 'var(--ink-muted)' }}>Tổng {totalGuests()} khách</span>
                <button type="button" className="btn btn-dark btn-sm" onClick={closeBar}>Xong</button>
              </div>
            </>}
          </div>
        </div>
      )}
      </div>
    </div>
  );
}

/**
 * Destination dropdown. With nothing typed it leads with the guest's recent
 * searches (docs/01 TM-04); cities jump to a search, listings open the room page.
 */
function Suggestions({ onDone }) {
  const state = useStore();
  const navigate = useNavigate();
  const [recent, setRecent] = useState(() => recentSearches());

  // The list is re-read each time the panel opens, so a search made a moment
  // ago is already there.
  useEffect(() => { if (state.suggestOpen) setRecent(recentSearches()); }, [state.suggestOpen]);

  const showRecent = !state.q.trim() && recent.length > 0;
  if (!state.suggestOpen || (!state.suggestions?.length && !showRecent)) return null;

  const pick = s => {
    set({ suggestOpen: false });
    onDone?.();
    if (s.kind === 'listing') { navigate(`/rooms/${s.value}`); return; }
    set({ q: s.value });
    applySearch({ replace: false });
  };

  const replay = entry => {
    onDone?.();
    set({
      suggestOpen: false,
      q: entry.q,
      checkIn: entry.checkIn ?? state.checkIn,
      checkOut: entry.checkOut ?? state.checkOut,
      guests: entry.guests ?? state.guests
    });
    applySearch({ replace: false });
  };

  return (
    <div className="suggest-list" role="listbox">
      {showRecent && <>
        <div className="suggest-head">
          Tìm kiếm gần đây
          <button className="link-btn" style={{ float: 'right', fontSize: 12 }}
                  onMouseDown={e => e.preventDefault()}
                  onClick={() => { clearSearchHistory(); setRecent([]); }}>Xoá</button>
        </div>
        {recent.map(entry => (
          <button type="button" className="suggest-row" role="option" key={`recent:${entry.q}`}
                  onMouseDown={e => e.preventDefault()} onClick={() => replay(entry)}>
            <span className="suggest-ic"><Icon name="search" size={18} /></span>
            <span style={{ minWidth: 0 }}>
              <b>{entry.q}</b>
              <span>{entry.checkIn ? dateRangeLabel(entry.checkIn, entry.checkOut) : 'Mọi ngày'}</span>
            </span>
          </button>
        ))}
      </>}

      <div className="suggest-head">{state.q.trim() ? 'Kết quả gợi ý' : 'Điểm đến phổ biến'}</div>
      {(state.suggestions ?? []).map(s => (
        <button type="button" className="suggest-row" role="option" key={`${s.kind}:${s.value}`}
                onMouseDown={e => e.preventDefault()} onClick={() => pick(s)}>
          <span className="suggest-ic"><Icon name={s.kind === 'city' ? 'map' : 'house'} size={18} /></span>
          <span style={{ minWidth: 0 }}>
            <b>{s.label}</b>
            <span>{s.sub}</span>
          </span>
        </button>
      ))}
    </div>
  );
}

function BellMenu() {
  const state = useStore();
  const navigate = useNavigate();
  const feed = state.notifications;
  const items = feed?.items ?? [];

  const open = async n => {
    try { await api.readNotification(n.id); } catch { /* non-blocking */ }
    set({ menu: null });
    await loadNotifications();
    if (n.link) navigate(n.link);
  };

  return (
    <div className="menu bell-menu" role="menu">
      <div className="bell-head">
        <b>Thông báo</b>
        {!!feed?.unread && (
          <button className="link-btn" onClick={async e => {
            e.stopPropagation();
            try { await api.readAllNotifications(); await loadNotifications(); }
            catch (err) { toast(err.message); }
          }}>Đánh dấu đã đọc</button>
        )}
      </div>
      {items.length
        ? items.map(n => (
            <button className={`bell-row ${n.unread ? 'is-unread' : ''}`} role="menuitem" key={n.id}
                    onClick={() => open(n)}>
              <b>{n.title}</b>
              <span>{n.body}</span>
            </button>))
        : <p className="bell-empty">Chưa có thông báo nào.</p>}
    </div>
  );
}

function AccountMenu() {
  const state = useStore();
  const navigate = useNavigate();
  const u = state.user;

  const goTo = href => { set({ menu: null }); navigate(href); };
  const auth = mode => set({ authMode: mode, authError: null, overlay: 'login', menu: null });

  if (!u) {
    return (
      <div className="menu" role="menu">
        <button className="bold" role="menuitem" onClick={() => auth('register')}>Đăng ký</button>
        <button className="bold" role="menuitem" onClick={() => auth('login')}>Đăng nhập</button>
        <hr />
        <button role="menuitem" onClick={() => goTo('/wishlists')}>Danh sách yêu thích ({state.favCount})</button>
        <button role="menuitem" onClick={() => goTo('/trips')}>Chuyến đi của tôi</button>
        <button role="menuitem" onClick={() => goTo('/trip-plans')}>Lịch trình chuyến đi</button>
        <button role="menuitem" onClick={() => goTo('/friends')}>Bạn bè</button>
        <button role="menuitem" onClick={() => goTo('/host')}>Cho thuê nhà trên StayHost</button>
        <hr />
        <button role="menuitem" onClick={() => openOverlay('language')}>Ngôn ngữ &amp; tiền tệ</button>
        <button role="menuitem" onClick={() => goTo('/help')}>Trung tâm trợ giúp</button>
      </div>
    );
  }

  const startHosting = async () => {
    set({ menu: null });
    if (!await becomeHost()) return;
    toast('Bạn đã sẵn sàng cho thuê nhà.');
    navigate('/hosting');
  };

  return (
    <div className="menu" role="menu">
      <div className="menu-user">
        <Avatar url={u.avatarUrl} initials={u.initials} />
        <div style={{ minWidth: 0 }}>
          <b>{u.displayName || u.fullName}</b>
          <span>{u.email}</span>
        </div>
      </div>
      <hr />
      <button className="bold" role="menuitem" onClick={() => goTo('/messages')}>
        Tin nhắn {!!u.unreadMessages && <span className="fav-count" style={{ marginLeft: 'auto' }}>{u.unreadMessages}</span>}
      </button>
      <button role="menuitem" onClick={() => goTo('/trips')}>Chuyến đi của tôi</button>
      <button role="menuitem" onClick={() => goTo('/wishlists')}>Danh sách yêu thích ({state.favCount})</button>
      <button role="menuitem" onClick={() => goTo('/experiences/bookings')}>Vé trải nghiệm</button>
      <button role="menuitem" onClick={() => goTo('/services/bookings')}>Dịch vụ đã đặt</button>
      <button role="menuitem" onClick={() => goTo('/wallet')}>Số dư &amp; thẻ quà tặng</button>
      <button role="menuitem" onClick={() => goTo('/resolutions')}>Trung tâm giải quyết</button>
      <button role="menuitem" onClick={() => goTo('/shield')}>StayShield</button>
      <hr />
      {u.isHost
        ? <button className="bold" role="menuitem" onClick={() => goTo('/hosting')}>Trang chủ nhà ({u.listingCount} chỗ nghỉ)</button>
        : <button className="bold" role="menuitem" onClick={startHosting}>Cho thuê nhà trên StayHost</button>}
      {u.role === 'Admin' && <button className="bold" role="menuitem" onClick={() => goTo('/admin')}>Trang quản trị</button>}
      <button role="menuitem" onClick={() => openOverlay('profile')}>Tài khoản</button>
      <button role="menuitem" onClick={() => goTo(`/users/${u.id}`)}>Hồ sơ công khai</button>
      <hr />
      <button role="menuitem" onClick={() => openOverlay('language')}>Ngôn ngữ &amp; tiền tệ</button>
      <button role="menuitem" onClick={() => goTo('/help')}>Trung tâm trợ giúp</button>
      <button role="menuitem" onClick={async () => { set({ menu: null }); await logout(); navigate('/'); }}>Đăng xuất</button>
    </div>
  );
}

/**
 * airbnb.com's /s/ page has no category strip — a "Filters" button followed by
 * one-tap chips, then the total-price switch on the right.
 */
function QuickBar() {
  const state = useStore();
  const filters = activeFilterCount();
  const amenities = state.meta?.amenities ?? [];

  // Same running order airbnb.com uses: booking perks first, then amenities.
  const chips = ['kitchen', 'pool', 'wifi', 'ac', 'washer', 'parking', 'tv', 'workspace', 'gym', 'pet']
    .map(key => amenities.find(a => a.key === key))
    .filter(Boolean);

  const flag = key => { set({ [key]: !state[key] }); applySearch(); };

  return (
    <div className="catbar-outer">
      <div className="quickbar">
        <button className="quick-chip is-lead" onClick={() => openOverlay('filters')}>
          <Icon name="filter" size={15} /> Bộ lọc{!!filters && <span className="count">{filters}</span>}
        </button>
        <span className="quick-div" />

        <div className="quick-scroll" id="cat-scroll">
          <button className={`quick-chip ${state.instantBookOnly ? 'is-on' : ''}`} onClick={() => flag('instantBookOnly')}>Đặt ngay</button>
          <button className={`quick-chip ${state.freeCancellationOnly ? 'is-on' : ''}`} onClick={() => flag('freeCancellationOnly')}>Huỷ miễn phí</button>
          <button className={`quick-chip ${state.guestFavoriteOnly ? 'is-on' : ''}`} onClick={() => flag('guestFavoriteOnly')}>Khách yêu thích</button>
          <button className={`quick-chip ${state.superhostOnly ? 'is-on' : ''}`} onClick={() => flag('superhostOnly')}>Siêu chủ nhà</button>
          {chips.map(a => (
            <button key={a.key} className={`quick-chip ${state.amenities.includes(a.key) ? 'is-on' : ''}`}
                    aria-pressed={state.amenities.includes(a.key)}
                    onClick={() => { toggleAmenity(a.key); applySearch(); }}>{a.label}</button>
          ))}
        </div>

        <button className={`total-toggle ${state.showTotalPrice ? 'is-on' : ''}`}
                aria-pressed={state.showTotalPrice}
                title="Hiện giá mỗi đêm đã gồm phí dịch vụ, phí dọn dẹp và thuế"
                onClick={() => set({ showTotalPrice: !state.showTotalPrice })}>
          <span className="switch" aria-hidden="true" /> Giá đã gồm thuế và phí
        </button>
      </div>
    </div>
  );
}

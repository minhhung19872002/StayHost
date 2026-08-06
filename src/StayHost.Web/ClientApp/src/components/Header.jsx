import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import {
  set, state as store, isDiscovery, activeFilterCount, guestLabel,
  loadSuggestions, loadNotifications, loadMe, logout, toast, openOverlay, openMenu, toggleAmenity
} from '../lib/store.js';
import { api } from '../lib/api.js';
import { applySearch } from '../lib/nav.js';
import { recentSearches, clearSearchHistory } from '../lib/history.js';
import { dateRangeLabel, debounce } from '../lib/format.js';
import { Icon } from './Icon.jsx';

const TABS = [
  { key: 'homes', label: 'Chỗ ở', icon: 'house' },
  { key: 'experiences', label: 'Trải nghiệm', icon: 'boutique' },
  { key: 'services', label: 'Dịch vụ', icon: 'pool' }
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
      if (store.suggestOpen && !e.target.closest?.('.seg-where')) patch.suggestOpen = false;
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
                  onClick={() => {
                    set({ tab: t.key });
                    if (t.key !== 'homes') toast('Bản demo — mới có phần Chỗ ở.');
                  }}>
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
            <span className={`avatar ${state.user ? '' : 'is-anon'}`} aria-hidden="true">
              {state.user ? state.user.initials : <Icon name="heart" size={15} />}
            </span>
          </button>
          {state.menu === 'account' && <AccountMenu />}
        </div>
      </div>
    </div>

    <SearchBar wide={discovery} onSubmit={submitSearch} onQueryInput={onQueryInput} />

    {onBrowse && !discovery && <QuickBar />}
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
 * The landing page keeps the tall "Địa điểm · Ngày · Khách" pill; every other
 * route gets the compact summary bar, the same way airbnb.com collapses it.
 */
function SearchBar({ wide, onSubmit, onQueryInput }) {
  const state = useStore();

  const placeholder = wide
    ? 'Tìm điểm đến'
    : state.q.trim()
      ? `Chỗ ở tại ${state.q.trim()}`
      : state.category !== 'all'
        ? state.meta?.categories.find(c => c.key === state.category)?.label ?? 'Chỗ ở'
        : 'Mọi nơi';

  const field = (
    <label className="seg seg-where">
      {wide && <span className="seg-cap">Địa điểm</span>}
      <input id="q" type="text" value={state.q} placeholder={placeholder} autoComplete="off"
             onChange={e => onQueryInput(e.target.value)}
             onFocus={() => { set({ suggestOpen: true }); loadSuggestions(); }}
             aria-autocomplete="list" aria-expanded={state.suggestOpen} />
      <Suggestions />
    </label>
  );

  return (
    <div className={`search-row ${wide ? 'is-wide' : ''}`}>
      <form className={`searchbar ${wide ? '' : 'is-compact'}`} onSubmit={onSubmit} role="search">
        {!wide && <span className="seg-lead" aria-hidden="true"><Icon name="house" size={20} /></span>}
        {field}
        <span className="seg-div" />
        <button type="button" className={wide ? 'seg seg-when' : 'seg'} onClick={() => openOverlay('dates')}>
          {wide && <span className="seg-cap">Ngày</span>}
          <span className="seg-val">{dateRangeLabel(state.checkIn, state.checkOut)}</span>
        </button>
        <span className="seg-div" />
        <button type="button" className={wide ? 'seg seg-who' : 'seg'} onClick={() => openOverlay('guests')}>
          {wide && <span className="seg-cap">Khách</span>}
          <span className="seg-val">{guestLabel()}</span>
        </button>
        <button type="submit" className="search-go" aria-label="Tìm kiếm">
          <Icon name="search" size={wide ? 17 : 15} />
        </button>
      </form>
    </div>
  );
}

/**
 * Destination dropdown. With nothing typed it leads with the guest's recent
 * searches (docs/01 TM-04); cities jump to a search, listings open the room page.
 */
function Suggestions() {
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
    if (s.kind === 'listing') { navigate(`/rooms/${s.value}`); return; }
    set({ q: s.value });
    applySearch({ replace: false });
  };

  const replay = entry => {
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
    <div className="suggest" role="listbox">
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
        <button role="menuitem" onClick={() => goTo('/host')}>Cho thuê nhà trên StayHost</button>
        <hr />
        <button role="menuitem" onClick={() => openOverlay('language')}>Ngôn ngữ &amp; tiền tệ</button>
        <button role="menuitem" onClick={() => goTo('/help')}>Trung tâm trợ giúp</button>
      </div>
    );
  }

  const becomeHost = async () => {
    try {
      await api.becomeHost();
      set({ menu: null });
      await loadMe();
      toast('Bạn đã sẵn sàng cho thuê nhà.');
      navigate('/hosting');
    } catch (err) { toast(err.message); }
  };

  return (
    <div className="menu" role="menu">
      <div className="menu-user">
        <span className="avatar">{u.initials}</span>
        <div style={{ minWidth: 0 }}>
          <b>{u.fullName}</b>
          <span>{u.email}</span>
        </div>
      </div>
      <hr />
      <button className="bold" role="menuitem" onClick={() => goTo('/messages')}>
        Tin nhắn {!!u.unreadMessages && <span className="fav-count" style={{ marginLeft: 'auto' }}>{u.unreadMessages}</span>}
      </button>
      <button role="menuitem" onClick={() => goTo('/trips')}>Chuyến đi của tôi</button>
      <button role="menuitem" onClick={() => goTo('/wishlists')}>Danh sách yêu thích ({state.favCount})</button>
      <button role="menuitem" onClick={() => goTo('/resolutions')}>Trung tâm giải quyết</button>
      <hr />
      {u.isHost
        ? <button className="bold" role="menuitem" onClick={() => goTo('/hosting')}>Trang chủ nhà ({u.listingCount} chỗ nghỉ)</button>
        : <button className="bold" role="menuitem" onClick={becomeHost}>Cho thuê nhà trên StayHost</button>}
      {u.role === 'Admin' && <button className="bold" role="menuitem" onClick={() => goTo('/admin')}>Trang quản trị</button>}
      <button role="menuitem" onClick={() => openOverlay('profile')}>Tài khoản</button>
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
                title="Hiển thị giá trọn kỳ nghỉ thay vì giá mỗi đêm"
                onClick={() => set({ showTotalPrice: !state.showTotalPrice })}>
          <span className="switch" aria-hidden="true" /> Tổng giá
        </button>
      </div>
    </div>
  );
}

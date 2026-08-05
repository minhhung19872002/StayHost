// Entry point: routing, rendering and one delegated event handler for the whole app.

import { $, debounce, toast, isoOf, parseIso, todayIso, money } from './util.js';
import * as store from './store.js';
import { state, set, notify } from './store.js';
import { api } from './api.js';

import { renderHeader } from './components/header.js';
import { renderFooter } from './components/footer.js';
import { renderOverlay } from './components/modals.js';
import { mountResultsMap, mountDetailMap, destroyMaps, highlightMarker } from './components/map.js';

import { renderBrowse } from './views/browse.js';
import { renderDetail } from './views/detail.js';
import { renderWishlists } from './views/wishlists.js';
import { renderTrips } from './views/trips.js';
import { renderTrip } from './views/trip.js';
import { renderHostPage } from './views/host.js';
import { renderHosting } from './views/hosting.js';
import { renderMessages } from './views/messages.js';
import { renderAdmin } from './views/admin.js';

const headerEl = $('#header');
const mainEl = $('#main');
const footerEl = $('#footer');
const overlayEl = $('#overlay-root');

/* --------------------------------------------------------------- rendering */

function renderView() {
  switch (state.route.name) {
    case 'detail': return renderDetail();
    case 'wishlists': return renderWishlists();
    case 'trips': return renderTrips();
    case 'trip': return renderTrip();
    case 'host': return renderHostPage();
    case 'hosting': return renderHosting();
    case 'messages': return renderMessages();
    case 'admin': return renderAdmin();
    default: return renderBrowse();
  }
}

function snapshotFocus() {
  const el = document.activeElement;
  if (!el || el === document.body) return null;
  const act = el.getAttribute?.('data-act');
  if (!act && !el.id) return null;
  const snap = { act, id: el.id, start: null, end: null };
  try { snap.start = el.selectionStart; snap.end = el.selectionEnd; } catch { /* not a text input */ }
  return snap;
}

function restoreFocus(snap) {
  if (!snap) return;
  const selector = snap.id ? `#${CSS.escape(snap.id)}` : `[data-act="${snap.act}"]`;
  const el = document.querySelector(selector);
  if (!el) return;
  el.focus({ preventScroll: true });
  if (snap.start != null) {
    try { el.setSelectionRange(snap.start, snap.end); } catch { /* unsupported input type */ }
  }
}

let rafPending = false;

/**
 * Last HTML written per region. A state change usually touches one region, so
 * comparing the freshly built string against this lets us skip the DOM write —
 * and with it the image re-decode, layout and paint — for everything else.
 */
const lastHtml = { header: null, main: null, footer: null, overlay: null };

function writeRegion(key, el, html) {
  if (lastHtml[key] === html) return false;
  lastHtml[key] = html;
  el.innerHTML = html;
  return true;
}

/**
 * Main is the only region that can contain the Leaflet map. Tearing that node
 * down forces a full tile reload, so the live element is detached and dropped
 * back into the freshly rendered placeholder instead.
 */
function writeMain(html) {
  if (lastHtml.main === html) return false;
  lastHtml.main = html;

  const liveMap = mainEl.querySelector('#map');
  liveMap?.remove();

  mainEl.innerHTML = html;

  const placeholder = mainEl.querySelector('#map');
  if (liveMap && placeholder) placeholder.replaceWith(liveMap);
  return true;
}

/** Drops the memo so the next render is guaranteed to repaint everything. */
function invalidateRender() {
  lastHtml.header = lastHtml.main = lastHtml.footer = lastHtml.overlay = null;
}

function render() {
  if (rafPending) return;
  rafPending = true;

  requestAnimationFrame(() => {
    rafPending = false;

    const focus = snapshotFocus();
    const modalScroll = document.querySelector('.modal-body')?.scrollTop ?? 0;

    writeRegion('header', headerEl, renderHeader());
    const mainChanged = writeMain(renderView());
    writeRegion('footer', footerEl, renderFooter());
    const overlayChanged = writeRegion('overlay', overlayEl, renderOverlay());

    document.body.style.overflow = state.overlay ? 'hidden' : '';

    if (overlayChanged) {
      const modalBody = document.querySelector('.modal-body');
      if (modalBody && modalScroll) modalBody.scrollTop = modalScroll;
    }

    restoreFocus(focus);

    if (state.route.name === 'browse' && !store.isDiscovery() && !state.hideMap) {
      mountResultsMap({ refit: mainChanged });
    }
    if (state.route.name === 'detail' && state.detail) mountDetailMap();
  });
}

store.subscribe(render);

/* ------------------------------------------------------------------ router */

function parseRoute(pathname = location.pathname) {
  const parts = pathname.replace(/^\/+|\/+$/g, '').split('/');
  if (parts[0] === 'rooms' && parts[1]) return { name: 'detail', param: decodeURIComponent(parts[1]) };
  if (parts[0] === 'wishlists') return { name: 'wishlists', param: null };
  if (parts[0] === 'trips') return parts[1]
    ? { name: 'trip', param: parts[1] }
    : { name: 'trips', param: null };
  if (parts[0] === 'host') return { name: 'host', param: null };
  if (parts[0] === 'hosting') return { name: 'hosting', param: null };
  if (parts[0] === 'messages') return { name: 'messages', param: parts[1] ?? null };
  if (parts[0] === 'admin') return { name: 'admin', param: null };
  return { name: 'browse', param: null };
}

function syncUrlFromSearch(replace = true) {
  if (state.route.name !== 'browse') return;
  const usp = new URLSearchParams();
  if (state.q.trim()) usp.set('q', state.q.trim());
  if (state.category !== 'all') usp.set('category', state.category);
  if (state.amenities.length) usp.set('amenities', state.amenities.join(','));
  if (state.roomType !== 'any') usp.set('roomType', state.roomType);
  if (state.meta && state.maxPrice < state.meta.maxPrice) usp.set('maxPrice', String(state.maxPrice));
  if (state.meta && state.minPrice > state.meta.minPrice) usp.set('minPrice', String(state.minPrice));
  if (state.superhostOnly) usp.set('superhost', '1');
  if (state.guestFavoriteOnly) usp.set('guestFavorite', '1');
  if (state.instantBookOnly) usp.set('instantBook', '1');
  if (state.freeCancellationOnly) usp.set('freeCancellation', '1');
  usp.set('checkIn', state.checkIn);
  usp.set('checkOut', state.checkOut);
  usp.set('guests', String(store.totalGuests()));
  history[replace ? 'replaceState' : 'pushState']({}, '', `/?${usp}`);
}

function readSearchFromUrl(search = location.search) {
  const usp = new URLSearchParams(search);
  state.q = usp.get('q') ?? '';
  state.category = usp.get('category') ?? 'all';
  state.amenities = (usp.get('amenities') ?? '').split(',').filter(Boolean);
  state.roomType = usp.get('roomType') ?? 'any';
  state.superhostOnly = usp.get('superhost') === '1';
  state.guestFavoriteOnly = usp.get('guestFavorite') === '1';
  state.instantBookOnly = usp.get('instantBook') === '1';
  state.freeCancellationOnly = usp.get('freeCancellation') === '1';

  if (state.meta) {
    state.minPrice = Number(usp.get('minPrice')) || state.meta.minPrice;
    state.maxPrice = Number(usp.get('maxPrice')) || state.meta.maxPrice;
  } else if (usp.get('maxPrice')) {
    state.pendingMaxPrice = Number(usp.get('maxPrice'));
  }

  if (usp.get('checkIn')) state.checkIn = usp.get('checkIn');
  if (usp.get('checkOut')) state.checkOut = usp.get('checkOut');
  const g = Number(usp.get('guests'));
  if (g > 0) { state.guests.adults = g; state.guests.children = 0; }
}

async function navigate(href, { push = true } = {}) {
  const url = new URL(href, location.origin);
  const route = parseRoute(url.pathname);
  if (push) history.pushState({}, '', href);

  if (route.name === 'browse') readSearchFromUrl(url.search);

  state.route = route;
  state.overlay = null;
  state.menu = null;
  destroyMaps();
  // A route change replaces everything anyway; drop the memo so nothing lingers.
  invalidateRender();

  if (route.name !== 'detail') {
    state.detail = null;
    state.quote = null;
    state.bookingResult = null;
  }

  notify();
  window.scrollTo({ top: 0, behavior: 'instant' });
  await loadRoute();
}

async function loadRoute() {
  switch (state.route.name) {
    case 'detail':
      await store.loadDetail(state.route.param);
      break;
    case 'wishlists':
      state.activeWishlist = null;
      await Promise.all([store.loadFavorites(), store.loadWishlists()]);
      break;
    case 'trips':
      await store.loadBookings();
      break;
    case 'trip':
      await store.loadTrip(state.route.param);
      break;
    case 'host':
      notify();
      break;
    case 'hosting':
      await store.loadHosting();
      break;
    case 'messages':
      await store.loadThreads();
      if (state.route.param) await store.openThread(Number(state.route.param));
      break;
    case 'admin':
      if (state.user?.role === 'Admin') await store.loadAdmin();
      else notify();
      break;
    default:
      if (store.isDiscovery()) {
        if (!state.home) await store.loadHome(); else notify();
      } else {
        await store.runSearch();
      }
  }
}

window.addEventListener('popstate', () => {
  state.route = parseRoute();
  readSearchFromUrl();
  destroyMaps();
  notify();
  loadRoute();
});

window.addEventListener('sh:open-listing', e => navigate(`/rooms/${e.detail}`));

// Hovering a result card lifts the matching price pin on the map.
document.addEventListener('mouseover', e => {
  const card = e.target.closest?.('[data-listing]');
  if (card) highlightMarker(card.dataset.listing, true);
});
document.addEventListener('mouseout', e => {
  const card = e.target.closest?.('[data-listing]');
  if (card) highlightMarker(card.dataset.listing, false);
});

/* ---------------------------------------------------------- carousel (DOM) */

/** Swaps the visible frame in place — no re-render, no image churn. */
function stepCarousel(id, dir) {
  const card = document.querySelector(`[data-listing="${id}"] .card-media`);
  if (!card) return;

  const imgs = Array.from(card.querySelectorAll('img'));
  const current = imgs.findIndex(i => i.classList.contains('is-current'));
  const next = Math.min(imgs.length - 1, Math.max(0, current + dir));
  if (next === current) return;

  // Deferred neighbours only get a real src the moment they come into range.
  for (const i of [next - 1, next, next + 1]) {
    const img = imgs[i];
    if (img?.dataset.src) {
      img.src = img.dataset.src;
      delete img.dataset.src;
      img.classList.remove('is-deferred');
    }
  }

  imgs[current]?.classList.remove('is-current');
  imgs[next]?.classList.add('is-current');
  state.carousel[id] = next;

  card.querySelectorAll('.carousel-dots i').forEach((dot, i) => dot.classList.toggle('is-on', i === next));
  const prevBtn = card.querySelector('.carousel-nav.prev');
  const nextBtn = card.querySelector('.carousel-nav.next');
  if (prevBtn) prevBtn.disabled = next === 0;
  if (nextBtn) nextBtn.disabled = next === imgs.length - 1;
}

/* --------------------------------------------------------- search debounce */

const debouncedSearch = debounce(() => {
  syncUrlFromSearch();
  store.runSearch();
}, 320);

const debouncedSuggest = debounce(async () => {
  try {
    state.suggestions = await api.suggest(state.q);
    notify();
  } catch { /* suggestions are a nicety, never block typing */ }
}, 180);

function refreshResults() {
  if (state.route.name !== 'browse') { notify(); return; }
  syncUrlFromSearch();
  if (store.isDiscovery()) {
    if (!state.home) store.loadHome(); else notify();
  } else {
    store.runSearch();
  }
}

/* ------------------------------------------------------------ date helpers */

/** Nights already booked for the open listing, so a range cannot straddle them. */
function blockedNights() {
  return new Set(state.detail?.unavailableDates ?? []);
}

function rangeHasBlockedNight(fromIso, toIso) {
  const blocked = blockedNights();
  if (!blocked.size) return false;
  for (let d = parseIso(fromIso); isoOf(d) < toIso; d.setDate(d.getDate() + 1)) {
    if (blocked.has(isoOf(d))) return true;
  }
  return false;
}

/**
 * Two-click range picking: the first click sets check-in (with a one-night
 * default so the panel always has a valid quote), the second sets check-out,
 * and any later click starts over — the same rhythm Airbnb's picker uses.
 */
function pickDate(iso) {
  const previous = { checkIn: state.checkIn, checkOut: state.checkOut };

  if (state.awaitingCheckout && iso > state.checkIn) {
    state.checkOut = iso;
    state.awaitingCheckout = false;
  } else {
    state.checkIn = iso;
    const next = parseIso(iso);
    next.setDate(next.getDate() + 1);
    state.checkOut = isoOf(next);
    state.awaitingCheckout = true;
  }

  if (rangeHasBlockedNight(state.checkIn, state.checkOut)) {
    Object.assign(state, previous);
    state.awaitingCheckout = false;
    toast('Khoảng ngày này đã có người đặt. Chọn ngày khác nhé.');
    notify();
    return;
  }

  onDatesChanged();
}

function shiftCalendar(dir) {
  const d = parseIso(state.checkIn);
  d.setMonth(d.getMonth() + dir);
  const iso = isoOf(d);
  if (iso >= todayIso()) {
    state.checkIn = iso;
    const out = parseIso(iso);
    out.setDate(out.getDate() + 3);
    state.checkOut = isoOf(out);
    onDatesChanged();
  }
}

function applyDatePreset(key) {
  const start = parseIso(todayIso());
  if (key === 'weekend') {
    // Next Friday → Sunday.
    const daysToFriday = (5 - start.getDay() + 7) % 7 || 7;
    start.setDate(start.getDate() + daysToFriday);
    state.checkIn = isoOf(start);
    start.setDate(start.getDate() + 2);
    state.checkOut = isoOf(start);
  } else {
    const spans = { week: 7, fortnight: 14, month: 30 };
    start.setDate(start.getDate() + 7);
    state.checkIn = isoOf(start);
    start.setDate(start.getDate() + (spans[key] ?? 3));
    state.checkOut = isoOf(start);
  }
  onDatesChanged();
}

function onDatesChanged() {
  if (state.checkOut <= state.checkIn) {
    const out = parseIso(state.checkIn);
    out.setDate(out.getDate() + 1);
    state.checkOut = isoOf(out);
  }
  if (state.route.name === 'detail') store.refreshQuote();
  else refreshResults();
  notify();
}

/* -------------------------------------------------------------- guest math */

function bumpGuests(delta) {
  const max = state.detail?.card.maxGuests ?? 16;
  const next = Math.min(max, Math.max(1, store.totalGuests() + delta));
  const diff = next - store.totalGuests();
  if (!diff) return;

  if (diff > 0) state.guests.adults += diff;
  else if (state.guests.children > 0) state.guests.children = Math.max(0, state.guests.children + diff);
  else state.guests.adults = Math.max(1, state.guests.adults + diff);

  if (state.route.name === 'detail') store.refreshQuote();
  else refreshResults();
  notify();
}

/* -------------------------------------------------------- price slider DOM */

function updatePriceSliderUi() {
  const meta = state.meta;
  const wrap = document.querySelector('.range-wrap');
  if (!meta || !wrap) return;

  const span = Math.max(1, meta.maxPrice - meta.minPrice);
  const lowPct = ((state.minPrice - meta.minPrice) / span) * 100;
  const highPct = ((state.maxPrice - meta.minPrice) / span) * 100;

  const fill = wrap.querySelector('.range-fill');
  if (fill) { fill.style.left = `${lowPct}%`; fill.style.right = `${100 - highPct}%`; }

  const amounts = document.querySelectorAll('.range-vals .amt');
  if (amounts[0]) amounts[0].textContent = money(state.minPrice);
  if (amounts[1]) amounts[1].textContent = money(state.maxPrice) + (state.maxPrice >= meta.maxPrice ? '+' : '');

  document.querySelectorAll('.histogram i').forEach((bar, i, all) => {
    const at = meta.minPrice + (span * i) / (all.length - 1);
    bar.classList.toggle('in', at >= state.minPrice && at <= state.maxPrice);
  });
}

/* ------------------------------------------------------------- interactions */

document.addEventListener('click', async e => {
  const target = e.target.closest('[data-act]');

  // Any click outside the account menu closes it.
  if (state.menu && !e.target.closest('.menu-anchor')) { state.menu = null; notify(); }
  if (state.suggestOpen && !e.target.closest('.seg-where')) { state.suggestOpen = false; notify(); }

  if (!target) return;
  const act = target.dataset.act;
  if (act === 'input-q' || act?.startsWith('set-check')) return;

  switch (act) {
    case 'noop':
    case 'demo-auth':
      e.preventDefault();
      toast('Bản demo — chức năng này chưa kết nối dịch vụ thật.');
      break;

    case 'go':
      e.preventDefault();
      navigate(target.dataset.href);
      break;

    case 'open-listing': {
      e.preventDefault();
      navigate(`/rooms/${target.dataset.slug}`);
      break;
    }

    case 'toggle-fav':
      e.preventDefault();
      e.stopPropagation();
      store.toggleFavorite(Number(target.dataset.id));
      break;

    case 'carousel':
      e.preventDefault();
      e.stopPropagation();
      stepCarousel(target.dataset.id, Number(target.dataset.dir));
      break;

    case 'pick-category':
      state.category = target.dataset.key;
      refreshResults();
      notify();
      break;

    /* ---------------------------------------------------------- wishlists */

    case 'open-wishlist':
      await store.openWishlist(Number(target.dataset.id));
      window.scrollTo({ top: 0, behavior: 'instant' });
      break;

    case 'close-wishlist':
      state.activeWishlist = null;
      await store.loadWishlists();
      break;

    case 'new-wishlist': {
      const name = prompt('Tên danh sách mới', 'Chuyến đi sắp tới');
      if (!name?.trim()) break;
      try {
        await api.createWishlist(name.trim());
        await store.loadWishlists();
        toast('Đã tạo danh sách.');
      } catch (err) { toast(err.message); }
      break;
    }

    case 'rename-wishlist': {
      const current = state.activeWishlist?.list.name ?? '';
      const name = prompt('Tên danh sách', current);
      if (!name?.trim() || name.trim() === current) break;
      try {
        await api.renameWishlist(Number(target.dataset.id), name.trim());
        await store.openWishlist(Number(target.dataset.id));
        toast('Đã đổi tên.');
      } catch (err) { toast(err.message); }
      break;
    }

    case 'delete-wishlist':
      if (!confirm('Xoá danh sách này? Các chỗ nghỉ trong đó cũng bị bỏ lưu.')) break;
      try {
        await api.deleteWishlist(Number(target.dataset.id));
        state.activeWishlist = null;
        await Promise.all([store.loadWishlists(), store.loadFavorites()]);
        toast('Đã xoá danh sách.');
      } catch (err) { toast(err.message); }
      break;

    case 'pick-suggestion': {
      e.preventDefault();
      state.suggestOpen = false;
      const { kind, value } = target.dataset;
      if (kind === 'listing') {
        navigate(`/rooms/${value}`);
      } else {
        state.q = value;
        if (state.route.name !== 'browse') { navigate(`/?q=${encodeURIComponent(value)}`); }
        else { refreshResults(); notify(); }
      }
      break;
    }

    case 'set-tab':
      state.tab = target.dataset.key;
      if (state.tab !== 'homes') toast('Bản demo — mới có phần Chỗ ở.');
      notify();
      break;

    case 'set-inspiration':
      state.inspirationTab = target.dataset.key;
      notify();
      break;

    case 'rail-scroll': {
      const track = document.querySelector(`[data-rail-track="${CSS.escape(target.dataset.key)}"]`);
      if (track) track.scrollBy({ left: Number(target.dataset.dir) * track.clientWidth * 0.8, behavior: 'smooth' });
      break;
    }

    case 'cat-scroll': {
      const scroller = document.getElementById('cat-scroll');
      if (scroller) scroller.scrollBy({ left: Number(target.dataset.dir) * 320, behavior: 'smooth' });
      break;
    }

    case 'date-preset':
      applyDatePreset(target.dataset.key);
      break;

    case 'toggle-map':
      state.hideMap = !state.hideMap;
      if (state.hideMap) destroyMaps();
      notify();
      break;

    case 'toggle-instant':
      state.instantBookOnly = !state.instantBookOnly;
      refreshResults();
      notify();
      break;

    case 'toggle-free-cancel':
      state.freeCancellationOnly = !state.freeCancellationOnly;
      refreshResults();
      notify();
      break;

    case 'toggle-total':
      state.showTotalPrice = !state.showTotalPrice;
      notify();
      break;

    case 'open': {
      e.preventDefault();
      const overlay = target.dataset.overlay;
      // Booking creates a financial record, so it needs a signed-in guest.
      if (overlay === 'checkout') {
        if (!requireAuth()) break;
        state.checkoutStep = 0;
        state.bookingError = null;
      }
      state.overlay = overlay;
      state.menu = null;
      notify();
      break;
    }

    case 'close-overlay':
      state.overlay = null;
      notify();
      break;

    case 'close-overlay-bg':
      if (e.target === target) { state.overlay = null; notify(); }
      break;

    case 'toggle-menu':
      state.menu = state.menu === 'account' ? null : 'account';
      notify();
      break;

    case 'toggle-bell':
      state.menu = state.menu === 'bell' ? null : 'bell';
      if (state.menu === 'bell') await store.loadNotifications();
      notify();
      break;

    case 'read-all-notifications':
      e.stopPropagation();
      try {
        await api.readAllNotifications();
        await store.loadNotifications();
      } catch (err) { toast(err.message); }
      break;

    case 'open-notification': {
      const link = target.dataset.link;
      try { await api.readNotification(Number(target.dataset.id)); } catch { /* non-blocking */ }
      state.menu = null;
      await store.loadNotifications();
      if (link) navigate(link);
      break;
    }

    /* -------------------------------------------------------------- admin */

    case 'admin-publish':
      try {
        await api.adminPublish(Number(target.dataset.id), target.dataset.published === 'true');
        await store.loadAdmin();
        toast('Đã cập nhật trạng thái chỗ nghỉ.');
      } catch (err) { toast(err.message); }
      break;

    case 'admin-resolve': {
      const note = prompt('Ghi chú kết luận (không bắt buộc)') ?? '';
      try {
        await api.adminResolveReport(Number(target.dataset.id), target.dataset.status, note.trim() || null);
        await store.loadAdmin();
        toast('Đã cập nhật báo cáo.');
      } catch (err) { toast(err.message); }
      break;
    }

    case 'submit-report':
      try {
        const res = await api.report({
          listingId: state.detail?.card.id,
          reason: target.dataset.reason,
          detail: null
        });
        state.overlay = null;
        notify();
        toast(res.message);
      } catch (err) { toast(err.message); }
      break;

    case 'guests-inc': bumpGuests(1); break;
    case 'guests-dec': bumpGuests(-1); break;

    case 'guest-inc':
    case 'guest-dec': {
      const key = target.dataset.key;
      const min = key === 'adults' ? 1 : 0;
      const delta = act === 'guest-inc' ? 1 : -1;
      state.guests[key] = Math.max(min, Math.min(16, state.guests[key] + delta));
      if (state.route.name === 'detail') store.refreshQuote();
      else refreshResults();
      notify();
      break;
    }

    case 'count-inc':
    case 'count-dec': {
      const key = target.dataset.key;
      const delta = act === 'count-inc' ? 1 : -1;
      state[key] = Math.max(0, Math.min(8, (state[key] || 0) + delta));
      refreshResults();
      notify();
      break;
    }

    case 'toggle-amenity': {
      const key = target.dataset.key;
      state.amenities = state.amenities.includes(key)
        ? state.amenities.filter(a => a !== key)
        : [...state.amenities, key];
      refreshResults();
      notify();
      break;
    }

    case 'set-room-type':
      state.roomType = target.dataset.key;
      refreshResults();
      notify();
      break;

    case 'toggle-superhost':
      state.superhostOnly = !state.superhostOnly;
      refreshResults();
      notify();
      break;

    case 'toggle-guest-fav':
      state.guestFavoriteOnly = !state.guestFavoriteOnly;
      refreshResults();
      notify();
      break;

    case 'reset-filters':
      store.resetFilters();
      state.q = '';
      refreshResults();
      notify();
      break;

    case 'load-more':
      store.runSearch({ append: true });
      break;

    case 'go-page':
      store.runSearch({ page: Number(target.dataset.page) });
      window.scrollTo({ top: 0, behavior: 'smooth' });
      break;

    case 'set-language':
      store.applyLanguage(target.dataset.key);
      break;

    case 'set-currency':
      store.applyCurrency(target.dataset.key);
      break;

    case 'pick-date':
      pickDate(target.dataset.date);
      break;

    case 'cal-shift':
      shiftCalendar(Number(target.dataset.dir));
      break;

    case 'clear-dates':
      state.checkIn = todayIso(9);
      state.checkOut = todayIso(12);
      onDatesChanged();
      break;

    case 'scroll-to': {
      const el = document.getElementById(target.dataset.target);
      if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
      break;
    }

    case 'photo-open':
      state.photoIndex = Number(target.dataset.index);
      notify();
      break;

    case 'photo-step': {
      const total = state.detail?.card.images.length ?? 0;
      state.photoIndex = Math.min(total - 1, Math.max(0, (state.photoIndex ?? 0) + Number(target.dataset.dir)));
      notify();
      break;
    }

    case 'photo-grid':
      state.photoIndex = null;
      notify();
      break;

    case 'share':
      await share();
      break;

    case 'confirm-booking': {
      captureCheckoutFields();
      const card = document.getElementById('card-number')?.value?.replace(/\D/g, '') ?? '';
      await store.book({
        guestName: state.checkoutName || state.user?.fullName || null,
        guestEmail: state.checkoutEmail || state.user?.email || null,
        guestNote: state.checkoutNote || null,
        paymentMethod: state.payMethod ?? 'card',
        cardLast4: card.length >= 4 ? card.slice(-4) : null
      });
      break;
    }

    case 'preview-cancel':
    case 'cancel-booking':
      try {
        const id = Number(target.dataset.id);
        const preview = await api.refundPreview(id);
        state.cancelPreview = { ...preview, bookingId: id };
        state.overlay = 'cancel-trip';
        notify();
      } catch (err) { toast(err.message); }
      break;

    case 'confirm-cancel':
      try {
        const id = Number(target.dataset.id);
        const result = await api.cancelBooking(id);
        state.overlay = null;
        state.cancelPreview = null;
        toast(`Đã huỷ. Hoàn lại ${new Intl.NumberFormat('vi-VN').format(result.refund)}₫.`);
        await Promise.all([store.loadBookings(), state.route.name === 'trip' ? store.loadTrip(id) : Promise.resolve()]);
      } catch (err) { toast(err.message); }
      break;

    case 'print-receipt':
      window.print();
      break;

    /* ----------------------------------------------------------- checkout */

    case 'checkout-next':
      state.checkoutStep = Math.min(2, (state.checkoutStep ?? 0) + 1);
      captureCheckoutFields();
      notify();
      break;

    case 'checkout-back':
      captureCheckoutFields();
      state.checkoutStep = Math.max(0, (state.checkoutStep ?? 0) - 1);
      notify();
      break;

    case 'set-pay-method':
      state.payMethod = target.dataset.key;
      notify();
      break;

    /* ------------------------------------------------------------ account */

    case 'open-auth':
      e.preventDefault();
      state.authMode = target.dataset.mode ?? 'login';
      state.authError = null;
      state.overlay = 'login';
      state.menu = null;
      notify();
      break;

    case 'switch-auth':
      state.authMode = target.dataset.mode;
      state.authError = null;
      state.resetLink = null;
      notify();
      break;

    case 'use-reset-link':
      state.authMode = 'reset';
      state.authError = null;
      notify();
      break;

    case 'profile-tab':
      state.profileTab = target.dataset.key;
      if (state.profileTab === 'devices') {
        try { state.sessions = await api.sessions(); } catch (err) { toast(err.message); }
      }
      state.authError = null;
      notify();
      break;

    case 'revoke-session':
      try {
        await api.revokeSession(Number(target.dataset.id));
        state.sessions = await api.sessions();
        toast('Đã đăng xuất thiết bị đó.');
        notify();
      } catch (err) { toast(err.message); }
      break;

    case 'send-verification':
      try {
        const res = await api.sendVerification();
        if (res.verifyLink) {
          const token = new URLSearchParams(res.verifyLink.split('?')[1]).get('token');
          await api.verifyEmail(token);
          await store.loadMe();
          toast('Email đã được xác minh.');
        } else {
          toast(res.message);
        }
      } catch (err) { toast(err.message); }
      break;

    case 'fill-demo': {
      const form = document.querySelector('[data-act="submit-auth"]');
      if (form) {
        form.email.value = 'host1@stayhost.vn';
        form.password.value = 'stayhost123';
      }
      break;
    }

    case 'logout':
      await store.logout();
      if (['hosting', 'messages'].includes(state.route.name)) navigate('/');
      break;

    case 'become-host':
      try {
        await api.becomeHost();
        await store.loadMe();
        toast('Bạn đã sẵn sàng cho thuê nhà.');
        navigate('/hosting');
      } catch (err) { toast(err.message); }
      break;

    /* ------------------------------------------------------------ hosting */

    case 'host-tab':
      state.hostingTab = target.dataset.key;
      notify();
      break;

    case 'new-listing':
      if (!requireAuth()) break;
      state.editingListing = null;
      state.authError = null;
      state.overlay = 'listing-editor';
      notify();
      break;

    case 'edit-listing': {
      const id = Number(target.dataset.id);
      state.editingListing = state.hosting?.listings.find(l => l.id === id) ?? null;
      state.authError = null;
      state.overlay = 'listing-editor';
      notify();
      break;
    }

    case 'toggle-listing-flag': {
      const key = target.dataset.key;
      state.editingListing = { ...(state.editingListing ?? {}), [key]: !(state.editingListing?.[key] ?? key !== 'x') };
      captureListingForm();
      notify();
      break;
    }

    case 'set-listing-tier':
      captureListingForm();
      state.editingListing = { ...(state.editingListing ?? {}), cancellationTier: target.dataset.key };
      notify();
      break;

    case 'pick-images':
      document.getElementById('image-input')?.click();
      break;

    case 'remove-listing-image': {
      captureListingForm();
      const index = Number(target.dataset.index);
      const images = (state.editingListing?.images ?? []).filter((_, i) => i !== index);
      state.editingListing = { ...(state.editingListing ?? {}), images };
      notify();
      break;
    }

    case 'toggle-listing-amenity': {
      captureListingForm();
      const key = target.dataset.key;
      const current = state.editingListing?.amenityKeys ?? [];
      state.editingListing = {
        ...(state.editingListing ?? {}),
        amenityKeys: current.includes(key) ? current.filter(k => k !== key) : [...current, key]
      };
      notify();
      break;
    }

    case 'save-listing':
      await submitListing();
      break;

    case 'delete-listing':
      if (confirm('Xoá hẳn chỗ nghỉ này?')) {
        state.overlay = null;
        await store.removeListing(Number(target.dataset.id));
      }
      break;

    case 'open-host-calendar':
      await store.loadHostCalendar(Number(target.dataset.id));
      state.overlay = 'host-block';
      notify();
      break;

    case 'remove-block':
      try {
        await api.removeBlock(Number(target.dataset.id));
        await store.loadHostCalendar(state.hostCalendar.listingId);
        toast('Đã bỏ khoá lịch.');
      } catch (err) { toast(err.message); }
      break;

    case 'host-month':
      state.hostMonthOffset = Math.max(0, (state.hostMonthOffset ?? 0) + Number(target.dataset.dir));
      notify();
      break;

    case 'remove-price-rule':
      try {
        await api.removePriceRule(Number(target.dataset.id));
        await store.loadHostCalendar(state.hostCalendar.listingId);
        toast('Đã bỏ quy tắc giá.');
      } catch (err) { toast(err.message); }
      break;

    case 'open-guest-review': {
      state.guestReviewBooking = state.hosting?.bookings.find(b => b.id === Number(target.dataset.id)) ?? null;
      state.overlay = 'guest-review';
      notify();
      break;
    }

    case 'respond-booking':
      await store.respondBooking(Number(target.dataset.id), target.dataset.mode);
      break;

    /* ----------------------------------------------------------- messaging */

    case 'open-thread':
      await store.openThread(Number(target.dataset.id));
      scrollInboxToEnd();
      break;

    case 'message-host': {
      if (!requireAuth()) break;
      const listingId = Number(target.dataset.id);
      state.overlay = null;
      notify();
      try {
        const thread = await api.sendMessage({ listingId, body: target.dataset.body || 'Chào bạn, mình muốn hỏi thêm về chỗ nghỉ.' });
        state.activeThread = thread;
        await store.loadThreads();
        navigate('/messages');
      } catch (err) { toast(err.message); }
      break;
    }

    /* ------------------------------------------------------------- reviews */

    case 'open-review': {
      if (!requireAuth()) break;
      const id = Number(target.dataset.id);
      state.reviewBooking = state.bookings.find(b => b.id === id) ?? null;
      state.reviewDraft = { rating: 5, cleanliness: 5, accuracy: 5, checkIn: 5, communication: 5, location: 5, value: 5, text: '' };
      state.overlay = 'review';
      notify();
      break;
    }

    case 'set-guest-star':
      state.guestReviewDraft = { ...(state.guestReviewDraft ?? { wouldHostAgain: true }), rating: Number(target.dataset.value) };
      notify();
      break;

    case 'toggle-host-again':
      state.guestReviewDraft = {
        ...(state.guestReviewDraft ?? { rating: 5 }),
        wouldHostAgain: !(state.guestReviewDraft?.wouldHostAgain ?? true)
      };
      notify();
      break;

    case 'set-star': {
      const { field, value } = target.dataset;
      state.reviewDraft = { ...(state.reviewDraft ?? {}), [field]: Number(value) };
      captureReviewText();
      notify();
      break;
    }
  }
});

/* ------------------------------------------------------------- form helpers */

function requireAuth() {
  if (state.user) return true;
  state.authMode = 'login';
  state.authError = null;
  state.overlay = 'login';
  notify();
  return false;
}

/** Keep unsaved editor input when a pill toggle forces a re-render. */
function captureListingForm() {
  const form = document.getElementById('listing-form');
  if (!form) return;
  const data = readListingForm(form);
  state.editingListing = { ...(state.editingListing ?? {}), ...data };
}

function readListingForm(form) {
  const num = name => Number(form[name]?.value ?? 0);
  return {
    id: Number(form.dataset.id) || 0,
    title: form.title?.value ?? '',
    city: form.city?.value ?? '',
    typeKey: form.typeKey?.value ?? 'house',
    roomTypeKey: form.roomTypeKey?.value ?? 'entire',
    bedrooms: num('bedrooms'),
    beds: num('beds'),
    bathrooms: num('bathrooms'),
    maxGuests: num('maxGuests'),
    pricePerNight: num('pricePerNight'),
    cleaningFee: num('cleaningFee'),
    minNights: num('minNights') || 1,
    description: form.description?.value ?? '',
    highlight: form.highlight?.value ?? '',
    images: (form.images?.value ?? '').split('\n').map(s => s.trim()).filter(Boolean)
  };
}

async function submitListing() {
  const form = document.getElementById('listing-form');
  if (!form) return;

  const data = readListingForm(form);
  const merged = { ...(state.editingListing ?? {}), ...data };
  const body = {
    ...merged,
    instantBook: merged.instantBook ?? true,
    isPublished: merged.isPublished ?? true,
    amenityKeys: merged.amenityKeys ?? [],
    latitude: merged.latitude ?? null,
    longitude: merged.longitude ?? null
  };

  state.editingListing = merged;
  await store.saveListing({ id: data.id || null, body });
}

/** Checkout spans three renders, so the fields are mirrored into state each hop. */
function captureCheckoutFields() {
  const name = document.getElementById('guest-name');
  const email = document.getElementById('guest-email');
  const note = document.getElementById('guest-note');
  if (name) state.checkoutName = name.value.trim();
  if (email) state.checkoutEmail = email.value.trim();
  if (note) state.checkoutNote = note.value.trim();
}

async function uploadImages(fileList) {
  const files = Array.from(fileList ?? []);
  if (!files.length) return;

  captureListingForm();
  state.uploading = true;
  notify();

  try {
    const form = new FormData();
    files.forEach(f => form.append('files', f));

    const res = await fetch('/api/uploads/images', { method: 'POST', body: form, credentials: 'same-origin' });
    const payload = await res.json().catch(() => null);
    if (!res.ok) throw new Error(payload?.message ?? 'Tải ảnh thất bại.');

    const images = [...(state.editingListing?.images ?? []), ...payload.urls];
    state.editingListing = { ...(state.editingListing ?? {}), images };
    toast(`Đã tải lên ${payload.urls.length} ảnh.`);
  } catch (err) {
    toast(err.message);
  } finally {
    state.uploading = false;
    notify();
  }
}

function captureReviewText() {
  const field = document.querySelector('[data-act="submit-review"] textarea[name="text"]');
  if (field) state.reviewDraft = { ...(state.reviewDraft ?? {}), text: field.value };
}

function scrollInboxToEnd() {
  requestAnimationFrame(() => {
    const box = document.getElementById('inbox-messages');
    if (box) box.scrollTop = box.scrollHeight;
  });
}

async function share() {
  const url = location.href;
  const title = state.detail?.card.title ?? 'StayHost OS';
  if (navigator.share) {
    try { await navigator.share({ title, url }); return; } catch { /* user dismissed */ }
  }
  try {
    await navigator.clipboard.writeText(url);
    toast('Đã sao chép liên kết chỗ nghỉ');
  } catch {
    toast(url);
  }
}

document.addEventListener('input', e => {
  const target = e.target.closest('[data-act]');
  if (!target) return;

  switch (target.dataset.act) {
    case 'input-q':
      state.q = target.value;
      state.suggestOpen = true;
      debouncedSuggest();
      debouncedSearch();
      break;

    case 'review-search':
      state.reviewQuery = target.value;
      notify();
      break;

    case 'set-checkin':
      if (!target.value) return;
      state.checkIn = target.value;
      onDatesChanged();
      break;

    case 'set-checkout':
      if (!target.value) return;
      state.checkOut = target.value;
      onDatesChanged();
      break;

    case 'set-min-price':
      state.minPrice = Math.min(Number(target.value), state.maxPrice - 100000);
      target.value = state.minPrice;
      updatePriceSliderUi();
      break;

    case 'set-max-price':
      state.maxPrice = Math.max(Number(target.value), state.minPrice + 100000);
      target.value = state.maxPrice;
      updatePriceSliderUi();
      break;

    case 'host-nights':
      state.hostCalcNights = Number(target.value);
      updateHostCalc();
      break;

    case 'host-rate':
      state.hostCalcRate = Number(target.value);
      updateHostCalc();
      break;
  }
});

function updateHostCalc() {
  const nights = state.hostCalcNights ?? 20;
  const rate = state.hostCalcRate ?? 1_500_000;
  const out = document.querySelector('.calc-out');
  if (out) out.innerHTML = `${money(nights * rate * 0.91)}<span style="font-size:16px;color:var(--ink-muted);font-weight:600"> / tháng</span>`;
  const labels = document.querySelectorAll('.calc label b');
  if (labels[0]) labels[0].textContent = String(nights);
  if (labels[1]) labels[1].textContent = money(rate);
}

document.addEventListener('change', async e => {
  if (e.target.id === 'image-input') {
    await uploadImages(e.target.files);
    e.target.value = '';
    return;
  }

  const target = e.target.closest('[data-act]');
  if (!target) return;

  switch (target.dataset.act) {
    case 'set-sort':
      state.sort = target.value;
      refreshResults();
      break;

    case 'set-min-price':
    case 'set-max-price':
      refreshResults();
      break;

    case 'review-sort':
      state.reviewSort = target.value;
      notify();
      break;
  }
});

document.addEventListener('submit', e => {
  const target = e.target.closest('[data-act]');
  if (!target) return;
  e.preventDefault();

  if (target.dataset.act === 'submit-search') {
    const input = target.querySelector('#q');
    if (input) state.q = input.value;
    if (state.route.name !== 'browse') { navigate('/'); return; }
    refreshResults();
  }

  const act = target.dataset.act;

  if (act === 'submit-auth') {
    const body = {
      email: target.email.value.trim(),
      password: target.password.value,
      fullName: target.fullName?.value?.trim() ?? '',
      phone: target.phone?.value?.trim() || null
    };
    (state.authMode === 'register' ? store.register(body) : store.login(body))
      .then(ok => { if (ok) store.loadMe(); });
  }

  if (act === 'submit-forgot') {
    state.authBusy = true;
    state.authError = null;
    notify();
    api.forgotPassword(target.email.value.trim())
      .then(res => {
        state.resetLink = res.resetLink;
        state.resetToken = res.resetLink ? new URLSearchParams(res.resetLink.split('?')[1]).get('token') : null;
        if (!res.resetLink) state.authError = 'Không tìm thấy tài khoản với email này.';
      })
      .catch(err => { state.authError = err.message; })
      .finally(() => { state.authBusy = false; notify(); });
  }

  if (act === 'submit-reset') {
    if (target.newPassword.value !== target.confirmPassword.value) {
      state.authError = 'Hai mật khẩu không khớp.';
      notify();
      return;
    }
    state.authBusy = true;
    state.authError = null;
    notify();
    api.resetPassword({ token: state.resetToken, newPassword: target.newPassword.value })
      .then(user => {
        state.user = user;
        state.overlay = null;
        state.resetLink = null;
        state.resetToken = null;
        toast('Đã đổi mật khẩu và đăng nhập lại.');
      })
      .catch(err => { state.authError = err.message; })
      .finally(() => { state.authBusy = false; notify(); });
  }

  if (act === 'submit-change-password') {
    if (target.newPassword.value !== target.confirmPassword.value) {
      state.authError = 'Hai mật khẩu mới không khớp.';
      notify();
      return;
    }
    state.authError = null;
    api.changePassword({
      currentPassword: target.currentPassword.value,
      newPassword: target.newPassword.value
    })
      .then(() => { state.overlay = null; toast('Đã đổi mật khẩu.'); })
      .catch(err => { state.authError = err.message; })
      .finally(notify);
  }

  if (act === 'submit-profile') {
    store.saveProfile({
      fullName: target.fullName.value.trim(),
      phone: target.phone.value.trim() || null,
      bio: target.bio.value.trim() || null
    });
  }

  if (act === 'submit-listing') submitListing();

  if (act === 'submit-review') {
    const draft = state.reviewDraft ?? {};
    store.submitReview(Number(target.dataset.booking), {
      bookingId: Number(target.dataset.booking),
      rating: draft.rating ?? 5,
      text: target.text.value.trim(),
      cleanliness: draft.cleanliness ?? 5,
      accuracy: draft.accuracy ?? 5,
      checkIn: draft.checkIn ?? 5,
      communication: draft.communication ?? 5,
      location: draft.location ?? 5,
      value: draft.value ?? 5
    });
  }

  if (act === 'submit-block') {
    api.addBlock({
      listingId: Number(target.dataset.listing),
      from: target.from.value,
      to: target.to.value,
      note: target.note.value.trim() || null
    })
      .then(() => store.loadHostCalendar(Number(target.dataset.listing)))
      .then(() => toast('Đã khoá lịch.'))
      .catch(err => toast(err.message));
  }

  if (act === 'submit-price-rule') {
    api.addPriceRule({
      listingId: Number(target.dataset.listing),
      name: target.name.value.trim(),
      from: target.from.value,
      to: target.to.value,
      nightlyRate: Number(target.rate.value)
    })
      .then(() => store.loadHostCalendar(Number(target.dataset.listing)))
      .then(() => toast('Đã thêm quy tắc giá.'))
      .catch(err => toast(err.message));
  }

  if (act === 'submit-guest-review') {
    api.reviewGuest(Number(target.dataset.booking), {
      rating: state.guestReviewDraft?.rating ?? 5,
      text: target.text.value.trim(),
      wouldHostAgain: state.guestReviewDraft?.wouldHostAgain ?? true
    })
      .then(() => {
        state.overlay = null;
        state.guestReviewBooking = null;
        toast('Đã gửi đánh giá khách.');
        return store.loadHosting();
      })
      .catch(err => toast(err.message))
      .finally(notify);
  }

  if (act === 'submit-message') {
    const input = target.body;
    const body = input.value.trim();
    if (!body) return;
    input.value = '';
    store.sendMessage({ threadId: Number(target.dataset.thread), body }).then(scrollInboxToEnd);
  }
});

document.addEventListener('keydown', e => {
  if (e.key === 'Escape' && state.overlay) { state.overlay = null; state.photoIndex = null; notify(); }
  if (e.key === 'Escape' && state.menu) { state.menu = null; notify(); }
  if (e.key === 'Escape' && state.suggestOpen) { state.suggestOpen = false; notify(); }

  // Arrow keys drive the photo viewer while it is open.
  if (state.overlay === 'photos' && state.photoIndex != null) {
    const total = state.detail?.card.images.length ?? 0;
    if (e.key === 'ArrowRight') { state.photoIndex = Math.min(total - 1, state.photoIndex + 1); notify(); }
    if (e.key === 'ArrowLeft') { state.photoIndex = Math.max(0, state.photoIndex - 1); notify(); }
  }
});

/* -------------------------------------------------------------- bootstrap */

async function boot() {
  state.route = parseRoute();
  readSearchFromUrl();
  render();

  await store.loadMeta();
  if (state.metaError) {
    mainEl.innerHTML = `<div class="shell" style="padding:60px 0">
      <div class="empty-state">
        <h3>Không kết nối được máy chủ</h3>
        <p>${state.metaError}</p>
      </div></div>`;
    return;
  }

  // Re-read now that meta supplies the price bounds the URL is relative to.
  readSearchFromUrl();
  await store.loadMe();
  notify();
  await Promise.all([store.loadFavorites(), store.loadNotifications(), loadRoute()]);
}

boot();

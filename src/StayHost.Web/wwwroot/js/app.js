// Entry point: routing, rendering and one delegated event handler for the whole app.

import { $, debounce, toast, isoOf, parseIso, todayIso, money } from './util.js';
import * as store from './store.js';
import { state, set, notify } from './store.js';
import { api } from './api.js';

import { renderHeader } from './components/header.js';
import { renderFooter } from './components/footer.js';
import { renderOverlay } from './components/modals.js';
import { mountResultsMap, mountDetailMap, destroyMaps } from './components/map.js';

import { renderBrowse } from './views/browse.js';
import { renderDetail } from './views/detail.js';
import { renderWishlists } from './views/wishlists.js';
import { renderTrips } from './views/trips.js';
import { renderHostPage } from './views/host.js';
import { renderHosting } from './views/hosting.js';
import { renderMessages } from './views/messages.js';

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
    case 'host': return renderHostPage();
    case 'hosting': return renderHosting();
    case 'messages': return renderMessages();
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

function render() {
  if (rafPending) return;
  rafPending = true;

  requestAnimationFrame(() => {
    rafPending = false;

    const focus = snapshotFocus();
    const modalScroll = document.querySelector('.modal-body')?.scrollTop ?? 0;

    headerEl.innerHTML = renderHeader();
    mainEl.innerHTML = renderView();
    footerEl.innerHTML = renderFooter();
    overlayEl.innerHTML = renderOverlay();

    document.body.style.overflow = state.overlay ? 'hidden' : '';

    const modalBody = document.querySelector('.modal-body');
    if (modalBody && modalScroll) modalBody.scrollTop = modalScroll;

    restoreFocus(focus);

    if (state.route.name === 'browse' && !store.isDiscovery() && !state.hideMap) mountResultsMap();
    if (state.route.name === 'detail' && state.detail) mountDetailMap();
  });
}

store.subscribe(render);

/* ------------------------------------------------------------------ router */

function parseRoute(pathname = location.pathname) {
  const parts = pathname.replace(/^\/+|\/+$/g, '').split('/');
  if (parts[0] === 'rooms' && parts[1]) return { name: 'detail', param: decodeURIComponent(parts[1]) };
  if (parts[0] === 'wishlists') return { name: 'wishlists', param: null };
  if (parts[0] === 'trips') return { name: 'trips', param: null };
  if (parts[0] === 'host') return { name: 'host', param: null };
  if (parts[0] === 'hosting') return { name: 'hosting', param: null };
  if (parts[0] === 'messages') return { name: 'messages', param: parts[1] ?? null };
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
      await store.loadFavorites();
      break;
    case 'trips':
      await store.loadBookings();
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

/* ---------------------------------------------------------- carousel (DOM) */

function stepCarousel(id, dir) {
  const card = document.querySelector(`[data-listing="${id}"] .card-media`);
  if (!card) return;

  const imgs = Array.from(card.querySelectorAll('img'));
  const current = imgs.findIndex(i => i.classList.contains('is-current'));
  const next = Math.min(imgs.length - 1, Math.max(0, current + dir));
  if (next === current) return;

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

    case 'toggle-total':
      state.showTotalPrice = !state.showTotalPrice;
      notify();
      break;

    case 'open':
      e.preventDefault();
      state.overlay = target.dataset.overlay;
      state.menu = null;
      notify();
      break;

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

    case 'share':
      await share();
      break;

    case 'confirm-booking': {
      const name = document.getElementById('guest-name')?.value?.trim() || null;
      const email = document.getElementById('guest-email')?.value?.trim() || null;
      await store.book({ guestName: name, guestEmail: email });
      break;
    }

    case 'cancel-booking':
      try {
        await api.cancelBooking(Number(target.dataset.id));
        toast('Đã huỷ đặt chỗ.');
        await store.loadBookings();
      } catch (err) { toast(err.message); }
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
      notify();
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

    case 'respond-booking':
      await store.respondBooking(Number(target.dataset.id), target.dataset.mode);
      break;

    /* ----------------------------------------------------------- messaging */

    case 'open-thread':
      await store.openThread(Number(target.dataset.id));
      scrollInboxToEnd();
      break;

    case 'open-listing-by-id': {
      const id = Number(target.dataset.id);
      const slug = state.threads.find(t => t.listingId === id)?.listingId ?? id;
      navigate(`/rooms/${slug}`);
      break;
    }

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

document.addEventListener('change', e => {
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

  if (act === 'submit-message') {
    const input = target.body;
    const body = input.value.trim();
    if (!body) return;
    input.value = '';
    store.sendMessage({ threadId: Number(target.dataset.thread), body }).then(scrollInboxToEnd);
  }
});

document.addEventListener('keydown', e => {
  if (e.key === 'Escape' && state.overlay) { state.overlay = null; notify(); }
  if (e.key === 'Escape' && state.menu) { state.menu = null; notify(); }
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
  await Promise.all([store.loadFavorites(), loadRoute()]);
}

boot();

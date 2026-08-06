// One mutable state object plus the actions that touch it, exposed to React
// through useSyncExternalStore. Keeping the store outside React means the
// business logic stays plain JS and testable, while React handles the DOM.

import { api } from './api.js';
import { todayIso, setCurrency } from './format.js';

const listeners = new Set();
let version = 0;

export const state = {
  // search criteria
  q: '',
  checkIn: todayIso(9),
  checkOut: todayIso(12),
  guests: { adults: 2, children: 0, infants: 0, pets: 0 },
  category: 'all',
  minPrice: 0,
  maxPrice: 0,
  amenities: [],
  sort: 'reco',
  roomType: 'any',
  bedrooms: 0,
  beds: 0,
  bathrooms: 0,
  superhostOnly: false,
  guestFavoriteOnly: false,
  instantBookOnly: false,
  freeCancellationOnly: false,

  // reference data
  meta: null,
  metaError: null,
  currency: { code: 'VND', label: 'Việt Nam Đồng', symbol: '₫', rateFromVnd: 1 },
  language: { code: 'vi', label: 'Tiếng Việt', region: 'Việt Nam' },

  // catalogue
  home: null,
  homeLoading: true,
  results: { total: 0, items: [], page: 1, pageSize: 24 },
  loading: true,
  loadingMore: false,
  detail: null,
  detailLoading: false,
  quote: null,
  suggestions: [],

  // account
  user: null,
  authError: null,
  authBusy: false,
  sessions: [],

  // guest data
  favorites: [],
  favCount: 0,
  wishlists: [],
  activeWishlist: null,
  bookings: [],
  trip: null,
  tripLoading: false,
  bookingResult: null,
  bookingError: null,

  // hosting + ops
  hosting: null,
  hostingLoading: false,
  hostCalendar: null,
  threads: [],
  activeThread: null,
  notifications: { unread: 0, items: [] },
  admin: null,

  // ui
  hideMap: false,
  showTotalPrice: false
};

/* ------------------------------------------------------------ react bridge */

export function subscribe(fn) {
  listeners.add(fn);
  return () => listeners.delete(fn);
}

export const getSnapshot = () => version;

export function notify() {
  version++;
  listeners.forEach(fn => fn());
}

/** Applies a patch and notifies in one step. */
export function set(patch) {
  Object.assign(state, patch);
  notify();
}

/* ----------------------------------------------------------------- helpers */

export const totalGuests = () => state.guests.adults + state.guests.children;

export function guestLabel() {
  const g = state.guests;
  const parts = [`${g.adults + g.children} khách`];
  if (g.infants) parts.push(`${g.infants} em bé`);
  if (g.pets) parts.push(`${g.pets} thú cưng`);
  return parts.join(', ');
}

/**
 * True while nothing is narrowed down. Airbnb shows curated carousels in that
 * state and switches to the flat result grid once you search.
 */
export function isDiscovery() {
  return !state.q.trim()
    && state.category === 'all'
    && state.amenities.length === 0
    && state.roomType === 'any'
    && !state.bedrooms && !state.beds && !state.bathrooms
    && !state.superhostOnly && !state.guestFavoriteOnly
    && !state.instantBookOnly && !state.freeCancellationOnly
    && (!state.meta || (state.minPrice <= state.meta.minPrice && state.maxPrice >= state.meta.maxPrice));
}

export function activeFilterCount() {
  const meta = state.meta;
  let n = state.amenities.length;
  if (state.category !== 'all') n++;
  if (meta && state.maxPrice < meta.maxPrice) n++;
  if (meta && state.minPrice > meta.minPrice) n++;
  if (state.roomType !== 'any') n++;
  if (state.bedrooms) n++;
  if (state.beds) n++;
  if (state.bathrooms) n++;
  if (state.superhostOnly) n++;
  if (state.guestFavoriteOnly) n++;
  if (state.instantBookOnly) n++;
  if (state.freeCancellationOnly) n++;
  return n;
}

export function resetFilters() {
  const meta = state.meta;
  Object.assign(state, {
    category: 'all',
    amenities: [],
    roomType: 'any',
    bedrooms: 0,
    beds: 0,
    bathrooms: 0,
    superhostOnly: false,
    guestFavoriteOnly: false,
    instantBookOnly: false,
    freeCancellationOnly: false,
    minPrice: meta ? meta.minPrice : 0,
    maxPrice: meta ? meta.maxPrice : 0
  });
}

/* ------------------------------------------------------------------ toasts */

let toastId = 0;
export const toasts = { items: [] };

export function toast(message) {
  const id = ++toastId;
  toasts.items = [...toasts.items, { id, message }];
  notify();
  setTimeout(() => {
    toasts.items = toasts.items.filter(t => t.id !== id);
    notify();
  }, 2800);
}

/* --------------------------------------------------------------- bootstrap */

export async function loadMeta() {
  try {
    const meta = await api.meta();
    state.meta = meta;
    if (!state.maxPrice) state.maxPrice = meta.maxPrice;
    if (!state.minPrice) state.minPrice = meta.minPrice;

    const savedCurrency = meta.currencies.find(c => c.code === localStorage.getItem('sh_currency'));
    if (savedCurrency) { state.currency = savedCurrency; setCurrency(savedCurrency); }

    const savedLang = meta.languages.find(l => l.code === localStorage.getItem('sh_language'));
    if (savedLang) state.language = savedLang;
  } catch (err) {
    state.metaError = err.message;
  }
  notify();
}

/* ------------------------------------------------------------------ search */

export function searchParams(page = 1) {
  const meta = state.meta;
  return {
    q: state.q.trim() || undefined,
    category: state.category !== 'all' ? state.category : undefined,
    minPrice: meta && state.minPrice > meta.minPrice ? state.minPrice : undefined,
    maxPrice: meta && state.maxPrice < meta.maxPrice ? state.maxPrice : undefined,
    guests: totalGuests(),
    amenities: state.amenities.length ? state.amenities : undefined,
    sort: state.sort,
    roomType: state.roomType !== 'any' ? state.roomType : undefined,
    bedrooms: state.bedrooms || undefined,
    beds: state.beds || undefined,
    bathrooms: state.bathrooms || undefined,
    superhost: state.superhostOnly || undefined,
    guestFavorite: state.guestFavoriteOnly || undefined,
    instantBook: state.instantBookOnly || undefined,
    freeCancellation: state.freeCancellationOnly || undefined,
    page,
    pageSize: 24
  };
}

let searchToken = 0;

export async function runSearch({ page = 1 } = {}) {
  const token = ++searchToken;
  state.loading = true;
  notify();

  try {
    const data = await api.search(searchParams(page));
    if (token !== searchToken) return;
    state.results = data;
  } catch (err) {
    if (token !== searchToken) return;
    state.results = { total: 0, items: [], page: 1, pageSize: 24 };
    toast(err.message);
  } finally {
    if (token === searchToken) {
      state.loading = false;
      notify();
    }
  }
}

export async function loadHome() {
  if (state.home) { notify(); return; }
  state.homeLoading = true;
  notify();
  try {
    state.home = await api.home();
  } catch (err) {
    toast(err.message);
  } finally {
    state.homeLoading = false;
    notify();
  }
}

export async function loadSuggestions() {
  try {
    state.suggestions = await api.suggest(state.q);
    notify();
  } catch { /* suggestions never block typing */ }
}

/* ----------------------------------------------------------------- account */

export async function loadMe() {
  try {
    state.user = await api.me();
  } catch {
    state.user = null;
  }
  notify();
}

async function runAuth(fn) {
  state.authBusy = true;
  state.authError = null;
  notify();
  try {
    state.user = await fn();
    toast(`Xin chào ${state.user.fullName}!`);
    await Promise.all([loadFavorites(), loadBookings(), loadNotifications()]);
    return true;
  } catch (err) {
    state.authError = err.message;
    return false;
  } finally {
    state.authBusy = false;
    notify();
  }
}

export const login = body => runAuth(() => api.login(body));
export const register = body => runAuth(() => api.register(body));

export async function logout() {
  try { await api.logout(); } catch { /* the cookie goes either way */ }
  Object.assign(state, {
    user: null, hosting: null, threads: [], favorites: [], favCount: 0,
    bookings: [], admin: null, wishlists: [], notifications: { unread: 0, items: [] }
  });
  toast('Đã đăng xuất.');
  notify();
}

export async function saveProfile(body) {
  try {
    state.user = await api.updateProfile(body);
    toast('Đã lưu hồ sơ.');
    return true;
  } catch (err) {
    toast(err.message);
    return false;
  } finally {
    notify();
  }
}

export async function loadSessions() {
  try {
    state.sessions = await api.sessions();
  } catch (err) { toast(err.message); }
  notify();
}

/* --------------------------------------------------------------- favorites */

export async function loadFavorites() {
  try {
    const favorites = await api.favorites();
    state.favorites = favorites;
    state.favCount = favorites.length;
  } catch { /* wishlist is non-critical on first paint */ }
  notify();
}

export async function toggleFavorite(id) {
  const flip = card => card.id === id ? { ...card, isFavorite: !card.isFavorite } : card;
  const flipAll = () => {
    state.results = { ...state.results, items: state.results.items.map(flip) };
    if (state.home) {
      state.home = { ...state.home, sections: state.home.sections.map(s => ({ ...s, items: s.items.map(flip) })) };
    }
    if (state.detail?.card.id === id) {
      state.detail = { ...state.detail, card: flip(state.detail.card) };
    }
    if (state.activeWishlist) {
      state.activeWishlist = { ...state.activeWishlist, items: state.activeWishlist.items.map(flip) };
    }
    notify();
  };

  flipAll();

  try {
    const res = await api.toggleFavorite(id);
    state.favCount = res.count;
    toast(res.isFavorite ? 'Đã lưu vào danh sách yêu thích' : 'Đã bỏ khỏi danh sách yêu thích');
    notify();
  } catch (err) {
    flipAll();
    toast(err.message);
  }
}

export async function loadWishlists() {
  try {
    state.wishlists = await api.wishlists();
  } catch (err) { toast(err.message); }
  notify();
}

export async function openWishlist(id) {
  try {
    state.activeWishlist = await api.wishlist(id);
  } catch (err) { toast(err.message); }
  notify();
}

/* ------------------------------------------------------------------ detail */

export async function loadDetail(idOrSlug) {
  state.detailLoading = true;
  state.detail = null;
  state.bookingResult = null;
  state.bookingError = null;
  notify();

  try {
    state.detail = await api.listing(idOrSlug);
    await refreshQuote();
  } catch (err) {
    state.detail = null;
    toast(err.message);
  } finally {
    state.detailLoading = false;
    notify();
  }
}

export async function refreshQuote() {
  if (!state.detail) return;
  try {
    state.quote = await api.quote({
      listingId: state.detail.card.id,
      checkIn: state.checkIn,
      checkOut: state.checkOut,
      guests: totalGuests()
    });
  } catch {
    state.quote = null;
  }
  notify();
}

export async function book(extra = {}) {
  if (!state.detail) return null;
  state.bookingError = null;
  notify();

  try {
    state.bookingResult = await api.book({
      listingId: state.detail.card.id,
      checkIn: state.checkIn,
      checkOut: state.checkOut,
      guests: totalGuests(),
      ...extra
    });
    toast(`Đặt chỗ thành công — mã ${state.bookingResult.reference}`);
    await loadBookings();
    return state.bookingResult;
  } catch (err) {
    state.bookingError = err.message;
    return null;
  } finally {
    notify();
  }
}

/* ------------------------------------------------------------------- trips */

export async function loadBookings() {
  try {
    state.bookings = await api.bookings();
  } catch { /* trips need an account; silence is fine when anonymous */ }
  notify();
}

export async function loadTrip(id) {
  state.tripLoading = true;
  state.trip = null;
  notify();
  try {
    state.trip = await api.booking(id);
  } catch (err) {
    toast(err.message);
  } finally {
    state.tripLoading = false;
    notify();
  }
}

export async function submitReview(bookingId, body) {
  try {
    await api.review(bookingId, body);
    toast('Cảm ơn bạn đã đánh giá!');
    await loadBookings();
    if (state.trip?.id === bookingId) await loadTrip(bookingId);
    return true;
  } catch (err) {
    toast(err.message);
    return false;
  }
}

/* ----------------------------------------------------------------- hosting */

export async function loadHosting() {
  if (!state.user) return;
  state.hostingLoading = true;
  notify();
  try {
    state.hosting = await api.hostDashboard();
  } catch (err) {
    toast(err.message);
  } finally {
    state.hostingLoading = false;
    notify();
  }
}

export async function saveListing(id, body) {
  try {
    const saved = id ? await api.updateListing(id, body) : await api.createListing(body);
    toast(id ? 'Đã cập nhật chỗ nghỉ.' : 'Đã đăng chỗ nghỉ mới.');
    await Promise.all([loadHosting(), loadMe()]);
    return saved;
  } catch (err) {
    toast(err.message);
    return null;
  }
}

export async function removeListing(id) {
  try {
    await api.deleteListing(id);
    toast('Đã xoá chỗ nghỉ.');
    await loadHosting();
  } catch (err) { toast(err.message); }
}

export async function loadHostCalendar(listingId) {
  try {
    state.hostCalendar = { listingId, ...(await api.hostCalendar(listingId)) };
  } catch (err) {
    toast(err.message);
    state.hostCalendar = null;
  }
  notify();
}

export async function respondBooking(id, action) {
  try {
    await api.respondBooking(id, action);
    toast(action === 'confirm' ? 'Đã xác nhận đặt chỗ.' : 'Đã từ chối đặt chỗ.');
    await loadHosting();
  } catch (err) { toast(err.message); }
}

/* --------------------------------------------------------------- messaging */

export async function loadThreads() {
  if (!state.user) return;
  try {
    state.threads = await api.threads();
  } catch (err) { toast(err.message); }
  notify();
}

export async function openThread(id) {
  try {
    state.activeThread = await api.thread(id);
    await loadThreads();
  } catch (err) { toast(err.message); }
  notify();
}

export async function sendMessage(body) {
  try {
    state.activeThread = await api.sendMessage(body);
    await loadThreads();
    return state.activeThread;
  } catch (err) {
    toast(err.message);
    return null;
  } finally {
    notify();
  }
}

/* ----------------------------------------------------------- notifications */

export async function loadNotifications() {
  if (!state.user) return;
  try {
    state.notifications = await api.notifications();
  } catch { /* the bell is optional chrome */ }
  notify();
}

/* ------------------------------------------------------------------- admin */

export async function loadAdmin() {
  try {
    state.admin = await api.adminOverview();
  } catch (err) {
    toast(err.message);
    state.admin = null;
  }
  notify();
}

/* ------------------------------------------------------------ preferences */

export function applyCurrency(code) {
  const c = state.meta?.currencies.find(x => x.code === code);
  if (!c) return;
  state.currency = c;
  setCurrency(c);
  localStorage.setItem('sh_currency', code);
  notify();
}

export function applyLanguage(code) {
  const l = state.meta?.languages.find(x => x.code === code);
  if (!l) return;
  state.language = l;
  localStorage.setItem('sh_language', code);
  notify();
}

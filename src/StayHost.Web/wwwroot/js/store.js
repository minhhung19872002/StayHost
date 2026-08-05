// Single mutable state object plus the actions that touch it. Any action that
// changes something visible calls `notify()`, which app.js turns into a re-render.

import { api } from './api.js';
import { todayIso, setCurrency, toast } from './util.js';

const listeners = new Set();

export const state = {
  route: { name: 'browse', param: null },

  meta: null,
  metaError: null,

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

  // ui
  tab: 'homes',
  showInlineFilters: false,
  /// The results page shows the map beside the list by default, like airbnb.com.
  hideMap: false,
  instantBookOnly: false,
  freeCancellationOnly: false,
  showTotalPrice: false,
  overlay: null,
  menu: null,
  loading: true,
  loadingMore: false,

  // account
  user: null,
  authError: null,
  authBusy: false,
  authMode: 'login',

  // hosting + messaging
  hosting: null,
  hostingLoading: false,
  hostingTab: 'overview',
  editingListing: null,
  hostCalendar: null,
  threads: [],
  activeThread: null,
  reviewBooking: null,

  // data
  home: null,
  homeLoading: true,
  results: { total: 0, items: [], page: 1, pageSize: 24 },
  favorites: [],
  favCount: 0,
  bookings: [],
  detail: null,
  detailLoading: false,
  quote: null,
  bookingResult: null,
  bookingError: null,

  carousel: {},
  currency: { code: 'VND', label: 'Việt Nam Đồng', symbol: '₫', rateFromVnd: 1 },
  language: { code: 'vi', label: 'Tiếng Việt', region: 'Việt Nam' },
  hoverListingId: null
};

export function subscribe(fn) { listeners.add(fn); return () => listeners.delete(fn); }
export function notify() { listeners.forEach(fn => fn(state)); }

export function set(patch) { Object.assign(state, patch); notify(); }

export const totalGuests = () => state.guests.adults + state.guests.children;

/**
 * True while the user has not narrowed anything down. Airbnb shows curated
 * carousels in that state and switches to the flat result grid once you search.
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

export function guestLabel() {
  const g = state.guests;
  const parts = [`${g.adults + g.children} khách`];
  if (g.infants) parts.push(`${g.infants} em bé`);
  if (g.pets) parts.push(`${g.pets} thú cưng`);
  return parts.join(', ');
}

/* --------------------------------------------------------------- bootstrap */

export async function loadMeta() {
  try {
    const meta = await api.meta();
    state.meta = meta;
    if (!state.maxPrice) state.maxPrice = meta.maxPrice;
    if (!state.minPrice) state.minPrice = meta.minPrice;

    const saved = localStorage.getItem('sh_currency');
    const found = meta.currencies.find(c => c.code === saved);
    if (found) { state.currency = found; setCurrency(found); }

    const savedLang = localStorage.getItem('sh_language');
    const lang = meta.languages.find(l => l.code === savedLang);
    if (lang) state.language = lang;
  } catch (err) {
    state.metaError = err.message;
  }
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

export async function loadHome() {
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

let searchToken = 0;

export async function runSearch({ append = false, page = 1 } = {}) {
  const token = ++searchToken;
  if (append) state.loadingMore = true; else state.loading = true;
  notify();

  try {
    const target = append ? state.results.page + 1 : page;
    const data = await api.search(searchParams(target));
    if (token !== searchToken) return;

    state.results = append
      ? { ...data, items: [...state.results.items, ...data.items] }
      : data;
  } catch (err) {
    if (token !== searchToken) return;
    state.results = { total: 0, items: [], page: 1, pageSize: 24 };
    toast(err.message);
  } finally {
    if (token === searchToken) {
      state.loading = false;
      state.loadingMore = false;
      notify();
    }
  }
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
    state.overlay = null;
    toast(`Xin chào ${state.user.fullName}!`);
    // Wishlist and trips move to the account on sign-in.
    await Promise.all([loadFavorites(), loadBookings()]);
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
  try {
    await api.logout();
  } catch { /* the cookie is dropped either way */ }
  state.user = null;
  state.hosting = null;
  state.threads = [];
  state.favorites = [];
  state.favCount = 0;
  state.bookings = [];
  state.menu = null;
  toast('Đã đăng xuất.');
  notify();
}

export async function saveProfile(body) {
  try {
    state.user = await api.updateProfile(body);
    toast('Đã lưu hồ sơ.');
    state.overlay = null;
  } catch (err) {
    toast(err.message);
  }
  notify();
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

export async function saveListing(payload) {
  try {
    const saved = payload.id
      ? await api.updateListing(payload.id, payload.body)
      : await api.createListing(payload.body);

    toast(payload.id ? 'Đã cập nhật chỗ nghỉ.' : 'Đã đăng chỗ nghỉ mới.');
    state.editingListing = null;
    await Promise.all([loadHosting(), loadMe()]);
    return saved;
  } catch (err) {
    state.authError = err.message;
    toast(err.message);
    notify();
    return null;
  }
}

export async function removeListing(id) {
  try {
    await api.deleteListing(id);
    toast('Đã xoá chỗ nghỉ.');
    await loadHosting();
  } catch (err) {
    toast(err.message);
  }
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

export async function respondBooking(id, action, reason) {
  try {
    await api.respondBooking(id, action, reason);
    toast(action === 'confirm' ? 'Đã xác nhận đặt chỗ.' : 'Đã từ chối đặt chỗ.');
    await loadHosting();
  } catch (err) {
    toast(err.message);
  }
}

/* --------------------------------------------------------------- messaging */

export async function loadThreads() {
  if (!state.user) return;
  try {
    state.threads = await api.threads();
  } catch (err) {
    toast(err.message);
  }
  notify();
}

export async function openThread(id) {
  try {
    state.activeThread = await api.thread(id);
    await loadThreads();
  } catch (err) {
    toast(err.message);
  }
  notify();
}

export async function sendMessage(body) {
  try {
    state.activeThread = await api.sendMessage(body);
    await loadThreads();
  } catch (err) {
    toast(err.message);
  }
  notify();
}

/* ----------------------------------------------------------------- reviews */

export async function submitReview(bookingId, body) {
  try {
    await api.review(bookingId, body);
    toast('Cảm ơn bạn đã đánh giá!');
    state.overlay = null;
    state.reviewBooking = null;
    await loadBookings();
  } catch (err) {
    toast(err.message);
  }
  notify();
}

/* --------------------------------------------------------------- favorites */

export async function loadFavorites() {
  try {
    const favorites = await api.favorites();
    state.favorites = favorites;
    state.favCount = favorites.length;
    notify();
  } catch { /* wishlist is non-critical on first paint */ }
}

export async function toggleFavorite(id) {
  // Optimistic: the heart should flip the instant it is clicked.
  const flip = card => card.id === id ? { ...card, isFavorite: !card.isFavorite } : card;
  const flipAll = () => {
    state.results.items = state.results.items.map(flip);
    if (state.home) {
      state.home = {
        ...state.home,
        sections: state.home.sections.map(s => ({ ...s, items: s.items.map(flip) }))
      };
    }
  };
  flipAll();
  if (state.detail?.card.id === id) {
    state.detail = { ...state.detail, card: { ...state.detail.card, isFavorite: !state.detail.card.isFavorite } };
  }
  notify();

  try {
    const res = await api.toggleFavorite(id);
    state.favCount = res.count;
    toast(res.isFavorite ? 'Đã lưu vào danh sách yêu thích' : 'Đã bỏ khỏi danh sách yêu thích');
    if (state.route.name === 'wishlists') await loadFavorites();
    else notify();
  } catch (err) {
    flipAll();
    toast(err.message);
    notify();
  }
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
  if (!state.detail) return;
  state.bookingError = null;
  notify();

  try {
    state.bookingResult = await api.book({
      listingId: state.detail.card.id,
      checkIn: state.checkIn,
      checkOut: state.checkOut,
      guests: totalGuests(),
      guestName: extra.guestName ?? null,
      guestEmail: extra.guestEmail ?? null,
      guestNote: extra.guestNote ?? null,
      paymentMethod: extra.paymentMethod ?? 'card',
      cardLast4: extra.cardLast4 ?? null
    });
    state.overlay = null;
    state.checkoutStep = 0;
    state.checkoutNote = '';
    toast('Đặt chỗ thành công — mã ' + state.bookingResult.reference);
    await loadBookings();
    // Land the guest on the confirmation so they see the receipt straight away.
    history.pushState({}, '', `/trips/${state.bookingResult.id}`);
    state.route = { name: 'trip', param: String(state.bookingResult.id) };
    state.trip = state.bookingResult;
    window.scrollTo({ top: 0, behavior: 'instant' });
  } catch (err) {
    state.bookingError = err.message;
  } finally {
    notify();
  }
}

/* ------------------------------------------------------------------- trips */

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

export async function loadBookings() {
  try {
    state.bookings = await api.bookings();
  } catch (err) {
    toast(err.message);
  } finally {
    notify();
  }
}

/* -------------------------------------------------------------- preferences */

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

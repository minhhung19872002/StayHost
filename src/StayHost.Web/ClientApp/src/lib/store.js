// One mutable state object plus the actions that touch it, exposed to React
// through useSyncExternalStore. Keeping the store outside React means the
// business logic stays plain JS and testable, while React handles the DOM.

import { api } from './api.js';
import { todayIso, isoOf, parseIso, setCurrency } from './format.js';

const listeners = new Set();
let version = 0;

export const state = {
  // search criteria
  q: '',
  checkIn: todayIso(9),
  checkOut: todayIso(12),
  // docs/01 TM-06 & TM-07 — 'exact' | 'weekend' | 'week' | 'month' | 'months'
  stay: 'exact',
  flexDays: 0,
  stayMonths: 1,
  startMonths: [],
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
  /** The booking currently holding dates while the guest is at checkout. */
  held: null,

  // hosting + ops
  hosting: null,
  hostingLoading: false,
  hostCalendar: null,
  threads: [],
  activeThread: null,
  notifications: { unread: 0, items: [] },
  admin: null,

  // ui — chrome that React renders but that lives outside component state so
  // any handler can flip it without prop-drilling.
  hideMap: false,
  showTotalPrice: false,
  /** docs/01 TM-12 — re-run the search whenever the map is moved. */
  searchOnMapMove: false,
  /** The map rectangle the current results were searched in, if any. */
  searchArea: null,
  tab: 'homes',
  menu: null,            // 'account' | 'bell' | null
  overlay: null,         // key into the overlay registry
  suggestOpen: false,
  inspirationTab: null,
  photoIndex: null,
  awaitingCheckout: false,

  // Which month the calendar is looking at. Its own state, because paging must
  // not touch the chosen dates: a check-out three months out is reached by
  // paging there, and rewriting the dates on the way makes that impossible.
  // Null means "wherever the check-in is".
  calendarMonth: null,

  // auth / profile modals
  authMode: 'login',     // login | register | forgot | reset
  profileTab: 'profile',
  resetLink: null,
  resetToken: null,
  // docs/01 TK-04 / TK-05 — the language picker, and somebody else's profile.
  spokenLanguages: [],
  publicProfile: null,
  publicProfileLoading: false,

  // checkout
  checkoutStep: 0,
  payMethod: 'card',
  // docs/01 ĐP-06 — take a deposit now instead of the whole amount.
  payDeposit: false,
  // docs/06 — the booking a StayShield case is being opened for.
  shieldBooking: null,
  shieldSide: 'guest',
  // Spend the guest's balance on this booking.
  useCredit: false,
  // docs/01 MR-09 — the room type chosen on a hotel listing.
  roomTypeId: null,
  // docs/01 ĐP-07 — let other people pay their share instead of paying it all.
  splitBill: false,
  splitEmails: '',
  checkoutName: '',
  checkoutEmail: '',
  checkoutNote: '',
  cancelPreview: null,

  // hosting
  hostingTab: 'today',
  editingListing: null,
  uploading: false,
  hostMonthOffset: 0,
  hostCalcNights: 20,
  hostCalcRate: 1_500_000,
  guestReviewBooking: null,
  guestReviewDraft: { rating: 5, wouldHostAgain: true },

  // reviews
  reviewBooking: null,
  reviewDraft: null,
  reviewQuery: '',
  reviewSort: 'recent'
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

/**
 * Sharing a listing, from the page header and from the photo viewer. Both offer
 * it, so both had better do the same thing.
 */
export async function shareListing(card) {
  const url = location.href;

  // The share sheet is the better offer where there is one; a dismissal is not
  // a failure, so it falls through to the clipboard either way.
  if (navigator.share) {
    try { await navigator.share({ title: card.title, url }); return; } catch { /* dismissed */ }
  }

  try { await navigator.clipboard.writeText(url); toast('Đã sao chép liên kết chỗ nghỉ'); }
  catch { toast(url); }
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
  const area = state.searchArea;
  return {
    ...(area ?? {}),
    // Dates go to the server so it can price every card with the same engine
    // checkout uses (docs/00 §6.8).
    checkIn: state.checkIn,
    checkOut: state.checkOut,
    // A loose wish instead of two firm dates (docs/01 TM-06, TM-07).
    stay: state.stay !== 'exact' ? state.stay : undefined,
    flex: state.flexDays || undefined,
    months: state.stay === 'months' ? state.stayMonths : undefined,
    startMonths: state.stay === 'months' && state.startMonths.length
      ? state.startMonths.join(',') : undefined,
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

/** Cached per date range, since the rails carry a priced total for those nights. */
let homeKey = '';

export async function loadHome() {
  const key = `${state.checkIn}|${state.checkOut}|${totalGuests()}`;
  if (state.home && key === homeKey) { notify(); return; }

  state.homeLoading = true;
  notify();
  try {
    state.home = await api.home({
      checkIn: state.checkIn,
      checkOut: state.checkOut,
      guests: totalGuests()
    });
    homeKey = key;
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

/** docs/01 TK-04 — fetched once; the list only changes when the server does. */
export async function loadSpokenLanguages() {
  if (state.spokenLanguages.length) return;
  try {
    state.spokenLanguages = await api.profileOptions();
  } catch { /* the editor falls back to whatever the profile already holds */ }
  notify();
}

/** docs/01 TK-05 — somebody else's public profile. */
export async function loadPublicProfile(id) {
  state.publicProfileLoading = true;
  state.publicProfile = null;
  notify();
  try {
    state.publicProfile = await api.publicProfile(id);
  } catch (err) {
    toast(err.message);
  } finally {
    state.publicProfileLoading = false;
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
    state.detail = await api.listing(idOrSlug, {
      checkIn: state.checkIn,
      checkOut: state.checkOut,
      guests: totalGuests()
    });
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
      adults: state.guests.adults,
      children: state.guests.children,
      infants: state.guests.infants,
      pets: state.guests.pets,
      roomTypeId: state.roomTypeId
    });
  } catch {
    state.quote = null;
  }
  notify();
}

/** docs/01 MR-09 — picking a room re-prices the panel against that room. */
export function setRoom(roomTypeId) {
  state.roomTypeId = roomTypeId;
  notify();
  refreshQuote();
}

/**
 * docs/01 ĐP-02 — entering checkout takes the dates off the market for 15
 * minutes. Nothing is charged here; `pay` does that.
 */
export async function holdDates(extra = {}) {
  if (!state.detail) return null;
  set({ bookingError: null });

  try {
    const held = await api.hold({
      listingId: state.detail.card.id,
      checkIn: state.checkIn,
      checkOut: state.checkOut,
      guests: totalGuests(),
      adults: state.guests.adults,
      children: state.guests.children,
      infants: state.guests.infants,
      pets: state.guests.pets,
      // docs/01 MR-09 — a hotel booking carries the room the guest picked.
      roomTypeId: state.roomTypeId,
      useCredit: state.useCredit,
      ...extra
    });
    set({ held });
    return held;
  } catch (err) {
    set({ bookingError: err.message });
    return null;
  }
}

/** Charges a held booking. The server re-prices and refuses on a mismatch. */
export async function payHeld(extra = {}) {
  if (!state.held) return null;
  set({ bookingError: null });

  try {
    state.bookingResult = await api.pay(state.held.id, extra);
    state.held = null;
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

/**
 * docs/01 ĐP-07 — turns the held booking into a split. Nobody is charged here;
 * everyone gets a link, and the dates are held for a day rather than 15 minutes.
 */
export async function openSplit(emails) {
  if (!state.held) return null;
  set({ bookingError: null });

  try {
    const split = await api.openSplit(state.held.id, emails);
    state.split = split;
    state.held = null;
    state.splitBill = false;
    state.splitEmails = '';
    toast(`Đã gửi liên kết cho ${split.shares.length - 1} người. Đơn giữ chỗ trong 24 giờ.`);
    await loadBookings();
    return split;
  } catch (err) {
    state.bookingError = err.message;
    toast(err.message);
    return null;
  } finally {
    notify();
  }
}

/** docs/01 ĐP-06 — the guest settles the rest before its date comes round. */
export async function payBalance(bookingId) {
  try {
    const updated = await api.payBalance(bookingId);
    state.trip = updated;
    toast('Đã thanh toán phần còn lại.');
    await loadBookings();
    return updated;
  } catch (err) {
    toast(err.message);
    return null;
  } finally {
    notify();
  }
}

/** Leaving checkout without paying puts the dates straight back on sale. */
export async function releaseHold() {
  const held = state.held;
  if (!held) return;
  set({ held: null });
  try { await api.release(held.id); } catch { /* the sweep will expire it anyway */ }
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
    // docs/03 §7 — the server says whether it went public straight away or is
    // waiting on the host, and that is what the guest needs to hear.
    const result = await api.review(bookingId, body);
    toast(result?.message ?? 'Cảm ơn bạn đã đánh giá!');
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

/* ------------------------------------------------------------- ui actions */

export const openOverlay = kind =>
  // A picker opened afresh should be looking at the dates it is showing, not at
  // wherever it was last paged to.
  set({ overlay: kind, menu: null, ...(kind === 'dates' ? { calendarMonth: null } : {}) });
export const closeOverlay = () => set({ overlay: null, photoIndex: null });
export const openMenu = kind => set({ menu: state.menu === kind ? null : kind });

/** Signed-out guests get the login modal instead of the action they asked for. */
export function requireAuth() {
  if (state.user) return true;
  set({ authMode: 'login', authError: null, overlay: 'login', menu: null });
  return false;
}

/* ------------------------------------------------------- dates and guests */

const blockedNights = () => new Set(state.detail?.unavailableDates ?? []);

function rangeHasBlockedNight(fromIso, toIso) {
  const blocked = blockedNights();
  if (!blocked.size) return false;
  for (const d = parseIso(fromIso); isoOf(d) < toIso; d.setDate(d.getDate() + 1)) {
    if (blocked.has(isoOf(d))) return true;
  }
  return false;
}

/**
 * Two-click range picking: the first click sets check-in with a one-night
 * default, the second closes the range, any later click starts over.
 */
export function pickDate(iso) {
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
    Object.assign(state, previous, { awaitingCheckout: false });
    toast('Khoảng ngày này đã có người đặt. Chọn ngày khác nhé.');
    notify();
    return;
  }

  normaliseDates();
}

/** The first of whichever month the calendar is showing on its left panel. */
export function calendarAnchor() {
  const from = parseIso(state.calendarMonth ?? state.checkIn);
  return new Date(from.getFullYear(), from.getMonth(), 1, 12);
}

/**
 * Pages the calendar. It moves the view and nothing else — this used to move the
 * check-in date with it, so paging forward to find a check-out three months away
 * silently rewrote the dates you had just chosen.
 */
export function shiftCalendar(dir) {
  const d = calendarAnchor();
  d.setMonth(d.getMonth() + dir);

  // Nobody can book the past, so there is nothing to see behind this month.
  const floor = parseIso(todayIso());
  if (d < new Date(floor.getFullYear(), floor.getMonth(), 1, 12)) return;

  state.calendarMonth = isoOf(d);
  notify();
}

/** Puts the view back on the chosen dates, for when the picker is opened afresh. */
export function resetCalendarView() {
  state.calendarMonth = null;
}

export function applyDatePreset(key) {
  const start = parseIso(todayIso());
  if (key === 'weekend') {
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

  // A preset chooses both ends at once, so nothing is half-picked afterwards,
  // and the view follows the dates it just set.
  state.awaitingCheckout = false;
  state.calendarMonth = null;
  normaliseDates();
}

export function clearDates() {
  state.checkIn = todayIso(9);
  state.checkOut = todayIso(12);
  state.stay = 'exact';
  state.flexDays = 0;
  state.startMonths = [];
  state.awaitingCheckout = false;
  // Dates that jump somewhere else take the view with them, or the picker sits
  // on a month with nothing selected in it.
  state.calendarMonth = null;
  normaliseDates();
}

/** docs/01 TM-06 & TM-07 — how loose the guest is willing to be about dates. */
export function setStayShape(patch) {
  Object.assign(state, patch);
  if (state.stay === 'exact' && state.flexDays === 0) normaliseDates();
  else notify();
}

/** Keeps check-out after check-in, then re-prices whatever screen is open. */
export function normaliseDates() {
  if (state.checkOut <= state.checkIn) {
    const out = parseIso(state.checkIn);
    out.setDate(out.getDate() + 1);
    state.checkOut = isoOf(out);
  }
  notify();
  if (state.detail) refreshQuote();
}

export function bumpGuest(key, delta) {
  const min = key === 'adults' ? 1 : 0;
  state.guests = { ...state.guests, [key]: Math.max(min, Math.min(16, state.guests[key] + delta)) };
  notify();
  if (state.detail) refreshQuote();
}

/** The book panel's single +/- pair, which only moves adults and children. */
export function bumpTotalGuests(delta) {
  const max = state.detail?.card.maxGuests ?? 16;
  const next = Math.min(max, Math.max(1, totalGuests() + delta));
  const diff = next - totalGuests();
  if (!diff) return;

  const g = { ...state.guests };
  if (diff > 0) g.adults += diff;
  else if (g.children > 0) g.children = Math.max(0, g.children + diff);
  else g.adults = Math.max(1, g.adults + diff);

  state.guests = g;
  notify();
  if (state.detail) refreshQuote();
}

/* ------------------------------------------------------------- filter ops */

export function toggleAmenity(key) {
  state.amenities = state.amenities.includes(key)
    ? state.amenities.filter(a => a !== key)
    : [...state.amenities, key];
  notify();
}

export function bumpCount(key, delta) {
  state[key] = Math.max(0, Math.min(8, (state[key] || 0) + delta));
  notify();
}

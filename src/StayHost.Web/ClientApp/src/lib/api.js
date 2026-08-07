// Thin wrapper over the ASP.NET Core API. Every call goes through `request`
// so failures surface the server's Vietnamese message instead of a raw status.

async function request(path, options = {}) {
  const res = await fetch(path, {
    credentials: 'same-origin',
    headers: options.body ? { 'Content-Type': 'application/json' } : undefined,
    ...options
  });

  if (res.status === 204) return null;

  const text = await res.text();
  const payload = text ? safeJson(text) : null;

  if (!res.ok) {
    const message = payload?.message || payload?.title || `Yêu cầu thất bại (${res.status}).`;
    const error = new Error(message);
    error.status = res.status;
    error.payload = payload;
    throw error;
  }
  return payload;
}

function safeJson(text) {
  try { return JSON.parse(text); } catch { return null; }
}

function qs(params) {
  const usp = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v === undefined || v === null || v === '' || v === false) continue;
    usp.set(k, Array.isArray(v) ? v.join(',') : String(v));
  }
  const s = usp.toString();
  return s ? `?${s}` : '';
}

export const api = {
  meta: () => request('/api/meta'),

  home: params => request(`/api/home${qs(params ?? {})}`),

  suggest: q => request(`/api/suggest${q ? `?q=${encodeURIComponent(q)}` : ''}`),

  search: params => request(`/api/listings${qs(params)}`),

  /** docs/01 TM-19 — how many results the current filters would return. */
  count: params => request(`/api/listings/count${qs(params)}`),

  /** docs/01 TM-05 / TĐ-09 — nightly rates and the next free windows. */
  listingCalendar: (id, params) => request(`/api/listings/${id}/calendar${qs(params ?? {})}`),


  listing: (idOrSlug, params) =>
    request(`/api/listings/${encodeURIComponent(idOrSlug)}${qs(params ?? {})}`),

  quote: params => request(`/api/quote${qs(params)}`),

  favorites: () => request('/api/favorites'),

  toggleFavorite: id => request(`/api/favorites/${id}`, { method: 'POST' }),

  wishlists: () => request('/api/wishlists'),
  wishlist: id => request(`/api/wishlists/${id}`),
  createWishlist: name => request('/api/wishlists', { method: 'POST', body: JSON.stringify({ name }) }),
  renameWishlist: (id, name) => request(`/api/wishlists/${id}`, { method: 'PUT', body: JSON.stringify({ name }) }),
  deleteWishlist: id => request(`/api/wishlists/${id}`, { method: 'DELETE' }),
  moveToWishlist: (listId, listingId) =>
    request(`/api/wishlists/${listId}/items/${listingId}`, { method: 'POST' }),

  bookings: () => request('/api/bookings'),

  /** Creates the 15-minute hold (docs/01 ĐP-02); no money moves yet. */
  hold: body => request('/api/bookings', { method: 'POST', body: JSON.stringify(body) }),

  /** Takes the money for a held booking; the server re-prices first (ĐP-12). */
  pay: (id, body) => request(`/api/bookings/${id}/pay`, { method: 'POST', body: JSON.stringify(body ?? {}) }),

  /** Abandons a hold so the dates go back on sale immediately. */
  release: id => request(`/api/bookings/${id}/release`, { method: 'POST' }),

  booking: id => request(`/api/bookings/${id}`),

  refundPreview: id => request(`/api/bookings/${id}/refund-preview`),

  cancelBooking: id => request(`/api/bookings/${id}/cancel`, { method: 'POST' }),

  review: (bookingId, body) =>
    request(`/api/bookings/${bookingId}/review`, { method: 'POST', body: JSON.stringify(body) }),

  /** docs/01 ĐG-08 — correct a review inside 48 hours, before it goes public. */
  editReview: (bookingId, body) =>
    request(`/api/bookings/${bookingId}/review`, { method: 'PUT', body: JSON.stringify(body) }),

  /* ------------------------------------------------------------- account */
  me: () => request('/api/account/me'),
  register: body => request('/api/account/register', { method: 'POST', body: JSON.stringify(body) }),
  login: body => request('/api/account/login', { method: 'POST', body: JSON.stringify(body) }),
  logout: () => request('/api/account/logout', { method: 'POST' }),
  updateProfile: body => request('/api/account/profile', { method: 'PUT', body: JSON.stringify(body) }),
  becomeHost: () => request('/api/account/become-host', { method: 'POST' }),
  changePassword: body => request('/api/account/change-password', { method: 'POST', body: JSON.stringify(body) }),
  forgotPassword: email => request('/api/account/forgot-password', { method: 'POST', body: JSON.stringify({ email }) }),
  resetPassword: body => request('/api/account/reset-password', { method: 'POST', body: JSON.stringify(body) }),
  sendVerification: () => request('/api/account/send-verification', { method: 'POST' }),
  verifyEmail: token => request('/api/account/verify-email', { method: 'POST', body: JSON.stringify({ token }) }),
  sessions: () => request('/api/account/sessions'),
  revokeSession: id => request(`/api/account/sessions/${id}`, { method: 'DELETE' }),
  /* docs/01 TK-04 — the languages the profile editor offers */
  profileOptions: () => request('/api/account/profile-options'),
  /* docs/01 TK-05 — somebody else's profile; no sign-in needed */
  publicProfile: id => request(`/api/users/${id}`),
  /* docs/01 TK-06 — identity verification */
  identityStatus: () => request('/api/account/identity'),
  submitIdentity: body => request('/api/account/identity', { method: 'POST', body: JSON.stringify(body) }),
  /* docs/01 TK-08 — two-factor */
  twoFactorState: () => request('/api/account/two-factor'),
  twoFactorVerify: body => request('/api/account/two-factor', { method: 'POST', body: JSON.stringify(body) }),
  twoFactorResend: challenge =>
    request('/api/account/two-factor/resend', { method: 'POST', body: JSON.stringify({ challenge }) }),
  enableTwoFactor: body =>
    request('/api/account/two-factor/enable', { method: 'POST', body: JSON.stringify(body) }),
  disableTwoFactor: password =>
    request('/api/account/two-factor/disable', { method: 'POST', body: JSON.stringify({ email: '', password }) }),
  /* docs/01 TK-10 — the notification matrix */
  notificationPrefs: () => request('/api/account/notifications'),
  setNotificationPref: body =>
    request('/api/account/notifications', { method: 'PUT', body: JSON.stringify(body) }),

  /* -------------------------------------------------------------- hosting */
  hostDashboard: () => request('/api/host/dashboard'),
  createListing: body => request('/api/host/listings', { method: 'POST', body: JSON.stringify(body) }),
  updateListing: (id, body) => request(`/api/host/listings/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteListing: id => request(`/api/host/listings/${id}`, { method: 'DELETE' }),
  hostCalendar: id => request(`/api/host/listings/${id}/calendar`),
  addBlock: body => request('/api/host/blocks', { method: 'POST', body: JSON.stringify(body) }),
  removeBlock: id => request(`/api/host/blocks/${id}`, { method: 'DELETE' }),
  addPriceRule: body => request('/api/host/price-rules', { method: 'POST', body: JSON.stringify(body) }),
  removePriceRule: id => request(`/api/host/price-rules/${id}`, { method: 'DELETE' }),
  reviewGuest: (bookingId, body) =>
    request(`/api/host/bookings/${bookingId}/review-guest`, { method: 'POST', body: JSON.stringify(body) }),
  respondBooking: (id, decision, reason) =>
    request(`/api/host/bookings/${id}/${decision}`, { method: 'POST', body: JSON.stringify({ reason: reason ?? null }) }),
  replyToReview: (reviewId, text) =>
    request(`/api/host/reviews/${reviewId}/reply`, { method: 'POST', body: JSON.stringify({ text }) }),

  /* ------------------------------------------------------ host operations */
  hostToday: () => request('/api/host/today'),
  multiCalendar: params => request(`/api/host/calendar${qs(params ?? {})}`),
  hostRules: id => request(`/api/host/listings/${id}/rules`),
  saveHostRules: (id, body) =>
    request(`/api/host/listings/${id}/rules`, { method: 'PUT', body: JSON.stringify(body) }),
  editDays: (id, body) =>
    request(`/api/host/listings/${id}/days`, { method: 'POST', body: JSON.stringify(body) }),
  hostPayout: () => request('/api/host/payout'),
  saveHostPayout: body => request('/api/host/payout', { method: 'PUT', body: JSON.stringify(body) }),
  superhostProgress: () => request('/api/host/superhost'),

  /* ------------------------------------------------------------- messages */
  /* --------------------------------------------------------- notifications */
  notifications: () => request('/api/notifications'),
  readAllNotifications: () => request('/api/notifications/read-all', { method: 'POST' }),
  readNotification: id => request(`/api/notifications/${id}/read`, { method: 'POST' }),

  /* -------------------------------------------------- resolution centre */
  resolutions: () => request('/api/resolutions'),
  openResolution: body => request('/api/resolutions', { method: 'POST', body: JSON.stringify(body) }),
  respondResolution: (id, body) =>
    request(`/api/resolutions/${id}/respond`, { method: 'POST', body: JSON.stringify(body) }),
  withdrawResolution: id => request(`/api/resolutions/${id}/withdraw`, { method: 'POST' }),
  adminResolutions: () => request('/api/resolutions/admin'),
  decideResolution: (id, body) =>
    request(`/api/resolutions/${id}/decide`, { method: 'POST', body: JSON.stringify(body) }),
  saveTaxRule: (id, body) =>
    request(`/api/admin/tax-rules/${id}`, { method: 'PUT', body: JSON.stringify(body) }),

  /* ------------------------------------------------------- reports / admin */
  report: body => request('/api/reports', { method: 'POST', body: JSON.stringify(body) }),
  adminOverview: () => request('/api/admin/overview'),
  adminPublish: (id, published) =>
    request(`/api/admin/listings/${id}/publish?published=${published}`, { method: 'POST' }),
  adminResolveReport: (id, status, resolution) =>
    request(`/api/admin/reports/${id}/resolve`, { method: 'POST', body: JSON.stringify({ status, resolution }) }),

  threads: () => request('/api/messages/threads'),
  thread: id => request(`/api/messages/threads/${id}`),
  sendMessage: body => request('/api/messages', { method: 'POST', body: JSON.stringify(body) }),

  /* docs/01 TN-08 — the host's saved phrases. */
  quickReplies: () => request('/api/messages/quick-replies'),
  addQuickReply: body => request('/api/messages/quick-replies', { method: 'POST', body: JSON.stringify(body) }),
  deleteQuickReply: id => request(`/api/messages/quick-replies/${id}`, { method: 'DELETE' }),

  /* docs/01 QL-19 — people helping run a listing. */
  /* docs/01 MR-01 → MR-04 — experiences, sold by the seat. */
  experiences: params => request(`/api/experiences${qs(params ?? {})}`),
  experience: idOrSlug => request(`/api/experiences/${idOrSlug}`),
  experienceQuote: (slotId, seats, priv) =>
    request(`/api/experiences/slots/${slotId}/quote${qs({ seats, priv })}`),
  bookExperience: (slotId, body) =>
    request(`/api/experiences/slots/${slotId}/book`, { method: 'POST', body: JSON.stringify(body) }),
  experienceBookings: () => request('/api/experiences/bookings'),
  cancelExperienceBooking: id =>
    request(`/api/experiences/bookings/${id}/cancel`, { method: 'POST' }),

  /* docs/01 TK-01 và TK-02 — xác thực bằng mã và đăng nhập qua nhà cung cấp. */
  verification: () => request('/api/account/verification'),
  sendCode: kind => request('/api/account/send-code', { method: 'POST', body: JSON.stringify({ kind }) }),
  confirmCode: (kind, code) =>
    request('/api/account/confirm-code', { method: 'POST', body: JSON.stringify({ kind, code }) }),
  externalConfig: () => request('/api/account/external/config'),
  externalSignIn: (provider, credential) =>
    request('/api/account/external', { method: 'POST', body: JSON.stringify({ provider, credential }) }),
  unlinkProvider: provider => request(`/api/account/external/${provider}`, { method: 'DELETE' }),

  /* docs/06 — StayShield. */
  shieldTerms: side => request(`/api/shield/terms${qs({ side })}`),
  shieldClaims: () => request('/api/shield'),
  shieldClaim: id => request(`/api/shield/${id}`),
  openShieldClaim: (bookingId, body) =>
    request(`/api/shield/bookings/${bookingId}`, { method: 'POST', body: JSON.stringify(body) }),
  respondShield: (id, body) =>
    request(`/api/shield/${id}/respond`, { method: 'POST', body: JSON.stringify(body) }),
  appealShield: (id, note) =>
    request(`/api/shield/${id}/appeal`, { method: 'POST', body: JSON.stringify({ note }) }),
  shieldQueue: () => request('/api/shield/admin/queue'),
  shieldRehousing: id => request(`/api/shield/admin/${id}/rehousing`),
  shieldFund: () => request('/api/shield/admin/fund'),
  decideShield: (id, body) =>
    request(`/api/shield/admin/${id}/decide`, { method: 'POST', body: JSON.stringify(body) }),
  recoverShield: (id, amount) =>
    request(`/api/shield/admin/${id}/recover`, { method: 'POST', body: JSON.stringify({ amount }) }),
  hostCancelBooking: (id, reason) =>
    request(`/api/host/bookings/${id}/cancel`, { method: 'POST', body: JSON.stringify({ reason }) }),

  /* Balance, gift cards and referrals. */
  wallet: () => request('/api/wallet'),
  buyGiftCard: body => request('/api/wallet/gift-cards', { method: 'POST', body: JSON.stringify(body) }),
  redeemGiftCard: code =>
    request('/api/wallet/redeem', { method: 'POST', body: JSON.stringify({ code }) }),
  inviteFriend: email =>
    request('/api/wallet/referrals', { method: 'POST', body: JSON.stringify({ email }) }),

  /* docs/01 MR-10 — best-price guarantee on a hotel room. */
  submitPriceMatch: (bookingId, body) =>
    request(`/api/bookings/${bookingId}/price-match`, { method: 'POST', body: JSON.stringify(body) }),
  priceMatch: bookingId => request(`/api/bookings/${bookingId}/price-match`),

  /* docs/01 MR-05 → MR-07 — services, booked by the slot. */
  services: params => request(`/api/services${qs(params ?? {})}`),
  service: idOrSlug => request(`/api/services/${idOrSlug}`),
  quoteService: (id, body) =>
    request(`/api/services/${id}/quote`, { method: 'POST', body: JSON.stringify(body) }),
  bookService: (id, body) =>
    request(`/api/services/${id}/book`, { method: 'POST', body: JSON.stringify(body) }),
  serviceBookings: () => request('/api/services/bookings'),
  cancelServiceBooking: id => request(`/api/services/bookings/${id}/cancel`, { method: 'POST' }),

  /* docs/01 ĐP-07 — one booking, up to sixteen payers. */
  openSplit: (id, emails) =>
    request(`/api/bookings/${id}/split`, { method: 'POST', body: JSON.stringify({ emails }) }),
  splitOf: id => request(`/api/bookings/${id}/split`),
  cancelSplit: id => request(`/api/bookings/${id}/split`, { method: 'DELETE' }),
  splitInvite: token => request(`/api/split/${token}`),
  paySplitShare: (token, body) =>
    request(`/api/split/${token}/pay`, { method: 'POST', body: JSON.stringify(body) }),

  /* docs/01 AT-07 — the help centre. */
  help: params => request(`/api/help${qs(params ?? {})}`),
  helpArticle: slug => request(`/api/help/${slug}`),

  /* docs/01 AT-11 — accounts the checks flagged. */
  riskFlags: () => request('/api/admin/risk'),
  resolveRiskFlag: (id, body) =>
    request(`/api/admin/risk/${id}/resolve`, { method: 'POST', body: JSON.stringify(body) }),

  payBalance: id => request(`/api/bookings/${id}/balance`, { method: 'POST' }),

  coHosts: () => request('/api/host/co-hosts'),
  inviteCoHost: body => request('/api/host/co-hosts', { method: 'POST', body: JSON.stringify(body) }),
  respondCoHost: (id, decision) => request(`/api/host/co-hosts/${id}/${decision}`, { method: 'POST' }),
  revokeCoHost: id => request(`/api/host/co-hosts/${id}`, { method: 'DELETE' }),

  /* docs/01 QL-10 — calendars kept on other platforms. */
  calendarFeeds: id => request(`/api/host/listings/${id}/feeds`),
  addCalendarFeed: (id, body) =>
    request(`/api/host/listings/${id}/feeds`, { method: 'POST', body: JSON.stringify(body) }),
  syncCalendarFeed: (id, feedId) =>
    request(`/api/host/listings/${id}/feeds/${feedId}/sync`, { method: 'POST' }),
  removeCalendarFeed: (id, feedId) =>
    request(`/api/host/listings/${id}/feeds/${feedId}`, { method: 'DELETE' })
};

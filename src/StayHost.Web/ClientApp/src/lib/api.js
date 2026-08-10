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

  // docs/01 TM-26 — a city landing page.
  city: key => request(`/api/cities/${encodeURIComponent(key)}`),

  // docs/01 AT-08 — the contextual support assistant.
  supportAssistant: () => request('/api/support/assistant'),
  // docs/01 AT-09 — reach a human support agent.
  supportTopics: () => request('/api/support/topics'),
  createSupportTicket: body => request('/api/support/tickets', { method: 'POST', body: JSON.stringify(body) }),
  supportTickets: () => request('/api/support/tickets'),
  resolveSupportTicket: (id, reply) => request(`/api/support/tickets/${id}/resolve`, { method: 'POST', body: JSON.stringify({ reply }) }),

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
  // docs/01 YT-03 — the guest's private note on a saved place.
  setWishlistNote: (listId, listingId, note) =>
    request(`/api/wishlists/${listId}/items/${listingId}/note`, { method: 'PUT', body: JSON.stringify({ note }) }),
  // docs/01 YT-05 — share a wishlist by link.
  shareWishlist: (id, on) => request(`/api/wishlists/${id}/share?on=${on}`, { method: 'POST' }),
  sharedWishlist: token => request(`/api/shared-wishlists/${encodeURIComponent(token)}`),

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

  // docs/01 CĐ-06 — request/withdraw a date-or-guest change; host responds elsewhere.
  requestChange: (id, body) => request(`/api/bookings/${id}/change-request`, { method: 'POST', body: JSON.stringify(body) }),
  withdrawChange: (id, reqId) => request(`/api/bookings/${id}/change-request/${reqId}/withdraw`, { method: 'POST' }),
  respondChange: (id, reqId, accept) =>
    request(`/api/host/bookings/${id}/change-request/${reqId}/respond`, { method: 'POST', body: JSON.stringify({ accept }) }),

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
  /* docs/01 CN-08 — titles and a draft description from the facts so far */
  copySuggestions: body => request('/api/host/copy-suggestions', { method: 'POST', body: JSON.stringify(body) }),
  /* docs/01 CN-10 — what comparable places nearby charge */
  marketPrice: params => request(`/api/host/market-price${qs(params)}`),
  /* docs/01 CN-14 — a what-if income estimate before publishing */
  incomeEstimate: params => request(`/api/host/income-estimate${qs(params)}`),
  /* docs/01 QL-09 + QL-18 — suggested price and improvements for a listing */
  listingAdvice: id => request(`/api/host/listings/${id}/advice`),
  /* docs/01 CN-15 — clone a listing into a fresh draft */
  duplicateListing: id => request(`/api/host/listings/${id}/duplicate`, { method: 'POST' }),
  /* docs/01 QL-13 — what happens if the host cancels, before they confirm */
  hostCancelPreview: id => request(`/api/host/bookings/${id}/cancel-preview`),
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
  /* docs/08 — quản trị người dùng. */
  adminSearchUsers: q => request(`/api/admin/users?q=${encodeURIComponent(q ?? '')}`),
  adminUser: id => request(`/api/admin/users/${id}`),
  adminLockPreview: (id, refundInFull) =>
    request(`/api/admin/users/${id}/lock-preview?refundInFull=${refundInFull ? 'true' : 'false'}`),
  adminSanction: (id, body) =>
    request(`/api/admin/users/${id}/sanction`, { method: 'POST', body: JSON.stringify(body) }),
  adminRestore: (id, body) =>
    request(`/api/admin/users/${id}/restore`, { method: 'POST', body: JSON.stringify(body) }),
  adminForcePasswordReset: (id, body) =>
    request(`/api/admin/users/${id}/force-password-reset`, { method: 'POST', body: JSON.stringify(body) }),
  adminForceIdentityRecheck: (id, body) =>
    request(`/api/admin/users/${id}/force-identity-recheck`, { method: 'POST', body: JSON.stringify(body) }),
  adminIdentity: (id, body) =>
    request(`/api/admin/users/${id}/identity`, { method: 'POST', body: JSON.stringify(body) }),
  adminAppeals: () => request('/api/admin/appeals'),
  adminDecideAppeal: (id, body) =>
    request(`/api/admin/appeals/${id}/decide`, { method: 'POST', body: JSON.stringify(body) }),
  adminDataRequests: () => request('/api/admin/data-requests'),
  adminErase: (id, body) =>
    request(`/api/admin/data-requests/${id}/erase`, { method: 'POST', body: JSON.stringify(body) }),
  adminOversight: () => request('/api/admin/oversight'),
  adminDecideApproval: (id, body) =>
    request(`/api/admin/approvals/${id}/decide`, { method: 'POST', body: JSON.stringify(body) }),
  adminImpersonate: body => request('/api/admin/impersonate', { method: 'POST', body: JSON.stringify(body) }),
  adminImpersonationCurrent: () => request('/api/admin/impersonate/current'),
  adminEndImpersonation: () => request('/api/admin/impersonate/end', { method: 'POST' }),
  adminGrantScopes: body => request('/api/admin/admins', { method: 'POST', body: JSON.stringify(body) }),
  adminMarkReviewed: (id, body) =>
    request(`/api/admin/admins/${id}/reviewed`, { method: 'POST', body: JSON.stringify(body) }),
  adminMergeUsers: body => request('/api/admin/users/merge', { method: 'POST', body: JSON.stringify(body) }),
  /* docs/08 §5.6 — hồ sơ nghiêm trọng Quản trị tối cao phải xem lại trong 24h */
  adminSevereReview: (id, body) =>
    request(`/api/admin/sanctions/${id}/severe-review`, { method: 'POST', body: JSON.stringify(body) }),
  /* docs/08 §2 — sửa hồ sơ, đọc tin nhắn của đúng một đơn */
  adminEditProfile: (id, body) =>
    request(`/api/admin/users/${id}/profile`, { method: 'POST', body: JSON.stringify(body) }),
  adminBookingThread: (bookingId, body) =>
    request(`/api/admin/users/bookings/${bookingId}/thread`, { method: 'POST', body: JSON.stringify(body) }),
  /* docs/08 §9 — cấp liên kết tải dữ liệu có hạn */
  adminFulfilExport: (id, body) =>
    request(`/api/admin/data-requests/${id}/export`, { method: 'POST', body: JSON.stringify(body) }),

  /* docs/08 §8 và §9 — phía người dùng: quyết định về mình, khiếu nại, dữ liệu cá nhân */
  mySanctions: () => request('/api/account/sanctions'),
  fileAppeal: (id, body) =>
    request(`/api/account/sanctions/${id}/appeal`, { method: 'POST', body: JSON.stringify(body) }),
  appealByToken: body =>
    request('/api/account/sanctions/appeal-by-token', { method: 'POST', body: JSON.stringify(body) }),
  myDataRequests: () => request('/api/account/data/requests'),
  askDataRequest: kind =>
    request('/api/account/data/requests', { method: 'POST', body: JSON.stringify({ kind }) }),

  /* docs/07 §7, §11 and §15 — the finance desk. */
  financeReport: () => request('/api/admin/finance'),
  reconciliation: day => request(`/api/admin/finance/reconciliation${day ? `?day=${day}` : ''}`),
  adminTransactions: q => request(`/api/admin/finance/transactions${q ? `?q=${encodeURIComponent(q)}` : ''}`),
  adminRefund: (bookingId, body) =>
    request(`/api/admin/finance/transactions/${bookingId}/refund`, { method: 'POST', body: JSON.stringify(body) }),
  adminAdjustPayout: (bookingId, body) =>
    request(`/api/admin/finance/payouts/${bookingId}/adjust`, { method: 'POST', body: JSON.stringify(body) }),
  chargebacks: () => request('/api/admin/finance/chargebacks'),
  openChargeback: body => request('/api/admin/finance/chargebacks', { method: 'POST', body: JSON.stringify(body) }),
  chargebackEvidence: (id, body) =>
    request(`/api/admin/finance/chargebacks/${id}/evidence`, { method: 'POST', body: JSON.stringify(body) }),
  decideChargeback: (id, body) =>
    request(`/api/admin/finance/chargebacks/${id}/decide`, { method: 'POST', body: JSON.stringify(body) }),

  /* docs/07 §2 and §4 — the catalogue, and the cards a guest has kept. */
  paymentCatalogue: () => request('/api/payment-methods/catalogue'),
  savedCards: () => request('/api/payment-methods'),
  addCard: body => request('/api/payment-methods', { method: 'POST', body: JSON.stringify(body) }),
  makeCardDefault: id => request(`/api/payment-methods/${id}/default`, { method: 'PUT' }),
  removeCard: id => request(`/api/payment-methods/${id}`, { method: 'DELETE' }),

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
  // docs/01 TC-04 — the host's tax year. No year asked for means the newest one.
  taxReport: year => request(`/api/host/tax-report${year ? `?year=${year}` : ''}`),
  // docs/01 QL-16 — per-listing views, saves, bookings, conversion, occupancy.
  performance: days => request(`/api/host/performance?days=${days ?? 30}`),
  report: body => request('/api/reports', { method: 'POST', body: JSON.stringify(body) }),
  // docs/01 AT-02 — the reasons live on the server so a listing and a review are
  // never offered the same list by two copies drifting apart.
  reportReasons: target => request(`/api/reports/reasons/${target}`),
  adminOverview: () => request('/api/admin/overview'),
  adminPublish: (id, published, reason) =>
    request(`/api/admin/listings/${id}/publish?published=${published}`, {
      method: 'POST', body: JSON.stringify({ reason }),
    }),
  adminResolveReport: (id, status, resolution) =>
    request(`/api/admin/reports/${id}/resolve`, { method: 'POST', body: JSON.stringify({ status, resolution }) }),
  // docs/01 AT-12 — host decline / anti-discrimination monitor.
  adminDeclineMonitor: () => request('/api/admin/decline-monitor'),
  // docs/01 QT-07 — help article management.
  adminHelpArticles: () => request('/api/admin/help-articles'),
  saveHelpArticle: body => request('/api/admin/help-articles', { method: 'POST', body: JSON.stringify(body) }),
  deleteHelpArticle: id => request(`/api/admin/help-articles/${id}`, { method: 'DELETE' }),
  // docs/01 QT-08 — feature flags.
  adminFeatureFlags: () => request('/api/admin/feature-flags'),
  saveFeatureFlag: body => request('/api/admin/feature-flags', { method: 'POST', body: JSON.stringify(body) }),
  // docs/01 AT-01 — the pre-publish review queue.
  adminPendingListings: () => request('/api/admin/listings/pending'),
  adminReviewListing: (id, decision, reason) =>
    request(`/api/admin/listings/${id}/review/${decision}`, {
      method: 'POST', body: JSON.stringify({ reason }),
    }),

  threads: filter => request(`/api/messages/threads${filter && filter !== 'all' ? `?filter=${filter}` : ''}`),
  archiveThread: (id, on) => request(`/api/messages/threads/${id}/archive?on=${on}`, { method: 'POST' }),
  thread: id => request(`/api/messages/threads/${id}`),
  sendMessage: body => request('/api/messages', { method: 'POST', body: JSON.stringify(body) }),

  /* docs/01 TN-08 — the host's saved phrases. */
  quickReplies: () => request('/api/messages/quick-replies'),
  // docs/01 ĐP-17, QL-14 — private offers on a thread.
  sendOffer: (threadId, body) =>
    request(`/api/messages/threads/${threadId}/offer`, { method: 'POST', body: JSON.stringify(body) }),
  withdrawOffer: id => request(`/api/messages/offers/${id}/withdraw`, { method: 'POST' }),
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
  // docs/01 TK-07 — company email verification.
  setWorkEmail: email => request('/api/account/work-email', { method: 'POST', body: JSON.stringify({ email }) }),
  confirmWorkEmail: code =>
    request('/api/account/work-email/confirm', { method: 'POST', body: JSON.stringify({ kind: 'workemail', code }) }),
  removeWorkEmail: () => request('/api/account/work-email', { method: 'DELETE' }),
  // docs/01 AT-03 — neighbour report channel (no account).
  neighborConcerns: () => request('/api/neighbor-reports/concerns'),
  submitNeighborReport: body => request('/api/neighbor-reports', { method: 'POST', body: JSON.stringify(body) }),
  adminNeighborReports: () => request('/api/admin/neighbor-reports'),
  resolveNeighborReport: (id, status, resolution) =>
    request(`/api/admin/neighbor-reports/${id}/resolve`, { method: 'POST', body: JSON.stringify({ status, resolution }) }),
  // docs/01 YT-07 — compare 2–5 listings side by side.
  compareListings: ids => request(`/api/listings/compare?ids=${ids.join(',')}`),
  // docs/01 TĐ-03, TN-06 — machine translation.
  translateConfig: () => request('/api/translate/config'),
  translate: (text, targetLang) =>
    request('/api/translate', { method: 'POST', body: JSON.stringify({ text, targetLang }) }),
  // docs/01 TM-23 — saved searches.
  savedSearches: () => request('/api/account/saved-searches'),
  saveSearch: body => request('/api/account/saved-searches', { method: 'POST', body: JSON.stringify(body) }),
  deleteSavedSearch: id => request(`/api/account/saved-searches/${id}`, { method: 'DELETE' }),
  // docs/01 AT-10 — block list.
  blocks: () => request('/api/account/blocks'),
  blockUser: userId => request('/api/account/blocks', { method: 'POST', body: JSON.stringify({ userId }) }),
  unblockUser: userId => request(`/api/account/blocks/${userId}`, { method: 'DELETE' }),
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

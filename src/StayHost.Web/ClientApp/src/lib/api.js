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

  home: () => request('/api/home'),

  suggest: q => request(`/api/suggest${q ? `?q=${encodeURIComponent(q)}` : ''}`),

  search: params => request(`/api/listings${qs(params)}`),

  listing: idOrSlug => request(`/api/listings/${encodeURIComponent(idOrSlug)}`),

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

  book: body => request('/api/bookings', { method: 'POST', body: JSON.stringify(body) }),

  booking: id => request(`/api/bookings/${id}`),

  refundPreview: id => request(`/api/bookings/${id}/refund-preview`),

  cancelBooking: id => request(`/api/bookings/${id}/cancel`, { method: 'POST' }),

  review: (bookingId, body) =>
    request(`/api/bookings/${bookingId}/review`, { method: 'POST', body: JSON.stringify(body) }),

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
  respondBooking: (id, action, reason) =>
    request(`/api/host/bookings/${id}/${action}`, { method: 'POST', body: JSON.stringify({ reason: reason ?? null }) }),

  /* ------------------------------------------------------------- messages */
  /* --------------------------------------------------------- notifications */
  notifications: () => request('/api/notifications'),
  readAllNotifications: () => request('/api/notifications/read-all', { method: 'POST' }),
  readNotification: id => request(`/api/notifications/${id}/read`, { method: 'POST' }),

  /* ------------------------------------------------------- reports / admin */
  report: body => request('/api/reports', { method: 'POST', body: JSON.stringify(body) }),
  adminOverview: () => request('/api/admin/overview'),
  adminPublish: (id, published) =>
    request(`/api/admin/listings/${id}/publish?published=${published}`, { method: 'POST' }),
  adminResolveReport: (id, status, resolution) =>
    request(`/api/admin/reports/${id}/resolve`, { method: 'POST', body: JSON.stringify({ status, resolution }) }),

  threads: () => request('/api/messages/threads'),
  thread: id => request(`/api/messages/threads/${id}`),
  sendMessage: body => request('/api/messages', { method: 'POST', body: JSON.stringify(body) })
};

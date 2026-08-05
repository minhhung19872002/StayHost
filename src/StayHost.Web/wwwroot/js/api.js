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

  search: params => request(`/api/listings${qs(params)}`),

  listing: idOrSlug => request(`/api/listings/${encodeURIComponent(idOrSlug)}`),

  quote: params => request(`/api/quote${qs(params)}`),

  favorites: () => request('/api/favorites'),

  toggleFavorite: id => request(`/api/favorites/${id}`, { method: 'POST' }),

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

  /* -------------------------------------------------------------- hosting */
  hostDashboard: () => request('/api/host/dashboard'),
  createListing: body => request('/api/host/listings', { method: 'POST', body: JSON.stringify(body) }),
  updateListing: (id, body) => request(`/api/host/listings/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteListing: id => request(`/api/host/listings/${id}`, { method: 'DELETE' }),
  hostCalendar: id => request(`/api/host/listings/${id}/calendar`),
  addBlock: body => request('/api/host/blocks', { method: 'POST', body: JSON.stringify(body) }),
  removeBlock: id => request(`/api/host/blocks/${id}`, { method: 'DELETE' }),
  respondBooking: (id, action, reason) =>
    request(`/api/host/bookings/${id}/${action}`, { method: 'POST', body: JSON.stringify({ reason: reason ?? null }) }),

  /* ------------------------------------------------------------- messages */
  threads: () => request('/api/messages/threads'),
  thread: id => request(`/api/messages/threads/${id}`),
  sendMessage: body => request('/api/messages', { method: 'POST', body: JSON.stringify(body) })
};

// docs/01 TM-04 — recent searches, kept on the device. Nothing here goes to the
// server: it is a convenience for one browser, not a profile.

const KEY = 'sh_recent_searches';
const LIMIT = 6;

export function recentSearches() {
  try {
    const raw = JSON.parse(localStorage.getItem(KEY) ?? '[]');
    return Array.isArray(raw) ? raw.slice(0, LIMIT) : [];
  } catch {
    return [];
  }
}

/** Newest first, de-duplicated by destination so the list stays useful. */
export function rememberSearch(entry) {
  if (!entry?.q?.trim()) return;

  const item = {
    q: entry.q.trim(),
    checkIn: entry.checkIn,
    checkOut: entry.checkOut,
    guests: entry.guests,
    at: Date.now()
  };

  const kept = recentSearches().filter(x => x.q.toLowerCase() !== item.q.toLowerCase());
  try {
    localStorage.setItem(KEY, JSON.stringify([item, ...kept].slice(0, LIMIT)));
  } catch {
    // A full or blocked storage quota must never break searching.
  }
}

export function clearSearchHistory() {
  try { localStorage.removeItem(KEY); } catch { /* nothing to do */ }
}

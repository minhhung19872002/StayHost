// Small helpers shared by every view. No framework — the app is a handful of
// pure render(state) -> HTML functions plus one delegated click handler.

/** Escape untrusted text before it goes into an innerHTML template. */
export function esc(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

/** Tagged template that escapes every interpolated value; arrays are joined. */
export function html(strings, ...values) {
  return strings.reduce((out, chunk, i) => {
    if (i === 0) return chunk;
    const v = values[i - 1];
    const rendered = Array.isArray(v) ? v.join('') : v;
    return out + (rendered ?? '') + chunk;
  });
}

export const $ = (sel, root = document) => root.querySelector(sel);
export const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

/* ------------------------------------------------------------ formatting */

const VND_FORMATTER = new Intl.NumberFormat('vi-VN');

export const CURRENCY_STATE = { code: 'VND', symbol: '₫', rate: 1 };

export function setCurrency(currency) {
  CURRENCY_STATE.code = currency.code;
  CURRENCY_STATE.symbol = currency.symbol;
  CURRENCY_STATE.rate = Number(currency.rateFromVnd) || 1;
}

/** Format a VND amount in whatever currency the user picked. */
export function money(vnd, opts = {}) {
  const { code, symbol, rate } = CURRENCY_STATE;
  if (code === 'VND') return VND_FORMATTER.format(Math.round(vnd)) + '₫';

  const converted = vnd * rate;
  const digits = converted < 10 ? 2 : converted < 1000 ? (opts.precise ? 2 : 0) : 0;
  return symbol + new Intl.NumberFormat('en-US', {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits
  }).format(converted);
}

export function todayIso(offsetDays = 0) {
  const d = new Date();
  d.setHours(12, 0, 0, 0);
  d.setDate(d.getDate() + offsetDays);
  return d.toISOString().slice(0, 10);
}

export function parseIso(iso) {
  const [y, m, d] = String(iso).split('-').map(Number);
  return new Date(y, (m || 1) - 1, d || 1, 12);
}

export function isoOf(date) {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

export function nightsBetween(checkIn, checkOut) {
  const n = Math.round((parseIso(checkOut) - parseIso(checkIn)) / 86400000);
  return n > 0 ? n : 1;
}

const SHORT_DATE = new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit' });
const LONG_DATE = new Intl.DateTimeFormat('vi-VN', { day: 'numeric', month: 'long', year: 'numeric' });

export const shortDate = iso => SHORT_DATE.format(parseIso(iso));
export const longDate = iso => LONG_DATE.format(parseIso(iso));

export function dateRangeLabel(checkIn, checkOut) {
  return `${shortDate(checkIn)} – ${shortDate(checkOut)}`;
}

export function plural(n, word) {
  return `${n} ${word}`;
}

/* -------------------------------------------------------------- behaviour */

export function debounce(fn, wait = 260) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), wait);
  };
}

let toastTimer = null;
export function toast(message) {
  const root = document.getElementById('toast-root');
  if (!root) return;
  root.innerHTML = `<div class="toast">${esc(message)}</div>`;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { root.innerHTML = ''; }, 2600);
}

/** Trap focus inside the open overlay and close it on Escape. */
export function wireOverlay(node, onClose) {
  const close = e => {
    if (e.type === 'keydown' && e.key !== 'Escape') return;
    if (e.type === 'mousedown' && e.target !== node) return;
    onClose();
  };
  node.addEventListener('mousedown', close);
  document.addEventListener('keydown', close, { once: true });
  const first = node.querySelector('input, button, select, [tabindex]');
  if (first) setTimeout(() => first.focus({ preventScroll: true }), 30);
}

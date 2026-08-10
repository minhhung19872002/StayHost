// Money and date formatting. Currency is a module-level setting because every
// price on the page must switch together the moment the user picks one.

const VND = new Intl.NumberFormat('vi-VN');

export const CURRENCY = { code: 'VND', symbol: '₫', rate: 1 };

export function setCurrency(currency) {
  CURRENCY.code = currency.code;
  CURRENCY.symbol = currency.symbol;
  CURRENCY.rate = Number(currency.rateFromVnd) || 1;
}

export function money(vnd, opts = {}) {
  const amount = Number(vnd) || 0;
  if (CURRENCY.code === 'VND') return `${VND.format(Math.round(amount))}₫`;

  const converted = amount * CURRENCY.rate;
  const digits = converted < 10 ? 2 : converted < 1000 ? (opts.precise ? 2 : 0) : 0;
  return CURRENCY.symbol + new Intl.NumberFormat('en-US', {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits
  }).format(converted);
}

/** Compact money for tight spots: 1.2tr, 850k. */
export function shortMoney(vnd) {
  if (!vnd) return '';
  if (vnd >= 1_000_000) return `${(vnd / 1_000_000).toFixed(1).replace('.0', '')}tr`;
  return `${Math.round(vnd / 1000)}k`;
}

export function todayIso(offsetDays = 0) {
  const d = new Date();
  d.setHours(12, 0, 0, 0);
  d.setDate(d.getDate() + offsetDays);
  return isoOf(d);
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

/**
 * Dates follow the language the guest picked, not the platform's home country.
 * A Korean reader switching the site to 한국어 and still seeing "19 tháng 8, 2026"
 * is being told the switch did not really happen. Set from applyLanguage; the
 * formatters are rebuilt rather than recreated per call, which matters on a page
 * showing a calendar.
 */
export const LOCALE = { tag: 'vi-VN' };

const BCP47 = {
  vi: 'vi-VN', en: 'en-GB', ja: 'ja-JP', ko: 'ko-KR',
  zh: 'zh-CN', fr: 'fr-FR', de: 'de-DE', es: 'es-ES'
};

let SHORT, LONG, TIME;

export function setLocale(code) {
  LOCALE.tag = BCP47[code] ?? 'vi-VN';
  SHORT = new Intl.DateTimeFormat(LOCALE.tag, { day: '2-digit', month: '2-digit' });
  LONG = new Intl.DateTimeFormat(LOCALE.tag, { day: 'numeric', month: 'long', year: 'numeric' });
  TIME = new Intl.DateTimeFormat(LOCALE.tag, {
    day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit'
  });
}

setLocale('vi');

/**
 * A formatter in the current language, for screens that need their own shape.
 * Cached per (locale, shape): a calendar builds one of these per cell otherwise,
 * and Intl formatters are not cheap to construct.
 */
const FORMATTERS = new Map();

export function dateFormat(options) {
  const key = LOCALE.tag + JSON.stringify(options);
  let f = FORMATTERS.get(key);
  if (!f) { f = new Intl.DateTimeFormat(LOCALE.tag, options); FORMATTERS.set(key, f); }
  return f;
}

/** Numbers in the reader's language: 1.234.567 in Vietnamese, 1,234,567 in English. */
export const number = value => new Intl.NumberFormat(LOCALE.tag).format(Number(value) || 0);

export const shortDate = iso => SHORT.format(parseIso(iso));
export const longDate = iso => LONG.format(parseIso(iso));
export const dateTime = value => TIME.format(new Date(value));
export const dateRangeLabel = (a, b) => `${shortDate(a)} – ${shortDate(b)}`;

export function debounce(fn, wait = 260) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), wait);
  };
}

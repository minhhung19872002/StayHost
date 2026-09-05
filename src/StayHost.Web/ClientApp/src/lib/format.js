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

/**
 * docs/01 TK-09 — the display timezone, the third of "ngôn ngữ, tiền tệ, múi
 * giờ". Null means the device's own clock, which is the only honest default.
 *
 * It reaches TIMESTAMPS ONLY — dateTime() and clockTime(). The date-only
 * helpers are deliberately left on the device clock: longDate('2026-09-05')
 * parses as UTC midnight, and a westward zone would move every check-in and
 * check-out back a day. This codebase has already lost a day to seven hours of
 * timezone drift twice (CLAUDE.md §4); a check-in date is a date, not an
 * instant, and instants are the only thing a zone may touch.
 */
export const ZONE = { id: null };

let SHORT, LONG, TIME;

function rebuild() {
  SHORT = new Intl.DateTimeFormat(LOCALE.tag, { day: '2-digit', month: '2-digit' });
  LONG = new Intl.DateTimeFormat(LOCALE.tag, { day: 'numeric', month: 'long', year: 'numeric' });
  TIME = new Intl.DateTimeFormat(LOCALE.tag, {
    day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
    ...(ZONE.id ? { timeZone: ZONE.id } : {})
  });
}

export function setLocale(code) {
  LOCALE.tag = BCP47[code] ?? 'vi-VN';
  rebuild();
}

export function setTimeZone(id) {
  // An unknown id must not take the whole app down over a clock preference:
  // Intl throws on a bad zone, so it is tried once here and dropped if bad.
  try {
    if (id) new Intl.DateTimeFormat('vi-VN', { timeZone: id });
    ZONE.id = id || null;
  } catch {
    ZONE.id = null;
  }
  rebuild();
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

/**
 * "Tháng 3, 2026" → "March 2026" → "2026년 3월".
 *
 * The server composes this one (Profiles.MonthLabel) and the dictionary cannot
 * help: t() normalises every digit to {}, so "Tháng 3" and "Tháng 9" share a
 * shape and a single entry could never name the month. The pattern is fixed and
 * server-owned, so it is read back and re-formatted in the reader's language.
 * Anything that does not match is returned untouched.
 */
const MONTH_LABEL = /^Tháng (\d{1,2}),\s*(\d{4})$/;

export function monthLabel(text) {
  const m = typeof text === 'string' && text.match(MONTH_LABEL);
  if (!m) return text;
  if (LOCALE.tag.startsWith('vi')) return text;

  return dateFormat({ month: 'long', year: 'numeric' })
    .format(new Date(Number(m[2]), Number(m[1]) - 1, 1));
}

export const shortDate = iso => SHORT.format(parseIso(iso));
export const longDate = iso => LONG.format(parseIso(iso));
export const dateTime = value => TIME.format(new Date(value));

/**
 * A formatter for an INSTANT — carries the display timezone, unlike
 * dateFormat(). The zone rides inside the options, so the shared cache keys the
 * two apart on its own.
 */
export const timeFormat = options =>
  dateFormat(ZONE.id ? { ...options, timeZone: ZONE.id } : options);

/**
 * Just the clock, in the reader's language.
 *
 * dateTime() carries the date too, and slicing the first five characters off it
 * to "get the time" only works in a language that happens to put the time last:
 * Vietnamese renders "22:42 26-08" and Japanese renders "08/26 22:42", so the
 * same slice showed a date to half the readers.
 */
export const clockTime = value =>
  timeFormat({ hour: '2-digit', minute: '2-digit' }).format(new Date(value));
export const dateRangeLabel = (a, b) => `${shortDate(a)} – ${shortDate(b)}`;

export function debounce(fn, wait = 260) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), wait);
  };
}

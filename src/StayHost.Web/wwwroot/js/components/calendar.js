import { esc, isoOf, parseIso, todayIso } from '../util.js';
import { state } from '../store.js';

const DOW = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
const MONTHS = ['Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6',
                'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12'];

/**
 * Two-month range picker. The first click after a complete range starts a new
 * range; the second click closes it (swapping if the user picked backwards).
 */
export function renderCalendar(anchorIso, months = 2) {
  const anchor = parseIso(anchorIso || state.checkIn);
  const panels = Array.from({ length: months }, (_, i) =>
    new Date(anchor.getFullYear(), anchor.getMonth() + i, 1, 12));

  return `
    <div class="cal-wrap" data-months="${months}">
      ${panels.map((m, i) => renderMonth(m, i === 0, i === panels.length - 1)).join('')}
    </div>
  `;
}

function renderMonth(monthStart, isFirst, isLast) {
  const year = monthStart.getFullYear();
  const month = monthStart.getMonth();
  const daysInMonth = new Date(year, month + 1, 0).getDate();

  // Monday-first offset.
  const lead = (new Date(year, month, 1).getDay() + 6) % 7;
  const today = todayIso();

  const blocked = new Set(state.detail?.unavailableDates ?? []);

  const cells = [];
  for (let i = 0; i < lead; i++) cells.push('<span></span>');

  for (let d = 1; d <= daysInMonth; d++) {
    const iso = isoOf(new Date(year, month, d, 12));
    const past = iso < today || blocked.has(iso);
    const isIn = iso === state.checkIn;
    const isOut = iso === state.checkOut;
    const between = iso > state.checkIn && iso < state.checkOut;

    const cls = ['cal-day'];
    if (isIn || isOut) cls.push('is-edge');
    else if (between) cls.push('is-between');

    cells.push(`
      <button class="${cls.join(' ')}" data-act="pick-date" data-date="${iso}"
              ${past ? 'disabled' : ''} aria-label="${esc(d)} ${esc(MONTHS[month])} ${esc(year)}">${d}</button>
    `);
  }

  return `
    <div class="cal-month">
      <div class="cal-head">
        ${isFirst
          ? `<button class="round-btn" data-act="cal-shift" data-dir="-1" aria-label="Tháng trước">‹</button>`
          : '<span style="width:28px"></span>'}
        <b>${esc(MONTHS[month])} ${esc(year)}</b>
        ${isLast
          ? `<button class="round-btn" data-act="cal-shift" data-dir="1" aria-label="Tháng sau">›</button>`
          : '<span style="width:28px"></span>'}
      </div>
      <div class="cal-grid" role="grid">
        ${DOW.map(d => `<span class="cal-dow">${d}</span>`).join('')}
        ${cells.join('')}
      </div>
    </div>
  `;
}

import { useStore } from '../lib/useStore.js';
import { pickDate, shiftCalendar } from '../lib/store.js';
import { isoOf, parseIso, todayIso } from '../lib/format.js';

const DOW = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
const MONTHS = ['Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6',
                'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12'];

/**
 * Range picker. The first click after a complete range starts a new one; the
 * second closes it. Nights the listing has already sold are disabled.
 */
export function Calendar({ months = 2 }) {
  const state = useStore();
  const anchor = parseIso(state.checkIn);
  const panels = Array.from({ length: months }, (_, i) =>
    new Date(anchor.getFullYear(), anchor.getMonth() + i, 1, 12));

  return (
    <div className="cal-wrap" data-months={months}>
      {panels.map((m, i) =>
        <Month key={`${m.getFullYear()}-${m.getMonth()}`} monthStart={m}
               isFirst={i === 0} isLast={i === panels.length - 1} state={state} />)}
    </div>
  );
}

function Month({ monthStart, isFirst, isLast, state }) {
  const year = monthStart.getFullYear();
  const month = monthStart.getMonth();
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const lead = (new Date(year, month, 1).getDay() + 6) % 7;   // Monday-first
  const today = todayIso();
  const blocked = new Set(state.detail?.unavailableDates ?? []);

  const cells = [];
  for (let i = 0; i < lead; i++) cells.push(<span key={`lead${i}`} />);

  for (let d = 1; d <= daysInMonth; d++) {
    const iso = isoOf(new Date(year, month, d, 12));
    const disabled = iso < today || blocked.has(iso);
    const edge = iso === state.checkIn || iso === state.checkOut;
    const between = iso > state.checkIn && iso < state.checkOut;

    cells.push(
      <button key={iso} type="button" disabled={disabled}
              className={`cal-day ${edge ? 'is-edge' : between ? 'is-between' : ''}`}
              onClick={() => pickDate(iso)}
              aria-label={`${d} ${MONTHS[month]} ${year}`}>{d}</button>
    );
  }

  return (
    <div className="cal-month">
      <div className="cal-head">
        {isFirst
          ? <button type="button" className="round-btn" onClick={() => shiftCalendar(-1)} aria-label="Tháng trước">‹</button>
          : <span style={{ width: 28 }} />}
        <b>{MONTHS[month]} {year}</b>
        {isLast
          ? <button type="button" className="round-btn" onClick={() => shiftCalendar(1)} aria-label="Tháng sau">›</button>
          : <span style={{ width: 28 }} />}
      </div>
      <div className="cal-grid" role="grid">
        {DOW.map(d => <span className="cal-dow" key={d}>{d}</span>)}
        {cells}
      </div>
    </div>
  );
}

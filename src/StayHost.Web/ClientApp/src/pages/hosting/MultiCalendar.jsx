import { useEffect, useState } from 'react';
import { api } from '../../lib/api.js';
import { set, loadHostCalendar, toast } from '../../lib/store.js';
import { shortMoney, isoOf, parseIso } from '../../lib/format.js';

const DOW = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];

/**
 * docs/01 QL-04 — one row per listing, one column per day, scrolling sideways.
 * A host with several places sees the whole month without opening each one.
 */
export function MultiCalendar() {
  const [data, setData] = useState(null);
  const [from, setFrom] = useState(() => isoOf(new Date()));
  const [error, setError] = useState(null);

  useEffect(() => {
    api.multiCalendar({ from, days: 45 }).then(setData).catch(e => setError(e.message));
  }, [from]);

  const shift = days => {
    const d = parseIso(from);
    d.setDate(d.getDate() + days);
    setFrom(isoOf(d));
  };

  if (error) return <div className="empty-state" style={{ marginTop: 24 }}><h3>{error}</h3></div>;
  if (!data) return <div className="stat skeleton" style={{ height: 240, border: 0, marginTop: 24 }} />;

  if (!data.rows.length) {
    return (
      <div className="empty-state" style={{ marginTop: 24 }}>
        <h3>Chưa có chỗ nghỉ nào để xem lịch</h3>
      </div>
    );
  }

  return (
    <div style={{ marginTop: 24 }}>
      <div className="page-head" style={{ marginBottom: 12 }}>
        <p className="section-sub" style={{ margin: 0 }}>
          {data.rows.length} chỗ nghỉ · {data.days} ngày từ {from}
        </p>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="round-btn" onClick={() => shift(-30)} aria-label="Lùi 30 ngày">‹</button>
          <button className="btn btn-outline btn-sm" onClick={() => setFrom(isoOf(new Date()))}>Hôm nay</button>
          <button className="round-btn" onClick={() => shift(30)} aria-label="Tiến 30 ngày">›</button>
        </div>
      </div>

      <div className="multi-cal">
        <div className="multi-cal-scroll">
          <div className="multi-cal-head">
            <div className="multi-cal-name" />
            {data.rows[0].days.map(d => {
              const date = parseIso(d.date);
              return (
                <div className="multi-cal-day" key={d.date}
                     data-weekend={date.getDay() === 0 || date.getDay() === 6}>
                  <b>{date.getDate()}</b>
                  <i>{DOW[date.getDay()]}</i>
                </div>
              );
            })}
          </div>

          {data.rows.map(row => (
            <div className="multi-cal-row" key={row.listingId}>
              <button className="multi-cal-name" onClick={async () => {
                await loadHostCalendar(row.listingId);
                set({ overlay: 'host-block', hostMonthOffset: 0 });
              }}>
                <b>{row.title}</b>
                <span>{row.isPublished ? 'Đang hiển thị' : 'Bản nháp'}</span>
              </button>

              {row.days.map(d => (
                <div className={`multi-cal-cell is-${d.state}`} key={d.date}
                     title={`${d.date} · ${d.state === 'booked' ? d.bookingReference : d.state === 'blocked' ? 'đã khoá' : 'còn trống'}`}
                     onClick={() => d.state === 'booked' && toast(`Đơn ${d.bookingReference}`)}>
                  {d.state === 'open' ? shortMoney(d.rate) : d.state === 'booked' ? '●' : '×'}
                </div>
              ))}
            </div>
          ))}
        </div>
      </div>

      <div className="host-cal-legend" style={{ marginTop: 12 }}>
        <span><i className="sw booked" /> Đã có khách</span>
        <span><i className="sw blocked" /> Bạn khoá</span>
        <span><i className="sw seasonal" /> Còn trống (hiện giá)</span>
      </div>
    </div>
  );
}

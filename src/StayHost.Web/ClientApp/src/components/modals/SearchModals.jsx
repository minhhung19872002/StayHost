import { useStore } from '../../lib/useStore.js';
import {
  set, state as store, activeFilterCount, resetFilters, totalGuests,
  toggleAmenity, bumpCount, bumpGuest, applyDatePreset, clearDates,
  applyCurrency, applyLanguage, closeOverlay, toast
} from '../../lib/store.js';
import { api } from '../../lib/api.js';
import { applySearch } from '../../lib/nav.js';
import { money, longDate, nightsBetween } from '../../lib/format.js';
import { Icon, AmenityIcon, CATEGORY_ICON } from '../Icon.jsx';
import { Calendar } from '../Calendar.jsx';
import { Modal, CountRow } from './Modal.jsx';

const SORTS = [
  ['reco', 'Đề xuất cho bạn'], ['low', 'Giá thấp đến cao'], ['high', 'Giá cao đến thấp'],
  ['rating', 'Đánh giá cao nhất'], ['reviews', 'Nhiều đánh giá nhất']
];

export function FiltersModal() {
  const state = useStore();
  const meta = state.meta;
  if (!meta) return <Modal title="Bộ lọc"><p>Đang tải…</p></Modal>;

  const span = Math.max(1, meta.maxPrice - meta.minPrice);
  const lowPct = ((state.minPrice - meta.minPrice) / span) * 100;
  const highPct = ((state.maxPrice - meta.minPrice) / span) * 100;
  const peak = Math.max(...meta.priceHistogram, 1);

  const groups = meta.amenities.reduce((acc, a) => {
    (acc[a.group] ||= []).push(a);
    return acc;
  }, {});

  // Dragging only repaints the slider; the search runs once the handle is dropped.
  const dragMin = v => set({ minPrice: Math.min(Number(v), state.maxPrice - 100000) });
  const dragMax = v => set({ maxPrice: Math.max(Number(v), state.minPrice + 100000) });

  return (
    <Modal title="Bộ lọc" foot={<>
      <button className="text-btn" onClick={() => { resetFilters(); set({ q: '' }); applySearch(); }}>Xoá tất cả</button>
      <button className="btn btn-dark btn-sm" onClick={closeOverlay}>
        Hiện {state.results.total} chỗ nghỉ{activeFilterCount() ? ` (${activeFilterCount()} bộ lọc)` : ''}
      </button>
    </>}>
      <section className="modal-section">
        <h3>Khoảng giá</h3>
        <span className="hint">Giá mỗi đêm, đã gồm phí và thuế</span>
        <div className="histogram">
          {meta.priceHistogram.map((h, i) => {
            const at = meta.minPrice + (span * i) / (meta.priceHistogram.length - 1);
            return <i key={i} className={at >= state.minPrice && at <= state.maxPrice ? 'in' : ''}
                      style={{ height: `${Math.max(6, (h / peak) * 100)}%` }} />;
          })}
        </div>
        <div className="range-wrap">
          <span className="range-track" />
          <span className="range-fill" style={{ left: `${lowPct}%`, right: `${100 - highPct}%` }} />
          <input type="range" min={meta.minPrice} max={meta.maxPrice} step={100000}
                 value={state.minPrice} aria-label="Giá tối thiểu"
                 onChange={e => dragMin(e.target.value)} onMouseUp={() => applySearch()} onTouchEnd={() => applySearch()} />
          <input type="range" min={meta.minPrice} max={meta.maxPrice} step={100000}
                 value={state.maxPrice} aria-label="Giá tối đa"
                 onChange={e => dragMax(e.target.value)} onMouseUp={() => applySearch()} onTouchEnd={() => applySearch()} />
        </div>
        <div className="range-vals">
          <label><span className="cap">Tối thiểu</span><div className="amt">{money(state.minPrice)}</div></label>
          <label><span className="cap">Tối đa</span>
            <div className="amt">{money(state.maxPrice)}{state.maxPrice >= meta.maxPrice ? '+' : ''}</div></label>
        </div>
      </section>

      <section className="modal-section">
        <h3>Loại nơi ở</h3>
        <span className="hint">Bạn muốn ở trọn chỗ nghỉ hay chia sẻ với người khác?</span>
        <div className="opt-grid">
          {meta.roomTypes.map(r => (
            <button key={r.key} className={`opt ${state.roomType === r.key ? 'is-on' : ''}`}
                    onClick={() => { set({ roomType: r.key }); applySearch(); }}>
              <b>{r.label}</b><span>{r.hint}</span>
            </button>
          ))}
        </div>
      </section>

      <section className="modal-section">
        <h3>Phòng và giường</h3>
        {[['Phòng ngủ', 'bedrooms'], ['Giường', 'beds'], ['Phòng tắm', 'bathrooms']].map(([label, key]) => (
          <CountRow key={key} label={label} value={state[key]}
                    display={state[key] ? `${state[key]}+` : 'Bất kỳ'}
                    decDisabled={state[key] <= 0}
                    onDec={() => { bumpCount(key, -1); applySearch(); }}
                    onInc={() => { bumpCount(key, 1); applySearch(); }} />
        ))}
      </section>

      <section className="modal-section">
        <h3>Loại chỗ ở</h3>
        <div className="pill-row" style={{ marginTop: 14 }}>
          {meta.categories.map(c => (
            <button key={c.key} className={`pill ${state.category === c.key ? 'is-on' : ''}`}
                    onClick={() => { set({ category: c.key }); applySearch(); }}>
              <Icon name={CATEGORY_ICON[c.key] ?? 'all'} size={17} /> {c.label} ({c.count})
            </button>
          ))}
        </div>
      </section>

      {Object.entries(groups).map(([group, items]) => (
        <section className="modal-section" key={group}>
          <h3>{group}</h3>
          <div className="pill-row" style={{ marginTop: 14 }}>
            {items.map(a => (
              <button key={a.key} className={`pill ${state.amenities.includes(a.key) ? 'is-on' : ''}`}
                      aria-pressed={state.amenities.includes(a.key)}
                      onClick={() => { toggleAmenity(a.key); applySearch(); }}>
                <AmenityIcon name={a.key} size={17} /> {a.label}
              </button>
            ))}
          </div>
        </section>
      ))}

      <section className="modal-section">
        <h3>Lựa chọn nổi bật</h3>
        <div className="pill-row" style={{ marginTop: 14 }}>
          <button className={`pill ${state.superhostOnly ? 'is-on' : ''}`}
                  onClick={() => { set({ superhostOnly: !state.superhostOnly }); applySearch(); }}>◈ Siêu chủ nhà</button>
          <button className={`pill ${state.guestFavoriteOnly ? 'is-on' : ''}`}
                  onClick={() => { set({ guestFavoriteOnly: !state.guestFavoriteOnly }); applySearch(); }}>♥ Khách yêu thích</button>
        </div>
      </section>

      <section className="modal-section">
        <h3>Sắp xếp kết quả</h3>
        <select className="field" style={{ marginTop: 14 }} value={state.sort}
                onChange={e => { set({ sort: e.target.value }); applySearch(); }}>
          {SORTS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
        </select>
      </section>
    </Modal>
  );
}

export function DatesModal() {
  const state = useStore();
  const nights = nightsBetween(state.checkIn, state.checkOut);

  return (
    <Modal title="Chọn ngày" foot={<>
      <button className="text-btn" onClick={() => { clearDates(); applySearch(); }}>Xoá ngày</button>
      <button className="btn btn-dark btn-sm" onClick={closeOverlay}>Xong</button>
    </>}>
      <div style={{ marginBottom: 20 }}>
        <h3 style={{ margin: 0, fontSize: 20, fontWeight: 800 }}>{nights} đêm</h3>
        <p style={{ margin: '4px 0 0', fontSize: 14, color: 'var(--ink-muted)' }}>
          {longDate(state.checkIn)} – {longDate(state.checkOut)}
        </p>
      </div>
      <Calendar months={2} />
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 20 }}>
        {[['Cuối tuần này', 'weekend'], ['1 tuần', 'week'], ['2 tuần', 'fortnight'], ['1 tháng', 'month']].map(([label, key]) => (
          <button className="pill" key={key} onClick={() => { applyDatePreset(key); applySearch(); }}>{label}</button>
        ))}
      </div>
    </Modal>
  );
}

const GUEST_ROWS = [
  ['adults', 'Người lớn', 'Từ 13 tuổi trở lên'],
  ['children', 'Trẻ em', 'Độ tuổi 2 – 12'],
  ['infants', 'Em bé', 'Dưới 2 tuổi'],
  ['pets', 'Thú cưng', 'Bạn mang theo thú hỗ trợ?']
];

export function GuestsModal() {
  const state = useStore();

  return (
    <Modal title="Khách" size="narrow" foot={<>
      <span style={{ fontSize: 13, color: 'var(--ink-muted)' }}>Tổng {totalGuests()} khách</span>
      <button className="btn btn-dark btn-sm" onClick={closeOverlay}>Xong</button>
    </>}>
      {GUEST_ROWS.map(([key, label, hint]) => (
        <CountRow key={key} label={label} hint={hint} value={state.guests[key]}
                  decDisabled={state.guests[key] <= (key === 'adults' ? 1 : 0)}
                  onDec={() => { bumpGuest(key, -1); applySearch(); }}
                  onInc={() => { bumpGuest(key, 1); applySearch(); }} />
      ))}
    </Modal>
  );
}

export function LanguageModal() {
  const state = useStore();
  const meta = state.meta;
  if (!meta) return <Modal title="Ngôn ngữ"><p>Đang tải…</p></Modal>;

  return (
    <Modal title="Ngôn ngữ & tiền tệ">
      <section className="modal-section">
        <h3>Ngôn ngữ đề xuất</h3>
        <div className="lang-grid" style={{ marginTop: 14 }}>
          {meta.languages.map(l => (
            <button key={l.code} className={`lang ${state.language.code === l.code ? 'is-on' : ''}`}
                    onClick={() => applyLanguage(l.code)}>
              <b>{l.label}</b><span>{l.region}</span>
            </button>
          ))}
        </div>
      </section>
      <section className="modal-section">
        <h3>Chọn loại tiền tệ</h3>
        <div className="lang-grid" style={{ marginTop: 14 }}>
          {meta.currencies.map(c => (
            <button key={c.code} className={`lang ${state.currency.code === c.code ? 'is-on' : ''}`}
                    onClick={() => applyCurrency(c.code)}>
              <b>{c.label}</b><span>{c.code} — {c.symbol}</span>
            </button>
          ))}
        </div>
      </section>
    </Modal>
  );
}

const HELP = [
  ['Tôi cần thay đổi ngày đặt chỗ', 'Vào Chuyến đi của tôi → chọn đặt chỗ → Chỉnh sửa ngày.'],
  ['Chủ nhà chưa phản hồi', 'Sau 24 giờ, StayHost sẽ tự huỷ và hoàn tiền toàn bộ.'],
  ['Tôi muốn được hoàn tiền', 'Huỷ trước 48 giờ nhận phòng để được hoàn 100% tiền phòng.'],
  ['Liên hệ hỗ trợ 24/7', 'Hotline 1900 1234 hoặc chat trực tiếp trong ứng dụng.']
];

export function HelpModal() {
  return (
    <Modal title="Trung tâm trợ giúp" size="narrow">
      <div style={{ display: 'grid', gap: 14 }}>
        {HELP.map(([q, a]) => (
          <div key={q} style={{ border: '1px solid var(--divider)', borderRadius: 12, padding: 16 }}>
            <b style={{ fontSize: 14.5 }}>{q}</b>
            <p style={{ margin: '6px 0 0', fontSize: 13.5, color: 'var(--ink-muted)', lineHeight: 1.55 }}>{a}</p>
          </div>
        ))}
      </div>
    </Modal>
  );
}

const REPORT_REASONS = ['Thông tin không chính xác', 'Không phải chỗ nghỉ thật', 'Lừa đảo',
                        'Nội dung xúc phạm', 'Lý do khác'];

export function ReportModal() {
  const send = async reason => {
    try {
      const res = await api.report({ listingId: store.detail?.card.id, reason, detail: null });
      closeOverlay();
      toast(res.message);
    } catch (err) { toast(err.message); }
  };

  return (
    <Modal title="Báo cáo chỗ nghỉ này" size="narrow">
      <p style={{ margin: '0 0 16px', fontSize: 13.5, color: 'var(--ink-muted)', lineHeight: 1.6 }}>
        Đội an toàn StayHost sẽ xem xét báo cáo của bạn.
      </p>
      <div style={{ display: 'grid', gap: 8 }}>
        {REPORT_REASONS.map(r => (
          <button className="opt" key={r} onClick={() => send(r)}><b>{r}</b></button>
        ))}
      </div>
    </Modal>
  );
}

export function ContactHostModal() {
  const state = useStore();
  const h = state.detail?.host;
  if (!h) return null;

  return (
    <Modal title={`Nhắn tin cho ${h.name}`} size="narrow">
      <p style={{ fontSize: 14, color: 'var(--ink-muted)', lineHeight: 1.6, margin: '0 0 16px' }}>
        {h.name} thường phản hồi {h.responseTime} · tỉ lệ phản hồi {h.responseRate}.
      </p>
      <label className="form-field">
        <span className="cap">Tin nhắn</span>
        <textarea rows={5} placeholder={`Chào ${h.name}, mình muốn hỏi về...`}
                  style={{ width: '100%', padding: '12px 14px', border: '1px solid var(--line)', borderRadius: 12, fontSize: 14 }} />
      </label>
      <button className="btn btn-primary btn-block"
              onClick={() => toast('Bản demo — chức năng này chưa kết nối dịch vụ thật.')}>Gửi tin nhắn</button>
    </Modal>
  );
}

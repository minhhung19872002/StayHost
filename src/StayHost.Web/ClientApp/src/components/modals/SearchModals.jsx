import { useState, useEffect } from 'react';
import { useStore } from '../../lib/useStore.js';
import {
  set, state as store, activeFilterCount, resetFilters, totalGuests,
  toggleAmenity, toggleHostLanguage, bumpCount, bumpGuest, applyDatePreset, clearDates, setStayShape,
  applyCurrency, applyLanguage, closeOverlay, settleDates, toast,
  shareViaDevice, copyShareLink, saveCurrentSearch
} from '../../lib/store.js';
import { api } from '../../lib/api.js';
import { applySearch } from '../../lib/nav.js';
import { money, longDate, nightsBetween } from '../../lib/format.js';
import { t } from '../../lib/i18n.js';
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

  // docs/01 TM-18 — the spoken-language set hosts choose from, for the filter chips.
  // Declared before the early return so the hook order never changes.
  const [spokenLangs, setSpokenLangs] = useState([]);
  useEffect(() => { api.profileOptions().then(setSpokenLangs).catch(() => setSpokenLangs([])); }, []);

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
      {/* docs/01 TM-23 — save this search to be alerted about new matches. */}
      {state.user && (
        <button className="btn btn-outline btn-sm" onClick={saveCurrentSearch}>Lưu tìm kiếm</button>
      )}
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

      {/*
        * docs/01 TM-15 — the four booking options as one group. Two are flags on
        * the search, two are amenities; a guest filtering for "tự nhận phòng"
        * should not have to know which is which.
        */}
      <section className="modal-section">
        <h3>Tuỳ chọn đặt</h3>
        <div className="pill-row" style={{ marginTop: 14 }}>
          <button className={`pill ${state.instantBookOnly ? 'is-on' : ''}`}
                  aria-pressed={state.instantBookOnly}
                  onClick={() => { set({ instantBookOnly: !state.instantBookOnly }); applySearch(); }}>
            <Icon name="ev" size={17} /> Đặt ngay
          </button>
          <button className={`pill ${state.amenities.includes('selfcheckin') ? 'is-on' : ''}`}
                  aria-pressed={state.amenities.includes('selfcheckin')}
                  onClick={() => { toggleAmenity('selfcheckin'); applySearch(); }}>
            <AmenityIcon name="selfcheckin" size={17} /> Tự nhận phòng
          </button>
          <button className={`pill ${state.amenities.includes('pet') ? 'is-on' : ''}`}
                  aria-pressed={state.amenities.includes('pet')}
                  onClick={() => { toggleAmenity('pet'); applySearch(); }}>
            <AmenityIcon name="pet" size={17} /> Cho thú cưng
          </button>
          <button className={`pill ${state.freeCancellationOnly ? 'is-on' : ''}`}
                  aria-pressed={state.freeCancellationOnly}
                  onClick={() => { set({ freeCancellationOnly: !state.freeCancellationOnly }); applySearch(); }}>
            <Icon name="heart" size={17} /> Huỷ miễn phí
          </button>
        </div>
      </section>

      <section className="modal-section">
        <h3>Lựa chọn nổi bật</h3>
        <div className="pill-row" style={{ marginTop: 14 }}>
          <button className={`pill ${state.superhostOnly ? 'is-on' : ''}`}
                  onClick={() => { set({ superhostOnly: !state.superhostOnly }); applySearch(); }}>◈ Siêu chủ nhà</button>
          <button className={`pill ${state.guestFavoriteOnly ? 'is-on' : ''}`}
                  onClick={() => { set({ guestFavoriteOnly: !state.guestFavoriteOnly }); applySearch(); }}>♥ Khách yêu thích</button>
        </div>
      </section>

      {/* docs/01 TM-18 — lọc theo ngôn ngữ chủ nhà nói được. */}
      {spokenLangs.length > 0 && (
        <section className="modal-section">
          <h3>Ngôn ngữ chủ nhà</h3>
          <div className="pill-row" style={{ marginTop: 14 }}>
            {spokenLangs.map(l => (
              <button key={l.code}
                      className={`pill ${state.hostLanguages.includes(l.code) ? 'is-on' : ''}`}
                      aria-pressed={state.hostLanguages.includes(l.code)}
                      onClick={() => { toggleHostLanguage(l.code); applySearch(); }}>{l.label}</button>
            ))}
          </div>
        </section>
      )}

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

/**
 * Everything inside the date picker, without the box around it: the header bar
 * drops it under the search field, a narrow screen gets it in a modal. Keeping
 * one copy is what stops the two going their separate ways.
 */
export function DateFields() {
  const state = useStore();
  const nights = nightsBetween(state.checkIn, state.checkOut);
  const flexible = state.stay !== 'exact' || state.flexDays > 0;

  const tab = state.stay === 'months' ? 'months' : flexible ? 'flex' : 'exact';
  const pick = next => {
    if (next === 'exact') setStayShape({ stay: 'exact', flexDays: 0 });
    else if (next === 'flex') setStayShape({ stay: 'exact', flexDays: 3 });
    else setStayShape({ stay: 'months', flexDays: 0, stayMonths: 1 });
    applySearch();
  };

  return <>
    <div className="date-tabs">
      {[['exact', 'Ngày cụ thể'], ['flex', 'Linh hoạt'], ['months', 'Theo tháng']].map(([key, label]) => (
        <button key={key} className={`date-tab ${tab === key ? 'is-on' : ''}`} onClick={() => pick(key)}>{label}</button>
      ))}
    </div>

    {tab === 'months' ? <MonthPicker state={state} /> : <>
      <div style={{ margin: '18px 0' }}>
        {state.pickingFrom ? <>
          <h3 style={{ margin: 0, fontSize: 20, fontWeight: 800 }}>Chọn ngày trả phòng</h3>
          <p style={{ margin: '4px 0 0', fontSize: 14, color: 'var(--ink-muted)' }}>
            Nhận phòng {longDate(state.pickingFrom)}
          </p>
        </> : <>
          <h3 style={{ margin: 0, fontSize: 20, fontWeight: 800 }}>{nights} đêm</h3>
          <p style={{ margin: '4px 0 0', fontSize: 14, color: 'var(--ink-muted)' }}>
            {longDate(state.checkIn)} – {longDate(state.checkOut)}
          </p>
        </>}
      </div>
      <Calendar months={2} />

      {tab === 'flex' && <FlexRow state={state} />}

      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 20 }}>
        {[['Cuối tuần này', 'weekend'], ['1 tuần', 'week'], ['2 tuần', 'fortnight'], ['1 tháng', 'month']].map(([label, key]) => (
          <button className="pill" key={key} onClick={() => { applyDatePreset(key); applySearch(); }}>{label}</button>
        ))}
      </div>
    </>}
  </>;
}

export function DatesModal() {
  return (
    <Modal title="Chọn ngày" foot={<>
      <button className="text-btn" onClick={() => { clearDates(); applySearch(); }}>Xoá ngày</button>
      <button className="btn btn-dark btn-sm" onClick={() => { settleDates(); closeOverlay(); }}>Xong</button>
    </>}>
      <DateFields />
    </Modal>
  );
}

/** docs/01 TM-06 — the same trip, a few days either side. */
function FlexRow({ state }) {
  return (
    <div style={{ marginTop: 18 }}>
      <p style={{ margin: '0 0 10px', fontSize: 14, fontWeight: 700 }}>Xê dịch được bao nhiêu ngày?</p>
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        {[0, 1, 2, 3, 5, 7].map(d => (
          <button key={d} className={`pill ${state.flexDays === d ? 'is-on' : ''}`}
                  onClick={() => { setStayShape({ stay: 'exact', flexDays: d }); applySearch(); }}>
            {d === 0 ? 'Đúng ngày' : `± ${d} ngày`}
          </button>
        ))}
      </div>
      <p style={{ margin: '10px 0 0', fontSize: 13, color: 'var(--ink-muted)' }}>
        Chúng tôi giữ nguyên số đêm và tìm chỗ còn trống ở những ngày gần nhất.
      </p>
    </div>
  );
}

/** docs/01 TM-07 — how many months, starting from which month. */
function MonthPicker({ state }) {
  const now = new Date();
  const months = Array.from({ length: 12 }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() + 1 + i, 1);
    return { key: `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`, d };
  });

  const toggle = key => {
    const on = state.startMonths.includes(key);
    setStayShape({ startMonths: on ? state.startMonths.filter(m => m !== key) : [...state.startMonths, key] });
    applySearch();
  };

  return (
    <div style={{ marginTop: 18 }}>
      <p style={{ margin: '0 0 10px', fontSize: 14, fontWeight: 700 }}>Ở bao nhiêu tháng?</p>
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        {[1, 2, 3, 6, 12].map(m => (
          <button key={m} className={`pill ${state.stayMonths === m ? 'is-on' : ''}`}
                  onClick={() => { setStayShape({ stayMonths: m }); applySearch(); }}>{m} tháng</button>
        ))}
      </div>

      <p style={{ margin: '18px 0 10px', fontSize: 14, fontWeight: 700 }}>Bắt đầu từ tháng nào?</p>
      <div className="month-grid">
        {months.map(({ key, d }) => (
          <button key={key} className={`month-cell ${state.startMonths.includes(key) ? 'is-on' : ''}`}
                  onClick={() => toggle(key)}>
            <b>Tháng {d.getMonth() + 1}</b>
            <span>{d.getFullYear()}</span>
          </button>
        ))}
      </div>
      <p style={{ margin: '12px 0 0', fontSize: 13, color: 'var(--ink-muted)' }}>
        Chưa chọn tháng nào thì chúng tôi tìm trong ba tháng tới.
      </p>
    </div>
  );
}

const GUEST_ROWS = [
  ['adults', 'Người lớn', 'Từ 13 tuổi trở lên'],
  ['children', 'Trẻ em', 'Độ tuổi 2 – 12'],
  ['infants', 'Em bé', 'Dưới 2 tuổi'],
  ['pets', 'Thú cưng', 'Bạn mang theo thú hỗ trợ?']
];

/** The guest counters on their own, for the header dropdown and the modal alike. */
export function GuestFields() {
  const state = useStore();

  return GUEST_ROWS.map(([key, label, hint]) => (
    <CountRow key={key} label={label} hint={hint} value={state.guests[key]}
              decDisabled={state.guests[key] <= (key === 'adults' ? 1 : 0)}
              onDec={() => { bumpGuest(key, -1); applySearch(); }}
              onInc={() => { bumpGuest(key, 1); applySearch(); }} />
  ));
}

export function GuestsModal() {
  return (
    <Modal title="Khách" size="narrow" foot={<>
      <span style={{ fontSize: 13, color: 'var(--ink-muted)' }}>Tổng {totalGuests()} khách</span>
      <button className="btn btn-dark btn-sm" onClick={closeOverlay}>Xong</button>
    </>}>
      <GuestFields />
    </Modal>
  );
}

export function LanguageModal() {
  const state = useStore();
  const meta = state.meta;
  if (!meta) return <Modal title={t('Ngôn ngữ')}><p>{t('Đang tải…')}</p></Modal>;

  return (
    <Modal title={t('Ngôn ngữ & tiền tệ')}>
      <section className="modal-section">
        <h3>{t('Ngôn ngữ đề xuất')}</h3>
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
        <h3>{t('Chọn loại tiền tệ')}</h3>
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

/**
 * docs/01 AT-02 — reports a listing, a person, a message or a review. Which one
 * comes from store.report, set by openReport() at the button that opened this.
 * The reasons come from the server so the four lists cannot drift from the ones
 * the domain offers.
 */
export function ReportModal() {
  const state = useStore();
  const subject = state.report ?? (store.detail?.card
    // The listing page opened this dialog directly for years; keep that working
    // rather than leave a dead button behind on a page nobody remembered to change.
    ? { target: 'listing', subjectId: store.detail.card.id, title: store.detail.card.title }
    : null);

  const [config, setConfig] = useState(null);
  const [reason, setReason] = useState(null);
  const [detail, setDetail] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!subject) return;
    api.reportReasons(subject.target).then(setConfig).catch(err => toast(err.message));
    // Only the kind of subject decides the reason list. Depending on the whole
    // subject would refetch on every render, since it is rebuilt each time.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [subject?.target]);

  if (!subject) return null;

  const send = async () => {
    setBusy(true);
    try {
      const res = await api.report({
        target: subject.target, subjectId: subject.subjectId, reason, detail: detail.trim() || null
      });
      closeOverlay();
      toast(res.message);
    } catch (err) {
      toast(err.message);
    } finally { setBusy(false); }
  };

  const label = config?.targetLabel ?? '';

  return (
    <Modal title={`Báo cáo ${label.toLowerCase()}`} size="narrow">
      {subject.title && (
        <p style={{ margin: '0 0 6px', fontSize: 14, fontWeight: 700 }}>{subject.title}</p>
      )}
      <p style={{ margin: '0 0 16px', fontSize: 13.5, color: 'var(--ink-muted)', lineHeight: 1.6 }}>
        Đội an toàn StayHost sẽ xem xét báo cáo của bạn. Người bị báo cáo không biết ai đã gửi.
      </p>

      {!config ? (
        <p style={{ fontSize: 13.5, color: 'var(--ink-muted)' }}>Đang tải…</p>
      ) : (
        <>
          <div style={{ display: 'grid', gap: 8 }}>
            {config.reasons.map(r => (
              <button className="opt" key={r} aria-pressed={reason === r}
                      style={reason === r ? { borderColor: 'var(--ink)', borderWidth: 2 } : undefined}
                      onClick={() => setReason(r)}><b>{r}</b></button>
            ))}
          </div>

          <label className="form-field" style={{ marginTop: 14 }}>
            <span className="cap">Mô tả thêm (không bắt buộc)</span>
            <textarea rows={3} value={detail} maxLength={2000}
                      onChange={e => setDetail(e.target.value)}
                      placeholder="Kể thêm chuyện gì đã xảy ra, càng cụ thể càng dễ xử lý."
                      style={{ width: '100%', padding: '12px 14px', border: '1px solid var(--line)',
                               borderRadius: 12, fontSize: 14 }} />
          </label>

          <button className="btn btn-primary" style={{ width: '100%', marginTop: 8 }}
                  disabled={!reason || busy} onClick={send}>
            {busy ? 'Đang gửi…' : 'Gửi báo cáo'}
          </button>
        </>
      )}
    </Modal>
  );
}

/**
 * docs/01 TĐ-18 — "qua link, mạng xã hội, email".
 *
 * Every destination is an ordinary link the browser opens. No provider script is
 * embedded: a share button that loads Facebook's SDK reports every visitor who
 * merely looked at the listing page, whether or not they ever shared anything.
 */
const SHARE_TARGETS = [
  ['Facebook', u => `https://www.facebook.com/sharer/sharer.php?u=${u}`],
  ['X (Twitter)', (u, t) => `https://twitter.com/intent/tweet?url=${u}&text=${t}`],
  ['WhatsApp', (u, t) => `https://wa.me/?text=${t}%20${u}`],
  ['Telegram', (u, t) => `https://t.me/share/url?url=${u}&text=${t}`],
  ['Messenger', u => `https://www.facebook.com/dialog/send?link=${u}&app_id=0&redirect_uri=${u}`]
];

export function ShareModal() {
  const state = useStore();
  const share = state.share;
  if (!share) return null;

  const u = encodeURIComponent(share.url);
  const t = encodeURIComponent(share.title);

  const email = `mailto:?subject=${t}&body=${encodeURIComponent(`${share.title}

${share.url}`)}`;

  return (
    <Modal title="Chia sẻ chỗ nghỉ này" size="narrow">
      <p style={{ margin: '0 0 16px', fontSize: 14, fontWeight: 600 }}>{share.title}</p>

      <div style={{ display: 'grid', gap: 8 }}>
        <button className="opt" onClick={() => copyShareLink(share.url)}>
          <b>Sao chép liên kết</b>
        </button>

        <a className="opt" href={email}><b>Gửi qua email</b></a>

        {SHARE_TARGETS.map(([label, build]) => (
          <a className="opt" key={label} target="_blank" rel="noreferrer noopener"
             href={build(u, t)}><b>{label}</b></a>
        ))}

        {/* The device's own sheet reaches apps a link never will, but only some
            browsers have it, so it is offered rather than relied on. */}
        {typeof navigator !== 'undefined' && navigator.share && (
          <button className="opt" onClick={() => shareViaDevice(share)}>
            <b>Ứng dụng khác trên máy…</b>
          </button>
        )}
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

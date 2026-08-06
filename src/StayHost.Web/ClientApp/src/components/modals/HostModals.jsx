import { useState } from 'react';
import { useStore } from '../../lib/useStore.js';
import {
  set, saveListing, removeListing, loadHostCalendar, loadHosting, closeOverlay, toast
} from '../../lib/store.js';
import { api } from '../../lib/api.js';
import { money, shortMoney, longDate, isoOf } from '../../lib/format.js';
import { AmenityIcon } from '../Icon.jsx';
import { Modal } from './Modal.jsx';

const BLANK_LISTING = {
  id: 0, title: '', city: '', typeKey: 'house', roomTypeKey: 'entire',
  bedrooms: 1, beds: 1, bathrooms: 1, maxGuests: 2,
  pricePerNight: 800000, cleaningFee: 200000, minNights: 1,
  instantBook: true, isPublished: true, cancellationTier: 'Moderate',
  description: '', highlight: '', images: [], amenityKeys: []
};

const TIERS = [
  ['Flexible', 'Linh hoạt', 'Hoàn 100% tiền phòng nếu huỷ trước 24 giờ.'],
  ['Moderate', 'Trung bình', 'Hoàn 100% nếu huỷ trước 5 ngày, sau đó 50%.'],
  ['Strict', 'Nghiêm ngặt', 'Hoàn 50% nếu huỷ trước 7 ngày, sau đó không hoàn.']
];

const ROOM_TYPES = [['entire', 'Nguyên căn'], ['private', 'Phòng riêng'], ['shared', 'Phòng chung']];

export function ListingEditorModal() {
  const state = useStore();
  const meta = state.meta;
  // The whole form is one controlled object so a pill toggle never loses typed text.
  const [form, setForm] = useState(() => ({ ...BLANK_LISTING, ...(state.editingListing ?? {}) }));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  const isNew = !form.id;
  const field = (key, value) => setForm(f => ({ ...f, [key]: value }));
  const num = (key, value) => field(key, Number(value) || 0);

  const save = async () => {
    setSaving(true);
    setError(null);
    const saved = await saveListing(form.id || null, {
      ...form,
      latitude: form.latitude ?? null,
      longitude: form.longitude ?? null
    });
    setSaving(false);
    if (saved) closeOverlay(); else setError('Không lưu được. Kiểm tra lại các trường bắt buộc.');
  };

  const upload = async files => {
    const list = Array.from(files ?? []);
    if (!list.length) return;

    set({ uploading: true });
    try {
      const body = new FormData();
      list.forEach(f => body.append('files', f));
      const res = await fetch('/api/uploads/images', { method: 'POST', body, credentials: 'same-origin' });
      const payload = await res.json().catch(() => null);
      if (!res.ok) throw new Error(payload?.message ?? 'Tải ảnh thất bại.');

      setForm(f => ({ ...f, images: [...f.images, ...payload.urls] }));
      toast(`Đã tải lên ${payload.urls.length} ảnh.`);
    } catch (err) {
      toast(err.message);
    } finally {
      set({ uploading: false });
    }
  };

  return (
    <Modal title={isNew ? 'Đăng chỗ nghỉ mới' : 'Chỉnh sửa chỗ nghỉ'} size="wide" foot={<>
      {form.id
        ? <button className="text-btn" onClick={async () => {
            if (!confirm('Xoá hẳn chỗ nghỉ này?')) return;
            closeOverlay();
            await removeListing(form.id);
          }}>Xoá chỗ nghỉ</button>
        : <span />}
      <button className="btn btn-primary btn-sm" onClick={save} disabled={saving}>
        {saving ? 'Đang lưu…' : isNew ? 'Đăng chỗ nghỉ' : 'Lưu thay đổi'}
      </button>
    </>}>
      <section className="modal-section">
        <h3>Thông tin cơ bản</h3>
        <div className="field-grid">
          <label className="form-field" style={{ gridColumn: '1/-1' }}>
            <span className="cap">Tiêu đề *</span>
            <input value={form.title} onChange={e => field('title', e.target.value)}
                   placeholder="Villa hồ bơi riêng gần biển Mỹ Khê" required />
          </label>
          <label className="form-field">
            <span className="cap">Thành phố *</span>
            <input value={form.city} onChange={e => field('city', e.target.value)}
                   placeholder="Đà Nẵng" list="city-list" required />
            <datalist id="city-list">
              {(meta?.cities ?? []).map(c => <option key={c} value={c} />)}
            </datalist>
          </label>
          <label className="form-field">
            <span className="cap">Loại chỗ ở</span>
            <select value={form.typeKey} onChange={e => field('typeKey', e.target.value)}>
              {(meta?.categories ?? []).filter(c => c.key !== 'all')
                .map(c => <option key={c.key} value={c.key}>{c.label}</option>)}
            </select>
          </label>
          <label className="form-field">
            <span className="cap">Loại nơi ở</span>
            <select value={form.roomTypeKey} onChange={e => field('roomTypeKey', e.target.value)}>
              {ROOM_TYPES.map(([v, t]) => <option key={v} value={v}>{t}</option>)}
            </select>
          </label>
          <label className="form-field">
            <span className="cap">Số khách tối đa</span>
            <input type="number" min={1} max={30} value={form.maxGuests}
                   onChange={e => num('maxGuests', e.target.value)} required />
          </label>
        </div>
      </section>

      <section className="modal-section">
        <h3>Bố trí</h3>
        <div className="field-grid">
          {[['bedrooms', 'Phòng ngủ', 0, 20], ['beds', 'Giường', 1, 40], ['bathrooms', 'Phòng tắm', 0, 20]].map(([key, label, min, max]) => (
            <label className="form-field" key={key}><span className="cap">{label}</span>
              <input type="number" min={min} max={max} value={form[key]} onChange={e => num(key, e.target.value)} /></label>
          ))}
        </div>
      </section>

      <section className="modal-section">
        <h3>Giá &amp; quy tắc</h3>
        <div className="field-grid">
          <label className="form-field"><span className="cap">Giá mỗi đêm (₫) *</span>
            <input type="number" min={50000} step={10000} value={form.pricePerNight}
                   onChange={e => num('pricePerNight', e.target.value)} required /></label>
          <label className="form-field"><span className="cap">Phí dọn dẹp (₫)</span>
            <input type="number" min={0} step={10000} value={form.cleaningFee}
                   onChange={e => num('cleaningFee', e.target.value)} /></label>
          <label className="form-field"><span className="cap">Số đêm tối thiểu</span>
            <input type="number" min={1} max={90} value={form.minNights}
                   onChange={e => num('minNights', e.target.value)} /></label>
        </div>
        <div className="pill-row" style={{ marginTop: 14 }}>
          <button type="button" className={`pill ${form.instantBook ? 'is-on' : ''}`}
                  onClick={() => field('instantBook', !form.instantBook)}>Đặt ngay không cần duyệt</button>
          <button type="button" className={`pill ${form.isPublished ? 'is-on' : ''}`}
                  onClick={() => field('isPublished', !form.isPublished)}>
            {form.isPublished ? 'Đang hiển thị công khai' : 'Đang là bản nháp'}
          </button>
        </div>
      </section>

      <section className="modal-section">
        <h3>Chính sách huỷ</h3>
        <span className="hint">Chính sách càng linh hoạt thì càng nhiều khách đặt.</span>
        <div className="opt-grid">
          {TIERS.map(([key, label, hint]) => (
            <button type="button" key={key} className={`opt ${form.cancellationTier === key ? 'is-on' : ''}`}
                    onClick={() => field('cancellationTier', key)}>
              <b>{label}</b><span>{hint}</span>
            </button>
          ))}
        </div>
      </section>

      <section className="modal-section">
        <h3>Mô tả</h3>
        <label className="form-field">
          <span className="cap">Giới thiệu chỗ nghỉ * <span style={{ fontWeight: 400 }}>(tối thiểu 40 ký tự)</span></span>
          <textarea rows={5} required value={form.description} onChange={e => field('description', e.target.value)}
                    placeholder="Kể về không gian, vị trí và điều khiến chỗ nghỉ của bạn đặc biệt."
                    style={{ width: '100%', padding: '12px 14px', border: '1px solid var(--line)', borderRadius: 12, fontSize: 14 }} />
        </label>
        <label className="form-field">
          <span className="cap">Điểm nổi bật</span>
          <input value={form.highlight ?? ''} onChange={e => field('highlight', e.target.value)}
                 placeholder="Hồ bơi riêng nhìn ra vườn dừa" />
        </label>
      </section>

      <section className="modal-section">
        <h3>Ảnh</h3>
        <span className="hint">Tải ảnh từ máy hoặc dán liên kết. Ảnh đầu tiên là ảnh bìa.</span>

        <label className="dropzone">
          <input type="file" accept="image/jpeg,image/png,image/webp,image/avif" multiple hidden
                 onChange={e => { upload(e.target.files); e.target.value = ''; }} />
          <b>{state.uploading ? 'Đang tải ảnh lên…' : 'Chọn ảnh từ máy'}</b>
          <span>JPG, PNG, WebP hoặc AVIF · tối đa 8MB mỗi ảnh</span>
        </label>

        {!!form.images.length && (
          <div className="thumb-grid">
            {form.images.map((url, i) => (
              <figure className="thumb" key={`${url}-${i}`}>
                <img src={url} alt={`Ảnh ${i + 1}`} loading="lazy" />
                {i === 0 && <figcaption>Ảnh bìa</figcaption>}
                <button type="button" className="thumb-remove" aria-label={`Xoá ảnh ${i + 1}`}
                        onClick={() => setForm(f => ({ ...f, images: f.images.filter((_, x) => x !== i) }))}>✕</button>
              </figure>
            ))}
          </div>
        )}

        <details style={{ marginTop: 14 }}>
          <summary style={{ fontSize: 13, fontWeight: 600, cursor: 'pointer' }}>Hoặc dán liên kết ảnh</summary>
          <textarea rows={4} value={form.images.join('\n')} placeholder="https://…"
                    onChange={e => field('images', e.target.value.split('\n').map(s => s.trim()).filter(Boolean))}
                    style={{ width: '100%', marginTop: 10, padding: '12px 14px', border: '1px solid var(--line)',
                             borderRadius: 12, fontSize: 13, fontFamily: 'ui-monospace,monospace' }} />
        </details>
      </section>

      <section className="modal-section">
        <h3>Tiện nghi</h3>
        <div className="pill-row" style={{ marginTop: 14 }}>
          {(meta?.amenities ?? []).map(a => (
            <button type="button" key={a.key} className={`pill ${form.amenityKeys.includes(a.key) ? 'is-on' : ''}`}
                    onClick={() => setForm(f => ({
                      ...f,
                      amenityKeys: f.amenityKeys.includes(a.key)
                        ? f.amenityKeys.filter(k => k !== a.key)
                        : [...f.amenityKeys, a.key]
                    }))}>
              <AmenityIcon name={a.key} size={16} /> {a.label}
            </button>
          ))}
        </div>
      </section>

      {error && <div className="form-error">{error}</div>}
    </Modal>
  );
}

/* --------------------------------------------------------- host calendar */

const HOST_MONTHS = ['Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6',
                     'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12'];

export function HostCalendarModal() {
  const state = useStore();
  const cal = state.hostCalendar;
  if (!cal) return null;

  const reload = () => loadHostCalendar(cal.listingId);

  const addBlock = async e => {
    e.preventDefault();
    const f = e.currentTarget;
    try {
      await api.addBlock({
        listingId: cal.listingId,
        from: f.from.value, to: f.to.value,
        note: f.note.value.trim() || null
      });
      await reload();
      toast('Đã khoá lịch.');
      f.reset();
    } catch (err) { toast(err.message); }
  };

  const addRule = async e => {
    e.preventDefault();
    const f = e.currentTarget;
    try {
      await api.addPriceRule({
        listingId: cal.listingId,
        name: f.name.value.trim(),
        from: f.from.value, to: f.to.value,
        nightlyRate: Number(f.rate.value)
      });
      await reload();
      toast('Đã thêm quy tắc giá.');
      f.reset();
    } catch (err) { toast(err.message); }
  };

  return (
    <Modal title="Lịch & giá">
      <HostMonths cal={cal} offset={state.hostMonthOffset} />

      <section className="modal-section">
        <h3>Khoá lịch</h3>
        <span className="hint">Chặn khoảng ngày bạn không muốn nhận khách (bảo trì, gia đình dùng…).</span>
        <form onSubmit={addBlock} style={{ marginTop: 14 }}>
          <div className="field-grid">
            <label className="form-field"><span className="cap">Từ ngày</span><input type="date" name="from" required /></label>
            <label className="form-field"><span className="cap">Đến ngày</span><input type="date" name="to" required /></label>
          </div>
          <label className="form-field"><span className="cap">Ghi chú</span><input name="note" placeholder="Bảo trì hồ bơi" /></label>
          <button type="submit" className="btn btn-dark btn-block">Khoá lịch</button>
        </form>
      </section>

      <section className="modal-section">
        <h3>Giá theo mùa</h3>
        <span className="hint">
          Giá cơ bản hiện tại: <b>{money(cal.basePrice ?? 0)}</b> / đêm.
          Quy tắc mùa sẽ thay thế giá này trong khoảng ngày đã chọn.
        </span>
        <form onSubmit={addRule} style={{ marginTop: 14 }}>
          <div className="field-grid">
            <label className="form-field" style={{ gridColumn: '1/-1' }}><span className="cap">Tên đợt</span>
              <input name="name" placeholder="Tết Nguyên đán" required /></label>
            <label className="form-field"><span className="cap">Từ ngày</span><input type="date" name="from" required /></label>
            <label className="form-field"><span className="cap">Đến ngày</span><input type="date" name="to" required /></label>
            <label className="form-field"><span className="cap">Giá mỗi đêm (₫)</span>
              <input type="number" name="rate" min={50000} step={10000}
                     defaultValue={Math.round((cal.basePrice ?? 800000) * 1.5)} required /></label>
          </div>
          <button type="submit" className="btn btn-outline btn-block">Thêm quy tắc giá</button>
        </form>

        {!!cal.priceRules?.length && (
          <div style={{ display: 'grid', gap: 8, marginTop: 14 }}>
            {cal.priceRules.map(r => (
              <div className="cal-row" key={r.id}>
                <span className="badge pending">{r.name}</span>
                <div style={{ flex: 1, minWidth: 0, fontSize: 13.5 }}>
                  {r.from} → {r.to} · <b>{money(r.nightlyRate)}</b>/đêm
                </div>
                <button className="text-btn" onClick={async () => {
                  try { await api.removePriceRule(r.id); await reload(); toast('Đã bỏ quy tắc giá.'); }
                  catch (err) { toast(err.message); }
                }}>Bỏ</button>
              </div>
            ))}
          </div>
        )}
      </section>

      {cal.bookings?.length ? (
        <div style={{ marginTop: 24 }}>
          <b style={{ fontSize: 13 }}>Lịch khách đã đặt</b>
          <div style={{ display: 'grid', gap: 8, marginTop: 10 }}>
            {cal.bookings.map(b => (
              <div className="cal-row" key={b.reference}>
                <span className="badge confirmed">Đã đặt</span>
                <div style={{ flex: 1, minWidth: 0, fontSize: 13.5 }}>
                  {b.checkIn} → {b.checkOut}
                  <span style={{ color: 'var(--ink-muted)' }}> · {b.guests} khách · {b.reference}</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      ) : <p style={{ marginTop: 24, fontSize: 13, color: 'var(--ink-muted)' }}>Chưa có lượt đặt nào sắp tới.</p>}

      {!!cal.blocks?.length && (
        <div style={{ marginTop: 22 }}>
          <b style={{ fontSize: 13 }}>Bạn đang khoá</b>
          <div style={{ display: 'grid', gap: 8, marginTop: 10 }}>
            {cal.blocks.map(b => (
              <div className="cal-row" key={b.id}>
                <span className="badge cancelled">Khoá</span>
                <div style={{ flex: 1, minWidth: 0, fontSize: 13.5 }}>
                  {b.from} → {b.to}
                  {b.note && <span style={{ color: 'var(--ink-muted)' }}> · {b.note}</span>}
                </div>
                <button className="text-btn" onClick={async () => {
                  try { await api.removeBlock(b.id); await reload(); toast('Đã bỏ khoá lịch.'); }
                  catch (err) { toast(err.message); }
                }}>Bỏ</button>
              </div>
            ))}
          </div>
        </div>
      )}
    </Modal>
  );
}

/** Two-month availability grid: bookings, blocks and seasonal rates at a glance. */
function HostMonths({ cal, offset }) {
  const anchor = new Date();
  anchor.setDate(1);
  anchor.setMonth(anchor.getMonth() + offset);

  const booked = new Set();
  for (const b of cal.bookings ?? []) expandRange(b.checkIn, b.checkOut, false).forEach(d => booked.add(d));

  const blocked = new Set();
  for (const b of cal.blocks ?? []) expandRange(b.from, b.to, true).forEach(d => blocked.add(d));

  const seasonal = new Map();
  for (const r of cal.priceRules ?? []) {
    expandRange(r.from, r.to, true).forEach(d => seasonal.set(d, r.nightlyRate));
  }

  return (
    <section className="modal-section">
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10, marginBottom: 12 }}>
        <h3 style={{ margin: 0 }}>Lịch trống</h3>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="round-btn" aria-label="Tháng trước"
                  onClick={() => set({ hostMonthOffset: Math.max(0, offset - 1) })}>‹</button>
          <button className="round-btn" aria-label="Tháng sau"
                  onClick={() => set({ hostMonthOffset: offset + 1 })}>›</button>
        </div>
      </div>
      <div className="host-cal">
        {[0, 1].map(i => {
          const m = new Date(anchor.getFullYear(), anchor.getMonth() + i, 1);
          return <MonthPanel key={`${m.getFullYear()}-${m.getMonth()}`} monthStart={m}
                             booked={booked} blocked={blocked} seasonal={seasonal} basePrice={cal.basePrice ?? 0} />;
        })}
      </div>
      <div className="host-cal-legend">
        <span><i className="sw booked" /> Đã có khách</span>
        <span><i className="sw blocked" /> Bạn khoá</span>
        <span><i className="sw seasonal" /> Giá mùa</span>
      </div>
    </section>
  );
}

function MonthPanel({ monthStart, booked, blocked, seasonal, basePrice }) {
  const year = monthStart.getFullYear();
  const month = monthStart.getMonth();
  const days = new Date(year, month + 1, 0).getDate();
  const lead = (new Date(year, month, 1).getDay() + 6) % 7;

  const cells = Array.from({ length: lead }, (_, i) => <span key={`lead${i}`} />);

  for (let d = 1; d <= days; d++) {
    const iso = `${year}-${String(month + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
    const cls = booked.has(iso) ? 'is-booked' : blocked.has(iso) ? 'is-blocked' : seasonal.has(iso) ? 'is-seasonal' : '';
    const rate = seasonal.get(iso) ?? basePrice;

    cells.push(
      <span className={`host-day ${cls}`} key={iso} title={`${iso} · ${money(rate)}`}>
        <b>{d}</b><i>{shortMoney(rate)}</i>
      </span>
    );
  }

  return (
    <div className="host-cal-month">
      <div className="host-cal-head">{HOST_MONTHS[month]} {year}</div>
      <div className="host-cal-grid">
        {['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'].map(d => <span className="host-dow" key={d}>{d}</span>)}
        {cells}
      </div>
    </div>
  );
}

function expandRange(fromIso, toIso, inclusive) {
  const out = [];
  const to = new Date(`${toIso}T12:00:00`);
  for (const d = new Date(`${fromIso}T12:00:00`); inclusive ? d <= to : d < to; d.setDate(d.getDate() + 1)) {
    out.push(isoOf(d));
  }
  return out;
}

/* ------------------------------------------------------- host reviews guest */

export function GuestReviewModal() {
  const state = useStore();
  const b = state.guestReviewBooking;
  const [draft, setDraft] = useState({ rating: 5, wouldHostAgain: true });
  if (!b) return null;

  const submit = async e => {
    e.preventDefault();
    try {
      await api.reviewGuest(b.id, { ...draft, text: e.currentTarget.text.value.trim() });
      set({ overlay: null, guestReviewBooking: null });
      toast('Đã gửi đánh giá khách.');
      await loadHosting();
    } catch (err) { toast(err.message); }
  };

  return (
    <Modal title="Đánh giá khách" size="narrow">
      <div style={{ paddingBottom: 16, borderBottom: '1px solid var(--divider)' }}>
        <div style={{ fontSize: 15, fontWeight: 700 }}>{b.guestName}</div>
        <div style={{ fontSize: 13, color: 'var(--ink-muted)' }}>
          {b.listingTitle} · {longDate(b.checkIn)} – {longDate(b.checkOut)}
        </div>
      </div>

      <form onSubmit={submit}>
        <div style={{ padding: '18px 0', borderBottom: '1px solid var(--divider)' }}>
          <b style={{ fontSize: 15 }}>Khách này thế nào?</b>
          <div className="star-row" style={{ marginTop: 10 }}>
            {[1, 2, 3, 4, 5].map(n => (
              <button type="button" key={n} aria-label={`${n} sao`}
                      className={`star ${n <= draft.rating ? 'is-on' : ''}`}
                      onClick={() => setDraft(d => ({ ...d, rating: n }))}>★</button>
            ))}
          </div>
        </div>

        <div className="count-row">
          <div className="tx"><b>Bạn sẽ đón lại khách này?</b></div>
          <button type="button" className={`pill ${draft.wouldHostAgain ? 'is-on' : ''}`}
                  onClick={() => setDraft(d => ({ ...d, wouldHostAgain: !d.wouldHostAgain }))}>
            {draft.wouldHostAgain ? 'Có' : 'Không'}
          </button>
        </div>

        <label className="form-field" style={{ marginTop: 18 }}>
          <span className="cap">Nhận xét</span>
          <textarea name="text" rows={5} required minLength={10}
                    placeholder="Khách giữ gìn nhà cửa, trao đổi rõ ràng…"
                    style={{ width: '100%', padding: '12px 14px', border: '1px solid var(--line)', borderRadius: 12, fontSize: 14 }} />
        </label>

        <button type="submit" className="btn btn-primary btn-block">Gửi đánh giá</button>
      </form>
    </Modal>
  );
}

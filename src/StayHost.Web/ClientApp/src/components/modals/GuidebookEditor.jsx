import { useEffect, useState } from 'react';
import { useStore } from '../../lib/useStore.js';
import { toast } from '../../lib/store.js';
import { api } from '../../lib/api.js';
import { t } from '../../lib/i18n.js';
import { Modal } from './Modal.jsx';

/**
 * docs/01 TĐ-22 — where a host writes the guidebook a guest reads on the
 * listing page.
 *
 * The keys and the order match Guidebooks.DisplayOrder on the server; the
 * labels match Guidebooks.Label, because the guest's headings and the host's
 * dropdown have to be the same words.
 */
const CATEGORIES = [
  ['Food', 'Quán ăn'],
  ['Cafe', 'Cà phê'],
  ['Sightseeing', 'Tham quan'],
  ['Nature', 'Thiên nhiên'],
  ['Shopping', 'Mua sắm'],
  ['Nightlife', 'Về đêm'],
  ['Transport', 'Đi lại'],
  ['Tip', 'Lời khuyên']
];

const LABEL = Object.fromEntries(CATEGORIES);

const EMPTY = { category: 'Food', name: '', note: '', address: '', latitude: '', longitude: '' };

export function GuidebookEditor() {
  const state = useStore();
  const listing = state.editingListing;
  const [places, setPlaces] = useState(null);
  const [form, setForm] = useState(EMPTY);
  const [editingId, setEditingId] = useState(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!listing) return;
    api.guidebook(listing.id).then(setPlaces).catch(err => { toast(err.message); setPlaces([]); });
  }, [listing]);

  if (!listing) return null;

  const field = (key, value) => setForm(f => ({ ...f, [key]: value }));
  const reset = () => { setForm(EMPTY); setEditingId(null); };

  /*
   * A coordinate is optional, but half of one is not: a lone latitude would put
   * the pin off the west coast of Africa. Send both or neither, and let the
   * server apply the same rule again (Guidebooks.HasPin).
   */
  const body = () => {
    const lat = parseFloat(form.latitude);
    const lng = parseFloat(form.longitude);
    const pinned = Number.isFinite(lat) && Number.isFinite(lng);
    return {
      category: form.category,
      name: form.name,
      note: form.note,
      address: form.address,
      latitude: pinned ? lat : null,
      longitude: pinned ? lng : null
    };
  };

  const submit = async () => {
    if (!form.name.trim()) { toast(t('Nhập tên địa điểm trước đã.')); return; }
    if (!!form.latitude.trim() !== !!form.longitude.trim()) {
      toast(t('Toạ độ phải có đủ cả vĩ độ và kinh độ, hoặc bỏ trống cả hai.'));
      return;
    }

    setBusy(true);
    try {
      const next = editingId
        ? await api.updateGuidebookPlace(listing.id, editingId, body())
        : await api.addGuidebookPlace(listing.id, body());
      setPlaces(next);
      reset();
      toast(editingId ? t('Đã cập nhật địa điểm.') : t('Đã thêm vào cẩm nang.'));
    } catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  const edit = p => {
    setEditingId(p.id);
    setForm({
      category: p.category,
      name: p.name,
      note: p.note ?? '',
      address: p.address ?? '',
      latitude: p.latitude == null ? '' : String(p.latitude),
      longitude: p.longitude == null ? '' : String(p.longitude)
    });
  };

  const remove = async p => {
    if (!confirm(t('Xoá địa điểm này khỏi cẩm nang?'))) return;
    setBusy(true);
    try {
      setPlaces(await api.deleteGuidebookPlace(listing.id, p.id));
      if (editingId === p.id) reset();
      toast(t('Đã xoá.'));
    } catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  return (
    <Modal title={t('Cẩm nang địa phương')} size="wide">
      <p style={{ margin: '0 0 18px', fontSize: 14, color: 'var(--ink-muted)', lineHeight: 1.6 }}>
        {t('Kể cho khách những chỗ bạn thật sự hay đi. Một dòng lý do đáng giá hơn mười cái tên.')}
      </p>

      <section className="modal-section">
        <h3>{editingId ? t('Sửa địa điểm') : t('Thêm địa điểm')}</h3>

        <label className="form-field" style={{ marginTop: 12 }}>
          <span className="cap">{t('Nhóm')}</span>
          <select className="field" value={form.category} onChange={e => field('category', e.target.value)}>
            {CATEGORIES.map(([key, label]) => <option key={key} value={key}>{t(label)}</option>)}
          </select>
        </label>

        <label className="form-field">
          <span className="cap">{t('Tên địa điểm')}</span>
          <input className="field" value={form.name} maxLength={160}
                 placeholder={t('vd: Bún chả cá 109 Nguyễn Chí Thanh')}
                 onChange={e => field('name', e.target.value)} />
        </label>

        <label className="form-field">
          <span className="cap">{t('Vì sao bạn giới thiệu chỗ này')}</span>
          <textarea className="field" rows={3} value={form.note} maxLength={600}
                    placeholder={t('vd: Ăn sáng ở đây, gọi thêm chả bò. Đông nhất 7–8h.')}
                    onChange={e => field('note', e.target.value)} />
        </label>

        <label className="form-field">
          <span className="cap">{t('Địa chỉ (không bắt buộc)')}</span>
          <input className="field" value={form.address} maxLength={240}
                 onChange={e => field('address', e.target.value)} />
        </label>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <label className="form-field">
            <span className="cap">{t('Vĩ độ (không bắt buộc)')}</span>
            <input className="field" inputMode="decimal" value={form.latitude}
                   onChange={e => field('latitude', e.target.value)} />
          </label>
          <label className="form-field">
            <span className="cap">{t('Kinh độ (không bắt buộc)')}</span>
            <input className="field" inputMode="decimal" value={form.longitude}
                   onChange={e => field('longitude', e.target.value)} />
          </label>
        </div>

        <div style={{ display: 'flex', gap: 10, marginTop: 6 }}>
          <button className="btn btn-primary btn-sm" onClick={submit} disabled={busy}>
            {editingId ? t('Lưu thay đổi') : t('Thêm vào cẩm nang')}
          </button>
          {editingId && <button className="text-btn" onClick={reset}>{t('Huỷ sửa')}</button>}
        </div>
      </section>

      <section className="modal-section">
        <h3>{t('Đang có trong cẩm nang')}</h3>

        {places === null && <p style={{ fontSize: 14, color: 'var(--ink-muted)' }}>{t('Đang tải…')}</p>}
        {places?.length === 0 && (
          <p style={{ fontSize: 14, color: 'var(--ink-muted)' }}>
            {t('Chưa có địa điểm nào. Khách sẽ không thấy mục này trên trang chỗ nghỉ.')}
          </p>
        )}

        <div style={{ display: 'grid', gap: 10, marginTop: 12 }}>
          {places?.map(p => (
            <div key={p.id} style={{ border: '1px solid var(--line)', borderRadius: 12, padding: 12 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 10, alignItems: 'flex-start' }}>
                <div style={{ minWidth: 0 }}>
                  <b>{p.name}</b>
                  <div className="meta">
                    {t(LABEL[p.category] ?? p.category)}
                    {p.distance ? ` · ${p.distance}` : ''}
                    {p.address ? ` · ${p.address}` : ''}
                  </div>
                  {p.note && <p style={{ margin: '6px 0 0', fontSize: 13.5, color: 'var(--ink-body)' }}>{p.note}</p>}
                </div>
                <div style={{ display: 'flex', gap: 8, flexShrink: 0 }}>
                  <button className="btn btn-outline btn-sm" onClick={() => edit(p)} disabled={busy}>{t('Sửa')}</button>
                  <button className="btn btn-outline btn-sm" onClick={() => remove(p)} disabled={busy}>{t('Xoá')}</button>
                </div>
              </div>
            </div>
          ))}
        </div>
      </section>
    </Modal>
  );
}

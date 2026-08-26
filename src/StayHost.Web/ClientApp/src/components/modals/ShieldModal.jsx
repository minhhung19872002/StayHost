import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../../lib/useStore.js';
import { set, closeOverlay, toast } from '../../lib/store.js';
import { api } from '../../lib/api.js';
import { money } from '../../lib/format.js';
import { t } from '../../lib/i18n.js';
import { Modal } from './Modal.jsx';

const GUEST_CASES = [
  ['K2', 'Tôi không vào được chỗ ở',
   'Mất chìa, sai mã cửa, chủ nhà không liên lạc được, hoặc có người khác đang ở'],
  ['K3', 'Chỗ ở khác xa mô tả',
   'Thiếu phòng, thiếu tiện nghi trọng yếu, sai địa chỉ, ảnh không phải chỗ ở thật'],
  ['K4', 'Chỗ ở không ở được',
   'Mất vệ sinh nặng, có sinh vật gây hại, hỏng điện nước, hoặc không an toàn']
];

const HOST_CASES = [
  ['C1', 'Khách làm hư hỏng hoặc mất đồ', 'Nội thất, thiết bị, đồ dùng bị hỏng, mất hoặc bị lấy đi'],
  ['C2', 'Chi phí khắc phục', 'Dọn sâu, giặt là đặc biệt, khử mùi thuốc lá, thay khoá'],
  ['C3', 'Mất thu nhập vì phải huỷ đơn sau', 'Chỗ ở cần sửa nên đơn kế tiếp không đón được khách'],
  ['C4', 'Khách gây thiệt hại cho bên thứ ba', 'Hàng xóm hoặc tài sản chung của toà nhà — tiền trả thẳng cho bên bị thiệt hại']
];

const THIRD_PARTY_KINDS = [['neighbour', 'Hàng xóm'], ['building', 'Ban quản lý toà nhà'], ['other', 'Bên khác']];

/**
 * docs/06 AT-06-03 và AT-06-04 — mở hồ sơ. The server re-checks everything this
 * form asks about (docs/06 §2.2, §3.4); the form is here to stop a guest wasting
 * their time on something that was never going to be accepted.
 */
export function ShieldModal() {
  const state = useStore();
  const navigate = useNavigate();
  const booking = state.shieldBooking;

  const forHost = state.shieldSide === 'host';
  const cases = forHost ? HOST_CASES : GUEST_CASES;

  const [kind, setKind] = useState(cases[0][0]);
  const [description, setDescription] = useState('');
  const [urgent, setUrgent] = useState(false);
  const [expenses, setExpenses] = useState('');
  const [photos, setPhotos] = useState([]);
  const [items, setItems] = useState([{ name: '', value: '', declared: false }]);
  const [thirdParty, setThirdParty] = useState({ name: '', contact: '', kind: 'neighbour' });
  const [uploading, setUploading] = useState(false);
  const [busy, setBusy] = useState(false);

  if (!booking) return null;

  const attach = async files => {
    const list = Array.from(files ?? []);
    if (!list.length) return;

    setUploading(true);
    try {
      const form = new FormData();
      list.slice(0, 8).forEach(f => form.append('files', f));
      const res = await fetch('/api/uploads/images', { method: 'POST', body: form, credentials: 'same-origin' });
      const payload = await res.json().catch(() => null);
      if (!res.ok) throw new Error(payload?.message ?? t('Tải ảnh thất bại.'));
      setPhotos(p => [...p, ...payload.urls].slice(0, 8));
    } catch (err) { toast(err.message); } finally { setUploading(false); }
  };

  const send = async () => {
    if (description.trim().length < 10) { toast(t('Mô tả giúp chúng tôi hiểu chuyện gì đã xảy ra.')); return; }
    if (!photos.length) { toast(t('Cần ít nhất một ảnh hoặc video làm bằng chứng.')); return; }
    if (kind === 'C4' && !thirdParty.name.trim()) { toast(t('Cho biết bên bị thiệt hại là ai.')); return; }

    setBusy(true);
    try {
      const claim = await api.openShieldClaim(booking.id, {
        kind,
        description: description.trim(),
        urgent,
        expensesClaimed: Number((expenses || '').replace(/\D/g, '')) || 0,
        rehousingDifference: 0,
        evidence: photos.map(url => ({ url, kind: 'photo' })),
        thirdPartyName: kind === 'C4' ? thirdParty.name.trim() : null,
        thirdPartyContact: kind === 'C4' ? thirdParty.contact.trim() || null : null,
        thirdPartyKind: kind === 'C4' ? thirdParty.kind : null,
        items: forHost
          ? items
              .filter(i => i.name.trim() && Number((i.value || '').replace(/\D/g, '')) > 0)
              .map(i => ({
                name: i.name.trim(),
                value: Number(i.value.replace(/\D/g, '')),
                declaredOnListing: i.declared
              }))
          : []
      });

      closeOverlay();
      set({ shieldBooking: null });
      toast(`${t('Đã mở hồ sơ')} ${claim.reference}. ${t('Chúng tôi sẽ phản hồi sớm.')}`);
      navigate(`/shield/${claim.id}`);
    } catch (err) { toast(err.message); } finally { setBusy(false); }
  };

  const total = items.reduce((sum, i) => sum + (Number((i.value || '').replace(/\D/g, '')) || 0), 0);

  return (
    <Modal title={t('Báo vấn đề với Staylio')} foot={<>
      <span style={{ fontSize: 12.5, color: 'var(--ink-muted)' }}>
        {t('Đơn')} {booking.reference}
      </span>
      <button className="btn btn-primary btn-sm" disabled={busy || uploading} onClick={send}>
        {busy ? t('Đang gửi…') : t('Gửi hồ sơ')}
      </button>
    </>}>
      <div className="shield-note">
        {t('Staylio Shield là chính sách hỗ trợ của Staylio. Hãy nhắn cho')} {forHost ? t('khách') : t('chủ nhà')} {t('trong Staylio trước — trao đổi trong sàn là bằng chứng bắt buộc.')}
      </div>

      <section className="modal-section">
        <h3>{t('Chuyện gì đã xảy ra?')}</h3>
        <div style={{ display: 'grid', gap: 10, marginTop: 12 }}>
          {cases.map(([key, label, hint]) => (
            <button type="button" key={key} className={`opt ${kind === key ? 'is-on' : ''}`}
                    onClick={() => setKind(key)}>
              <b>{t(label)}</b><span>{t(hint)}</span>
            </button>
          ))}
        </div>
      </section>

      <section className="modal-section">
        <h3>{t('Mô tả')}</h3>
        <textarea rows={4} value={description} onChange={e => setDescription(e.target.value)}
                  placeholder={t('Kể lại cụ thể: bạn tới lúc mấy giờ, đã liên hệ ai, họ trả lời thế nào…')}
                  style={{ width: '100%', marginTop: 12, padding: '12px 14px',
                           border: '1px solid var(--line)', borderRadius: 12, fontSize: 14 }} />
      </section>

      {!forHost && (
        <section className="modal-section">
          <h3>{t('Có gấp không?')}</h3>
          <button type="button" className={`opt ${urgent ? 'is-on' : ''}`} style={{ marginTop: 12 }}
                  onClick={() => setUrgent(u => !u)}>
            <b>{t('Có nguy hiểm, hoặc chỗ ở đang có người lạ')}</b>
            <span>{t('Trường hợp này không phải chờ chủ nhà trả lời')}</span>
          </button>

          <label className="form-field" style={{ marginTop: 14 }}>
            <span className="cap">{t('Chi phí phát sinh bạn đã trả (nếu có)')}</span>
            <input inputMode="numeric" value={expenses} placeholder="1.500.000"
                   onChange={e => setExpenses(e.target.value)} />
          </label>
          <p style={{ fontSize: 12.5, color: 'var(--ink-muted)', margin: 0, lineHeight: 1.6 }}>
            {t('Áp dụng khi chủ nhà huỷ hoặc bạn không vào được, tối đa')} {money(3_000_000)} {t('mỗi đơn và phải có hoá đơn.')}
          </p>
        </section>
      )}

      {kind === 'C4' && (
        <section className="modal-section">
          <h3>{t('Bên bị thiệt hại')}</h3>
          <p style={{ fontSize: 13, color: 'var(--ink-muted)', margin: '4px 0 12px', lineHeight: 1.6 }}>
            {t('Tiền bồi thường được trả thẳng cho bên này, không qua tài khoản của bạn, và bạn không phải tự chịu phần đầu.')}
          </p>
          <div className="pill-row" style={{ marginBottom: 12 }}>
            {THIRD_PARTY_KINDS.map(([key, label]) => (
              <button type="button" key={key} className={`pill ${thirdParty.kind === key ? 'is-on' : ''}`}
                      onClick={() => setThirdParty(tp => ({ ...tp, kind: key }))}>{t(label)}</button>
            ))}
          </div>
          <div className="field-grid">
            <label className="form-field"><span className="cap">{t('Tên bên bị thiệt hại')}</span>
              <input value={thirdParty.name} placeholder={t('Chị Lan, căn 704')}
                     onChange={e => setThirdParty(tp => ({ ...tp, name: e.target.value }))} /></label>
            <label className="form-field"><span className="cap">{t('Liên hệ')}</span>
              <input value={thirdParty.contact} placeholder={t('Số điện thoại hoặc email')}
                     onChange={e => setThirdParty(tp => ({ ...tp, contact: e.target.value }))} /></label>
          </div>
        </section>
      )}

      {forHost && (
        <section className="modal-section">
          <h3>{t('Liệt kê từng món')}</h3>
          <div style={{ display: 'grid', gap: 10, marginTop: 12 }}>
            {items.map((item, i) => (
              <div className="shield-item-row" key={i}>
                <input value={item.name} placeholder={t('Tên món')}
                       onChange={e => setItems(x => x.map((v, j) => j === i ? { ...v, name: e.target.value } : v))} />
                <input value={item.value} inputMode="numeric" placeholder={t('Giá trị')}
                       onChange={e => setItems(x => x.map((v, j) => j === i ? { ...v, value: e.target.value } : v))} />
                <label className="shield-declared">
                  <input type="checkbox" checked={item.declared}
                         onChange={e => setItems(x => x.map((v, j) => j === i ? { ...v, declared: e.target.checked } : v))} />
                  {t('Đã khai báo')}
                </label>
              </div>
            ))}
          </div>
          <button className="text-btn" style={{ marginTop: 10 }}
                  onClick={() => setItems(x => [...x, { name: '', value: '', declared: false }])}>
            {t('+ Thêm món')}
          </button>
          <p style={{ fontSize: 12.5, color: 'var(--ink-muted)', margin: '10px 0 0', lineHeight: 1.6 }}>
            {t('Tổng yêu cầu')} {money(total)}.
            {kind === 'C4'
              ? ` ${t('Hồ sơ bên thứ ba không trừ phần tự chịu.')}`
              : ` ${t('Bạn tự chịu')} ${money(500_000)} ${t('đầu tiên.')}`}
            {' '}{t('Đồ trên')} {money(15_000_000)} {t('chỉ được tính đủ nếu đã khai báo trong tin đăng từ trước.')}
          </p>
        </section>
      )}

      <section className="modal-section">
        <h3>{t('Bằng chứng')}</h3>
        <p style={{ fontSize: 13, color: 'var(--ink-muted)', margin: '4px 0 12px' }}>
          {t('Ảnh hoặc video hiện trạng. Bắt buộc có ít nhất một tệp.')}
        </p>

        {!!photos.length && (
          <div className="bubble-photos" style={{ marginBottom: 12 }}>
            {photos.map((url, i) => (
              <img src={url} alt={`${t('Bằng chứng')} ${i + 1}`} key={i} title={t('Bấm để bỏ')}
                   style={{ cursor: 'pointer' }}
                   onClick={() => setPhotos(p => p.filter((_, j) => j !== i))} />
            ))}
          </div>
        )}

        <label className="btn btn-outline btn-sm" style={{ cursor: 'pointer' }}>
          <input type="file" accept="image/jpeg,image/png,image/webp,image/avif" multiple hidden
                 onChange={e => { attach(e.target.files); e.target.value = ''; }} />
          {uploading ? t('Đang tải…') : t('Thêm ảnh')}
        </label>
      </section>
    </Modal>
  );
}

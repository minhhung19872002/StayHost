import { useEffect, useState } from 'react';
import { api } from '../lib/api.js';
import { toast } from '../lib/store.js';
import { t } from '../lib/i18n.js';

/**
 * docs/01 AT-03 — the neighbour channel. No login: anyone living near a StayHost
 * can flag a problem (noise, parties, safety) and it reaches the moderation team.
 */
export function Neighbors() {
  const [concerns, setConcerns] = useState([]);
  const [sent, setSent] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => { api.neighborConcerns().then(setConcerns).catch(() => setConcerns([])); }, []);

  const submit = async e => {
    e.preventDefault();
    const f = e.currentTarget;
    setBusy(true);
    try {
      const res = await api.submitNeighborReport({
        location: f.location.value.trim(),
        category: f.category.value,
        detail: f.detail.value.trim(),
        contact: f.contact.value.trim() || null
      });
      setSent(true);
      toast(res.message || t('Đã gửi phản ánh.'));
    } catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  return (
    <div className="shell" style={{ paddingBlock: '28px 90px', maxWidth: 720 }}>
      <h1 className="section-title">{t('Phản ánh của hàng xóm')}</h1>
      <p className="section-sub" style={{ marginBottom: 20 }}>
        {t('Bạn sống gần một chỗ cho thuê ngắn hạn trên StayHost và có lo ngại (tiếng ồn, tụ tập, an toàn…)? Gửi phản ánh tại đây — ')}<b>{t('không cần tài khoản')}</b>{t('. Đội ngũ StayHost sẽ xem xét.')}
      </p>

      {sent ? (
        <div className="empty-state">
          <h3>{t('Đã nhận phản ánh của bạn')}</h3>
          <p>{t('Cảm ơn bạn đã lên tiếng. Chúng tôi sẽ xem xét và xử lý theo chính sách cộng đồng.')}</p>
          <button className="btn btn-outline btn-sm" style={{ marginTop: 12 }} onClick={() => setSent(false)}>
            {t('Gửi phản ánh khác')}
          </button>
        </div>
      ) : (
        <form onSubmit={submit}>
          <label className="form-field"><span className="cap">{t('Địa chỉ hoặc khu vực *')}</span>
            <input name="location" required maxLength={300}
                   placeholder={t('Số nhà, tên đường, phường/xã, thành phố…')} /></label>

          <label className="form-field"><span className="cap">{t('Loại lo ngại')}</span>
            <select name="category">
              {concerns.map(c => <option key={c.value} value={c.value}>{c.label}</option>)}
            </select></label>

          <label className="form-field"><span className="cap">{t('Mô tả sự việc *')}</span>
            <textarea name="detail" rows={5} required maxLength={2000}
              placeholder={t('Chuyện gì xảy ra, khi nào, thường xuyên ra sao…')}
              style={{ width: '100%', padding: '12px 14px', border: '1px solid var(--line)', borderRadius: 12, fontSize: 16 }} /></label>

          <label className="form-field"><span className="cap">{t('Liên hệ của bạn (không bắt buộc)')}</span>
            <input name="contact" maxLength={200} placeholder={t('Email hoặc số điện thoại, nếu muốn chúng tôi phản hồi')} /></label>
          <p className="field-note">{t('Bạn có thể gửi ẩn danh. Thông tin liên hệ chỉ dùng để phản hồi nếu cần.')}</p>

          <button type="submit" className="btn btn-primary btn-block" disabled={busy}>
            {busy ? t('Đang gửi…') : t('Gửi phản ánh')}
          </button>
        </form>
      )}
    </div>
  );
}

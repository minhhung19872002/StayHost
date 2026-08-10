import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api } from '../lib/api.js';
import { toast } from '../lib/store.js';
import { money, longDate, dateTime } from '../lib/format.js';
import { t } from '../lib/i18n.js';

/**
 * docs/01 ĐP-07 — what someone sees when they open the link they were sent.
 * No account needed: the token in the address is the credential, and it only
 * ever shows one share of one booking.
 */
export function Split() {
  const { token } = useParams();
  const navigate = useNavigate();
  const [invite, setInvite] = useState(null);
  const [missing, setMissing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [name, setName] = useState('');

  useEffect(() => {
    api.splitInvite(token).then(setInvite).catch(() => setMissing(true));
  }, [token]);

  if (missing) {
    return (
      <div className="shell" style={{ paddingBlock: '40px 90px' }}>
        <div className="empty-state">
          <h3>{t('Liên kết không còn hiệu lực')}</h3>
          <p>{t('Có thể lượt chia hoá đơn đã hoàn tất hoặc đã hết hạn.')}</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }} onClick={() => navigate('/')}>
            {t('Khám phá chỗ nghỉ')}
          </button>
        </div>
      </div>
    );
  }

  if (!invite) {
    return <div className="shell" style={{ paddingBlock: '40px 90px' }}>
      <div className="stat skeleton" style={{ height: 240, border: 0 }} />
    </div>;
  }

  const pay = async () => {
    const card = document.getElementById('split-card')?.value?.replace(/\D/g, '') ?? '';
    setBusy(true);
    try {
      setInvite(await api.paySplitShare(token, {
        name: name.trim() || null,
        cardLast4: card.length >= 4 ? card.slice(-4) : null
      }));
      toast(t('Cảm ơn bạn, phần của bạn đã được thanh toán.'));
    } catch (err) { toast(err.message); } finally { setBusy(false); }
  };

  const done = invite.shareStatus === 'Paid';
  const closed = invite.splitStatus !== 'Collecting';

  return (
    <div className="shell shell-narrow" style={{ paddingBlock: '34px 90px' }}>
      <h1 className="section-title">{t('Trả phần của bạn')}</h1>
      <p className="section-sub">
        {invite.listingTitle} · {invite.city} · {t('mã đặt chỗ')} <b>{invite.reference}</b>
      </p>

      <section className="detail-section" style={{ paddingTop: 18 }}>
        <div className="kv-grid">
          <Kv label={t('Nhận phòng')} value={longDate(invite.checkIn)} />
          <Kv label={t('Trả phòng')} value={longDate(invite.checkOut)} />
          <Kv label={t('Số đêm')} value={`${invite.nights} ${t('đêm')}`} />
          <Kv label={t('Khách')} value={`${invite.guests} ${t('khách')}`} />
        </div>
      </section>

      <section className="detail-section">
        <div className="split-amount">
          <span>{t('Phần của bạn')}</span>
          <b>{money(invite.amount)}</b>
          <i>{t('trong tổng')} {money(invite.total)} · {invite.paidCount}/{invite.peopleCount} {t('người đã trả')}</i>
        </div>

        {done ? (
          <div className="book-alert"><b>{t('Đã trả xong')}</b>
            <span>{invite.splitStatusLabel}. {t('Không cần làm gì thêm.')}</span></div>
        ) : closed ? (
          <div className="book-alert is-error"><b>{invite.splitStatusLabel}</b>
            <span>{t('Phần đã trả của mọi người được hoàn về phương thức ban đầu.')}</span></div>
        ) : <>
          <p style={{ fontSize: 13.5, color: 'var(--ink-muted)', margin: '0 0 14px' }}>
            {t('Đơn chỉ được xác nhận khi tất cả mọi người đã trả. Hạn chót')} {dateTime(invite.expiresAt)}.
          </p>
          <div className="field-grid">
            <label className="form-field"><span className="cap">{t('Tên của bạn')}</span>
              <input value={name} placeholder="Nguyễn Văn A" onChange={e => setName(e.target.value)} /></label>
            <label className="form-field"><span className="cap">{t('Số thẻ')}</span>
              <input id="split-card" inputMode="numeric" defaultValue="4242 4242 4242 4242" /></label>
          </div>
          <button className="btn btn-primary" style={{ marginTop: 8 }} disabled={busy} onClick={pay}>
            {busy ? t('Đang xử lý…') : `${t('Trả')} ${money(invite.amount)}`}
          </button>
        </>}
      </section>
    </div>
  );
}

function Kv({ label, value }) {
  return (
    <div className="kv">
      <span className="kv-label">{label}</span>
      <b>{value}</b>
    </div>
  );
}

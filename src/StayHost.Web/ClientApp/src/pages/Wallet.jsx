import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, toast } from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate } from '../lib/format.js';
import { t } from '../lib/i18n.js';

/**
 * Balance, gift cards and referrals in one place. The balance is the sum of an
 * append-only run of entries, so it can always be explained line by line.
 */
export function Wallet() {
  const state = useStore();
  const navigate = useNavigate();
  const [w, setW] = useState(null);

  const load = () => api.wallet().then(setW).catch(e => toast(e.message));
  useEffect(() => { if (state.user) load(); }, [state.user]);

  if (!state.user) {
    return <div className="shell" style={{ paddingBlock: '60px 90px' }}>
      <div className="empty-state"><h3>{t('Đăng nhập để xem số dư')}</h3>
        <button className="btn btn-primary" style={{ marginTop: 18 }}
                onClick={() => set({ authMode: 'login', authError: null, overlay: 'login' })}>{t('Đăng nhập')}</button>
      </div></div>;
  }

  if (!w) return <div className="shell" style={{ paddingBlock: '40px 90px' }}>
    <div className="stat skeleton" style={{ height: 260, border: 0 }} /></div>;

  return (
    <div className="shell" style={{ paddingBlock: '30px 90px' }}>
      <h1 className="section-title">{t('Số dư StayHost')}</h1>
      <p className="section-sub">{t('Số dư được trừ vào tiền phòng, không trừ vào phí và thuế.')}</p>

      <div className="wallet-balance">
        <span>{t('Bạn đang có')}</span>
        <b>{money(w.balance)}</b>
        {/* docs/01 TC-07 — said before it happens, not after. Absent entirely
            while nothing on the account has a hạn dùng. */}
        {w.nextExpiryAt && (
          <span className="wallet-expiry">
            {money(w.expiringAmount)} {t('hết hạn ngày')} {longDate(w.nextExpiryAt.slice(0, 10))}
          </span>
        )}
        {w.balance > 0 && (
          <button className="btn btn-primary btn-sm" style={{ marginTop: 12 }}
                  onClick={() => navigate('/')}>{t('Dùng cho chuyến tới')}</button>
        )}
      </div>

      <Redeem onDone={load} />
      <Buy wallet={w} onDone={load} />
      <Invite wallet={w} onDone={load} />

      <section style={{ marginTop: 40 }}>
        <h2 className="section-title" style={{ fontSize: 20 }}>{t('Lịch sử số dư')}</h2>
        {w.entries.length ? (
          <div className="table-wrap">
            <table className="admin-table">
              <thead><tr><th>{t('Ngày')}</th><th>{t('Lý do')}</th><th>{t('Ghi chú')}</th><th style={{ textAlign: 'right' }}>{t('Số tiền')}</th></tr></thead>
              <tbody>
                {w.entries.map(e => (
                  <tr key={e.id}>
                    <td>{longDate(e.createdAt.slice(0, 10))}</td>
                    {/* CreditRules.ReasonLabel is a closed set; the memo is the
                        server's sentence too, though the ones carrying a gift-card
                        or booking reference fall through t() unchanged. */}
                    <td>{t(e.reasonLabel)}</td>
                    <td>
                      {t(e.memo)}
                      {e.expiresAt && (
                        <span className="meta"> · {t('dùng đến')} {longDate(e.expiresAt.slice(0, 10))}</span>
                      )}
                    </td>
                    <td style={{ textAlign: 'right', color: e.amount < 0 ? 'var(--ink-muted)' : 'var(--brand-dark)' }}>
                      {e.amount > 0 ? '+' : ''}{money(e.amount)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : <p className="section-sub">{t('Chưa có giao dịch nào.')}</p>}
      </section>
    </div>
  );
}

function Redeem({ onDone }) {
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);

  const redeem = async e => {
    e.preventDefault();
    setBusy(true);
    try {
      await api.redeemGiftCard(code.trim());
      setCode('');
      toast('Đã cộng thẻ quà tặng vào số dư.');
      onDone();
    } catch (err) { toast(err.message); } finally { setBusy(false); }
  };

  return (
    <section style={{ marginTop: 34 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>{t('Đổi thẻ quà tặng')}</h2>
      <form onSubmit={redeem} style={{ display: 'flex', gap: 10, maxWidth: 460, marginTop: 12, flexWrap: 'wrap' }}>
        <input value={code} placeholder="GC-XXXXXXXXXX" required
               onChange={e => setCode(e.target.value.toUpperCase())}
               style={{ flex: 1, minWidth: 200, padding: '12px 14px', border: '1px solid var(--line)', borderRadius: 12 }} />
        <button className="btn btn-primary btn-sm" disabled={busy}>{t('Đổi')}</button>
      </form>
    </section>
  );
}

function Buy({ wallet, onDone }) {
  const [form, setForm] = useState({ amount: 500000, email: '', name: '', message: '' });
  const [busy, setBusy] = useState(false);

  const buy = async e => {
    e.preventDefault();
    setBusy(true);
    try {
      const card = await api.buyGiftCard({
        amount: Number(form.amount),
        recipientEmail: form.email.trim(),
        recipientName: form.name.trim() || null,
        message: form.message.trim() || null
      });
      toast(`Đã tạo thẻ ${card.code}.`);
      setForm(f => ({ ...f, email: '', message: '' }));
      onDone();
    } catch (err) { toast(err.message); } finally { setBusy(false); }
  };

  return (
    <section style={{ marginTop: 34 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>{t('Tặng thẻ cho người khác')}</h2>
      <p className="section-sub">
        {t('Từ')} {money(wallet.minGiftCard)} {t('đến')} {money(wallet.maxGiftCard)}. {t('Mã được gửi tới email người nhận.')}
      </p>

      <form onSubmit={buy} style={{ maxWidth: 560, marginTop: 14 }}>
        <div className="field-grid">
          <label className="form-field"><span className="cap">{t('Số tiền')}</span>
            <input type="number" min={wallet.minGiftCard} max={wallet.maxGiftCard} step={100000}
                   value={form.amount} onChange={e => setForm(f => ({ ...f, amount: e.target.value }))} /></label>
          <label className="form-field"><span className="cap">{t('Email người nhận')}</span>
            <input type="email" required value={form.email}
                   onChange={e => setForm(f => ({ ...f, email: e.target.value }))} /></label>
          <label className="form-field"><span className="cap">{t('Tên người nhận')}</span>
            <input value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} /></label>
          <label className="form-field"><span className="cap">{t('Lời nhắn')}</span>
            <input value={form.message} placeholder={t('Chúc mừng sinh nhật!')}
                   onChange={e => setForm(f => ({ ...f, message: e.target.value }))} /></label>
        </div>
        <button className="btn btn-primary" style={{ marginTop: 14 }} disabled={busy}>
          {busy ? t('Đang xử lý…') : t('Mua thẻ quà tặng')}
        </button>
      </form>

      {!!wallet.giftCards.length && (
        <div style={{ marginTop: 20, display: 'grid', gap: 10 }}>
          {wallet.giftCards.map(g => (
            <div className="team-row" key={g.id}>
              <div style={{ minWidth: 0, flex: 1 }}>
                <b>{g.code}</b>
                <div className="team-sub">
                  {money(g.amount)} · {t('gửi tới')} {g.recipientEmail} · {longDate(g.createdAt.slice(0, 10))}
                </div>
              </div>
              <span className={`badge ${g.status === 'Redeemed' ? 'confirmed' : 'pending'}`}>{t(g.statusLabel)}</span>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

function Invite({ wallet, onDone }) {
  const [email, setEmail] = useState('');
  const [busy, setBusy] = useState(false);

  const invite = async e => {
    e.preventDefault();
    setBusy(true);
    try {
      const r = await api.inviteFriend(email.trim());
      setEmail('');
      toast(`Đã gửi lời mời — mã ${r.code}.`);
      onDone();
    } catch (err) { toast(err.message); } finally { setBusy(false); }
  };

  return (
    <section style={{ marginTop: 34 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>{t('Giới thiệu bạn bè')}</h2>
      <p className="section-sub">
        {t('Bạn được')} {money(wallet.referrerReward)} {t('và người bạn mời được')} {money(wallet.inviteeReward)}, {t('sau khi họ đi chuyến đầu tiên.')}
      </p>

      <form onSubmit={invite} style={{ display: 'flex', gap: 10, maxWidth: 460, marginTop: 12, flexWrap: 'wrap' }}>
        <input type="email" required value={email} placeholder="ban@vidu.vn"
               onChange={e => setEmail(e.target.value)}
               style={{ flex: 1, minWidth: 200, padding: '12px 14px', border: '1px solid var(--line)', borderRadius: 12 }} />
        <button className="btn btn-primary btn-sm" disabled={busy}>{t('Gửi lời mời')}</button>
      </form>

      {!!wallet.referrals.length && (
        <div style={{ marginTop: 20, display: 'grid', gap: 10 }}>
          {wallet.referrals.map(r => (
            <div className="team-row" key={r.id}>
              <div style={{ minWidth: 0, flex: 1 }}>
                <b>{r.inviteeName ?? r.inviteeEmail}</b>
                <div className="team-sub">{t('Mã')} {r.code} · {t('mời ngày')} {longDate(r.createdAt.slice(0, 10))}</div>
              </div>
              <span className={`badge ${r.status === 'Rewarded' ? 'confirmed' : 'pending'}`}>{t(r.statusLabel)}</span>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

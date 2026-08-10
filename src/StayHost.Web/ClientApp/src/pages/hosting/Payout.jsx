import { useEffect, useState } from 'react';
import { api } from '../../lib/api.js';
import { toast } from '../../lib/store.js';
import { money, longDate, dateTime } from '../../lib/format.js';
import { t } from '../../lib/i18n.js';

const SCHEDULES = [
  ['PerBooking', 'Sau từng đơn', '24 giờ sau khi khách nhận phòng.'],
  ['Weekly', 'Hằng tuần', 'Gộp các đơn trong tuần, chuyển một lần.'],
  ['Monthly', 'Hằng tháng', 'Bắt buộc với kỳ ở từ 28 đêm trở lên.']
];

/**
 * docs/01 QL-20 — the account the money goes to and when it lands. Only the
 * last four digits are ever kept, so the form always asks for the full number.
 */
export function Payout() {
  const [data, setData] = useState(null);
  const [form, setForm] = useState({ bankName: '', accountName: '', accountNumber: '', schedule: 'PerBooking' });
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api.hostPayout().then(d => {
      setData(d);
      setForm({
        bankName: d.bankName ?? '',
        accountName: d.accountName ?? '',
        accountNumber: '',
        schedule: d.schedule ?? 'PerBooking'
      });
    }).catch(e => toast(e.message));
  }, []);

  if (!data) return <div className="stat skeleton" style={{ height: 200, border: 0, marginTop: 24 }} />;

  const save = async e => {
    e.preventDefault();
    setBusy(true);
    try {
      const saved = await api.saveHostPayout(form);
      setData(saved);
      setForm(f => ({ ...f, accountNumber: '' }));
      toast(t('Đã lưu tài khoản nhận tiền.'));
    } catch (err) {
      toast(err.message);
    } finally {
      setBusy(false);
    }
  };

  const pending = data.upcoming.filter(r => r.status !== 'Đã chuyển');

  return (
    <div style={{ marginTop: 24 }}>
      <section>
        <h2 className="section-title" style={{ fontSize: 20 }}>{t('Tài khoản nhận tiền')}</h2>
        <p className="section-sub">
          {data.accountLast4
            ? `${t('Đang trả về')} ${data.bankName ?? t('ngân hàng')} •••• ${data.accountLast4}`
            : t('Chưa có tài khoản nào — thêm để nhận được tiền.')}
        </p>

        {/* docs/07 §12.2 — a host whose money is not moving needs to be told why
            here, on the screen they came to, not left to guess from a silence. */}
        {!!data.accountLast4 && !data.verified && (
          <p className="notice notice-warn">
            {t('Tài khoản chưa xác minh. Chúng tôi chỉ chuyển tiền sau khi chuyển thử thành công và tên chủ tài khoản khớp hồ sơ đã xác minh danh tính.')}
          </p>
        )}
        {!!data.frozenUntil && (
          <p className="notice notice-warn">
            {t('Bạn vừa đổi tài khoản nhận tiền, các khoản chuyển tạm hoãn tới')} {dateTime(data.frozenUntil)}.
          </p>
        )}
        {data.owedToPlatform > 0 && (
          <p className="notice notice-warn">
            {t('Bạn còn nợ StayHost')} {money(data.owedToPlatform)} {t('— khoản này được khấu trừ vào lần chuyển kế tiếp.')}
          </p>
        )}

        <form onSubmit={save} style={{ maxWidth: 560, marginTop: 16 }}>
          <div className="field-grid">
            <label className="form-field"><span className="cap">{t('Ngân hàng')}</span>
              <input value={form.bankName} placeholder="Vietcombank"
                     onChange={e => setForm(f => ({ ...f, bankName: e.target.value }))} /></label>
            <label className="form-field"><span className="cap">{t('Chủ tài khoản')}</span>
              <input value={form.accountName} placeholder="NGUYEN VAN A"
                     onChange={e => setForm(f => ({ ...f, accountName: e.target.value }))} /></label>
            <label className="form-field" style={{ gridColumn: '1/-1' }}>
              <span className="cap">{t('Số tài khoản')}</span>
              <input value={form.accountNumber} inputMode="numeric"
                     placeholder={data.accountLast4 ? `•••• ${data.accountLast4} ${t('— nhập lại để đổi')}` : '0071000987654'}
                     onChange={e => setForm(f => ({ ...f, accountNumber: e.target.value }))} />
            </label>
          </div>

          <div className="opt-grid" style={{ marginTop: 8 }}>
            {SCHEDULES.map(([key, label, hint]) => (
              <button type="button" key={key} className={`opt ${form.schedule === key ? 'is-on' : ''}`}
                      onClick={() => setForm(f => ({ ...f, schedule: key }))}>
                <b>{t(label)}</b><span>{t(hint)}</span>
              </button>
            ))}
          </div>

          <p style={{ fontSize: 12.5, color: 'var(--ink-muted)', lineHeight: 1.5, margin: '14px 0' }}>
            {t('StayHost chỉ lưu 4 số cuối của tài khoản.')}
          </p>
          <button type="submit" className="btn btn-primary" disabled={busy}>
            {busy ? t('Đang lưu…') : t('Lưu tài khoản')}
          </button>
        </form>
      </section>

      <section style={{ marginTop: 40 }}>
        <h2 className="section-title" style={{ fontSize: 20 }}>{t('Lịch trả tiền')}</h2>
        <p className="section-sub">
          {pending.length
            ? `${pending.length} ${t('khoản sắp chuyển · tổng')} ${money(pending.reduce((s, r) => s + r.amount, 0))}`
            : t('Chưa có khoản nào chờ chuyển.')}
        </p>

        {!!data.upcoming.length && (
          <div className="table-wrap" style={{ marginTop: 16 }}>
            <table className="admin-table">
              <thead>
                <tr><th>{t('Đơn')}</th><th>{t('Chỗ nghỉ')}</th><th>{t('Ngày chuyển')}</th><th>{t('Số tiền')}</th><th>{t('Trạng thái')}</th></tr>
              </thead>
              <tbody>
                {data.upcoming.map(r => (
                  <tr key={r.reference}>
                    <td><b>{r.reference}</b></td>
                    <td>{r.listingTitle}</td>
                    <td>{longDate(r.dueOn)}</td>
                    <td>{money(r.amount)}</td>
                    <td>
                      {/* The comparison stays on the Vietnamese the server sent; only
                          what is shown goes through the dictionary. */}
                      <span className={`badge ${r.status === 'Đã chuyển' ? 'confirmed' : 'pending'}`}>{t(r.status)}</span>
                      {/* docs/07 §12.4 — "báo chủ nhà lý do". */}
                      {!!r.holdReason && (
                        <div style={{ fontSize: 12, color: 'var(--ink-muted)', marginTop: 4 }}>{t(r.holdReason)}</div>
                      )}
                      {!r.holdReason && r.attempts > 1 && (
                        <div style={{ fontSize: 12, color: 'var(--ink-muted)', marginTop: 4 }}>
                          {t('Đã thử')} {r.attempts} {t('lần')}
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* docs/07 §12.3 — the money leaves as one bank line a day, so the history
          is grouped the way the statement will read, with the bookings inside. */}
      {!!data.history?.length && (
        <section style={{ marginTop: 40 }}>
          <h2 className="section-title" style={{ fontSize: 20 }}>{t('Đã chuyển')}</h2>
          <p className="section-sub">
            {t('Mỗi mã chuyển là một lần chuyển khoản; các đơn trong cùng một ngày đi chung một lần.')}
          </p>

          <div style={{ display: 'grid', gap: 14, marginTop: 16 }}>
            {groupByTransfer(data.history).map(g => (
              <div className="stat" key={g.key} style={{ padding: 16 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
                  <b>{money(g.total)}</b>
                  <span style={{ fontSize: 12.5, color: 'var(--ink-muted)' }}>
                    {g.key} · {g.paidAt ? dateTime(g.paidAt) : ''}
                  </span>
                </div>
                <div style={{ marginTop: 10, display: 'grid', gap: 6 }}>
                  {g.rows.map(r => (
                    <div key={r.reference}
                         style={{ display: 'flex', justifyContent: 'space-between', gap: 12, fontSize: 13 }}>
                      <span style={{ minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis' }}>
                        <b>{r.reference}</b> · {r.listingTitle}
                      </span>
                      <span>{money(r.amount - r.deducted)}</span>
                    </div>
                  ))}
                  {g.deducted > 0 && (
                    <div style={{ fontSize: 12.5, color: 'var(--ink-muted)', marginTop: 2 }}>
                      {t('Đã khấu trừ')} {money(g.deducted)} {t('vào khoản nợ StayHost.')}
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}

/** One card per bank transfer, keeping the per-booking lines inside it. */
function groupByTransfer(rows) {
  const out = [];
  const index = new Map();

  for (const r of rows) {
    const key = r.transferReference || r.reference;
    let group = index.get(key);
    if (!group) {
      group = { key, total: 0, deducted: 0, paidAt: r.paidAt, rows: [] };
      index.set(key, group);
      out.push(group);
    }
    // The headline is what reached the bank, so it can be matched against a
    // statement line — not the gross the booking earned.
    group.total += r.amount - r.deducted;
    group.deducted += r.deducted;
    group.rows.push(r);
  }

  return out;
}

/** docs/03 §8 — progress towards Siêu chủ nhà, checked every quarter. */
export function SuperhostProgress() {
  const [data, setData] = useState(null);

  useEffect(() => { api.superhostProgress().then(setData).catch(() => setData(null)); }, []);
  if (!data) return null;

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>{t('Tiến độ Siêu chủ nhà')}</h2>
      <p className="section-sub">
        {data.isSuperhost ? t('Bạn đang là Siêu chủ nhà.') + ' ' : ''}
        {data.wouldQualify ? t('Bạn đang đạt cả bốn tiêu chí.') : t('Còn tiêu chí chưa đạt.')}
        {' '}{t('Xét lại vào')} {longDate(data.nextReview)}.
      </p>

      <div style={{ display: 'grid', gap: 10, marginTop: 16 }}>
        {data.criteria.map(c => (
          <div className="cal-row" key={c.key}>
            <span className={`badge ${c.met ? 'confirmed' : 'pending'}`}>{c.met ? t('Đạt') : t('Chưa đạt')}</span>
            <div style={{ flex: 1, minWidth: 0, fontSize: 13.5 }}>
              <b>{t(c.label)}</b>
              <span style={{ color: 'var(--ink-muted)' }}> · {t('hiện tại')} {t(c.current)}</span>
            </div>
            <span style={{ fontSize: 12.5, color: 'var(--ink-muted)' }}>{t('mục tiêu')} {t(c.target)}</span>
          </div>
        ))}
      </div>
    </section>
  );
}

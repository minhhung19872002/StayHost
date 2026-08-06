import { useEffect, useState } from 'react';
import { api } from '../../lib/api.js';
import { toast } from '../../lib/store.js';
import { money, longDate } from '../../lib/format.js';

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
      toast('Đã lưu tài khoản nhận tiền.');
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
        <h2 className="section-title" style={{ fontSize: 20 }}>Tài khoản nhận tiền</h2>
        <p className="section-sub">
          {data.accountLast4
            ? `Đang trả về ${data.bankName ?? 'ngân hàng'} •••• ${data.accountLast4}`
            : 'Chưa có tài khoản nào — thêm để nhận được tiền.'}
        </p>

        <form onSubmit={save} style={{ maxWidth: 560, marginTop: 16 }}>
          <div className="field-grid">
            <label className="form-field"><span className="cap">Ngân hàng</span>
              <input value={form.bankName} placeholder="Vietcombank"
                     onChange={e => setForm(f => ({ ...f, bankName: e.target.value }))} /></label>
            <label className="form-field"><span className="cap">Chủ tài khoản</span>
              <input value={form.accountName} placeholder="NGUYEN VAN A"
                     onChange={e => setForm(f => ({ ...f, accountName: e.target.value }))} /></label>
            <label className="form-field" style={{ gridColumn: '1/-1' }}>
              <span className="cap">Số tài khoản</span>
              <input value={form.accountNumber} inputMode="numeric"
                     placeholder={data.accountLast4 ? `•••• ${data.accountLast4} — nhập lại để đổi` : '0071000987654'}
                     onChange={e => setForm(f => ({ ...f, accountNumber: e.target.value }))} />
            </label>
          </div>

          <div className="opt-grid" style={{ marginTop: 8 }}>
            {SCHEDULES.map(([key, label, hint]) => (
              <button type="button" key={key} className={`opt ${form.schedule === key ? 'is-on' : ''}`}
                      onClick={() => setForm(f => ({ ...f, schedule: key }))}>
                <b>{label}</b><span>{hint}</span>
              </button>
            ))}
          </div>

          <p style={{ fontSize: 12.5, color: 'var(--ink-muted)', lineHeight: 1.5, margin: '14px 0' }}>
            StayHost chỉ lưu 4 số cuối của tài khoản.
          </p>
          <button type="submit" className="btn btn-primary" disabled={busy}>
            {busy ? 'Đang lưu…' : 'Lưu tài khoản'}
          </button>
        </form>
      </section>

      <section style={{ marginTop: 40 }}>
        <h2 className="section-title" style={{ fontSize: 20 }}>Lịch trả tiền</h2>
        <p className="section-sub">
          {pending.length
            ? `${pending.length} khoản sắp chuyển · tổng ${money(pending.reduce((s, r) => s + r.amount, 0))}`
            : 'Chưa có khoản nào chờ chuyển.'}
        </p>

        {!!data.upcoming.length && (
          <div className="table-wrap" style={{ marginTop: 16 }}>
            <table className="admin-table">
              <thead>
                <tr><th>Đơn</th><th>Chỗ nghỉ</th><th>Ngày chuyển</th><th>Số tiền</th><th>Trạng thái</th></tr>
              </thead>
              <tbody>
                {data.upcoming.map(r => (
                  <tr key={r.reference}>
                    <td><b>{r.reference}</b></td>
                    <td>{r.listingTitle}</td>
                    <td>{longDate(r.dueOn)}</td>
                    <td>{money(r.amount)}</td>
                    <td><span className={`badge ${r.status === 'Đã chuyển' ? 'confirmed' : 'pending'}`}>{r.status}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

/** docs/03 §8 — progress towards Siêu chủ nhà, checked every quarter. */
export function SuperhostProgress() {
  const [data, setData] = useState(null);

  useEffect(() => { api.superhostProgress().then(setData).catch(() => setData(null)); }, []);
  if (!data) return null;

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Tiến độ Siêu chủ nhà</h2>
      <p className="section-sub">
        {data.isSuperhost ? 'Bạn đang là Siêu chủ nhà. ' : ''}
        {data.wouldQualify ? 'Bạn đang đạt cả bốn tiêu chí.' : 'Còn tiêu chí chưa đạt.'}
        {' '}Xét lại vào {longDate(data.nextReview)}.
      </p>

      <div style={{ display: 'grid', gap: 10, marginTop: 16 }}>
        {data.criteria.map(c => (
          <div className="cal-row" key={c.key}>
            <span className={`badge ${c.met ? 'confirmed' : 'pending'}`}>{c.met ? 'Đạt' : 'Chưa đạt'}</span>
            <div style={{ flex: 1, minWidth: 0, fontSize: 13.5 }}>
              <b>{c.label}</b>
              <span style={{ color: 'var(--ink-muted)' }}> · hiện tại {c.current}</span>
            </div>
            <span style={{ fontSize: 12.5, color: 'var(--ink-muted)' }}>mục tiêu {c.target}</span>
          </div>
        ))}
      </div>
    </section>
  );
}

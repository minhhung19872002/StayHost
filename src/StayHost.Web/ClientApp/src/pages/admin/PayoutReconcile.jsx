import { useState } from 'react';
import { api } from '../../lib/api.js';
import { toast } from '../../lib/store.js';
import { money, longDate } from '../../lib/format.js';
import { t } from '../../lib/i18n.js';
import { splitRows, parseAmount } from '../../lib/statement.js';

/**
 * docs/07 §15.4 — the bank's own record of what left the account, read against
 * the transfers this platform says it made.
 *
 * The *Đã chuyển* button posts a payout to the ledger on one person's say-so,
 * and it is the only thing that does. This is the second pair of eyes docs/07 §7
 * asks for on the incoming side, pointed the other way: paste the outgoing
 * lines, and every one the bank shows for the right reference and the right
 * amount confirms its transfer.
 *
 * Nothing here can mark a transfer failed. A transfer missing from today's
 * statement is far more often a statement that has not caught up than a bank
 * that refused, and the two look identical from here.
 */
export function PayoutReconcilePanel() {
  const [text, setText] = useState('');
  const [cols, setCols] = useState({ ref: 0, amount: 1, memo: 2 });
  const [note, setNote] = useState('');
  const [result, setResult] = useState(null);
  const [busy, setBusy] = useState(false);

  const preview = splitRows(text).map(cells => ({
    bankReference: (cells[cols.ref] ?? '').trim(),
    amount: parseAmount(cells[cols.amount]),
    description: (cells[cols.memo] ?? '').trim()
  }));

  // A debit with no id of its own cannot be told apart from the same debit
  // tomorrow, so those lines are left out rather than guessed at.
  const usable = preview.filter(r => r.bankReference && r.amount > 0);

  const submit = async () => {
    if (!usable.length || !note.trim()) return;
    setBusy(true);
    try {
      setResult(await api.reconcilePayouts(note.trim(), usable));
      toast(t('Đã đối chiếu xong sao kê.'));
    } catch (err) {
      toast(err.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <section style={{ marginTop: 32 }}>
      <h3 style={{ fontSize: 17, fontWeight: 800 }}>{t('Đối chiếu sao kê ngân hàng')}</h3>
      <p className="section-sub">
        {t('Dán các dòng chuyển đi trong sao kê. Dòng nào ngân hàng ghi đúng mã lệnh và đúng số tiền thì lệnh đó được xác nhận và ghi sổ; mọi trường hợp khác chỉ được báo lại để người xem.')}
      </p>

      <textarea rows={6} value={text} onChange={e => setText(e.target.value)}
                placeholder={t('Dán sao kê vào đây, mỗi dòng một giao dịch')}
                style={{ width: '100%', marginTop: 12, fontFamily: 'monospace', fontSize: 13 }} />

      <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginTop: 10, alignItems: 'flex-end' }}>
        <ColumnPicker label={t('Cột mã giao dịch')} value={cols.ref}
                      onChange={v => setCols({ ...cols, ref: v })} />
        <ColumnPicker label={t('Cột số tiền')} value={cols.amount}
                      onChange={v => setCols({ ...cols, amount: v })} />
        <ColumnPicker label={t('Cột nội dung')} value={cols.memo}
                      onChange={v => setCols({ ...cols, memo: v })} />

        <label className="form-field" style={{ flex: '1 1 220px' }}>
          <span className="cap">{t('Ghi chú')}</span>
          <input value={note} onChange={e => setNote(e.target.value)}
                 placeholder={t('Sao kê ngày nào, ai đối chiếu')} />
        </label>

        <button className="btn btn-primary btn-sm" disabled={busy || !usable.length || !note.trim()}
                onClick={submit}>
          {t('Đối chiếu')}{usable.length ? ' · ' + usable.length : ''}
        </button>
      </div>

      {result && <Result result={result} />}
    </section>
  );
}

function Result({ result }) {
  return (
    <>
      <p style={{ marginTop: 16, fontSize: 14 }}>
        <b>{result.settled}</b> {t('lệnh được xác nhận')} · <b>{result.pending}</b> {t('cần xem lại')}
        {' · '}<b>{result.skipped}</b> {t('đã xác nhận trước đó')}
      </p>

      <div className="table-wrap" style={{ marginTop: 10 }}>
        <table className="admin-table">
          <thead>
            <tr>
              <th>{t('Mã giao dịch')}</th>
              <th>{t('Số tiền')}</th>
              <th>{t('Nội dung')}</th>
              <th>{t('Kết quả')}</th>
            </tr>
          </thead>
          <tbody>
            {result.rows.map(r => (
              <tr key={r.bankReference + r.description}>
                <td>{r.bankReference}</td>
                <td>{money(r.amount)}</td>
                <td style={{ maxWidth: 280 }}>{r.description}</td>
                <td>
                  <b>{t(r.verdictLabel)}</b>
                  <div style={{ color: 'var(--ink-muted)', fontSize: 12.5 }}>{t(r.explanation)}</div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* The half a statement cannot show. A transfer exported days ago and
          never seen on any statement is the failure this screen exists to
          catch, and no line will ever mention it. */}
      <h4 style={{ marginTop: 22, fontSize: 15, fontWeight: 800 }}>
        {t('Lệnh đã tải file mà ngân hàng chưa xác nhận')}
      </h4>

      {!result.stillAwaitingBank.length ? (
        <p style={{ color: 'var(--ink-muted)', fontSize: 13.5 }}>
          {t('Không còn lệnh nào đang chờ.')}
        </p>
      ) : (
        <div className="table-wrap" style={{ marginTop: 10 }}>
          <table className="admin-table">
            <thead>
              <tr>
                <th>{t('Mã chuyển')}</th>
                <th>{t('Chủ nhà')}</th>
                <th>{t('Số tiền')}</th>
                <th>{t('Đến hạn')}</th>
                <th>{t('Đã chờ')}</th>
              </tr>
            </thead>
            <tbody>
              {result.stillAwaitingBank.map(b => (
                <tr key={b.id}>
                  <td><b>{b.reference}</b></td>
                  <td>
                    {b.hostName}<br />
                    <span style={{ color: 'var(--ink-muted)', fontSize: 12.5 }}>
                      {b.bankName} · {b.accountName}
                    </span>
                  </td>
                  <td>{money(b.amount)}</td>
                  <td>{longDate(b.dueOn)}</td>
                  <td style={{ color: b.daysWaiting > 2 ? 'var(--danger)' : 'inherit' }}>
                    {b.daysWaiting} {t('ngày')}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}

/** One column of the pasted file, chosen by the person looking at it. */
function ColumnPicker({ label, value, onChange }) {
  return (
    <label className="form-field" style={{ width: 150 }}>
      <span className="cap">{label}</span>
      <select value={value} onChange={e => onChange(Number(e.target.value))}>
        {[0, 1, 2, 3, 4, 5, 6, 7].map(i => (
          <option key={i} value={i}>{t('Cột')} {i + 1}</option>
        ))}
      </select>
    </label>
  );
}

import { useEffect, useState } from 'react';
import { api } from '../../lib/api.js';
import { toast } from '../../lib/store.js';
import { money, longDate } from '../../lib/format.js';
import { t } from '../../lib/i18n.js';
import { splitRows, parseAmount } from '../../lib/statement.js';

/**
 * docs/07 §2.3 — reading a bank statement, and the queue of credits it could
 * not place.
 *
 * The paste box takes whatever the bank exported. Splitting it into columns is
 * done here, in front of the person looking at their own file, because banks
 * disagree about column order, headings and decimal separators, and a parser
 * tuned to one of them breaks the week they change it. What must not be guessed
 * at happens on the server: which booking a memo belongs to, and whether this
 * credit has been seen before.
 */
export function BankTransferPanel() {
  const [desk, setDesk] = useState(null);
  const [text, setText] = useState('');
  const [cols, setCols] = useState({ ref: 0, amount: 1, memo: 2 });
  const [result, setResult] = useState(null);
  const [busy, setBusy] = useState(false);

  const load = () => api.bankTransferDesk().then(setDesk).catch(() => setDesk(null));
  useEffect(() => { load(); }, []);

  const rows = splitRows(text);

  const preview = rows.map(cells => ({
    bankReference: (cells[cols.ref] ?? '').trim(),
    amount: parseAmount(cells[cols.amount]),
    description: (cells[cols.memo] ?? '').trim()
  }));

  // docs/07 §2.3 — a credit with no id of its own cannot be told apart from the
  // same credit tomorrow, so those lines are left out rather than guessed at.
  const usable = preview.filter(r => r.bankReference && r.amount > 0);

  const submit = async () => {
    if (!usable.length) return;
    setBusy(true);
    try {
      setResult(await api.importStatement(usable));
      await load();
      toast(t('Đã nhập xong sao kê.'));
    } catch (err) { toast(err.message); } finally { setBusy(false); }
  };

  const resolve = async credit => {
    const note = window.prompt(t('Đã xử lý thế nào? (hoàn lại, liên hệ khách, sửa mã đơn…)'));
    if (!note?.trim()) return;
    try {
      await api.resolveBankCredit(credit.id, note.trim());
      await load();
      toast(t('Đã ghi lại.'));
    } catch (err) { toast(err.message); }
  };

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>{t('Chuyển khoản ngân hàng')}</h2>
      <p className="section-sub">
        {t('Dán sao kê vào đây. Mỗi dòng cần mã giao dịch của ngân hàng, số tiền và nội dung — mã đơn nằm trong nội dung.')}
      </p>

      {desk && (
        <div className="stat-grid" style={{ marginTop: 16 }}>
          <Figure label={t('Đang chờ tiền về')} value={String(desk.awaited.length)}
                  note={money(desk.awaited.reduce((s, a) => s + a.amount, 0))} />
          <Figure label={t('Cần người xử lý')} value={String(desk.open.length)}
                  note={t('Tiền đã về nhưng chưa đặt vào đâu được')} />
        </div>
      )}

      <textarea
        className="input" rows={8} value={text} onChange={e => setText(e.target.value)}
        placeholder={'FT26081300007\t2672000\tCT DEN SH1A2B3C4D NGUYEN VAN A'}
        style={{ marginTop: 16, fontFamily: 'ui-monospace, monospace', fontSize: 13 }} />

      {rows.length > 0 && (
        <>
          <div style={{ display: 'flex', gap: 12, marginTop: 12, flexWrap: 'wrap' }}>
            <ColumnPick label={t('Cột mã giao dịch')} value={cols.ref} count={rows[0].length}
                        onChange={v => setCols(c => ({ ...c, ref: v }))} />
            <ColumnPick label={t('Cột số tiền')} value={cols.amount} count={rows[0].length}
                        onChange={v => setCols(c => ({ ...c, amount: v }))} />
            <ColumnPick label={t('Cột nội dung')} value={cols.memo} count={rows[0].length}
                        onChange={v => setCols(c => ({ ...c, memo: v }))} />
          </div>

          <p className="section-sub" style={{ marginTop: 10 }}>
            {rows.length} {t('dòng đọc được')} · {usable.length} {t('dòng dùng được')}
            {usable.length < rows.length && ` · ${t('phần còn lại thiếu mã giao dịch hoặc số tiền')}`}
          </p>

          <button className="btn btn-primary" style={{ marginTop: 12 }}
                  disabled={busy || !usable.length} onClick={submit}>
            {busy ? t('Đang nhập…') : `${t('Nhập')} ${usable.length} ${t('dòng')}`}
          </button>
        </>
      )}

      {result && (
        <div className="table-wrap" style={{ marginTop: 20 }}>
          <p className="section-sub">
            {result.settled} {t('khớp đơn')} · {result.pending} {t('cần xử lý')} · {result.skipped} {t('đã nhập trước đó')}
          </p>
          <table className="admin-table">
            <thead>
              <tr><th>{t('Giao dịch')}</th><th>{t('Số tiền')}</th><th>{t('Kết quả')}</th></tr>
            </thead>
            <tbody>
              {result.rows.map((r, i) => (
                <tr key={`${r.bankReference}-${i}`}>
                  <td><b>{r.bankReference || '—'}</b><span>{r.description}</span></td>
                  <td>{money(r.amount)}</td>
                  <td>
                    <span className={`badge ${badgeFor(r.verdict)}`}>{t(r.verdictLabel)}</span>
                    <span>{t(r.explanation)}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Money that arrived and could not be placed. It stays here until a
          person says what happened to it — this row is the only record. */}
      {desk?.open?.length > 0 && (
        <div className="table-wrap" style={{ marginTop: 20 }}>
          <h3 style={{ fontSize: 16, fontWeight: 700, marginBottom: 10 }}>{t('Chờ xử lý')}</h3>
          <table className="admin-table">
            <thead>
              <tr>
                <th>{t('Giao dịch')}</th><th>{t('Số tiền')}</th>
                <th>{t('Kết quả')}</th><th>{t('Nhập lúc')}</th><th />
              </tr>
            </thead>
            <tbody>
              {desk.open.map(c => (
                <tr key={c.id}>
                  <td><b>{c.bankReference}</b><span>{c.description}</span></td>
                  <td>
                    {money(c.amount)}
                    {c.expected > 0 && c.expected !== c.amount && (
                      <span>{t('đơn chờ')} {money(c.expected)}</span>
                    )}
                  </td>
                  <td>
                    <span className={`badge ${badgeFor(c.verdict)}`}>{t(c.verdictLabel)}</span>
                    {c.matchedReference && <span>{c.matchedReference}</span>}
                  </td>
                  <td>{longDate(c.importedAt)}</td>
                  <td><button className="link-btn" onClick={() => resolve(c)}>{t('Đã xử lý')}</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* What the desk is waiting for, so a credit in hand can be recognised. */}
      {desk?.awaited?.length > 0 && (
        <details style={{ marginTop: 20 }}>
          <summary style={{ cursor: 'pointer', fontWeight: 600 }}>
            {t('Đang chờ tiền về')} ({desk.awaited.length})
          </summary>
          <div className="table-wrap" style={{ marginTop: 12 }}>
            <table className="admin-table">
              <thead><tr><th>{t('Mã đơn')}</th><th>{t('Số tiền')}</th></tr></thead>
              <tbody>
                {desk.awaited.map(a => (
                  <tr key={a.reference}><td><b>{a.reference}</b></td><td>{money(a.amount)}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
        </details>
      )}
    </section>
  );
}

function badgeFor(verdict) {
  if (verdict === 'Paid') return 'confirmed';
  if (verdict === 'AlreadySeen') return 'pending';
  return 'cancelled';
}

function Figure({ label, value, note }) {
  return (
    <div className="stat">
      <div className="stat-label">{label}</div>
      <div className="stat-value">{value}</div>
      <div className="stat-note">{note}</div>
    </div>
  );
}

function ColumnPick({ label, value, count, onChange }) {
  return (
    <label style={{ display: 'grid', gap: 4, fontSize: 13 }}>
      <span style={{ color: 'var(--ink-muted)' }}>{label}</span>
      <select className="input" value={value} onChange={e => onChange(Number(e.target.value))}>
        {Array.from({ length: count }, (_, i) => (
          <option key={i} value={i}>{t('Cột')} {i + 1}</option>
        ))}
      </select>
    </label>
  );
}
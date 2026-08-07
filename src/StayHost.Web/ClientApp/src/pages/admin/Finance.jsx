import { useEffect, useState } from 'react';
import { api } from '../../lib/api.js';
import { toast } from '../../lib/store.js';
import { money, longDate } from '../../lib/format.js';

/** docs/07 TC-A-04 — fee revenue, money held for others, tax owed, losses. */
export function FinancePanel() {
  const [d, setD] = useState(null);

  useEffect(() => { api.financeReport().then(setD).catch(() => setD(null)); }, []);
  if (!d) return null;

  const groups = [...new Set(d.lines.map(l => l.group))];

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Báo cáo tài chính</h2>
      <p className="section-sub">
        Doanh thu tính từ {longDate(d.from)} đến {longDate(d.to)}; các khoản giữ hộ là số dư hiện tại.
      </p>

      <div className="stat-grid" style={{ marginTop: 16 }}>
        <Figure label="Doanh thu phí" value={money(d.feeRevenue)} note="Phí khách + phí chủ nhà" />
        <Figure label="Đang giữ hộ" value={money(d.heldForOthers)} note="Tiền của người khác" />
        <Figure label="Thuế phải nộp" value={money(d.taxPayable)} note="Thu hộ cơ quan thuế" />
        <Figure label="Thất thoát" value={money(d.losses)} note="Chi phí, nợ khó đòi, thua khiếu nại" />
      </div>

      {/* A report off an unbalanced ledger is a report of nothing. */}
      {d.ledgerDifference !== 0 && (
        <div className="book-alert is-error" style={{ marginTop: 12 }}>
          <b>Sổ sách không cân: lệch {money(d.ledgerDifference)}</b>
          <span>Mọi con số trên đây đều không đáng tin cho tới khi tìm ra chỗ lệch.</span>
        </div>
      )}

      <div className="table-wrap" style={{ marginTop: 16 }}>
        <table className="admin-table">
          <thead><tr><th>Khoản</th><th>Nhóm</th><th>Số tiền</th></tr></thead>
          <tbody>
            {groups.flatMap(g => d.lines.filter(l => l.group === g).map(l => (
              <tr key={l.key}>
                <td><b>{l.label}</b></td>
                <td>{l.group}</td>
                <td>{money(l.amount)}</td>
              </tr>
            )))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function Figure({ label, value, note }) {
  return (
    <div className="stat">
      <span className="cap">{label}</span>
      <b style={{ display: 'block', fontSize: 22, margin: '6px 0 2px' }}>{value}</b>
      <span style={{ fontSize: 12.5, color: 'var(--ink-muted)' }}>{note}</span>
    </div>
  );
}

/** docs/07 §7 — one line out of place against the gateway is the alarm. */
export function ReconciliationPanel() {
  const [day, setDay] = useState(() => new Date().toISOString().slice(0, 10));
  const [d, setD] = useState(null);

  const load = on => api.reconciliation(on).then(setD).catch(e => toast(e.message));
  useEffect(() => { load(day); }, []);

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Đối soát với cổng thanh toán</h2>
      <p className="section-sub">Chạy mỗi ngày. Lệch một giao dịch là báo động, không được bỏ qua.</p>

      <div style={{ display: 'flex', gap: 10, marginTop: 14, alignItems: 'flex-end', flexWrap: 'wrap' }}>
        <label className="form-field" style={{ margin: 0, maxWidth: 200 }}>
          <span className="cap">Ngày</span>
          <input type="date" value={day} onChange={e => setDay(e.target.value)} />
        </label>
        <button className="btn btn-outline btn-sm" onClick={() => load(day)}>Đối soát</button>
      </div>

      {!!d && (
        <>
          <div className={`book-alert ${d.balanced ? '' : 'is-error'}`} style={{ marginTop: 14 }}>
            <b>{d.summary}</b>
            <span>
              Sàn {d.oursCount} giao dịch · {money(d.oursTotal)} — cổng {d.theirsCount} giao dịch · {money(d.theirsTotal)}
            </span>
          </div>

          {!!d.discrepancies.length && (
            <div className="table-wrap" style={{ marginTop: 16 }}>
              <table className="admin-table">
                <thead><tr><th>Mã</th><th>Loại lệch</th><th>Sàn</th><th>Cổng</th><th>Chênh</th></tr></thead>
                <tbody>
                  {d.discrepancies.map(x => (
                    <tr key={x.reference}>
                      <td><b>{x.reference}</b></td>
                      <td>{x.kindLabel}</td>
                      <td>{money(x.ours)}</td>
                      <td>{money(x.theirs)}</td>
                      <td>{money(x.difference)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </section>
  );
}

/** docs/07 TC-A-02 — tra cứu giao dịch, hoàn tiền thủ công, điều chỉnh khoản chuyển. */
export function TransactionsPanel() {
  const [q, setQ] = useState('');
  const [rows, setRows] = useState([]);
  const [busy, setBusy] = useState(false);

  const load = async term => {
    try { setRows(await api.adminTransactions(term)); }
    catch (err) { toast(err.message); }
  };
  useEffect(() => { load(''); }, []);

  const refund = async t => {
    const raw = prompt(`Hoàn bao nhiêu cho đơn ${t.bookingReference}? Tối đa ${money(t.amount - t.refunded)}`);
    if (!raw) return;
    const reason = prompt('Lý do hoàn tiền thủ công (bắt buộc)');
    if (!reason) return;
    setBusy(true);
    try {
      await api.adminRefund(t.bookingId, { amount: Number(raw.replace(/\D/g, '')), reason });
      await load(q);
      toast('Đã hoàn tiền.');
    } catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  const adjust = async (t, release) => {
    const reason = prompt(release ? 'Lý do mở lại khoản chuyển' : 'Lý do tạm giữ khoản chuyển');
    if (!reason) return;
    setBusy(true);
    try {
      await api.adminAdjustPayout(t.bookingId, { release, reason });
      await load(q);
      toast(release ? 'Đã mở lại khoản chuyển.' : 'Đã tạm giữ khoản chuyển.');
    } catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Tra cứu giao dịch</h2>
      <p className="section-sub">Tìm theo mã đơn, mã giao dịch hoặc email khách.</p>

      <div style={{ display: 'flex', gap: 10, marginTop: 14, alignItems: 'flex-end', flexWrap: 'wrap' }}>
        <label className="form-field" style={{ margin: 0, maxWidth: 320, flex: 1 }}>
          <span className="cap">Từ khoá</span>
          <input value={q} onChange={e => setQ(e.target.value)} placeholder="SH1234ABCD" />
        </label>
        <button className="btn btn-outline btn-sm" onClick={() => load(q)}>Tìm</button>
      </div>

      <div className="table-wrap" style={{ marginTop: 16 }}>
        <table className="admin-table">
          <thead>
            <tr><th>Đơn</th><th>Khách</th><th>Số tiền</th><th>Trạng thái</th><th>Chuyển cho chủ nhà</th><th /></tr>
          </thead>
          <tbody>
            {rows.map(t => (
              <tr key={t.bookingId}>
                <td><b>{t.bookingReference}</b><span>{t.paymentReference} · {t.listingTitle}</span></td>
                <td>{t.guestEmail}</td>
                <td>
                  {money(t.amount)}
                  {t.refunded > 0 && <span>đã hoàn {money(t.refunded)}</span>}
                </td>
                <td>{t.bookingStatusLabel}<span>{t.paymentStatus}</span></td>
                <td>
                  {t.payoutStatus === 'Paid' ? 'Đã chuyển' : t.payoutStatus === 'OnHold' ? 'Tạm giữ' : 'Chờ chuyển'}
                  {!!t.payoutHoldReason && <span>{t.payoutHoldReason}</span>}
                  {!!t.payoutReference && <span>{t.payoutReference}</span>}
                </td>
                <td style={{ whiteSpace: 'nowrap' }}>
                  <button className="link-btn" disabled={busy} onClick={() => refund(t)}>Hoàn tiền</button>
                  {t.payoutStatus === 'OnHold' && (
                    <button className="link-btn" style={{ marginLeft: 8 }} disabled={busy}
                            onClick={() => adjust(t, true)}>Mở lại</button>
                  )}
                  {t.payoutStatus === 'Scheduled' && (
                    <button className="link-btn" style={{ marginLeft: 8 }} disabled={busy}
                            onClick={() => adjust(t, false)}>Tạm giữ</button>
                  )}
                </td>
              </tr>
            ))}
            {!rows.length && <tr><td colSpan={6}>Không có giao dịch nào khớp.</td></tr>}
          </tbody>
        </table>
      </div>
    </section>
  );
}

/** docs/07 §11 — khách đã báo ngân hàng: giữ tiền, gom bằng chứng, theo kết quả. */
export function ChargebackPanel() {
  const [rows, setRows] = useState([]);
  const [busy, setBusy] = useState(false);

  const load = async () => {
    try { setRows(await api.chargebacks()); }
    catch (err) { toast(err.message); }
  };
  useEffect(() => { load(); }, []);

  const open = async () => {
    const ref = prompt('Mã đơn khách khiếu nại với ngân hàng');
    if (!ref) return;
    const amount = prompt('Số tiền ngân hàng đã thu lại (để trống = toàn bộ đã trả)') ?? '';
    const reason = prompt('Lý do ngân hàng đưa ra') ?? '';
    setBusy(true);
    try {
      setRows(await api.openChargeback({
        bookingReference: ref.trim(),
        amount: Number(amount.replace(/\D/g, '')) || 0,
        reason
      }));
      toast('Đã mở hồ sơ. Khoản chuyển cho chủ nhà bị giữ lại cho tới khi có kết quả.');
    } catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  const contest = async c => {
    const evidence = prompt(`Bằng chứng đã gửi ngân hàng:\n\n${c.checklist.join('\n')}`);
    if (!evidence) return;
    setBusy(true);
    try { setRows(await api.chargebackEvidence(c.id, { evidence })); toast('Đã ghi nhận bằng chứng.'); }
    catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  const decide = async (c, won) => {
    // docs/07 §11 — the host only wears it when arbitration put it there.
    const hostAtFault = won ? false : confirm('Phân xử có kết luận lỗi thuộc về chủ nhà không?');
    setBusy(true);
    try {
      setRows(await api.decideChargeback(c.id, { won, hostAtFault }));
      toast(won ? 'Đã ghi nhận thắng.' : 'Đã ghi nhận thua.');
    } catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  return (
    <section style={{ marginTop: 40 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <div>
          <h2 className="section-title" style={{ fontSize: 20 }}>Khiếu nại với ngân hàng</h2>
          <p className="section-sub" style={{ margin: 0 }}>
            Bằng chứng phải gửi trong 7 ngày. Chủ nhà chỉ mất tiền khi phân xử kết luận lỗi thuộc về họ.
          </p>
        </div>
        <button className="btn btn-outline btn-sm" disabled={busy} onClick={open}>+ Mở hồ sơ</button>
      </div>

      <div className="table-wrap" style={{ marginTop: 16 }}>
        <table className="admin-table">
          <thead>
            <tr><th>Đơn</th><th>Số tiền</th><th>Trạng thái</th><th>Hạn gửi bằng chứng</th><th /></tr>
          </thead>
          <tbody>
            {rows.map(c => (
              <tr key={c.id}>
                <td><b>{c.bookingReference}</b><span>{c.listingTitle} · {c.reason}</span></td>
                <td>{money(c.amount)}</td>
                <td>
                  <span className={`badge ${c.status === 'Won' ? 'confirmed' : c.status === 'Received' ? 'pending' : 'cancelled'}`}>
                    {c.statusLabel}
                  </span>
                  {c.hostAtFault && <span>Chủ nhà chịu khoản này</span>}
                </td>
                <td>
                  {longDate(c.evidenceDueBy)}
                  {c.evidenceOverdue && <span>Đã quá hạn</span>}
                </td>
                <td style={{ whiteSpace: 'nowrap' }}>
                  {c.status === 'Received' && (
                    <button className="link-btn" disabled={busy} onClick={() => contest(c)}>Gửi bằng chứng</button>
                  )}
                  {(c.status === 'Received' || c.status === 'Contested') && (
                    <>
                      <button className="link-btn" style={{ marginLeft: 8 }} disabled={busy}
                              onClick={() => decide(c, true)}>Thắng</button>
                      <button className="link-btn" style={{ marginLeft: 8 }} disabled={busy}
                              onClick={() => decide(c, false)}>Thua</button>
                    </>
                  )}
                </td>
              </tr>
            ))}
            {!rows.length && <tr><td colSpan={5}>Chưa có hồ sơ nào.</td></tr>}
          </tbody>
        </table>
      </div>
    </section>
  );
}

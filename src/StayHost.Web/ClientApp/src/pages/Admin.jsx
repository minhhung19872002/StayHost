import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, loadAdmin, toast } from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate, dateTime } from '../lib/format.js';

const REPORT_STATUS = {
  Open: ['pending', 'Mới'],
  Reviewing: ['pending', 'Đang xem xét'],
  Resolved: ['confirmed', 'Đã xử lý'],
  Dismissed: ['cancelled', 'Đã bỏ qua']
};

export function Admin() {
  const state = useStore();
  const navigate = useNavigate();

  useEffect(() => { if (state.user?.role === 'Admin') loadAdmin(); }, [state.user]);

  if (!state.user) return <Gate message="Đăng nhập để vào trang quản trị" showLogin />;
  if (state.user.role !== 'Admin') return <Gate message="Tài khoản của bạn không có quyền quản trị" />;

  const d = state.admin;

  if (!d) {
    return (
      <div className="shell" style={{ paddingBlock: '34px 90px' }}>
        <div className="sk-line skeleton" style={{ width: 240, height: 26 }} />
        <div className="stat-grid" style={{ marginTop: 24 }}>
          {Array.from({ length: 4 }, (_, i) => <div className="stat skeleton" key={i} style={{ height: 110, border: 0 }} />)}
        </div>
      </div>
    );
  }

  const publish = async (id, published) => {
    try { await api.adminPublish(id, published); await loadAdmin(); toast('Đã cập nhật trạng thái chỗ nghỉ.'); }
    catch (err) { toast(err.message); }
  };

  const resolve = async (id, status) => {
    const note = prompt('Ghi chú kết luận (không bắt buộc)') ?? '';
    try { await api.adminResolveReport(id, status, note.trim() || null); await loadAdmin(); toast('Đã cập nhật báo cáo.'); }
    catch (err) { toast(err.message); }
  };

  return (
    <div className="shell" style={{ paddingBlock: '30px 90px' }}>
      <h1 className="section-title">Quản trị StayHost</h1>
      <p className="section-sub">Tổng quan nền tảng, kiểm duyệt chỗ nghỉ và xử lý báo cáo</p>

      <div className="stat-grid" style={{ marginTop: 22 }}>
        <Stat label="Doanh thu nền tảng" value={money(d.platformRevenue)} note={`Tổng giao dịch ${money(d.grossVolume)}`} />
        <Stat label="Chỗ nghỉ" value={`${d.publishedListings}/${d.listings}`} note={`${d.drafts} bản nháp`} />
        <Stat label="Lượt đặt" value={String(d.bookings)} note={`${d.activeBookings} đang hiệu lực`} />
        <Stat label="Người dùng" value={String(d.users)} note={`${d.hosts} chủ nhà`} />
      </div>

      <div className="stat-grid" style={{ marginTop: 16 }}>
        <Stat label="Báo cáo đang mở" value={String(d.openReports)} note="Cần xử lý" />
        <Stat label="Email chờ gửi" value={String(d.queuedEmails)} note="Hàng đợi giao dịch" />
      </div>

      <RiskPanel />

      <Arbitration />
      <LedgerPanel ledger={d.ledger} />
      <TaxRules settings={d.settings} />
      <AuditLog rows={d.auditLog} />

      <section style={{ marginTop: 40 }}>
        <h2 className="section-title" style={{ fontSize: 20 }}>Báo cáo chỗ nghỉ</h2>
        {d.reports.length ? (
          <div style={{ marginTop: 16, display: 'grid', gap: 12 }}>
            {d.reports.map(r => {
              const [cls, label] = REPORT_STATUS[r.status] ?? REPORT_STATUS.Open;
              const open = r.status === 'Open' || r.status === 'Reviewing';
              return (
                <article className="host-booking" key={r.id}>
                  <div style={{ minWidth: 0 }}>
                    <h3>{r.listingTitle}</h3>
                    <div className="meta">{r.reason}{r.detail ? ` — ${r.detail}` : ''}</div>
                    <div className="meta">Báo cáo bởi {r.reporterName} · {longDate(r.createdAt.slice(0, 10))}</div>
                    {r.resolution && <div className="meta">Kết luận: {r.resolution}</div>}
                    <span className={`badge ${cls}`} style={{ marginTop: 8 }}>{label}</span>
                  </div>
                  <div className="host-booking-actions">
                    {open && <>
                      <button className="btn btn-primary btn-sm" onClick={() => resolve(r.id, 'Resolved')}>Đã xử lý</button>
                      <button className="btn btn-outline btn-sm" onClick={() => resolve(r.id, 'Dismissed')}>Bỏ qua</button>
                    </>}
                    <button className="btn btn-outline btn-sm" onClick={() => publish(r.listingId, false)}>Gỡ chỗ nghỉ</button>
                  </div>
                </article>
              );
            })}
          </div>
        ) : <p className="section-sub">Chưa có báo cáo nào.</p>}
      </section>

      <section style={{ marginTop: 40 }}>
        <h2 className="section-title" style={{ fontSize: 20 }}>Chỗ nghỉ mới nhất</h2>
        <div className="table-wrap">
          <table className="admin-table">
            <thead>
              <tr><th>Chỗ nghỉ</th><th>Chủ nhà</th><th>Giá</th><th>Đánh giá</th><th>Trạng thái</th><th /></tr>
            </thead>
            <tbody>
              {d.recentListings.map(l => (
                <tr key={l.id}>
                  <td>
                    <b>{l.title}</b>
                    <span>{l.city} · {longDate(l.createdAt.slice(0, 10))}</span>
                  </td>
                  <td>{l.hostName}</td>
                  <td>{money(l.pricePerNight)}</td>
                  <td>{l.reviewCount ? `★ ${l.rating.toFixed(2)} (${l.reviewCount})` : '—'}</td>
                  <td><span className={`badge ${l.isPublished ? 'confirmed' : 'pending'}`}>
                    {l.isPublished ? 'Hiển thị' : 'Nháp'}</span></td>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    <button className="btn btn-outline btn-sm" onClick={() => publish(l.id, !l.isPublished)}>
                      {l.isPublished ? 'Gỡ hiển thị' : 'Duyệt'}
                    </button>
                    <button className="btn btn-outline btn-sm" onClick={() => navigate(`/rooms/${l.slug}`)}>Xem</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

/**
 * docs/01 QT-05 — the cases waiting on a decision, and the ruling itself. The
 * awarded amount moves between the two sides in the ledger, so the panel below
 * must still balance afterwards.
 */
function Arbitration() {
  const [cases, setCases] = useState(null);

  const load = () => api.adminResolutions().then(setCases).catch(() => setCases([]));
  useEffect(() => { load(); }, []);

  if (!cases?.length) return null;
  const open = cases.filter(c => c.status !== 'Resolved' && c.status !== 'Withdrawn');

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Phân xử tranh chấp</h2>
      <p className="section-sub">{open.length} hồ sơ đang mở · {cases.length} tổng cộng</p>

      <div style={{ display: 'grid', gap: 12, marginTop: 16 }}>
        {cases.map(c => <ArbitrationCase key={c.id} kase={c} onDone={load} />)}
      </div>
    </section>
  );
}

function ArbitrationCase({ kase: c, onDone }) {
  const [busy, setBusy] = useState(false);
  const canDecide = c.status === 'Disputed' || c.status === 'Accepted';

  const decide = async e => {
    e.preventDefault();
    const f = e.currentTarget;
    setBusy(true);
    try {
      await api.decideResolution(c.id, {
        amountAwarded: Number(f.amount.value),
        decision: f.decision.value.trim()
      });
      toast('Đã phân xử và chuyển tiền.');
      onDone();
    } catch (err) { toast(err.message); } finally { setBusy(false); }
  };

  return (
    <article className="host-booking" style={{ alignItems: 'flex-start' }}>
      <div style={{ minWidth: 0, flex: 1 }}>
        <h3>{c.kindLabel} · {c.listingTitle}</h3>
        <div className="meta">
          Hồ sơ {c.reference} · đơn {c.bookingReference} · {c.openedByName} ({c.openedByHost ? 'chủ nhà' : 'khách'})
        </div>
        <div className="meta">
          Yêu cầu <b style={{ color: 'var(--ink)' }}>{money(c.amountClaimed)}</b>
          {c.amountAwarded > 0 && <> · đã chuyển <b style={{ color: 'var(--brand)' }}>{money(c.amountAwarded)}</b></>}
        </div>
        <p style={{ margin: '10px 0 0', fontSize: 13.5, lineHeight: 1.6, color: 'var(--ink-body)' }}>{c.description}</p>
        {c.response && (
          <p style={{ margin: '8px 0 0', fontSize: 13.5, color: 'var(--ink-muted)' }}>
            <b>Bên kia:</b> {c.response}
          </p>
        )}
        {c.decision && (
          <p style={{ margin: '8px 0 0', fontSize: 13.5, color: 'var(--brand-dark)' }}>
            <b>Đã phân xử:</b> {c.decision}
          </p>
        )}
        <span className={`badge ${c.statusBadge}`} style={{ marginTop: 8 }}>{c.statusLabel}</span>

        {canDecide && (
          <form onSubmit={decide} style={{ marginTop: 14, maxWidth: 560 }}>
            <div className="field-grid">
              <label className="form-field"><span className="cap">Số tiền chuyển (₫)</span>
                <input type="number" name="amount" min={0} step={10000}
                       defaultValue={c.amountClaimed} required /></label>
              <label className="form-field" style={{ gridColumn: '1/-1' }}>
                <span className="cap">Lý do phân xử</span>
                <input name="decision" required minLength={10}
                       placeholder="Bằng chứng cho thấy thiệt hại có thật nhưng thấp hơn mức yêu cầu." />
              </label>
            </div>
            <button type="submit" className="btn btn-primary btn-sm" disabled={busy}>
              {busy ? 'Đang xử lý…' : 'Phân xử và chuyển tiền'}
            </button>
          </form>
        )}
      </div>
    </article>
  );
}

/** docs/01 QT-06 — the tax rules an operator can change without a deploy. */
function TaxRules({ settings }) {
  const [rules, setRules] = useState(settings?.taxRules ?? []);
  if (!settings) return null;

  const save = async rule => {
    try { await api.saveTaxRule(rule.id, rule); toast('Đã lưu quy tắc thuế.'); }
    catch (err) { toast(err.message); }
  };

  const patch = (id, key, value) =>
    setRules(rs => rs.map(r => (r.id === id ? { ...r, [key]: value } : r)));

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Phí và thuế</h2>
      <p className="section-sub">
        Phí dịch vụ khách {Math.round(settings.guestServiceFeeRate * 100)}% ·
        phí chủ nhà {Math.round(settings.hostServiceFeeRate * 100)}% ·
        trần giảm giá {settings.maxDiscountPercent}%
      </p>

      <div className="table-wrap" style={{ marginTop: 16 }}>
        <table className="admin-table">
          <thead>
            <tr><th>Khu vực</th><th>Tên</th><th>Cách tính</th><th>Giá trị</th><th>Bật</th><th /></tr>
          </thead>
          <tbody>
            {rules.map(r => (
              <tr key={r.id}>
                <td><b>{r.city ?? 'Toàn quốc'}</b><span>{r.country}</span></td>
                <td>{r.name}</td>
                <td>{METHOD_LABELS[r.method] ?? r.method}</td>
                <td>
                  <input type="number" step="0.0001" min="0" value={r.value} style={{ width: 120 }}
                         onChange={e => patch(r.id, 'value', Number(e.target.value))} />
                </td>
                <td>
                  <input type="checkbox" checked={r.isActive}
                         onChange={e => patch(r.id, 'isActive', e.target.checked)} />
                </td>
                <td><button className="btn btn-outline btn-sm" onClick={() => save(r)}>Lưu</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

const METHOD_LABELS = {
  Percentage: 'Phần trăm',
  PerNight: 'Mỗi đêm',
  PerGuestPerNight: 'Mỗi khách/đêm',
  PerStay: 'Mỗi lượt ở'
};

/** docs/01 QT-09 — who did what, when, and what changed. */
function AuditLog({ rows }) {
  if (!rows?.length) return null;

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Nhật ký quản trị</h2>
      <p className="section-sub">{rows.length} thao tác gần nhất</p>

      <div className="table-wrap" style={{ marginTop: 16 }}>
        <table className="admin-table">
          <thead>
            <tr><th>Lúc</th><th>Người làm</th><th>Hành động</th><th>Đối tượng</th><th>Trước → sau</th></tr>
          </thead>
          <tbody>
            {rows.map((r, i) => (
              <tr key={i}>
                <td>{dateTime(r.at)}</td>
                <td>{r.actor}</td>
                <td><b>{r.action}</b>{r.note && <span>{r.note}</span>}</td>
                <td>{r.target}</td>
                <td>{[r.before, r.after].filter(Boolean).join(' → ') || '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

/**
 * The daily reconciliation of docs/03 §5. A non-zero imbalance means a
 * transaction was written without its other half — the spec calls that an
 * alarm, so it is styled as one rather than buried in a table.
 */
function LedgerPanel({ ledger }) {
  if (!ledger) return null;
  const balanced = ledger.imbalance === 0;

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Sổ ghi tiền</h2>
      <p className="section-sub">
        {ledger.transactions} giao dịch · {ledger.entries} bút toán ·{' '}
        <span className={`badge ${balanced ? 'confirmed' : 'cancelled'}`}>
          {balanced ? 'Cân bằng' : `LỆCH ${money(ledger.imbalance)}`}
        </span>
      </p>

      {!balanced && (
        <div className="book-alert is-error" style={{ marginTop: 12 }}>
          <b>Sổ sách không cân</b>
          <span>Tổng tiền vào không bằng tổng tiền ra. Dừng mọi thao tác tài chính và kiểm tra ngay.</span>
        </div>
      )}

      <div className="table-wrap" style={{ marginTop: 16 }}>
        <table className="admin-table">
          <thead>
            <tr><th>Tài khoản</th><th>Nợ</th><th>Có</th><th>Số dư</th></tr>
          </thead>
          <tbody>
            {ledger.accounts.map(a => (
              <tr key={a.account}>
                <td><b>{a.label}</b><span>{a.account}</span></td>
                <td>{money(a.debits)}</td>
                <td>{money(a.credits)}</td>
                <td>{money(a.balance)}</td>
              </tr>
            ))}
            {!ledger.accounts.length && (
              <tr><td colSpan={4}>Chưa có giao dịch nào được ghi sổ.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function Gate({ message, showLogin }) {
  return (
    <div className="shell" style={{ paddingBlock: '60px 90px' }}>
      <div className="empty-state">
        <h3>{message}</h3>
        <p>Trang này dành cho đội vận hành StayHost.</p>
        {showLogin && (
          <button className="btn btn-primary" style={{ marginTop: 18 }}
                  onClick={() => set({ authMode: 'login', authError: null, overlay: 'login' })}>Đăng nhập</button>
        )}
      </div>
    </div>
  );
}

/**
 * docs/01 AT-11 — accounts the checks flagged. These are hints for a person:
 * nothing was blocked on the way in, so the job here is to look and decide.
 */
function RiskPanel() {
  const [flags, setFlags] = useState(null);

  const reload = () => api.riskFlags().then(setFlags).catch(e => toast(e.message));
  useEffect(() => { reload(); }, []);

  const resolve = async (flag, acted) => {
    const note = prompt(acted ? 'Đã làm gì với tài khoản này?' : 'Vì sao bỏ qua?') ?? '';
    try {
      await api.resolveRiskFlag(flag.id, { resolution: note.trim() || null, acted });
      toast(acted ? 'Đã ghi nhận xử lý.' : 'Đã bỏ qua cảnh báo.');
      reload();
    } catch (err) { toast(err.message); }
  };

  if (!flags) return null;

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Cảnh báo bất thường</h2>
      <p className="section-sub">
        Máy chỉ đánh dấu để người xem lại — không đơn nào bị chặn vì những dấu hiệu này.
      </p>

      {flags.length ? (
        <div style={{ marginTop: 16, display: 'grid', gap: 12 }}>
          {flags.map(f => (
            <article className="host-booking" key={f.id}>
              <div style={{ minWidth: 0 }}>
                <h3>{f.summary}</h3>
                <div className="meta">{f.detail}</div>
                <div className="meta">
                  {f.userName} · {f.userEmail}
                  {f.bookingReference ? ` · đơn ${f.bookingReference}` : ''}
                  {' · '}{dateTime(f.createdAt)}
                </div>
                <span className={`badge ${f.severityBadge}`} style={{ marginTop: 8 }}>{f.severityLabel}</span>
              </div>
              <div className="host-booking-actions">
                <button className="btn btn-primary btn-sm" onClick={() => resolve(f, true)}>Đã xử lý</button>
                <button className="btn btn-outline btn-sm" onClick={() => resolve(f, false)}>Bỏ qua</button>
              </div>
            </article>
          ))}
        </div>
      ) : <p className="section-sub">Không có cảnh báo nào đang mở.</p>}
    </section>
  );
}

function Stat({ label, value, note }) {
  return (
    <div className="stat">
      <div className="value" style={{ fontSize: 'clamp(20px,2.4vw,26px)' }}>{value}</div>
      <div className="label">{label}</div>
      <div className="note">{note}</div>
    </div>
  );
}

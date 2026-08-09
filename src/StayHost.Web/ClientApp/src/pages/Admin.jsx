import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, loadAdmin, toast } from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate, dateTime } from '../lib/format.js';
import { FinancePanel, ReconciliationPanel, TransactionsPanel, ChargebackPanel } from './admin/Finance.jsx';
import { UserAdminPanel, AppealsPanel, OversightPanel, DataRequestsPanel } from './admin/Users.jsx';

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
    // docs/08 §1.4 — gỡ hay duyệt tin đều là quyết định, phải có lý do đi kèm.
    const reason = prompt(published ? 'Lý do duyệt hiển thị lại (≥10 ký tự)' : 'Lý do gỡ hiển thị (≥10 ký tự)');
    if (!reason) return;
    try { await api.adminPublish(id, published, reason.trim()); await loadAdmin(); toast('Đã cập nhật trạng thái chỗ nghỉ.'); }
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

      <ShieldPanel />

      <RiskPanel />

      <Arbitration />
      <LedgerPanel ledger={d.ledger} />
      <FinancePanel />
      <ReconciliationPanel />
      <TransactionsPanel />
      <ChargebackPanel />
      <UserAdminPanel />
      <AppealsPanel />
      <DataRequestsPanel />
      <OversightPanel />
      <TaxRules settings={d.settings} />
      <AuditLog rows={d.auditLog} />

      <section style={{ marginTop: 40 }}>
        <h2 className="section-title" style={{ fontSize: 20 }}>Báo cáo</h2>
        {d.reports.length ? (
          <div style={{ marginTop: 16, display: 'grid', gap: 12 }}>
            {d.reports.map(r => {
              const [cls, label] = REPORT_STATUS[r.status] ?? REPORT_STATUS.Open;
              const open = r.status === 'Open' || r.status === 'Reviewing';
              return (
                <article className="host-booking" key={r.id}>
                  <div style={{ minWidth: 0 }}>
                    {/* docs/01 AT-02 — a report is about a listing, a person, a
                        message or a review, so the queue has to say which. */}
                    <h3><span className="badge" style={{ marginRight: 8 }}>{r.targetLabel}</span>{r.subjectTitle}</h3>
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
                    {/* Only a listing can be unpublished; the other three
                        subjects are handled from the user console. */}
                    {r.target === 'Listing' && r.subjectId && (
                      <button className="btn btn-outline btn-sm" onClick={() => publish(r.subjectId, false)}>Gỡ chỗ nghỉ</button>
                    )}
                  </div>
                </article>
              );
            })}
          </div>
        ) : <p className="section-sub">Chưa có báo cáo nào.</p>}
      </section>

      <SupportQueue />

      <ModerationQueue />

      <FeatureFlags />

      <HelpArticlesAdmin />

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
 * docs/01 AT-01 — new listings held for review before the public can see them.
 * Empty unless the review gate (Moderation:NewListingsRequireApproval) is on, so
 * on a platform that publishes straight away this panel simply says so.
 */
function ModerationQueue() {
  const navigate = useNavigate();
  const [rows, setRows] = useState(null);

  const load = async () => {
    try { setRows(await api.adminPendingListings()); }
    catch { setRows([]); }
  };
  useEffect(() => { load(); }, []);

  const decide = async (id, decision) => {
    // docs/08 §1.4 — duyệt hay từ chối đều là quyết định, phải có lý do.
    const reason = prompt(decision === 'approve'
      ? 'Lý do duyệt hiển thị (≥10 ký tự)'
      : 'Lý do từ chối, gửi cho chủ nhà (≥10 ký tự)');
    if (!reason) return;
    try {
      await api.adminReviewListing(id, decision, reason.trim());
      await load();
      toast(decision === 'approve' ? 'Đã duyệt tin đăng.' : 'Đã từ chối tin đăng.');
    } catch (err) { toast(err.message); }
  };

  if (!rows) return null;

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Chờ duyệt trước khi hiển thị</h2>
      <p className="section-sub">
        {rows.length ? `${rows.length} tin đăng đang chờ kiểm duyệt` : 'Không có tin nào đang chờ duyệt'}
      </p>
      {rows.length > 0 && (
        <div className="table-wrap" style={{ marginTop: 16 }}>
          <table className="admin-table">
            <thead>
              <tr><th>Chỗ nghỉ</th><th>Chủ nhà</th><th>Giá</th><th>Gửi lúc</th><th /></tr>
            </thead>
            <tbody>
              {rows.map(l => (
                <tr key={l.id}>
                  <td><b>{l.title}</b><span>{l.city}</span></td>
                  <td>{l.hostName}</td>
                  <td>{money(l.pricePerNight)}</td>
                  <td>{l.submittedForReviewAt ? dateTime(l.submittedForReviewAt) : '—'}</td>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    <button className="btn btn-primary btn-sm" onClick={() => decide(l.id, 'approve')}>Duyệt</button>
                    <button className="btn btn-outline btn-sm" onClick={() => decide(l.id, 'reject')}>Từ chối</button>
                    <button className="btn btn-outline btn-sm" onClick={() => navigate(`/rooms/${l.slug}`)}>Xem</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

const BLANK_ARTICLE = {
  id: null, slug: '', title: '', category: '', audience: 'Everyone',
  summary: '', body: '', sortOrder: 0
};

/**
 * docs/01 QT-07 — manage help centre articles. The list on the left, an editor on
 * the right; saving upserts by id, and a blank form creates a new one.
 */
function HelpArticlesAdmin() {
  const [rows, setRows] = useState(null);
  const [draft, setDraft] = useState(null);

  const load = async () => {
    try { setRows(await api.adminHelpArticles()); }
    catch { setRows([]); }
  };
  useEffect(() => { load(); }, []);

  const save = async () => {
    try {
      await api.saveHelpArticle(draft);
      setDraft(null);
      await load();
      toast('Đã lưu bài trợ giúp.');
    } catch (err) { toast(err.message); }
  };

  const remove = async a => {
    if (!confirm(`Xoá bài "${a.title}"?`)) return;
    try { await api.deleteHelpArticle(a.id); setDraft(null); await load(); toast('Đã xoá bài.'); }
    catch (err) { toast(err.message); }
  };

  if (!rows) return null;
  const field = (k, v) => setDraft(d => ({ ...d, [k]: v }));

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Bài viết trợ giúp</h2>
      <p className="section-sub">{rows.length} bài · sửa nội dung trung tâm trợ giúp (AT-07)</p>

      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', margin: '12px 0' }}>
        <button className="btn btn-sm" onClick={() => setDraft({ ...BLANK_ARTICLE })}>+ Bài mới</button>
      </div>

      {draft && (
        <div style={{ padding: 14, border: '1px solid var(--line)', borderRadius: 12, marginBottom: 16 }}>
          <div className="field-grid">
            <label className="form-field"><span className="cap">Tiêu đề *</span>
              <input value={draft.title} onChange={e => field('title', e.target.value)} /></label>
            <label className="form-field"><span className="cap">Đường dẫn (slug)</span>
              <input value={draft.slug} placeholder="tự tạo từ tiêu đề" onChange={e => field('slug', e.target.value)} /></label>
            <label className="form-field"><span className="cap">Danh mục</span>
              <input value={draft.category} onChange={e => field('category', e.target.value)} /></label>
            <label className="form-field"><span className="cap">Đối tượng</span>
              <select value={draft.audience} onChange={e => field('audience', e.target.value)}>
                <option value="Everyone">Chung</option>
                <option value="Guest">Khách</option>
                <option value="Host">Chủ nhà</option>
              </select></label>
            <label className="form-field"><span className="cap">Thứ tự</span>
              <input type="number" value={draft.sortOrder} onChange={e => field('sortOrder', Number(e.target.value) || 0)} /></label>
          </div>
          <label className="form-field"><span className="cap">Tóm tắt</span>
            <input value={draft.summary} onChange={e => field('summary', e.target.value)} /></label>
          <label className="form-field"><span className="cap">Nội dung *</span>
            <textarea rows={8} value={draft.body} onChange={e => field('body', e.target.value)}
              style={{ width: '100%', padding: '12px 14px', border: '1px solid var(--line)', borderRadius: 12, fontSize: 14 }} /></label>
          <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
            <button className="btn btn-primary btn-sm" onClick={save}>Lưu</button>
            <button className="btn btn-outline btn-sm" onClick={() => setDraft(null)}>Huỷ</button>
          </div>
        </div>
      )}

      <div className="table-wrap">
        <table className="admin-table">
          <thead>
            <tr><th>Bài</th><th>Danh mục</th><th>Đối tượng</th><th /></tr>
          </thead>
          <tbody>
            {rows.map(a => (
              <tr key={a.id}>
                <td><b>{a.title}</b><span>{a.slug}</span></td>
                <td>{a.category}</td>
                <td>{a.audience === 'Guest' ? 'Khách' : a.audience === 'Host' ? 'Chủ nhà' : 'Chung'}</td>
                <td style={{ whiteSpace: 'nowrap' }}>
                  <button className="btn btn-outline btn-sm" onClick={() => setDraft({ ...a })}>Sửa</button>
                  <button className="btn btn-outline btn-sm" onClick={() => remove(a)}>Xoá</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

/**
 * docs/01 QT-08 — turn features on for a share of users. Each flag has a master
 * switch and a 0–100 rollout; changes take effect on the next page load for the
 * users who fall in the slice.
 */
function FeatureFlags() {
  const [rows, setRows] = useState(null);

  const load = async () => {
    try { setRows(await api.adminFeatureFlags()); }
    catch { setRows([]); }
  };
  useEffect(() => { load(); }, []);

  const save = async (flag, changes) => {
    try {
      await api.saveFeatureFlag({
        key: flag.key,
        description: flag.description,
        enabled: changes.enabled ?? flag.enabled,
        rolloutPercent: changes.rolloutPercent ?? flag.rolloutPercent
      });
      await load();
      toast('Đã cập nhật tính năng.');
    } catch (err) { toast(err.message); }
  };

  if (!rows) return null;

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Bật tính năng theo tỉ lệ</h2>
      <p className="section-sub">{rows.length} tính năng · thay đổi áp dụng ở lần tải trang kế tiếp</p>
      <div className="table-wrap" style={{ marginTop: 16 }}>
        <table className="admin-table">
          <thead>
            <tr><th>Tính năng</th><th>Trạng thái</th><th>Tỉ lệ (%)</th></tr>
          </thead>
          <tbody>
            {rows.map(f => (
              <tr key={f.key}>
                <td><b>{f.key}</b><span>{f.description}</span></td>
                <td>
                  <button className={`pill ${f.enabled ? 'is-on' : ''}`}
                          onClick={() => save(f, { enabled: !f.enabled })}>
                    {f.enabled ? 'Đang bật' : 'Đang tắt'}
                  </button>
                </td>
                <td>
                  <input type="number" min={0} max={100} defaultValue={f.rolloutPercent}
                         style={{ width: 90 }}
                         onBlur={e => {
                           const v = Math.max(0, Math.min(100, Number(e.target.value) || 0));
                           if (v !== f.rolloutPercent) save(f, { rolloutPercent: v });
                         }} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
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
 * docs/06 §8 AT-06-09 and AT-06-12 — the queue of StayShield cases and the fund
 * behind them. Deciding one pays it in the same step, so nothing sits decided
 * but unpaid.
 */
function ShieldPanel() {
  const [rows, setRows] = useState(null);
  const [fund, setFund] = useState(null);
  const [rehousing, setRehousing] = useState(null);

  const reload = () => {
    api.shieldQueue().then(setRows).catch(e => toast(e.message));
    api.shieldFund().then(setFund).catch(() => setFund(null));
  };
  useEffect(() => { reload(); }, []);

  const decide = async (claim, approve) => {
    const reason = prompt(approve ? 'Lý do chấp nhận' : 'Lý do từ chối') ?? '';
    if (!reason.trim()) return;

    const body = { approve, reason: reason.trim() };

    if (approve && claim.side === 'Guest') {
      body.remedy = 'Refunded';
      const nights = prompt('Số đêm khách không được ở?', '1');
      body.nightsUnused = Number(nights) || 1;
    }

    if (approve && claim.side === 'Host') {
      const amount = prompt(`Duyệt bao nhiêu trong ${money(claim.claimed)}?`,
        String(Math.round(claim.claimed)));
      body.approvedAmount = Number((amount ?? '').replace(/\D/g, '')) || 0;
      const fromGuest = prompt('Thu được bao nhiêu từ khách?', '0');
      body.recoverFromGuest = Number((fromGuest ?? '').replace(/\D/g, '')) || 0;
      body.depositAvailable = 0;
    }

    try {
      await api.decideShield(claim.id, body);
      toast('Đã ra quyết định và chi tiền.');
      reload();
    } catch (err) { toast(err.message); }
  };

  const recover = async claim => {
    const raw = prompt(`Thu hồi bao nhiêu về quỹ? (đã chi ${money(claim.paidFromFund)})`);
    const amount = Number((raw ?? '').replace(/\D/g, ''));
    if (!amount) return;
    try {
      await api.recoverShield(claim.id, amount);
      toast('Đã ghi hoàn lại quỹ.');
      reload();
    } catch (err) { toast(err.message); }
  };

  if (!rows) return null;

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>StayShield</h2>
      <p className="section-sub">
        Chính sách hỗ trợ của sàn. Quyết định nào cũng phải nêu lý do và được ghi vào nhật ký.
      </p>

      {fund && (
        <div className="stat-grid" style={{ marginTop: 16 }}>
          <Stat label="Số dư quỹ" value={money(fund.balance)}
                note={`Trích ${(fund.contributionRate * 100).toFixed(0)}% phí dịch vụ`} />
          <Stat label="Trích tháng này" value={money(fund.contributedThisMonth)} note="Từ doanh thu phí" />
          <Stat label="Chi tháng này" value={money(fund.spentThisMonth)}
                note={fund.alarm
                  ? `Vượt ngưỡng ${(fund.alarmRate * 100).toFixed(0)}% — báo tài chính`
                  : 'Trong ngưỡng'} />
          <Stat label="Thu hồi tháng này" value={money(fund.recoveredThisMonth)} note="Hoàn lại quỹ" />
        </div>
      )}

      {rows.length ? (
        <div style={{ marginTop: 16, display: 'grid', gap: 12 }}>
          {rows.map(c => (
            <article className="host-booking" key={c.id}>
              <div style={{ minWidth: 0 }}>
                <h3>{c.kindLabel} · {c.reference}</h3>
                <div className="meta">{c.listingTitle} · đơn {c.bookingReference} · {c.openedByName}</div>
                <div className="meta">{c.description}</div>
                {c.claimed > 0 && <div className="meta">Yêu cầu {money(c.claimed)}</div>}
                {c.approved > 0 && <div className="meta">Đã duyệt {money(c.approved)}
                  {c.paidFromFund > 0 ? ` · quỹ chi ${money(c.paidFromFund)}` : ''}</div>}
                <div className="meta">Hạn ra quyết định {dateTime(c.decisionDueAt)}</div>
                <div style={{ display: 'flex', gap: 6, marginTop: 8, flexWrap: 'wrap' }}>
                  <span className={`badge ${c.statusBadge}`}>{c.statusLabel}</span>
                  {c.needsManualReview && <span className="badge pending">Duyệt tay</span>}
                  {c.appealed && <span className="badge pending">Khiếu nại</span>}
                </div>
              </div>
              <div className="host-booking-actions">
                {(c.status !== 'Settled' && c.status !== 'Rejected') && <>
                  <button className="btn btn-primary btn-sm" onClick={() => decide(c, true)}>Chấp nhận</button>
                  <button className="btn btn-outline btn-sm" onClick={() => decide(c, false)}>Từ chối</button>
                </>}
                {c.side === 'Guest' && (
                  <button className="btn btn-outline btn-sm" onClick={async () => {
                    try { setRehousing(await api.shieldRehousing(c.id)); }
                    catch (err) { toast(err.message); }
                  }}>Tìm chỗ thay thế</button>
                )}
                {c.paidFromFund > c.recoveredLater &&
                  <button className="btn btn-outline btn-sm" onClick={() => recover(c)}>Thu hồi</button>}
              </div>
            </article>
          ))}
        </div>
      ) : <p className="section-sub" style={{ marginTop: 16 }}>Không có hồ sơ nào đang mở.</p>}

      <Rehousing data={rehousing} onClose={() => setRehousing(null)} />
    </section>
  );
}

/**
 * docs/06 AT-06-08 — what support offers before anybody mentions a refund.
 * Level 1 of §2.3 comes first for a reason: a guest with nowhere to sleep wants
 * a bed tonight, not their money back next week.
 */
function Rehousing({ data, onClose }) {
  if (!data) return null;

  return (
    <div className="rehouse">
      <div className="rehouse-head">
        <div style={{ minWidth: 0 }}>
          <b>Chỗ thay thế cho hồ sơ {data.reference}</b>
          <div className="meta">
            {data.city} · {data.nights} đêm còn lại ({data.from} → {data.to}) · {data.guests} khách ·
            {' '}khách đã trả {money(data.alreadyPaid)} cho những đêm này
          </div>
          <div className="meta">Sàn bù chênh lệch tối đa {money(data.topUpCeiling)}</div>
        </div>
        <button className="btn btn-outline btn-sm" onClick={onClose}>Đóng</button>
      </div>

      {data.options.length ? (
        <div className="table-wrap" style={{ marginTop: 12 }}>
          <table className="admin-table">
            <thead>
              <tr><th>Chỗ nghỉ</th><th>Sức chứa</th><th>Cách</th>
                <th style={{ textAlign: 'right' }}>Giá</th>
                <th style={{ textAlign: 'right' }}>Chênh lệch</th><th /></tr>
            </thead>
            <tbody>
              {data.options.map(o => (
                <tr key={o.listingId}>
                  <td><b>{o.title}</b><span>{o.typeLabel} · ★ {o.rating.toFixed(2)} ({o.reviewCount})</span></td>
                  <td>{o.maxGuests} khách · {o.bedrooms} phòng ngủ</td>
                  <td>{o.distanceKm} km</td>
                  <td style={{ textAlign: 'right' }}>{money(o.total)}</td>
                  <td style={{ textAlign: 'right' }}>
                    {o.difference > 0 ? `+${money(o.difference)}` : 'Không chênh'}
                  </td>
                  <td>
                    <span className={`badge ${o.withinTopUp ? 'confirmed' : 'pending'}`}>
                      {o.withinTopUp ? 'Trong hạn mức' : 'Cần duyệt thêm'}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="section-sub" style={{ marginTop: 12 }}>
          Không còn chỗ nào trống ở {data.city} cho những ngày này. Chuyển sang mức 2 hoặc mức 3.
        </p>
      )}
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

/** docs/01 AT-09 — the support desk queue, urgent tickets first. */
function SupportQueue() {
  const [tickets, setTickets] = useState(null);
  const load = () => api.supportTickets().then(setTickets).catch(() => setTickets([]));
  useEffect(() => { load(); }, []);

  const resolve = async t => {
    const reply = prompt(`Trả lời cho "${t.subject}" (không bắt buộc):`) ?? '';
    try { await api.resolveSupportTicket(t.id, reply.trim() || null); load(); toast('Đã xử lý phiếu hỗ trợ.'); }
    catch (err) { toast(err.message); }
  };

  if (!tickets) return null;

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Phiếu hỗ trợ ({tickets.length})</h2>
      {tickets.length ? (
        <div style={{ marginTop: 16, display: 'grid', gap: 12 }}>
          {tickets.map(t => (
            <article className="host-booking" key={t.id}>
              <div style={{ minWidth: 0 }}>
                <h3>
                  {t.urgent && <span className="badge cancelled" style={{ marginRight: 8 }}>Khẩn cấp</span>}
                  {t.subject}
                </h3>
                <div className="meta">{t.requesterName}{t.bookingReference ? ` · đơn ${t.bookingReference}` : ''} · {longDate(t.createdAt.slice(0, 10))}</div>
                <div className="meta" style={{ whiteSpace: 'pre-wrap' }}>{t.message}</div>
              </div>
              <div className="host-booking-actions">
                <button className="btn btn-primary btn-sm" onClick={() => resolve(t)}>Xử lý</button>
              </div>
            </article>
          ))}
        </div>
      ) : <p className="section-sub">Không có phiếu hỗ trợ nào đang mở.</p>}
    </section>
  );
}

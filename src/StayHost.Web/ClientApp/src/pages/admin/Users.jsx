import { useEffect, useState } from 'react';
import { api } from '../../lib/api.js';
import { toast } from '../../lib/store.js';
import { money, longDate, dateTime } from '../../lib/format.js';

/* docs/08 §5.2 — each block names what it leaves alone. */
const RESTRICTIONS = [
  ['NoNewBookings', 'Không được đặt đơn mới'],
  ['NoNewListings', 'Không được đăng tin mới'],
  ['ListingsHiddenFromSearch', 'Tin đăng bị ẩn khỏi tìm kiếm'],
  ['NoReviews', 'Không được viết đánh giá'],
  ['NoNewConversations', 'Không được nhắn tin cho người mới'],
  ['PayoutsHeld', 'Khoản chuyển tiền bị giữ lại']
];

const SEVERE = [
  'Đe doạ hoặc bạo lực',
  'Nội dung liên quan tới trẻ em',
  'Gian lận thanh toán có bằng chứng',
  'Giả mạo giấy tờ',
  'Chiếm đoạt tài khoản người khác',
  'Lừa đảo có tổ chức'
];

const BAN_GROUNDS = [
  'Gian lận tiền',
  'Giả mạo danh tính',
  'Đe doạ an toàn người khác',
  'Tái phạm nhiều lần sau khi đã cảnh cáo và tạm khoá'
];

/**
 * docs/08 §4 — find somebody, then everything the console may show about them.
 *
 * The buttons offered come from the server's own answer to "what may this admin
 * do", so a role never sees an action it would be refused.
 */
export function UserAdminPanel() {
  const [q, setQ] = useState('');
  const [rows, setRows] = useState([]);
  const [open, setOpen] = useState(null);

  const search = async term => {
    try { setRows(await api.adminSearchUsers(term)); }
    catch (err) { toast(err.message); }
  };

  const load = async id => {
    try { setOpen(await api.adminUser(id)); }
    catch (err) { toast(err.message); }
  };

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Quản trị người dùng</h2>
      <p className="section-sub">
        Tìm theo email, số điện thoại, tên, mã đơn, mã tin đăng hoặc mã giao dịch.
      </p>

      <div style={{ display: 'flex', gap: 10, marginTop: 14, alignItems: 'flex-end', flexWrap: 'wrap' }}>
        <label className="form-field" style={{ margin: 0, maxWidth: 340, flex: 1 }}>
          <span className="cap">Từ khoá</span>
          <input value={q} onChange={e => setQ(e.target.value)}
                 onKeyDown={e => e.key === 'Enter' && search(q)}
                 placeholder="guest@stayhost.vn" />
        </label>
        <button className="btn btn-outline btn-sm" onClick={() => search(q)}>Tìm</button>
      </div>

      {!!rows.length && (
        <div className="table-wrap" style={{ marginTop: 16 }}>
          <table className="admin-table">
            <thead>
              <tr><th>Người dùng</th><th>Vai trò</th><th>Trạng thái</th><th>Tham gia</th><th /></tr>
            </thead>
            <tbody>
              {rows.map(u => (
                <tr key={u.id}>
                  <td><b>{u.fullName}</b><span>{u.email}{u.phone ? ` · ${u.phone}` : ''}</span></td>
                  <td>{u.role}</td>
                  <td>
                    <span className={`badge ${u.statusLabel === 'Bình thường' ? 'confirmed' : 'cancelled'}`}>
                      {u.statusLabel}
                    </span>
                  </td>
                  <td>{longDate(u.joinedAt)}</td>
                  <td><button className="link-btn" onClick={() => load(u.id)}>Mở hồ sơ</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {!!open && <UserProfilePanel d={open} reload={() => load(open.id)} close={() => setOpen(null)} />}
    </section>
  );
}

function UserProfilePanel({ d, reload, close }) {
  const [preview, setPreview] = useState(null);
  const [busy, setBusy] = useState(false);
  const [identity, setIdentity] = useState(null);

  const may = action => d.allowed.includes(action);

  const run = async (fn, done) => {
    setBusy(true);
    try { await fn(); toast(done); await reload(); }
    catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  const ask = (label) => {
    const reason = prompt(`${label}\n\nLý do (bắt buộc, ít nhất 10 ký tự):`);
    if (!reason || reason.trim().length < 10) {
      if (reason !== null) toast('Cần ghi lý do ít nhất 10 ký tự.');
      return null;
    }
    return reason.trim();
  };

  const warn = () => {
    const reason = ask('Gửi cảnh cáo');
    if (!reason) return;
    const policy = prompt('Dẫn chiếu chính sách (ví dụ: docs/03 §9)') ?? '';
    run(() => api.adminSanction(d.id, { level: 'Warning', reason, policy }), 'Đã gửi cảnh cáo.');
  };

  const restrict = () => {
    const kind = prompt(`Hạn chế nào?\n\n${RESTRICTIONS.map((r, i) => `${i + 1}. ${r[1]}`).join('\n')}`);
    const picked = RESTRICTIONS[Number(kind) - 1];
    if (!picked) return;
    const reason = ask(`Hạn chế: ${picked[1]}`);
    if (!reason) return;
    const liftedWhen = prompt('Điều kiện để được gỡ hạn chế') ?? '';
    run(() => api.adminSanction(d.id, {
      level: 'Restriction', restriction: picked[0], reason, liftedWhen
    }), 'Đã áp hạn chế.');
  };

  // docs/08 §6 — the cost is shown before anything happens, never after.
  const showPreview = async () => {
    try { setPreview(await api.adminLockPreview(d.id)); }
    catch (err) { toast(err.message); }
  };

  const suspend = () => {
    const reason = ask('Tạm khoá tài khoản');
    if (!reason) return;
    const severe = prompt(
      `Có phải vi phạm nghiêm trọng theo §5.6 không? Để trống nếu không.\n\n${
        SEVERE.map((g, i) => `${i + 1}. ${g}`).join('\n')}`);
    const days = prompt('Tạm khoá bao nhiêu ngày? Để trống = tới khi xử lý xong.');
    run(() => api.adminSanction(d.id, {
      level: 'Suspension', reason,
      severeGround: SEVERE[Number(severe) - 1] ?? null,
      days: Number(days) || null,
      refundInFull: true
    }), 'Đã tạm khoá tài khoản.');
  };

  const ban = () => {
    const ground = prompt(
      `Khoá vĩnh viễn chỉ dùng cho:\n\n${BAN_GROUNDS.map((g, i) => `${i + 1}. ${g}`).join('\n')}`);
    const picked = BAN_GROUNDS[Number(ground) - 1];
    if (!picked) return;
    const reason = ask(`Khoá vĩnh viễn: ${picked}`);
    if (!reason) return;
    run(() => api.adminSanction(d.id, { level: 'Ban', reason, severeGround: picked }),
        'Đã khoá vĩnh viễn.');
  };

  const restore = () => {
    const reason = ask('Khôi phục tài khoản');
    if (!reason) return;
    run(() => api.adminRestore(d.id, { reason }), 'Đã khôi phục tài khoản.');
  };

  const viewIdentity = async () => {
    const reason = ask('Xem ảnh giấy tờ tuỳ thân');
    if (!reason) return;
    try { setIdentity(await api.adminIdentity(d.id, { reason })); }
    catch (err) { toast(err.message); }
  };

  return (
    <div className="stat" style={{ marginTop: 20, padding: 20 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <b style={{ fontSize: 17 }}>{d.fullName}</b>
          <div style={{ fontSize: 13, color: 'var(--ink-muted)' }}>
            {d.email}{d.phone ? ` · ${d.phone}` : ''} · tham gia {longDate(d.joinedAt)}
          </div>
        </div>
        <button className="link-btn" onClick={close}>Đóng</button>
      </div>

      <div style={{ display: 'flex', gap: 8, marginTop: 12, flexWrap: 'wrap' }}>
        <span className={`badge ${d.isLocked ? 'cancelled' : 'confirmed'}`}>{d.statusLabel}</span>
        {d.identityVerified && <span className="badge confirmed">Đã xác minh danh tính</span>}
        {d.emailConfirmed && <span className="badge confirmed">Email đã xác thực</span>}
        {d.isHost && <span className="badge">{d.isSuperhost ? 'Siêu chủ nhà' : 'Chủ nhà'}</span>}
        {!!d.suspendedUntil && <span className="badge pending">Tới {dateTime(d.suspendedUntil)}</span>}
      </div>

      <div className="stat-grid" style={{ marginTop: 16 }}>
        <Cell label="Đơn đặt" value={String(d.bookings)} note={`${d.cancellations} huỷ · ${d.cancellationRate}%`} />
        <Cell label="Đánh giá đã viết" value={String(d.reviewsWritten)} note={`${d.reportsAgainst} báo cáo bị nhận`} />
        <Cell label="Số dư" value={money(d.balance)} note={d.cards.join(' · ') || 'Chưa lưu thẻ nào'} />
        <Cell label="Tài khoản nhận tiền"
              value={d.payoutAccountLast4 ? `•••• ${d.payoutAccountLast4}` : '—'}
              note={d.payoutBankName ?? 'Chỉ vai Tài chính xem được'} />
      </div>

      {/* docs/08 §4 and QT-U-03 — same signal that catches fraud. */}
      {!!d.relatedAccounts.length && (
        <div style={{ marginTop: 18 }}>
          <span className="cap">Tài khoản có liên quan</span>
          <div style={{ display: 'grid', gap: 6, marginTop: 8 }}>
            {d.relatedAccounts.map(r => (
              <div key={r.id} style={{ fontSize: 13 }}>
                <b>{r.fullName}</b> · {r.email} · <span style={{ color: 'var(--ink-muted)' }}>{r.statusLabel}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {!!d.sanctions.length && (
        <div style={{ marginTop: 18 }}>
          <span className="cap">Hồ sơ vi phạm</span>
          <div className="table-wrap" style={{ marginTop: 8 }}>
            <table className="admin-table">
              <thead><tr><th>Mức</th><th>Lý do</th><th>Người quyết định</th><th>Khi nào</th></tr></thead>
              <tbody>
                {d.sanctions.map(s => (
                  <tr key={s.id}>
                    <td>
                      <b>{s.levelLabel}</b>
                      {!!s.restrictionLabel && <span>{s.restrictionLabel}</span>}
                      {s.overturnedOnAppeal && <span>Đã gỡ theo khiếu nại</span>}
                      {!!s.liftedAt && !s.overturnedOnAppeal && <span>Đã gỡ</span>}
                    </td>
                    <td>{s.reason}<span>{s.policy}</span></td>
                    <td>{s.decidedBy}</td>
                    <td>{dateTime(s.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* docs/08 §6 and QT-U-07 — what a lock would cost, before it happens. */}
      {!!preview && (
        <div className={`book-alert ${preview.guestsStaying ? '' : 'is-error'}`} style={{ marginTop: 16 }}>
          <b>{preview.warning}</b>
          {!!preview.openDisputeNotice && <span>{preview.openDisputeNotice}</span>}
          {!!preview.safetyNotice && <span>{preview.safetyNotice}</span>}
          <div style={{ marginTop: 8, display: 'grid', gap: 4, fontSize: 12.5 }}>
            {preview.lines.map(l => (
              <div key={l.bookingId}>
                <b>{l.reference}</b> · {l.counterparty} · {l.note}
                {l.money > 0 ? ` · ${money(l.money)}` : ''}
              </div>
            ))}
          </div>
        </div>
      )}

      {!!identity && (
        <div style={{ marginTop: 16 }}>
          <span className="cap">{identity.documentLabel} •••• {identity.documentLast4}</span>
          <p style={{ fontSize: 12.5, color: 'var(--ink-muted)', margin: '6px 0' }}>
            Ảnh có đóng dấu mờ: <b>{identity.watermark}</b>. Lượt xem này đã được ghi nhật ký riêng.
          </p>
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
            {[identity.frontImageUrl, identity.backImageUrl, identity.selfieImageUrl]
              .filter(Boolean)
              .map(url => (
                <div key={url} className="id-shot">
                  <img src={url} alt="" />
                  <span>{identity.watermark}</span>
                </div>
              ))}
          </div>
        </div>
      )}

      <div style={{ display: 'flex', gap: 10, marginTop: 18, flexWrap: 'wrap' }}>
        <button className="btn btn-outline btn-sm" onClick={showPreview}>Xem trước hậu quả khoá</button>
        {may('Warn') && <button className="btn btn-outline btn-sm" disabled={busy} onClick={warn}>Cảnh cáo</button>}
        {may('Restrict') && <button className="btn btn-outline btn-sm" disabled={busy} onClick={restrict}>Hạn chế</button>}
        {may('Suspend') && !d.isLocked &&
          <button className="btn btn-outline btn-sm" disabled={busy} onClick={suspend}>Tạm khoá</button>}
        {may('Ban') && !d.isLocked &&
          <button className="btn btn-outline btn-sm" disabled={busy} onClick={ban}>Khoá vĩnh viễn</button>}
        {may('Restore') && d.isLocked &&
          <button className="btn btn-primary btn-sm" disabled={busy} onClick={restore}>Khôi phục</button>}
        {may('ViewIdentityDocuments') &&
          <button className="btn btn-outline btn-sm" onClick={viewIdentity}>Xem giấy tờ</button>}
        {may('ForcePasswordReset') && (
          <button className="btn btn-outline btn-sm" disabled={busy} onClick={() => {
            const reason = ask('Buộc đổi mật khẩu và huỷ mọi phiên');
            if (reason) run(() => api.adminForcePasswordReset(d.id, { reason }), 'Đã huỷ mọi phiên.');
          }}>Buộc đổi mật khẩu</button>
        )}
        {may('ForceIdentityRecheck') && (
          <button className="btn btn-outline btn-sm" disabled={busy} onClick={() => {
            const reason = ask('Buộc xác minh lại danh tính');
            if (reason) run(() => api.adminForceIdentityRecheck(d.id, { reason }), 'Đã yêu cầu xác minh lại.');
          }}>Xác minh lại danh tính</button>
        )}
      </div>
    </div>
  );
}

function Cell({ label, value, note }) {
  return (
    <div className="stat">
      <span className="cap">{label}</span>
      <b style={{ display: 'block', fontSize: 20, margin: '6px 0 2px' }}>{value}</b>
      <span style={{ fontSize: 12.5, color: 'var(--ink-muted)' }}>{note}</span>
    </div>
  );
}

/** docs/08 §8 — the appeal queue, and who may read each one. */
export function AppealsPanel() {
  const [rows, setRows] = useState([]);
  const [busy, setBusy] = useState(false);

  const load = async () => {
    try { setRows(await api.adminAppeals()); }
    catch { /* an admin without the role simply does not see this */ }
  };
  useEffect(() => { load(); }, []);

  if (!rows.length) return null;

  const decide = async (a, result) => {
    const outcome = prompt(
      'Kết quả phải nêu lý do, ít nhất 40 ký tự — trả lời cụt lủn không phải là trả lời:');
    if (!outcome || outcome.trim().length < 40) {
      if (outcome !== null) toast('Kết quả quá ngắn.');
      return;
    }
    setBusy(true);
    try {
      setRows(await api.adminDecideAppeal(a.id, { result, outcome }));
      toast('Đã trả lời khiếu nại.');
    } catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Khiếu nại quyết định</h2>
      <p className="section-sub">
        Mỗi quyết định được khiếu nại một lần. Người xét lại phải khác người ra quyết định.
      </p>

      <div className="table-wrap" style={{ marginTop: 16 }}>
        <table className="admin-table">
          <thead>
            <tr><th>Người khiếu nại</th><th>Quyết định</th><th>Lý lẽ</th><th>Hạn trả lời</th><th /></tr>
          </thead>
          <tbody>
            {rows.map(a => (
              <tr key={a.id}>
                <td><b>{a.userName}</b></td>
                <td>{a.sanctionLevel}<span>{a.sanctionReason}</span></td>
                <td style={{ maxWidth: 320 }}>{a.argument}</td>
                <td>
                  {longDate(a.dueBy)}
                  {a.overdue && <span>Đã quá hạn</span>}
                </td>
                <td style={{ whiteSpace: 'nowrap' }}>
                  {a.status !== 'Open'
                    ? <span className="badge">{a.statusLabel}</span>
                    : a.mayReview
                      ? <>
                          <button className="link-btn" disabled={busy}
                                  onClick={() => decide(a, 'Upheld')}>Giữ nguyên</button>
                          <button className="link-btn" style={{ marginLeft: 8 }} disabled={busy}
                                  onClick={() => decide(a, 'Reduced')}>Giảm mức</button>
                          <button className="link-btn" style={{ marginLeft: 8 }} disabled={busy}
                                  onClick={() => decide(a, 'Overturned')}>Gỡ bỏ</button>
                        </>
                      : <span style={{ fontSize: 12.5, color: 'var(--ink-muted)' }}>
                          Bạn đã ra quyết định này
                        </span>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

/** docs/08 §10 — the scoreboard, the alarms, and what is waiting on a second name. */
export function OversightPanel() {
  const [d, setD] = useState(null);
  const [busy, setBusy] = useState(false);

  const load = async () => {
    try { setD(await api.adminOversight()); }
    catch { setD(null); }
  };
  useEffect(() => { load(); }, []);

  if (!d) return null;

  const decide = async (m, approve) => {
    const reason = prompt(approve ? 'Ghi chú khi duyệt' : 'Lý do từ chối') ?? '';
    if (reason.trim().length < 10) { toast('Cần ghi lý do ít nhất 10 ký tự.'); return; }
    setBusy(true);
    try { await api.adminDecideApproval(m.id, { approve, reason }); await load(); toast('Đã xử lý.'); }
    catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Giám sát quản trị viên</h2>
      <p className="section-sub">
        Khoản tiền từ {money(d.twoPersonThreshold)} trở lên cần hai người duyệt.
        Mỗi tháng {d.randomReviewPercent}% quyết định được đọc lại.
      </p>

      {!!d.flags.length && (
        <div className="book-alert is-error" style={{ marginTop: 14 }}>
          <b>{d.flags.length} cảnh báo cần xem</b>
          {d.flags.map((f, i) => <span key={i}>{f.adminName}: {f.label} — {f.detail}</span>)}
        </div>
      )}

      {!!d.pendingApprovals.length && (
        <div style={{ marginTop: 16 }}>
          <span className="cap">Chờ người thứ hai duyệt</span>
          <div className="table-wrap" style={{ marginTop: 8 }}>
            <table className="admin-table">
              <thead><tr><th>Việc</th><th>Số tiền</th><th>Người đề nghị</th><th /></tr></thead>
              <tbody>
                {d.pendingApprovals.map(m => (
                  <tr key={m.id}>
                    <td><b>{m.target}</b><span>{m.reason}</span></td>
                    <td>{money(m.amount)}</td>
                    <td>{m.requestedBy}</td>
                    <td style={{ whiteSpace: 'nowrap' }}>
                      {m.mayApprove
                        ? <>
                            <button className="link-btn" disabled={busy}
                                    onClick={() => decide(m, true)}>Duyệt</button>
                            <button className="link-btn" style={{ marginLeft: 8 }} disabled={busy}
                                    onClick={() => decide(m, false)}>Từ chối</button>
                          </>
                        : <span style={{ fontSize: 12.5, color: 'var(--ink-muted)' }}>
                            Bạn là người đề nghị
                          </span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <div className="table-wrap" style={{ marginTop: 16 }}>
        <table className="admin-table">
          <thead>
            <tr><th>Quản trị viên</th><th>Quyền</th><th>Đã xem</th><th>Quyết định</th><th>Bị khiếu nại</th><th>Rà soát</th></tr>
          </thead>
          <tbody>
            {d.admins.map(a => (
              <tr key={a.adminUserId}>
                <td>
                  <b>{a.name}</b>
                  {!a.twoFactorEnabled && <span>Chưa bật bảo mật 2 lớp</span>}
                </td>
                <td>{a.scopes}</td>
                <td>{a.profilesViewed}</td>
                <td>{a.decisions}</td>
                <td>
                  {a.appealsUpheld}/{a.appealsAgainst}
                  {a.looksUnreliable && <span>Tỉ lệ {a.overturnRatePercent}% bất thường</span>}
                </td>
                <td>
                  {a.accessReviewDue ? 'Tới hạn' : 'Đã rà soát'}
                  {a.scopeLooksUnused && <span>Chưa dùng quyền quá 90 ngày</span>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {!!d.randomSample.length && (
        <div style={{ marginTop: 18 }}>
          <span className="cap">Mẫu rà soát ngẫu nhiên tháng này</span>
          <div style={{ display: 'grid', gap: 6, marginTop: 8, fontSize: 13 }}>
            {d.randomSample.map(x => (
              <div key={x.id}>
                <b>{x.level}</b> · {x.userName} · {x.decidedBy} · {longDate(x.at)}
                <span style={{ color: 'var(--ink-muted)' }}> — {x.reason}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </section>
  );
}

/** docs/08 §9 — export and erasure requests, with what is standing in the way. */
export function DataRequestsPanel() {
  const [rows, setRows] = useState([]);
  const [busy, setBusy] = useState(false);

  const load = async () => {
    try { setRows(await api.adminDataRequests()); }
    catch { /* not this admin's job */ }
  };
  useEffect(() => { load(); }, []);

  if (!rows.length) return null;

  const erase = async r => {
    const reason = prompt('Lý do xử lý yêu cầu xoá (bắt buộc, ít nhất 10 ký tự):');
    if (!reason || reason.trim().length < 10) return;
    setBusy(true);
    try { setRows(await api.adminErase(r.id, { reason })); toast('Đã ẩn danh tài khoản.'); }
    catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>Yêu cầu dữ liệu cá nhân</h2>
      <p className="section-sub">
        Xoá tài khoản là ẩn danh hoá: tên, ảnh, email, giấy tờ bị xoá; đơn đặt và sổ ghi tiền giữ nguyên.
      </p>

      <div className="table-wrap" style={{ marginTop: 16 }}>
        <table className="admin-table">
          <thead><tr><th>Người dùng</th><th>Yêu cầu</th><th>Hạn</th><th>Trạng thái</th><th /></tr></thead>
          <tbody>
            {rows.map(r => (
              <tr key={r.id}>
                <td><b>{r.userName}</b><span>{r.email}</span></td>
                <td>{r.kindLabel}</td>
                <td>{longDate(r.dueBy)}{r.overdue && <span>Đã quá hạn</span>}</td>
                <td>
                  {r.statusLabel}
                  {!!r.blockers.length && <span>Vướng: {r.blockers.join(', ')}</span>}
                </td>
                <td>
                  {r.kind === 'Erase' && r.status === 'Open' && r.mayErase && (
                    <button className="link-btn" disabled={busy} onClick={() => erase(r)}>Xoá</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, loadAdmin, toast } from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate } from '../lib/format.js';

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

      <LedgerPanel ledger={d.ledger} />

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

function Stat({ label, value, note }) {
  return (
    <div className="stat">
      <div className="value" style={{ fontSize: 'clamp(20px,2.4vw,26px)' }}>{value}</div>
      <div className="label">{label}</div>
      <div className="note">{note}</div>
    </div>
  );
}

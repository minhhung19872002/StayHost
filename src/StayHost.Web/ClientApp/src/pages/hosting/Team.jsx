import { useEffect, useState } from 'react';
import { api } from '../../lib/api.js';
import { state as store, toast } from '../../lib/store.js';
import { longDate } from '../../lib/format.js';

/**
 * docs/01 QL-19 — invite someone to help run a listing, choose how much they
 * may touch, and take it back. Both sides live here: the people this host
 * invited, and the invitations waiting for this host.
 */
export function Team() {
  const [board, setBoard] = useState(null);
  const [error, setError] = useState(null);

  const reload = () => api.coHosts().then(setBoard).catch(e => setError(e.message));
  useEffect(() => { reload(); }, []);

  if (error) return <div className="empty-state" style={{ marginTop: 24 }}><h3>{error}</h3></div>;
  if (!board) return <div className="stat skeleton" style={{ height: 200, border: 0, marginTop: 24 }} />;

  return (
    <div style={{ marginTop: 24, display: 'grid', gap: 34 }}>
      <Invites invites={board.helping} onDone={reload} />
      <InviteForm scopes={board.scopes} onDone={reload} />
      <Granted rows={board.invited} onDone={reload} />
    </div>
  );
}

function Invites({ invites, onDone }) {
  const pending = invites.filter(i => i.status === 'invited');
  if (!pending.length) return null;

  const answer = async (id, decision) => {
    try {
      await api.respondCoHost(id, decision);
      toast(decision === 'accept' ? 'Bạn đã nhận lời mời đồng quản lý.' : 'Đã từ chối lời mời.');
      onDone();
    } catch (err) { toast(err.message); }
  };

  return (
    <section>
      <h2 className="section-title" style={{ fontSize: 20 }}>Lời mời dành cho bạn</h2>
      {pending.map(i => (
        <div className="team-row" key={i.id}>
          <div style={{ minWidth: 0, flex: 1 }}>
            <b>{i.ownerName}</b>
            <div className="team-sub">{i.listingTitle ?? 'Tất cả chỗ nghỉ'} · {i.scopeLabel}</div>
          </div>
          <button className="btn btn-primary btn-sm" onClick={() => answer(i.id, 'accept')}>Nhận lời</button>
          <button className="btn btn-outline btn-sm" onClick={() => answer(i.id, 'decline')}>Từ chối</button>
        </div>
      ))}
    </section>
  );
}

function InviteForm({ scopes, onDone }) {
  const listings = store.hosting?.listings ?? [];
  const [email, setEmail] = useState('');
  const [listingId, setListingId] = useState('');
  const [picked, setPicked] = useState(['calendar', 'messages']);
  const [busy, setBusy] = useState(false);

  const toggle = key => setPicked(p => (p.includes(key) ? p.filter(k => k !== key) : [...p, key]));

  const send = async e => {
    e.preventDefault();
    setBusy(true);
    try {
      await api.inviteCoHost({
        email: email.trim(),
        listingId: listingId ? Number(listingId) : null,
        scopes: picked
      });
      setEmail('');
      toast('Đã gửi lời mời đồng quản lý.');
      onDone();
    } catch (err) { toast(err.message); } finally { setBusy(false); }
  };

  return (
    <section>
      <h2 className="section-title" style={{ fontSize: 20 }}>Mời người đồng quản lý</h2>
      <p className="section-sub">Dù được cấp quyền nào, người đồng quản lý cũng không thấy tài khoản nhận tiền của bạn.</p>

      <form onSubmit={send} style={{ maxWidth: 560, marginTop: 16 }}>
        <div className="field-grid">
          <label className="form-field"><span className="cap">Email người được mời</span>
            <input type="email" required value={email} placeholder="ban@vidu.vn"
                   onChange={e => setEmail(e.target.value)} /></label>
          <label className="form-field"><span className="cap">Áp dụng cho</span>
            <select value={listingId} onChange={e => setListingId(e.target.value)}>
              <option value="">Tất cả chỗ nghỉ của tôi</option>
              {listings.map(l => <option key={l.id} value={l.id}>{l.title}</option>)}
            </select>
          </label>
        </div>

        <p className="cap" style={{ margin: '14px 0 8px' }}>Được làm gì</p>
        <div className="pill-row">
          {scopes.map(s => (
            <button type="button" key={s.key} className={`pill ${picked.includes(s.key) ? 'is-on' : ''}`}
                    onClick={() => toggle(s.key)}>{s.label}</button>
          ))}
        </div>

        <button type="submit" className="btn btn-primary" style={{ marginTop: 18 }}
                disabled={busy || !picked.length}>Gửi lời mời</button>
      </form>
    </section>
  );
}

function Granted({ rows, onDone }) {
  const revoke = async row => {
    if (!confirm(`Thu hồi quyền của ${row.name ?? row.email}?`)) return;
    try {
      await api.revokeCoHost(row.id);
      toast('Đã thu hồi quyền đồng quản lý.');
      onDone();
    } catch (err) { toast(err.message); }
  };

  return (
    <section>
      <h2 className="section-title" style={{ fontSize: 20 }}>Người đang đồng quản lý</h2>
      {rows.length ? rows.map(r => (
        <div className="team-row" key={r.id}>
          <div style={{ minWidth: 0, flex: 1 }}>
            <b>{r.name ?? r.email}</b>
            <div className="team-sub">
              {r.listingTitle ?? 'Tất cả chỗ nghỉ'} · {r.scopeLabel} · mời ngày {longDate(r.invitedAt)}
            </div>
          </div>
          <span className={`badge ${r.status === 'active' ? 'confirmed' : 'pending'}`}>{r.statusLabel}</span>
          <button className="btn btn-outline btn-sm" onClick={() => revoke(r)}>Thu hồi</button>
        </div>
      )) : <p className="section-sub">Chưa mời ai.</p>}
    </section>
  );
}

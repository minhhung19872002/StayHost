import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { toast, loadMe } from '../lib/store.js';
import { api } from '../lib/api.js';
import { Avatar } from '../components/Avatar.jsx';

const VISIBILITY = [
  ['Friends', 'Chỉ bạn bè'],
  ['Public', 'Mọi người'],
  ['Private', 'Chỉ mình tôi']
];

/**
 * docs/01 XH-01, XH-02 — friends, incoming requests, and the privacy of your own
 * journey map. Open where signed-in members manage their connections.
 */
export function Friends() {
  const state = useStore();
  const navigate = useNavigate();
  const [friends, setFriends] = useState(null);
  const [requests, setRequests] = useState([]);

  const load = async () => {
    try {
      const [f, r] = await Promise.all([api.friends(), api.friendRequests()]);
      setFriends(f); setRequests(r);
    } catch { setFriends([]); }
  };
  useEffect(() => { if (state.user) load(); }, [state.user]);

  if (!state.user) {
    return <div className="shell" style={{ paddingBlock: '40px 90px' }}>
      <div className="empty-state"><h3>Đăng nhập để kết nối bạn bè</h3></div>
    </div>;
  }
  if (!friends) return <div className="shell" style={{ paddingBlock: '40px 90px' }}>
    <div className="sk-line skeleton" style={{ width: 220, height: 26 }} /></div>;

  const respond = async (id, decision) => {
    try { await api.respondFriend(id, decision); await load(); toast(decision === 'accept' ? 'Đã kết bạn.' : 'Đã từ chối.'); }
    catch (err) { toast(err.message); }
  };
  const unfriend = async userId => {
    if (!confirm('Huỷ kết bạn với người này?')) return;
    try { await api.removeFriend(userId); await load(); toast('Đã huỷ kết bạn.'); }
    catch (err) { toast(err.message); }
  };
  const setVis = async v => {
    try { await api.setJourneyVisibility(v); await loadMe(); toast('Đã cập nhật quyền riêng tư hành trình.'); }
    catch (err) { toast(err.message); }
  };

  return (
    <div className="shell" style={{ paddingBlock: '28px 90px', maxWidth: 760 }}>
      <h1 className="section-title">Bạn bè</h1>
      <p className="section-sub">Kết nối để xem nơi bạn bè đã đi và sắp đi.</p>

      {/* docs/01 XH-02 — who may see my journey map. */}
      <section className="modal-section" style={{ marginTop: 12 }}>
        <h3>Hành trình của tôi hiển thị với</h3>
        <div className="pill-row" style={{ marginTop: 10 }}>
          {VISIBILITY.map(([v, label]) => (
            <button key={v} className={`pill ${state.user.journeyVisibility === v ? 'is-on' : ''}`}
                    onClick={() => setVis(v)}>{label}</button>
          ))}
        </div>
      </section>

      {requests.length > 0 && (
        <section style={{ marginTop: 28 }}>
          <h2 className="section-title" style={{ fontSize: 18 }}>Lời mời kết bạn ({requests.length})</h2>
          {requests.map(r => (
            <div className="verify-row" key={r.id}>
              <Avatar url={r.avatarUrl} initials={r.initials} size={40} />
              <div style={{ flex: 1, minWidth: 0 }}><b>{r.name}</b></div>
              <button className="btn btn-primary btn-sm" onClick={() => respond(r.id, 'accept')}>Chấp nhận</button>
              <button className="btn btn-outline btn-sm" onClick={() => respond(r.id, 'decline')}>Từ chối</button>
            </div>
          ))}
        </section>
      )}

      <section style={{ marginTop: 28 }}>
        <h2 className="section-title" style={{ fontSize: 18 }}>Bạn bè ({friends.length})</h2>
        {friends.length === 0
          ? <p className="section-sub">Chưa có bạn bè nào. Mở hồ sơ của ai đó và bấm “Kết bạn”.</p>
          : friends.map(f => (
              <div className="verify-row" key={f.userId}>
                <Avatar url={f.avatarUrl} initials={f.initials} size={40} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <button className="text-btn" style={{ fontWeight: 700 }}
                          onClick={() => navigate(`/users/${f.userId}`)}>{f.name}</button>
                </div>
                <button className="btn btn-outline btn-sm" onClick={() => navigate(`/users/${f.userId}`)}>Hành trình</button>
                <button className="btn btn-outline btn-sm" onClick={() => unfriend(f.userId)}>Huỷ kết bạn</button>
              </div>
            ))}
      </section>
    </div>
  );
}

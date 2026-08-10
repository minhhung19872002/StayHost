import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { api } from '../lib/api.js';
import { Card } from '../components/Card.jsx';

/**
 * docs/01 YT-05 — a wishlist shared by link, read-only, for anyone who has it.
 * No sign-in: the token in the URL is the permission.
 */
export function SharedWishlist() {
  const { token } = useParams();
  const navigate = useNavigate();
  const [detail, setDetail] = useState(null);
  const [missing, setMissing] = useState(false);

  useEffect(() => {
    api.sharedWishlist(token).then(setDetail).catch(() => setMissing(true));
  }, [token]);

  if (missing) {
    return (
      <div className="shell" style={{ paddingBlock: '40px 90px' }}>
        <div className="empty-state">
          <h3>Không mở được danh sách này</h3>
          <p>Liên kết có thể đã bị chủ danh sách tắt chia sẻ.</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }} onClick={() => navigate('/')}>Về trang chủ</button>
        </div>
      </div>
    );
  }

  if (!detail) {
    return <div className="shell" style={{ paddingBlock: '40px 90px' }}>
      <div className="sk-line skeleton" style={{ width: 260, height: 28 }} />
    </div>;
  }

  return (
    <div className="shell" style={{ paddingBlock: '28px 80px' }}>
      <h1 className="section-title" style={{ fontSize: 26 }}>{detail.list.name}</h1>
      <p className="section-sub">Danh sách yêu thích được chia sẻ · {detail.items.length} chỗ nghỉ</p>

      {detail.items.length ? (
        <div className="card-grid" style={{ marginTop: 20 }}>
          {detail.items.map(e => (
            <div key={e.card.id}>
              <Card card={e.card} lazy />
              <GroupVote token={token} entry={e} />
            </div>
          ))}
        </div>
      ) : (
        <p className="section-sub" style={{ marginTop: 24 }}>Danh sách này chưa có chỗ nghỉ nào.</p>
      )}
    </div>
  );
}

/** docs/01 YT-06 — thumbs up/down for the group, one vote per viewer. */
function GroupVote({ token, entry }) {
  const [v, setV] = useState({ up: entry.up, down: entry.down, mine: entry.myVote });
  const [busy, setBusy] = useState(false);

  const vote = async up => {
    if (busy) return;
    setBusy(true);
    try {
      const r = await api.voteSharedWishlist(token, entry.card.id, up);
      setV({ up: r.up, down: r.down, mine: r.myVote });
    } catch { /* ignore */ } finally { setBusy(false); }
  };

  return (
    <div style={{ display: 'flex', gap: 8, marginTop: 8, alignItems: 'center' }}>
      <button className={`btn btn-sm ${v.mine === true ? 'btn-primary' : 'btn-outline'}`}
              disabled={busy} onClick={() => vote(true)} aria-pressed={v.mine === true}>👍 {v.up}</button>
      <button className={`btn btn-sm ${v.mine === false ? 'btn-primary' : 'btn-outline'}`}
              disabled={busy} onClick={() => vote(false)} aria-pressed={v.mine === false}>👎 {v.down}</button>
    </div>
  );
}

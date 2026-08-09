import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, loadFavorites, loadWishlists, openWishlist, toast } from '../lib/store.js';
import { api } from '../lib/api.js';
import { Card } from '../components/Card.jsx';
import { Icon } from '../components/Icon.jsx';

/** docs/01 YT-05 — turn a shareable link on/off and copy it. */
function ShareWishlist({ list, onChanged }) {
  const shared = !!list.shareToken;

  const toggle = async () => {
    try {
      await api.shareWishlist(list.id, !shared);
      onChanged?.();
      if (!shared) toast('Đã bật chia sẻ. Sao chép liên kết để gửi bạn bè.');
    } catch (err) { toast(err.message); }
  };

  const copy = async () => {
    const url = `${location.origin}/wishlist/${list.shareToken}`;
    try { await navigator.clipboard.writeText(url); toast('Đã sao chép liên kết chia sẻ.'); }
    catch { toast(url); }
  };

  return (
    <>
      <button className="btn btn-outline btn-sm" onClick={toggle}>
        {shared ? 'Tắt chia sẻ' : 'Chia sẻ'}
      </button>
      {shared && <button className="btn btn-dark btn-sm" onClick={copy}>Sao chép liên kết</button>}
    </>
  );
}

/** docs/01 YT-03 — a private note the guest keeps on a saved place. */
function WishlistNote({ listId, listingId, note, onSaved }) {
  const [editing, setEditing] = useState(false);
  const [text, setText] = useState(note ?? '');

  const save = async () => {
    try {
      await api.setWishlistNote(listId, listingId, text.trim() || null);
      setEditing(false);
      onSaved?.();
    } catch (err) { toast(err.message); }
  };

  if (!editing) {
    return (
      <button className="wishlist-note-btn" onClick={() => { setText(note ?? ''); setEditing(true); }}>
        {note ? `📝 ${note}` : '+ Thêm ghi chú'}
      </button>
    );
  }

  return (
    <div className="wishlist-note-edit">
      <textarea rows={2} value={text} maxLength={500} autoFocus
                placeholder="Ghi chú cho riêng bạn về chỗ này…"
                onChange={e => setText(e.target.value)} />
      <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end', marginTop: 6 }}>
        <button className="btn btn-outline btn-sm" onClick={() => setEditing(false)}>Huỷ</button>
        <button className="btn btn-dark btn-sm" onClick={save}>Lưu</button>
      </div>
    </div>
  );
}

export function Wishlists() {
  const state = useStore();

  useEffect(() => {
    set({ activeWishlist: null });
    loadFavorites();
    loadWishlists();
  }, []);

  return state.activeWishlist ? <One /> : <Index />;
}

function Index() {
  const state = useStore();
  const navigate = useNavigate();
  const lists = state.wishlists ?? [];

  const create = async () => {
    const name = prompt('Tên danh sách mới', 'Chuyến đi sắp tới');
    if (!name?.trim()) return;
    try { await api.createWishlist(name.trim()); await loadWishlists(); toast('Đã tạo danh sách.'); }
    catch (err) { toast(err.message); }
  };

  return (
    <div className="shell" style={{ paddingBlock: '30px 90px' }}>
      <div className="page-head">
        <div>
          <h1 className="section-title">Danh sách yêu thích</h1>
          <p className="section-sub">
            {lists.length} danh sách · {lists.reduce((n, l) => n + l.count, 0)} chỗ nghỉ đã lưu
          </p>
        </div>
        <button className="btn btn-outline btn-sm" onClick={create}>+ Tạo danh sách</button>
      </div>

      {lists.length ? (
        <div className="wl-grid">
          {lists.map(list => (
            <article className="wl-card" key={list.id} onClick={() => {
              openWishlist(list.id);
              window.scrollTo({ top: 0, behavior: 'instant' });
            }}>
              <div className="wl-cover">
                {list.coverImages?.length
                  ? list.coverImages.slice(0, 4).map((url, i) => <img src={url} alt="" key={i} loading="lazy" decoding="async" />)
                  : <div className="wl-empty"><Icon name="heart" size={28} /></div>}
              </div>
              <div className="wl-body">
                <b>{list.name}</b>
                <span>{list.count} chỗ nghỉ{list.isDefault ? ' · mặc định' : ''}</span>
              </div>
            </article>
          ))}
        </div>
      ) : (
        <div className="empty-state" style={{ marginTop: 24 }}>
          <h3>Chưa lưu chỗ nghỉ nào</h3>
          <p>Nhấn ♥ trên bất kỳ chỗ nghỉ để lưu lại đây.</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }} onClick={() => navigate('/')}>Khám phá chỗ nghỉ</button>
        </div>
      )}
    </div>
  );
}

function One() {
  const state = useStore();
  const navigate = useNavigate();
  const detail = state.activeWishlist;
  const list = detail.list;

  const rename = async () => {
    const name = prompt('Tên danh sách', list.name);
    if (!name?.trim() || name.trim() === list.name) return;
    try { await api.renameWishlist(list.id, name.trim()); await openWishlist(list.id); toast('Đã đổi tên.'); }
    catch (err) { toast(err.message); }
  };

  const remove = async () => {
    if (!confirm('Xoá danh sách này? Các chỗ nghỉ trong đó cũng bị bỏ lưu.')) return;
    try {
      await api.deleteWishlist(list.id);
      set({ activeWishlist: null });
      await Promise.all([loadWishlists(), loadFavorites()]);
      toast('Đã xoá danh sách.');
    } catch (err) { toast(err.message); }
  };

  return (
    <div className="shell" style={{ paddingBlock: '26px 90px' }}>
      <button className="back-link" onClick={() => { set({ activeWishlist: null }); loadWishlists(); }}>
        ← Tất cả danh sách
      </button>

      <div className="page-head" style={{ marginTop: 8 }}>
        <div>
          <h1 className="section-title">{list.name}</h1>
          <p className="section-sub">{list.count} chỗ nghỉ</p>
        </div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <button className="btn btn-outline btn-sm" onClick={rename}>Đổi tên</button>
          <ShareWishlist list={list} onChanged={() => openWishlist(list.id)} />
          {!list.isDefault && <button className="btn btn-outline btn-sm" onClick={remove}>Xoá danh sách</button>}
        </div>
      </div>

      {detail.items.length ? (
        <div className="card-grid" style={{ marginTop: 20 }}>
          {detail.items.map(e => (
            <div key={e.card.id}>
              <Card card={e.card} lazy />
              <WishlistNote listId={list.id} listingId={e.card.id} note={e.note}
                            onSaved={() => openWishlist(list.id)} />
            </div>
          ))}
        </div>
      ) : (
        <div className="empty-state" style={{ marginTop: 24 }}>
          <h3>Danh sách này còn trống</h3>
          <p>Nhấn ♥ trên chỗ nghỉ bạn thích để thêm vào đây.</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }} onClick={() => navigate('/')}>Khám phá chỗ nghỉ</button>
        </div>
      )}
    </div>
  );
}

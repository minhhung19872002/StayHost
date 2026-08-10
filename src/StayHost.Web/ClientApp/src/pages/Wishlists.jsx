import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, loadFavorites, loadWishlists, openWishlist, toast } from '../lib/store.js';
import { api } from '../lib/api.js';
import { money } from '../lib/format.js';
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

/** docs/01 YT-07 — a side-by-side comparison of 2–5 listings. */
function CompareTable({ cards, onClose, navigate }) {
  const rows = [
    ['Giá / đêm', c => money(c.pricePerNight)],
    ['Đánh giá', c => c.reviewCount ? `★ ${c.rating.toFixed(2)} (${c.reviewCount})` : 'Chưa có'],
    ['Thành phố', c => c.city],
    ['Loại', c => c.roomTypeLabel],
    ['Khách tối đa', c => `${c.maxGuests}`],
    ['Phòng ngủ', c => `${c.bedrooms}`],
    ['Giường', c => `${c.beds}`],
    ['Phòng tắm', c => `${c.bathrooms}`],
    ['Đặt ngay', c => c.instantBook ? '✓' : '—'],
    ['Siêu chủ nhà', c => c.isSuperhost ? '✓' : '—'],
    ['Phí dọn dẹp', c => money(c.cleaningFee)]
  ];

  return (
    <section style={{ marginTop: 20, marginBottom: 8 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 }}>
        <h2 className="section-title" style={{ fontSize: 18 }}>So sánh {cards.length} chỗ</h2>
        <button className="text-btn" onClick={onClose}>Đóng</button>
      </div>
      <div className="table-wrap">
        <table className="admin-table compare-table">
          <thead>
            <tr>
              <th />
              {cards.map(c => (
                <th key={c.id} style={{ minWidth: 150 }}>
                  <button className="text-btn" style={{ fontWeight: 700, textAlign: 'left' }}
                          onClick={() => navigate(`/rooms/${c.slug}`)}>{c.title}</button>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map(([label, fn]) => (
              <tr key={label}>
                <td style={{ fontWeight: 600, color: 'var(--ink-muted)' }}>{label}</td>
                {cards.map(c => <td key={c.id}>{fn(c)}</td>)}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function One() {
  const state = useStore();
  const navigate = useNavigate();
  const detail = state.activeWishlist;
  const list = detail.list;
  const [compare, setCompare] = useState(null);   // docs/01 YT-07

  // docs/01 YT-07 — compare up to five saved places side by side.
  const openCompare = async () => {
    const ids = detail.items.slice(0, 5).map(e => e.card.id);
    if (ids.length < 2) { toast('Cần ít nhất 2 chỗ để so sánh.'); return; }
    try { setCompare(await api.compareListings(ids)); }
    catch (err) { toast(err.message); }
  };

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
          {detail.items.length >= 2 && (
            <button className="btn btn-outline btn-sm" onClick={openCompare}>So sánh</button>
          )}
          <ShareWishlist list={list} onChanged={() => openWishlist(list.id)} />
          {!list.isDefault && <button className="btn btn-outline btn-sm" onClick={remove}>Xoá danh sách</button>}
        </div>
      </div>

      {compare && <CompareTable cards={compare} onClose={() => setCompare(null)} navigate={navigate} />}

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

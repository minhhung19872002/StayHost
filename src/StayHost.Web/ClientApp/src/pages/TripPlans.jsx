import { useEffect, useState } from 'react';
import { useStore } from '../lib/useStore.js';
import { toast } from '../lib/store.js';
import { api } from '../lib/api.js';

/**
 * docs/01 CĐ-10, CĐ-11 — merge bookings into one trip and build a day-by-day
 * itinerary together with invited friends.
 */
export function TripPlans() {
  const state = useStore();
  const [plans, setPlans] = useState(null);
  const [openId, setOpenId] = useState(null);

  const load = () => api.tripPlans().then(setPlans).catch(() => setPlans([]));
  useEffect(() => { if (state.user) load(); }, [state.user]);

  if (!state.user) return <div className="shell" style={{ paddingBlock: '40px 90px' }}>
    <div className="empty-state"><h3>Đăng nhập để lên lịch trình chuyến đi</h3></div></div>;
  if (!plans) return <div className="shell" style={{ paddingBlock: '40px 90px' }}>
    <div className="sk-line skeleton" style={{ width: 220, height: 26 }} /></div>;

  const create = async () => {
    const name = prompt('Tên chuyến (vd: Miền Trung tháng 9)');
    if (!name?.trim()) return;
    try { const p = await api.createTripPlan(name.trim()); await load(); setOpenId(p.id); toast('Đã tạo chuyến.'); }
    catch (err) { toast(err.message); }
  };
  const remove = async id => {
    if (!confirm('Xoá chuyến này?')) return;
    try { await api.deleteTripPlan(id); if (openId === id) setOpenId(null); await load(); toast('Đã xoá.'); }
    catch (err) { toast(err.message); }
  };

  return (
    <div className="shell" style={{ paddingBlock: '28px 90px', maxWidth: 820 }}>
      <div className="page-head">
        <div>
          <h1 className="section-title">Lịch trình chuyến đi</h1>
          <p className="section-sub">Gộp nhiều đơn thành một chuyến, lên lịch theo ngày, mời bạn cùng thêm địa điểm.</p>
        </div>
        <button className="btn btn-primary btn-sm" onClick={create}>+ Chuyến mới</button>
      </div>

      {plans.length === 0
        ? <div className="empty-state" style={{ marginTop: 20 }}><h3>Chưa có chuyến nào</h3>
            <p>Tạo một chuyến rồi thêm các đơn đã đặt vào.</p></div>
        : <div style={{ display: 'grid', gap: 10, marginTop: 16 }}>
            {plans.map(p => (
              <div key={p.id} style={{ border: '1px solid var(--line)', borderRadius: 12, padding: 14 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 10 }}>
                  <div>
                    <b>{p.name}</b>
                    <div className="meta">{p.bookingCount} đơn · {p.memberCount} bạn cùng đi{p.isOwner ? '' : ' · được mời'}</div>
                  </div>
                  <div style={{ display: 'flex', gap: 8 }}>
                    <button className="btn btn-outline btn-sm" onClick={() => setOpenId(openId === p.id ? null : p.id)}>
                      {openId === p.id ? 'Đóng' : 'Mở'}
                    </button>
                    {p.isOwner && <button className="btn btn-outline btn-sm" onClick={() => remove(p.id)}>Xoá</button>}
                  </div>
                </div>
                {openId === p.id && <TripDetail id={p.id} onChanged={load} />}
              </div>
            ))}
          </div>}
    </div>
  );
}

function TripDetail({ id, onChanged }) {
  const [d, setD] = useState(null);
  const [myBookings, setMyBookings] = useState([]);
  const [friends, setFriends] = useState([]);

  const load = () => api.tripPlan(id).then(setD).catch(() => setD(null));
  useEffect(() => {
    load();
    api.bookings().then(b => setMyBookings(b || [])).catch(() => {});
    api.friends().then(setFriends).catch(() => {});
  }, [id]);

  if (!d) return <div className="sk-line skeleton" style={{ width: 180, height: 18, marginTop: 12 }} />;

  const addBooking = async bid => { try { await api.addTripBooking(id, bid); await load(); onChanged?.(); } catch (e) { toast(e.message); } };
  const removeBooking = async bid => { try { await api.removeTripBooking(id, bid); await load(); onChanged?.(); } catch (e) { toast(e.message); } };
  const invite = async uid => { try { await api.addTripMember(id, uid); await load(); onChanged?.(); toast('Đã mời bạn.'); } catch (e) { toast(e.message); } };
  const addItem = async () => {
    const day = prompt('Ngày (YYYY-MM-DD)');
    if (!day?.trim()) return;
    const title = prompt('Địa điểm / hoạt động');
    if (!title?.trim()) return;
    try { await api.addTripItem(id, { day: day.trim(), title: title.trim(), note: null }); await load(); }
    catch (e) { toast(e.message); }
  };
  const removeItem = async iid => { try { await api.removeTripItem(id, iid); await load(); } catch (e) { toast(e.message); } };

  // Group itinerary items by day.
  const byDay = {};
  for (const it of d.items) (byDay[it.day] ||= []).push(it);
  const days = Object.keys(byDay).sort();

  const inTrip = new Set(d.bookings.map(b => b.bookingId));
  const addable = myBookings.filter(b => !inTrip.has(b.id));
  const memberIds = new Set(d.members.map(m => m.userId));
  const invitable = friends.filter(f => !memberIds.has(f.userId));

  return (
    <div style={{ marginTop: 14, borderTop: '1px solid var(--divider)', paddingTop: 12 }}>
      {/* Bookings in the trip */}
      <h4 style={{ margin: '0 0 6px', fontWeight: 800 }}>Đơn trong chuyến</h4>
      {d.bookings.length === 0 ? <p className="meta">Chưa có đơn nào.</p>
        : d.bookings.map(b => (
            <div className="verify-row" key={b.bookingId}>
              <div style={{ flex: 1, minWidth: 0 }}><b>{b.listingTitle}</b>
                <div className="meta">{b.city} · {b.checkIn} → {b.checkOut}</div></div>
              {d.isOwner && <button className="btn btn-outline btn-sm" onClick={() => removeBooking(b.bookingId)}>Bỏ</button>}
            </div>
          ))}
      {d.isOwner && addable.length > 0 && (
        <div style={{ marginTop: 6 }}>
          <select className="field" style={{ maxWidth: 320 }} defaultValue=""
                  onChange={e => { if (e.target.value) addBooking(Number(e.target.value)); e.target.value = ''; }}>
            <option value="">+ Thêm đơn đã đặt…</option>
            {addable.map(b => <option key={b.id} value={b.id}>{b.listingTitle} ({b.checkIn})</option>)}
          </select>
        </div>
      )}

      {/* Companions */}
      <h4 style={{ margin: '16px 0 6px', fontWeight: 800 }}>Cùng đi</h4>
      <div className="chip-wrap">
        {d.members.map(m => <span className="quick-chip" key={m.userId}>{m.name}{m.isOwner ? ' (chủ)' : ''}</span>)}
      </div>
      {d.isOwner && invitable.length > 0 && (
        <select className="field" style={{ maxWidth: 320, marginTop: 6 }} defaultValue=""
                onChange={e => { if (e.target.value) invite(Number(e.target.value)); e.target.value = ''; }}>
          <option value="">+ Mời bạn bè…</option>
          {invitable.map(f => <option key={f.userId} value={f.userId}>{f.name}</option>)}
        </select>
      )}

      {/* Itinerary by day */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', margin: '16px 0 6px' }}>
        <h4 style={{ margin: 0, fontWeight: 800 }}>Lịch trình theo ngày</h4>
        {d.canEdit && <button className="btn btn-outline btn-sm" onClick={addItem}>+ Thêm địa điểm</button>}
      </div>
      {days.length === 0 ? <p className="meta">Chưa có mục nào. {d.canEdit ? 'Bấm “Thêm địa điểm”.' : ''}</p>
        : days.map(day => (
            <div key={day} style={{ marginBottom: 8 }}>
              <div className="meta" style={{ fontWeight: 700 }}>{day}</div>
              {byDay[day].map(it => (
                <div className="verify-row" key={it.id}>
                  <div style={{ flex: 1, minWidth: 0 }}>{it.title}
                    {it.note && <span className="meta"> · {it.note}</span>}
                    <span className="meta"> · {it.addedBy}</span></div>
                  {d.canEdit && <button className="text-btn" onClick={() => removeItem(it.id)}>Xoá</button>}
                </div>
              ))}
            </div>
          ))}
    </div>
  );
}

import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import {
  set, loadThreads, openThread, sendMessage, respondBooking, openReport, toast,
  sendOffer, withdrawOffer, bookOffer, setInboxFilter, archiveThread
} from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate } from '../lib/format.js';
import { TranslateButton } from '../components/TranslateButton.jsx';

const TIME = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit'
});

export function Messages() {
  const state = useStore();
  const { id } = useParams();
  const navigate = useNavigate();

  useEffect(() => {
    if (!state.user) return;
    loadThreads().then(() => { if (id) openThread(Number(id)); });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state.user, id]);

  if (!state.user) {
    return (
      <div className="shell" style={{ paddingBlock: '60px 90px' }}>
        <div className="empty-state">
          <h3>Đăng nhập để xem tin nhắn</h3>
          <p>Trao đổi với chủ nhà hoặc khách của bạn ngay trong StayHost.</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }}
                  onClick={() => set({ authMode: 'login', authError: null, overlay: 'login' })}>Đăng nhập</button>
        </div>
      </div>
    );
  }

  const threads = state.threads;
  const active = state.activeThread;
  const filter = state.inboxFilter;

  // docs/01 TN-05 — the inbox filters.
  const FILTERS = [
    ['all', 'Tất cả'], ['unread', 'Chưa đọc'], ['needsreply', 'Cần trả lời'], ['archived', 'Đã lưu trữ']
  ];

  return (
    <div className="shell" style={{ paddingBlock: '26px 60px' }}>
      <h1 className="section-title">Tin nhắn</h1>
      <p className="section-sub">{threads.length} cuộc trò chuyện</p>

      <div className="pill-row" style={{ marginBottom: 16 }}>
        {FILTERS.map(([key, label]) => (
          <button key={key} className={`pill ${filter === key ? 'is-on' : ''}`}
                  onClick={() => setInboxFilter(key)}>{label}</button>
        ))}
      </div>

      {threads.length ? (
        <div className="inbox">
          <aside className="inbox-list">
            {threads.map(t => (
              <div key={t.id} className={`inbox-row ${active?.summary.id === t.id ? 'is-active' : ''}`}>
                <button className="inbox-row-main" onClick={() => openThread(t.id)}>
                  <img src={t.listingImage} alt="" loading="lazy" decoding="async" />
                  <div style={{ minWidth: 0, flex: 1 }}>
                    <div className="inbox-row-head">
                      <b>{t.counterpartName}</b>
                      {!!t.unreadCount && <span className="fav-count">{t.unreadCount}</span>}
                      {t.needsReply && <span className="badge pending" style={{ marginLeft: 6 }}>Cần trả lời</span>}
                    </div>
                    <div className="inbox-row-sub">{t.listingTitle}</div>
                    <div className="inbox-row-last">{t.lastMessage ?? 'Chưa có tin nhắn'}</div>
                  </div>
                </button>
                <button className="inbox-archive" title={t.isArchived ? 'Bỏ lưu trữ' : 'Lưu trữ'}
                        onClick={() => archiveThread(t.id, !t.isArchived)}>
                  {t.isArchived ? '↩' : '🗄'}
                </button>
              </div>
            ))}
          </aside>
          <section className="inbox-pane">
            {active ? <Conversation thread={active} onOpenListing={slug => navigate(`/rooms/${slug}`)} />
              : <div className="inbox-empty"><p>Chọn một cuộc trò chuyện để xem nội dung.</p></div>}
          </section>
        </div>
      ) : filter !== 'all' ? (
        <div className="empty-state" style={{ marginTop: 24 }}>
          <h3>Không có cuộc trò chuyện nào</h3>
          <p>Không có tin nhắn nào khớp bộ lọc này.</p>
        </div>
      ) : (
        <div className="empty-state" style={{ marginTop: 24 }}>
          <h3>Chưa có tin nhắn nào</h3>
          <p>Mở một chỗ nghỉ và bấm "Nhắn tin cho chủ nhà" để bắt đầu.</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }} onClick={() => navigate('/')}>Khám phá chỗ nghỉ</button>
        </div>
      )}
    </div>
  );
}

function Conversation({ thread, onOpenListing }) {
  const s = thread.summary;
  const boxRef = useRef(null);
  const inputRef = useRef(null);
  const [pending, setPending] = useState([]);
  const [uploading, setUploading] = useState(false);

  // New messages arrive at the bottom, so the pane follows them.
  useEffect(() => {
    const box = boxRef.current;
    if (box) box.scrollTop = box.scrollHeight;
  }, [thread.messages.length, s.id]);

  const send = async e => {
    e.preventDefault();
    const input = e.currentTarget.body;
    const body = input.value.trim();
    if (!body && !pending.length) return;

    input.value = '';
    setPending([]);
    await sendMessage({ threadId: s.id, body, attachments: pending });
  };

  // docs/01 TN-02 — photos go through the same upload endpoint as listing images.
  const attach = async files => {
    const list = Array.from(files ?? []);
    if (!list.length) return;

    setUploading(true);
    try {
      const form = new FormData();
      list.slice(0, 6).forEach(f => form.append('files', f));
      const res = await fetch('/api/uploads/images', { method: 'POST', body: form, credentials: 'same-origin' });
      const payload = await res.json().catch(() => null);
      if (!res.ok) throw new Error(payload?.message ?? 'Tải ảnh thất bại.');
      setPending(p => [...p, ...payload.urls].slice(0, 6));
    } catch (err) {
      toast(err.message);
    } finally {
      setUploading(false);
    }
  };

  return <>
    <header className="inbox-head">
      <span className="avatar">{s.counterpartInitials}</span>
      <div style={{ minWidth: 0, flex: 1 }}>
        <b>{s.counterpartName}</b>
        <span>{s.viewerIsHost ? 'Khách' : 'Chủ nhà'} · {s.listingTitle}</span>
      </div>
      <button className="btn btn-outline btn-sm" onClick={() => onOpenListing(s.listingSlug)}>Xem chỗ nghỉ</button>
    </header>

    <BookingCard booking={thread.booking} />

    <OfferPanel thread={thread} summary={s} />

    {!thread.contactsUnlocked && (
      <div className="inbox-notice">
        Số điện thoại, email và đường liên kết được che cho tới khi đơn được xác nhận.
        Giao dịch ngoài StayHost không được bảo vệ.
      </div>
    )}

    <div className="inbox-messages" ref={boxRef}>
      {thread.messages.length
        ? thread.messages.map(m => (
            m.isSystem
              ? <div className="bubble is-system" key={m.id}>
                  <p>{m.body}</p>
                  <time>{TIME.format(new Date(m.sentAt))}</time>
                </div>
              : <div className={`bubble ${m.mine ? 'mine' : ''}`} key={m.id}>
                  {m.body && <p>{m.body}</p>}
                  {/* docs/01 TN-06 — dịch tin của người kia, chỉ hiện khi bật. */}
                  {!m.mine && m.body && <TranslateButton text={m.body} />}
                  {!!m.attachments?.length && (
                    <div className="bubble-photos">
                      {m.attachments.map((url, i) => (
                        <a href={url} target="_blank" rel="noreferrer" key={i}>
                          <img src={url} alt={`Ảnh ${i + 1}`} loading="lazy" />
                        </a>
                      ))}
                    </div>
                  )}
                  {m.contactsMasked && (
                    <span className="bubble-note">Đã che thông tin liên hệ cho tới khi đơn được xác nhận.</span>
                  )}
                  <time>{TIME.format(new Date(m.sentAt))}</time>
                  {/* docs/01 AT-02 — only on what the other side sent; reporting
                      your own message is not a moderation signal. */}
                  {!m.mine && (
                    <button className="bubble-report"
                            title="Báo cáo tin nhắn này"
                            onClick={() => openReport('message', m.id, 'Tin nhắn trong hội thoại này')}>⚑</button>
                  )}
                </div>
          ))
        : <div className="inbox-empty"><p>Hãy gửi lời chào đầu tiên.</p></div>}
    </div>

    <QuickReplies replies={thread.quickReplies} onPick={body => {
      if (inputRef.current) {
        inputRef.current.value = body;
        inputRef.current.focus();
      }
    }} />

    {!!pending.length && (
      <div className="bubble-photos" style={{ padding: '8px 16px 0' }}>
        {pending.map((url, i) => (
          <img src={url} alt={`Sẽ gửi ${i + 1}`} key={i}
               onClick={() => setPending(p => p.filter((_, x) => x !== i))}
               title="Bấm để bỏ" style={{ cursor: 'pointer' }} />
        ))}
      </div>
    )}

    <form className="inbox-compose" onSubmit={send}>
      <label className="btn btn-outline btn-sm" style={{ cursor: 'pointer' }} title="Gửi ảnh">
        <input type="file" accept="image/jpeg,image/png,image/webp,image/avif" multiple hidden
               onChange={e => { attach(e.target.files); e.target.value = ''; }} />
        {uploading ? '…' : '📷'}
      </label>
      <input name="body" ref={inputRef} placeholder="Nhập tin nhắn…" autoComplete="off" />
      <button type="submit" className="btn btn-primary btn-sm">Gửi</button>
    </form>
  </>;
}

/** docs/01 TN-03 — the order this conversation is about, with the action to take. */
function BookingCard({ booking }) {
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  if (!booking) return null;

  const answer = async decision => {
    setBusy(true);
    await respondBooking(booking.id, decision);
    setBusy(false);
  };

  return (
    <div className="inbox-booking">
      <div style={{ minWidth: 0, flex: 1 }}>
        <b>Đơn {booking.reference}</b>
        <div style={{ color: 'var(--ink-muted)' }}>
          {longDate(booking.checkIn)} → {longDate(booking.checkOut)} · {booking.nights} đêm ·
          {' '}{booking.guests} khách · {money(booking.total)}
        </div>
      </div>
      <span className={`badge ${booking.statusBadge}`}>{booking.statusLabel}</span>
      {booking.needsHostAnswer ? <>
        <button className="btn btn-primary btn-sm" disabled={busy} onClick={() => answer('confirm')}>Xác nhận</button>
        <button className="btn btn-outline btn-sm" disabled={busy} onClick={() => answer('decline')}>Từ chối</button>
      </> : (
        <button className="btn btn-outline btn-sm" onClick={() => navigate(`/trips/${booking.id}`)}>Xem đơn</button>
      )}
    </div>
  );
}

/**
 * docs/01 ĐP-17, QL-14 — private offers on this thread. The host gets a small
 * form to send one; the guest gets a card per offer with a "book at this price"
 * button while it is live. Booking routes through the normal checkout carrying
 * the offer id, so pricing, the ledger and the hold all behave as usual.
 */
function OfferPanel({ thread, summary }) {
  const offers = thread.offers ?? [];
  const isHost = summary.viewerIsHost;

  return (
    <div className="offer-panel">
      {offers.map(o => (
        <div className="offer-card" key={o.id}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <b>Ưu đãi riêng · {money(o.nightlyRate)}/đêm</b>
            <div className="meta">
              {o.nights} đêm · {longDate(o.checkIn)} → {longDate(o.checkOut)} · tổng {money(o.stayTotal)}
            </div>
            <div className="meta">{o.statusLabel}</div>
          </div>
          {!isHost && o.isLive && (
            <button className="btn btn-primary btn-sm"
                    onClick={() => bookOffer(thread, o)}>Đặt với giá này</button>
          )}
          {isHost && o.isLive && (
            <button className="btn btn-outline btn-sm" onClick={() => withdrawOffer(o.id)}>Thu hồi</button>
          )}
        </div>
      ))}
      {isHost && <SendOfferForm threadId={summary.id} />}
    </div>
  );
}

function SendOfferForm({ threadId }) {
  const [open, setOpen] = useState(false);
  const [checkIn, setCheckIn] = useState('');
  const [checkOut, setCheckOut] = useState('');
  const [rate, setRate] = useState('');
  const [guests, setGuests] = useState(2);
  const [busy, setBusy] = useState(false);

  if (!open) {
    return <button className="btn btn-outline btn-sm offer-add" onClick={() => setOpen(true)}>
      + Gửi ưu đãi riêng
    </button>;
  }

  const send = async () => {
    setBusy(true);
    try {
      await sendOffer(threadId, {
        checkIn, checkOut, guests: Number(guests), nightlyRate: Number(rate)
      });
      setOpen(false); setCheckIn(''); setCheckOut(''); setRate('');
      toast('Đã gửi ưu đãi riêng, hiệu lực 24 giờ.');
    } catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  return (
    <div className="offer-form">
      <div className="offer-form-row">
        <label>Nhận phòng<input type="date" value={checkIn} onChange={e => setCheckIn(e.target.value)} /></label>
        <label>Trả phòng<input type="date" value={checkOut} onChange={e => setCheckOut(e.target.value)} /></label>
      </div>
      <div className="offer-form-row">
        <label>Giá/đêm<input type="number" min="0" step="50000" value={rate}
                             onChange={e => setRate(e.target.value)} placeholder="800000" /></label>
        <label>Số khách<input type="number" min="1" value={guests}
                              onChange={e => setGuests(e.target.value)} /></label>
      </div>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
        <button className="btn btn-outline btn-sm" onClick={() => setOpen(false)}>Huỷ</button>
        <button className="btn btn-primary btn-sm" disabled={busy || !checkIn || !checkOut || !rate}
                onClick={send}>Gửi ưu đãi</button>
      </div>
    </div>
  );
}

/** docs/01 TN-08 — phrases the host reuses, one tap to drop into the box. */
function QuickReplies({ replies, onPick }) {
  const [items, setItems] = useState(replies ?? []);
  useEffect(() => { setItems(replies ?? []); }, [replies]);

  const add = async () => {
    const title = prompt('Tên mẫu (ví dụ: Hướng dẫn nhận phòng)');
    if (!title?.trim()) return;
    const body = prompt('Nội dung mẫu');
    if (!body?.trim()) return;

    try {
      const saved = await api.addQuickReply({ title: title.trim(), body: body.trim(), sortOrder: items.length });
      setItems(x => [...x, saved]);
      toast('Đã lưu mẫu trả lời.');
    } catch (err) { toast(err.message); }
  };

  // Guests never see this row: the server sends an empty list to them.
  if (!replies) return null;

  return (
    <div className="quick-replies">
      {items.map(r => (
        <button className="pill" key={r.id} onClick={() => onPick(r.body)}
                onContextMenu={async e => {
                  e.preventDefault();
                  if (!confirm(`Xoá mẫu "${r.title}"?`)) return;
                  await api.deleteQuickReply(r.id);
                  setItems(x => x.filter(y => y.id !== r.id));
                }}>{r.title}</button>
      ))}
      <button className="pill" onClick={add}>+ Mẫu trả lời</button>
    </div>
  );
}

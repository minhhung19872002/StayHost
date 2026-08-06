import { useEffect, useRef } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, loadThreads, openThread, sendMessage } from '../lib/store.js';

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

  return (
    <div className="shell" style={{ paddingBlock: '26px 60px' }}>
      <h1 className="section-title">Tin nhắn</h1>
      <p className="section-sub">{threads.length} cuộc trò chuyện</p>

      {threads.length ? (
        <div className="inbox">
          <aside className="inbox-list">
            {threads.map(t => (
              <button key={t.id} className={`inbox-row ${active?.summary.id === t.id ? 'is-active' : ''}`}
                      onClick={() => openThread(t.id)}>
                <img src={t.listingImage} alt="" loading="lazy" decoding="async" />
                <div style={{ minWidth: 0, flex: 1 }}>
                  <div className="inbox-row-head">
                    <b>{t.counterpartName}</b>
                    {!!t.unreadCount && <span className="fav-count">{t.unreadCount}</span>}
                  </div>
                  <div className="inbox-row-sub">{t.listingTitle}</div>
                  <div className="inbox-row-last">{t.lastMessage ?? 'Chưa có tin nhắn'}</div>
                </div>
              </button>
            ))}
          </aside>
          <section className="inbox-pane">
            {active ? <Conversation thread={active} onOpenListing={slug => navigate(`/rooms/${slug}`)} />
              : <div className="inbox-empty"><p>Chọn một cuộc trò chuyện để xem nội dung.</p></div>}
          </section>
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

  // New messages arrive at the bottom, so the pane follows them.
  useEffect(() => {
    const box = boxRef.current;
    if (box) box.scrollTop = box.scrollHeight;
  }, [thread.messages.length, s.id]);

  const send = async e => {
    e.preventDefault();
    const input = e.currentTarget.body;
    const body = input.value.trim();
    if (!body) return;
    input.value = '';
    await sendMessage({ threadId: s.id, body });
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

    <div className="inbox-messages" ref={boxRef}>
      {thread.messages.length
        ? thread.messages.map(m => (
            <div className={`bubble ${m.mine ? 'mine' : ''}`} key={m.id}>
              <p>{m.body}</p>
              <time>{TIME.format(new Date(m.sentAt))}</time>
            </div>
          ))
        : <div className="inbox-empty"><p>Hãy gửi lời chào đầu tiên.</p></div>}
    </div>

    <form className="inbox-compose" onSubmit={send}>
      <input name="body" placeholder="Nhập tin nhắn…" autoComplete="off" required />
      <button type="submit" className="btn btn-primary btn-sm">Gửi</button>
    </form>
  </>;
}

import { esc } from '../util.js';
import { state } from '../store.js';

const TIME = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit'
});

export function renderMessages() {
  if (!state.user) {
    return `
      <div class="shell" style="padding-block:60px 90px">
        <div class="empty-state">
          <h3>Đăng nhập để xem tin nhắn</h3>
          <p>Trao đổi với chủ nhà hoặc khách của bạn ngay trong StayHost.</p>
          <button class="btn btn-primary" style="margin-top:18px" data-act="open-auth" data-mode="login">Đăng nhập</button>
        </div>
      </div>`;
  }

  const threads = state.threads;
  const active = state.activeThread;

  return `
    <div class="shell" style="padding-block:26px 60px">
      <h1 class="section-title">Tin nhắn</h1>
      <p class="section-sub">${esc(threads.length)} cuộc trò chuyện</p>

      ${threads.length ? `
        <div class="inbox">
          <aside class="inbox-list">
            ${threads.map(t => threadRow(t, active?.summary.id === t.id)).join('')}
          </aside>
          <section class="inbox-pane">
            ${active ? conversation(active) : `
              <div class="inbox-empty">
                <p>Chọn một cuộc trò chuyện để xem nội dung.</p>
              </div>`}
          </section>
        </div>
      ` : `
        <div class="empty-state" style="margin-top:24px">
          <h3>Chưa có tin nhắn nào</h3>
          <p>Mở một chỗ nghỉ và bấm "Nhắn tin cho chủ nhà" để bắt đầu.</p>
          <button class="btn btn-primary" style="margin-top:18px" data-act="go" data-href="/">Khám phá chỗ nghỉ</button>
        </div>
      `}
    </div>
  `;
}

function threadRow(t, isActive) {
  return `
    <button class="inbox-row ${isActive ? 'is-active' : ''}" data-act="open-thread" data-id="${esc(t.id)}">
      <img src="${esc(t.listingImage)}" alt="" loading="lazy" decoding="async">
      <div style="min-width:0;flex:1">
        <div class="inbox-row-head">
          <b>${esc(t.counterpartName)}</b>
          ${t.unreadCount ? `<span class="fav-count">${esc(t.unreadCount)}</span>` : ''}
        </div>
        <div class="inbox-row-sub">${esc(t.listingTitle)}</div>
        <div class="inbox-row-last">${esc(t.lastMessage ?? 'Chưa có tin nhắn')}</div>
      </div>
    </button>
  `;
}

function conversation(active) {
  const s = active.summary;
  return `
    <header class="inbox-head">
      <span class="avatar">${esc(s.counterpartInitials)}</span>
      <div style="min-width:0;flex:1">
        <b>${esc(s.counterpartName)}</b>
        <span>${esc(s.viewerIsHost ? 'Khách' : 'Chủ nhà')} · ${esc(s.listingTitle)}</span>
      </div>
      <button class="btn btn-outline btn-sm" data-act="open-listing-by-id" data-id="${esc(s.listingId)}">Xem chỗ nghỉ</button>
    </header>

    <div class="inbox-messages" id="inbox-messages">
      ${active.messages.length
        ? active.messages.map(m => `
            <div class="bubble ${m.mine ? 'mine' : ''}">
              <p>${esc(m.body)}</p>
              <time>${esc(TIME.format(new Date(m.sentAt)))}</time>
            </div>`).join('')
        : '<div class="inbox-empty"><p>Hãy gửi lời chào đầu tiên.</p></div>'}
    </div>

    <form class="inbox-compose" data-act="submit-message" data-thread="${esc(s.id)}">
      <input name="body" placeholder="Nhập tin nhắn…" autocomplete="off" required>
      <button type="submit" class="btn btn-primary btn-sm">Gửi</button>
    </form>
  `;
}

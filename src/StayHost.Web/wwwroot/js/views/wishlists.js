import { esc } from '../util.js';
import { state } from '../store.js';
import { renderCard } from '../components/card.js';

export function renderWishlists() {
  const items = state.favorites;

  return `
    <div class="shell" style="padding-block:30px 90px">
      <h1 class="section-title">Chỗ nghỉ đã lưu</h1>
      <p class="section-sub">${esc(items.length)} chỗ nghỉ trong danh sách của bạn</p>

      ${items.length ? `
        <div class="card-grid">${items.map(c => renderCard(c, { lazy: true })).join('')}</div>
      ` : `
        <div class="empty-state" style="margin-top:24px">
          <h3>Chưa lưu chỗ nghỉ nào</h3>
          <p>Nhấn ♥ trên bất kỳ chỗ nghỉ để lưu lại đây.</p>
          <button class="btn btn-primary" style="margin-top:18px" data-act="go" data-href="/">Khám phá chỗ nghỉ</button>
        </div>
      `}
    </div>
  `;
}

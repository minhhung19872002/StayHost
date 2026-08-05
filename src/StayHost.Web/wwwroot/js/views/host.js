import { esc, money } from '../util.js';
import { state } from '../store.js';

const STATS = [
  { value: '500+', label: 'Chỗ nghỉ đang vận hành', note: 'Trên 12 tỉnh thành' },
  { value: '4.9★', label: 'Điểm đánh giá trung bình', note: 'Từ 18.000 lượt khách' },
  { value: '+30%', label: 'Doanh thu tăng thêm', note: 'Sau 6 tháng tối ưu giá' },
  { value: '24/7', label: 'Đội vận hành trực', note: 'Phản hồi khách dưới 5 phút' }
];

const STEPS = [
  { n: 1, title: 'Kể về chỗ nghỉ của bạn', text: 'Chọn loại hình, vị trí và số khách có thể ở. Mất khoảng 10 phút.' },
  { n: 2, title: 'Làm nó nổi bật', text: 'Đội ngũ StayHost chụp ảnh, viết mô tả và thiết lập giá theo mùa cho bạn.' },
  { n: 3, title: 'Đón khách đầu tiên', text: 'Bật lịch, chúng tôi lo phần còn lại: tin nhắn, dọn dẹp, bảo trì.' }
];

const FAQ = [
  ['StayHost thu phí bao nhiêu?', 'Phí dịch vụ 9% trên mỗi lượt đặt thành công. Không có phí đăng tin, không phí duy trì.'],
  ['Tôi có toàn quyền với lịch không?', 'Có. Bạn khoá ngày bất cứ lúc nào; hệ thống tự chặn đặt chỗ trùng.'],
  ['Nhà bị hư hỏng thì sao?', 'Mỗi lượt đặt được bảo vệ tới 1 tỷ đồng cho thiệt hại tài sản.'],
  ['Khi nào tôi được thanh toán?', 'Chuyển khoản 24 giờ sau khi khách nhận phòng, thẳng vào tài khoản ngân hàng của bạn.']
];

export function renderHostPage() {
  const nights = state.hostCalcNights ?? 20;
  const rate = state.hostCalcRate ?? 1_500_000;
  const monthly = nights * rate * 0.91;

  return `
    <div class="shell" style="padding-bottom:90px">
      <section class="host-hero">
        <div style="min-width:0">
          <div class="host-eyebrow">CHO CHỦ NHÀ CHO THUÊ NGẮN HẠN</div>
          <h1>Giữ căn nhà.<br><span>Bỏ</span> phần việc.</h1>
          <p>StayHost OS vận hành thay bạn: trả lời khách, tối ưu giá, điều phối dọn dẹp và bảo trì — bạn chỉ nhận doanh thu.</p>
          <div class="host-cta">
            <button class="btn btn-primary" data-act="open" data-overlay="login">Đăng nhà cho thuê →</button>
            <button class="btn btn-outline" data-act="scroll-to" data-target="how-it-works">Xem cách hoạt động</button>
          </div>
        </div>
        <div class="host-media">
          <img src="https://images.pexels.com/photos/1029599/pexels-photo-1029599.jpeg?auto=compress&cs=tinysrgb&w=1200"
               alt="Villa cho thuê trên StayHost" loading="eager" decoding="async">
        </div>
      </section>

      <section class="stat-grid">
        ${STATS.map(s => `
          <div class="stat">
            <div class="value">${esc(s.value)}</div>
            <div class="label">${esc(s.label)}</div>
            <div class="note">${esc(s.note)}</div>
          </div>
        `).join('')}
      </section>

      <section style="margin-top:56px" id="how-it-works">
        <h2 class="section-title">Ba bước để bắt đầu</h2>
        <p class="section-sub">Từ lúc đăng ký tới lượt khách đầu tiên, trung bình 6 ngày.</p>
        <div class="step-grid">
          ${STEPS.map(s => `
            <div class="step">
              <div class="n">${esc(s.n)}</div>
              <b>${esc(s.title)}</b>
              <p>${esc(s.text)}</p>
            </div>
          `).join('')}
        </div>
      </section>

      <section style="margin-top:56px">
        <h2 class="section-title">Ước tính doanh thu</h2>
        <p class="section-sub">Kéo thanh trượt để xem bạn có thể thu về bao nhiêu mỗi tháng.</p>
        <div class="calc">
          <div class="calc-out">${money(monthly)}<span style="font-size:16px;color:var(--ink-muted);font-weight:600"> / tháng</span></div>
          <div style="margin-top:22px;display:grid;gap:20px;grid-template-columns:repeat(auto-fit,minmax(min(100%,240px),1fr))">
            <label>
              <div style="font-size:13px;font-weight:700;margin-bottom:8px">Số đêm cho thuê: <b style="color:var(--brand)">${esc(nights)}</b></div>
              <input type="range" min="1" max="30" value="${esc(nights)}" style="width:100%" data-act="host-nights">
            </label>
            <label>
              <div style="font-size:13px;font-weight:700;margin-bottom:8px">Giá mỗi đêm: <b style="color:var(--brand)">${money(rate)}</b></div>
              <input type="range" min="300000" max="6000000" step="100000" value="${esc(rate)}" style="width:100%" data-act="host-rate">
            </label>
          </div>
          <p style="margin:18px 0 0;font-size:13px;color:var(--ink-muted)">Đã trừ phí dịch vụ 9%. Con số chỉ mang tính tham khảo.</p>
        </div>
      </section>

      <section style="margin-top:56px">
        <h2 class="section-title">Câu hỏi thường gặp</h2>
        <div style="margin-top:20px;border-top:1px solid var(--divider)">
          ${FAQ.map(([q, a]) => `
            <details style="border-bottom:1px solid var(--divider);padding:18px 0">
              <summary style="font-size:16px;font-weight:600;cursor:pointer;list-style:none">${esc(q)}</summary>
              <p style="margin:12px 0 0;font-size:14.5px;line-height:1.6;color:var(--ink-body)">${esc(a)}</p>
            </details>
          `).join('')}
        </div>
      </section>
    </div>
  `;
}

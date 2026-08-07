import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { becomeHost, openOverlay, toast } from '../lib/store.js';
import { money } from '../lib/format.js';

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

export function Host() {
  const state = useStore();
  const navigate = useNavigate();

  /*
   * The one button this page exists for. It used to open the sign-in box
   * unconditionally, so somebody already signed in was handed a login form and
   * no way to reach the wizard at all — the only working route in was a menu
   * item three levels down.
   */
  const startListing = async () => {
    if (!state.user) { openOverlay('login'); return; }

    if (!state.user.isHost && !await becomeHost()) return;
    if (!state.user.isHost) toast('Bạn đã sẵn sàng cho thuê nhà.');

    // The intent travels with the navigation. Opening the editor here instead
    // would be undone a tick later: App closes every overlay on a route change.
    navigate('/hosting', { state: { newListing: true } });
  };
  const [nights, setNights] = useState(20);
  const [rate, setRate] = useState(1_500_000);

  const hostRate = state.meta?.fees?.hostServiceFeeRate ?? 0.03;
  const pct = Math.round(hostRate * 100);
  const monthly = nights * rate * (1 - hostRate);

  const faq = [
    ['StayHost thu phí bao nhiêu?', `Phí dịch vụ ${pct}% trên mỗi lượt đặt thành công. Không có phí đăng tin, không phí duy trì.`],
    ['Tôi có toàn quyền với lịch không?', 'Có. Bạn khoá ngày bất cứ lúc nào; hệ thống tự chặn đặt chỗ trùng.'],
    ['Nhà bị hư hỏng thì sao?', 'Mỗi lượt đặt được bảo vệ tới 1 tỷ đồng cho thiệt hại tài sản.'],
    ['Khi nào tôi được thanh toán?', 'Chuyển khoản 24 giờ sau khi khách nhận phòng, thẳng vào tài khoản ngân hàng của bạn.']
  ];

  return (
    <div className="shell" style={{ paddingBottom: 90 }}>
      <section className="host-hero">
        <div style={{ minWidth: 0 }}>
          <div className="host-eyebrow">CHO CHỦ NHÀ CHO THUÊ NGẮN HẠN</div>
          <h1>Giữ căn nhà.<br /><span>Bỏ</span> phần việc.</h1>
          <p>
            StayHost OS vận hành thay bạn: trả lời khách, tối ưu giá, điều phối dọn dẹp
            và bảo trì — bạn chỉ nhận doanh thu.
          </p>
          <div className="host-cta">
            <button className="btn btn-primary" onClick={startListing}>
              {state.user?.isHost ? 'Đăng thêm chỗ nghỉ →' : 'Đăng nhà cho thuê →'}
            </button>
            <button className="btn btn-outline"
                    onClick={() => document.getElementById('how-it-works')?.scrollIntoView({ behavior: 'smooth' })}>
              Xem cách hoạt động
            </button>
          </div>
        </div>
        <div className="host-media">
          <img src="https://images.pexels.com/photos/1029599/pexels-photo-1029599.jpeg?auto=compress&cs=tinysrgb&w=1200"
               alt="Villa cho thuê trên StayHost" loading="eager" decoding="async" />
        </div>
      </section>

      <section className="stat-grid">
        {STATS.map(s => (
          <div className="stat" key={s.label}>
            <div className="value">{s.value}</div>
            <div className="label">{s.label}</div>
            <div className="note">{s.note}</div>
          </div>
        ))}
      </section>

      <section style={{ marginTop: 56 }} id="how-it-works">
        <h2 className="section-title">Ba bước để bắt đầu</h2>
        <p className="section-sub">Từ lúc đăng ký tới lượt khách đầu tiên, trung bình 6 ngày.</p>
        <div className="step-grid">
          {STEPS.map(s => (
            <div className="step" key={s.n}>
              <div className="n">{s.n}</div>
              <b>{s.title}</b>
              <p>{s.text}</p>
            </div>
          ))}
        </div>
      </section>

      <section style={{ marginTop: 56 }}>
        <h2 className="section-title">Ước tính doanh thu</h2>
        <p className="section-sub">Kéo thanh trượt để xem bạn có thể thu về bao nhiêu mỗi tháng.</p>
        <div className="calc">
          <div className="calc-out">
            {money(monthly)}
            <span style={{ fontSize: 16, color: 'var(--ink-muted)', fontWeight: 600 }}> / tháng</span>
          </div>
          <div style={{ marginTop: 22, display: 'grid', gap: 20,
                        gridTemplateColumns: 'repeat(auto-fit,minmax(min(100%,240px),1fr))' }}>
            <label>
              <div style={{ fontSize: 13, fontWeight: 700, marginBottom: 8 }}>
                Số đêm cho thuê: <b style={{ color: 'var(--brand)' }}>{nights}</b>
              </div>
              <input type="range" min={1} max={30} value={nights} style={{ width: '100%' }}
                     onChange={e => setNights(Number(e.target.value))} />
            </label>
            <label>
              <div style={{ fontSize: 13, fontWeight: 700, marginBottom: 8 }}>
                Giá mỗi đêm: <b style={{ color: 'var(--brand)' }}>{money(rate)}</b>
              </div>
              <input type="range" min={300000} max={6000000} step={100000} value={rate} style={{ width: '100%' }}
                     onChange={e => setRate(Number(e.target.value))} />
            </label>
          </div>
          <p style={{ margin: '18px 0 0', fontSize: 13, color: 'var(--ink-muted)' }}>
            Đã trừ phí dịch vụ {pct}%. Con số chỉ mang tính tham khảo.
          </p>
        </div>
      </section>

      <section style={{ marginTop: 56 }}>
        <h2 className="section-title">Câu hỏi thường gặp</h2>
        <div style={{ marginTop: 20, borderTop: '1px solid var(--divider)' }}>
          {faq.map(([q, a]) => (
            <details key={q} style={{ borderBottom: '1px solid var(--divider)', padding: '18px 0' }}>
              <summary style={{ fontSize: 16, fontWeight: 600, cursor: 'pointer', listStyle: 'none' }}>{q}</summary>
              <p style={{ margin: '12px 0 0', fontSize: 14.5, lineHeight: 1.6, color: 'var(--ink-body)' }}>{a}</p>
            </details>
          ))}
        </div>
      </section>
    </div>
  );
}

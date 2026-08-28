import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { becomeHost, openOverlay, toast } from '../lib/store.js';
import { money } from '../lib/format.js';
import { t } from '../lib/i18n.js';

const STATS = [
  { value: '500+', label: 'Chỗ nghỉ đang vận hành', note: 'Trên 12 tỉnh thành' },
  { value: '4.9★', label: 'Điểm đánh giá trung bình', note: 'Từ 18.000 lượt khách' },
  { value: '+30%', label: 'Doanh thu tăng thêm', note: 'Sau 6 tháng tối ưu giá' },
  { value: '24/7', label: 'Đội vận hành trực', note: 'Phản hồi khách dưới 5 phút' }
];

const STEPS = [
  { n: 1, title: 'Kể về chỗ nghỉ của bạn', text: 'Chọn loại hình, vị trí và số khách có thể ở. Mất khoảng 10 phút.' },
  { n: 2, title: 'Làm nó nổi bật', text: 'Đội ngũ Staylio chụp ảnh, viết mô tả và thiết lập giá theo mùa cho bạn.' },
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
    if (!state.user.isHost) toast(t('Bạn đã sẵn sàng cho thuê nhà.'));

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
    [t('Staylio thu phí bao nhiêu?'), `${t('Phí dịch vụ')} ${pct}% ${t('trên mỗi lượt đặt thành công. Không có phí đăng tin, không phí duy trì.')}`],
    [t('Tôi có toàn quyền với lịch không?'), t('Có. Bạn khoá ngày bất cứ lúc nào; hệ thống tự chặn đặt chỗ trùng.')],
    /*
     * This line used to promise "bảo vệ tới 1 tỷ đồng cho thiệt hại tài sản",
     * which was wrong twice over. The ceiling is 75 triệu một hồ sơ
     * (`ShieldSettings.HostClaimCeiling`), and since the customer's decision of
     * 17/08/2026 the fund does not pay for damage at all: the guest settles it
     * in cash at check-out and Staylio only rules on it (docs/06 §3.3, C1/C2 are
     * `Shield.SettledAtCheckout`). It also read like insurance, which docs/06
     * §11 forbids in as many words.
     */
    [t('Nhà bị hư hỏng thì sao?'),
      t('Bạn báo khách ngay lúc trả phòng và nhận tiền mặt tại chỗ; Staylio phân xử và ghi nhận nhưng không thu hộ. Cửa sổ mở hồ sơ là 24 giờ sau khi khách đi.')],
    [t('Staylio Shield hỗ trợ những gì?'),
      t('Chính sách hỗ trợ đứng sau phần mất thu nhập khi phải khoá lịch và thiệt hại gây ra cho bên thứ ba, tối đa 75 triệu đồng mỗi hồ sơ và 350 triệu đồng một năm, sau mức tự chịu 500.000₫.')],
    [t('Khi nào tôi được thanh toán?'), t('Chuyển khoản 24 giờ sau khi khách nhận phòng, thẳng vào tài khoản ngân hàng của bạn.')]
  ];

  return (
    <div className="shell" style={{ paddingBottom: 90 }}>
      <section className="host-hero">
        <div style={{ minWidth: 0 }}>
          <div className="host-eyebrow">{t('CHO CHỦ NHÀ CHO THUÊ NGẮN HẠN')}</div>
          <h1>{t('Giữ căn nhà.')}<br /><span>{t('Bỏ')}</span> {t('phần việc.')}</h1>
          <p>
            {t('Staylio vận hành thay bạn: trả lời khách, tối ưu giá, điều phối dọn dẹp và bảo trì — bạn chỉ nhận doanh thu.')}
          </p>
          <div className="host-cta">
            <button className="btn btn-primary" onClick={startListing}>
              {state.user?.isHost ? t('Đăng thêm chỗ nghỉ →') : t('Đăng nhà cho thuê →')}
            </button>
            <button className="btn btn-outline"
                    onClick={() => document.getElementById('how-it-works')?.scrollIntoView({ behavior: 'smooth' })}>
              {t('Xem cách hoạt động')}
            </button>
          </div>
        </div>
        <div className="host-media">
          <img src="https://images.pexels.com/photos/1029599/pexels-photo-1029599.jpeg?auto=compress&cs=tinysrgb&w=1200"
               alt={t('Villa cho thuê trên Staylio')} loading="eager" decoding="async" />
        </div>
      </section>

      <section className="stat-grid">
        {STATS.map(s => (
          <div className="stat" key={s.label}>
            <div className="value">{s.value}</div>
            <div className="label">{t(s.label)}</div>
            <div className="note">{t(s.note)}</div>
          </div>
        ))}
      </section>

      <section style={{ marginTop: 56 }} id="how-it-works">
        <h2 className="section-title">{t('Ba bước để bắt đầu')}</h2>
        <p className="section-sub">{t('Từ lúc đăng ký tới lượt khách đầu tiên, trung bình 6 ngày.')}</p>
        <div className="step-grid">
          {STEPS.map(s => (
            <div className="step" key={s.n}>
              <div className="n">{s.n}</div>
              <b>{t(s.title)}</b>
              <p>{t(s.text)}</p>
            </div>
          ))}
        </div>
      </section>

      <section style={{ marginTop: 56 }}>
        <h2 className="section-title">{t('Ước tính doanh thu')}</h2>
        <p className="section-sub">{t('Kéo thanh trượt để xem bạn có thể thu về bao nhiêu mỗi tháng.')}</p>
        <div className="calc">
          <div className="calc-out">
            {money(monthly)}
            <span style={{ fontSize: 16, color: 'var(--ink-muted)', fontWeight: 600 }}> {t('/ tháng')}</span>
          </div>
          <div style={{ marginTop: 22, display: 'grid', gap: 20,
                        gridTemplateColumns: 'repeat(auto-fit,minmax(min(100%,240px),1fr))' }}>
            <label>
              <div style={{ fontSize: 13, fontWeight: 700, marginBottom: 8 }}>
                {t('Số đêm cho thuê:')} <b style={{ color: 'var(--brand)' }}>{nights}</b>
              </div>
              <input type="range" min={1} max={30} value={nights} style={{ width: '100%' }}
                     onChange={e => setNights(Number(e.target.value))} />
            </label>
            <label>
              <div style={{ fontSize: 13, fontWeight: 700, marginBottom: 8 }}>
                {t('Giá mỗi đêm:')} <b style={{ color: 'var(--brand)' }}>{money(rate)}</b>
              </div>
              <input type="range" min={300000} max={6000000} step={100000} value={rate} style={{ width: '100%' }}
                     onChange={e => setRate(Number(e.target.value))} />
            </label>
          </div>
          <p style={{ margin: '18px 0 0', fontSize: 13, color: 'var(--ink-muted)' }}>
            {t('Đã trừ phí dịch vụ')} {pct}%. {t('Con số chỉ mang tính tham khảo.')}
          </p>
        </div>
      </section>

      <section style={{ marginTop: 56 }}>
        <h2 className="section-title">{t('Câu hỏi thường gặp')}</h2>
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

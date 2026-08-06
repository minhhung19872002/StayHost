import { useStore } from '../lib/useStore.js';
import { openOverlay, toast } from '../lib/store.js';

const COLUMNS = [
  {
    title: 'Hỗ trợ',
    links: ['Trung tâm trợ giúp', 'AirCover cho khách', 'Chống phân biệt đối xử',
            'Hỗ trợ người khuyết tật', 'Tuỳ chọn huỷ', 'Báo cáo lo ngại khu dân cư']
  },
  {
    title: 'Đón tiếp khách',
    links: ['Cho thuê nhà trên StayHost', 'AirCover cho Chủ nhà', 'Tài nguyên cho Chủ nhà',
            'Diễn đàn cộng đồng', 'Đón tiếp khách có trách nhiệm', 'Tham gia khoá học miễn phí']
  },
  {
    title: 'StayHost',
    links: ['Trang tin tức', 'Tính năng mới', 'Cơ hội nghề nghiệp',
            'Nhà đầu tư', 'Chỗ ở khẩn cấp StayHost.org', 'Thẻ quà tặng']
  },
  {
    title: 'Khám phá',
    links: ['Chỗ nghỉ ven biển', 'Villa có hồ bơi', 'Homestay vùng cao',
            'Cabin gỗ Đà Lạt', 'Căn hộ dài hạn', 'Chỗ nghỉ cho thú cưng']
  }
];

const LEGAL = ['© 2026 StayHost OS, Inc.', 'Quyền riêng tư', 'Điều khoản', 'Sơ đồ trang web', 'Thông tin công ty'];

const demo = e => { e.preventDefault(); toast('Bản demo — chức năng này chưa kết nối dịch vụ thật.'); };

export function Footer() {
  const state = useStore();

  return <>
    <div className="footer-cols">
      {COLUMNS.map(col => (
        <div className="footer-col" key={col.title}>
          <h4>{col.title}</h4>
          <ul>
            {col.links.map(l => <li key={l}><a href="#" onClick={demo}>{l}</a></li>)}
          </ul>
        </div>
      ))}
    </div>
    <div className="footer-bottom">
      <div className="footer-bottom-inner">
        <div className="footer-legal">
          {LEGAL.map(l => <span key={l}>{l}</span>)}
        </div>
        <div className="footer-prefs">
          <button onClick={() => openOverlay('language')}>⊕ {state.language.label}</button>
          <button onClick={() => openOverlay('language')}>{state.currency.symbol} {state.currency.code}</button>
          <button onClick={demo} aria-label="Facebook">ⓕ</button>
          <button onClick={demo} aria-label="X">✕</button>
          <button onClick={demo} aria-label="Instagram">◉</button>
        </div>
      </div>
    </div>
  </>;
}

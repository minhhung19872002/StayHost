import { state } from './store.js';

/**
 * docs/01 — interface translation, the dictionary way (like every localised site):
 * a table of the app's own Vietnamese UI strings mapped to each language. Wrap a
 * string with t('…') and Vietnamese returns it unchanged, another language looks
 * it up. Missing entries fall back to English, then to the original — so a
 * half-filled language is readable rather than broken.
 *
 * Keyed by the Vietnamese source so existing markup only needs wrapping, not a new
 * key invented for every label. This is the global chrome first (header, menu,
 * footer, common buttons); page bodies are wrapped in later batches.
 */
const EN = {
  // Header nav
  'Tất cả': 'All',
  'Chỗ ở': 'Homes',
  'Trải nghiệm': 'Experiences',
  'Dịch vụ': 'Services',
  'Cho thuê nhà': 'Become a host',
  'Trang chủ nhà': 'Hosting',
  'Thông báo': 'Notifications',
  'Mọi nơi': 'Anywhere',
  'khách': 'guests',
  // Account menu
  'Đăng ký': 'Sign up',
  'Đăng nhập': 'Log in',
  'Đăng xuất': 'Log out',
  'Tài khoản': 'Account',
  'Tin nhắn': 'Messages',
  'Chuyến đi của tôi': 'My trips',
  'Lịch trình chuyến đi': 'Trip plans',
  'Bạn bè': 'Friends',
  'Cho thuê nhà trên StayHost': 'Host your place on StayHost',
  'Dịch vụ đã đặt': 'Booked services',
  'Danh sách yêu thích': 'Wishlists',
  'Vé trải nghiệm': 'Experience tickets',
  'Số dư & thẻ quà tặng': 'Balance & gift cards',
  'Trung tâm giải quyết': 'Resolution centre',
  'Trang quản trị': 'Admin',
  'Hồ sơ công khai': 'Public profile',
  'Đánh dấu đã đọc': 'Mark as read',
  'Chưa có thông báo nào.': 'No notifications yet.',
  // Search bar & quick filters
  'Địa điểm': 'Where',
  'Ngày': 'When',
  'Khách': 'Guests',
  'Tìm điểm đến': 'Search destinations',
  'Xoá ngày': 'Clear dates',
  'Xong': 'Done',
  'Tổng': 'Total',
  'Tìm kiếm gần đây': 'Recent searches',
  'Xoá': 'Clear',
  'Mọi ngày': 'Any dates',
  'Kết quả gợi ý': 'Suggestions',
  'Điểm đến phổ biến': 'Popular destinations',
  'Bộ lọc': 'Filters',
  'Đặt ngay': 'Instant Book',
  'Huỷ miễn phí': 'Free cancellation',
  'Khách yêu thích': 'Guest favourite',
  'Siêu chủ nhà': 'Superhost',
  'Giá đã gồm thuế và phí': 'Prices include taxes and fees',
  // Footer columns
  'Hỗ trợ': 'Support',
  'Trung tâm trợ giúp': 'Help centre',
  'StayShield cho khách': 'StayShield for guests',
  'Chống phân biệt đối xử': 'Anti-discrimination',
  'Hỗ trợ người khuyết tật': 'Disability support',
  'Tuỳ chọn huỷ': 'Cancellation options',
  'Báo cáo lo ngại khu dân cư': 'Report a neighbourhood concern',
  'Đón tiếp khách': 'Hosting',
  'StayShield cho Chủ nhà': 'StayShield for hosts',
  'Tài nguyên cho Chủ nhà': 'Host resources',
  'Diễn đàn cộng đồng': 'Community forum',
  'Đón tiếp khách có trách nhiệm': 'Responsible hosting',
  'Tham gia khoá học miễn phí': 'Join a free course',
  'Trang tin tức': 'Newsroom',
  'Tính năng mới': "What's new",
  'Cơ hội nghề nghiệp': 'Careers',
  'Nhà đầu tư': 'Investors',
  'Chỗ ở khẩn cấp StayHost.org': 'Emergency stays — StayHost.org',
  'Thẻ quà tặng': 'Gift cards',
  'Khám phá': 'Explore',
  'Chỗ nghỉ ven biển': 'Beachfront stays',
  'Villa có hồ bơi': 'Villas with a pool',
  'Homestay vùng cao': 'Highland homestays',
  'Cabin gỗ Đà Lạt': 'Đà Lạt wooden cabins',
  'Căn hộ dài hạn': 'Long-term apartments',
  'Chỗ nghỉ cho thú cưng': 'Pet-friendly stays',
  'Điểm đến': 'Destinations',
  // Footer legal row
  'Quyền riêng tư': 'Privacy',
  'Điều khoản': 'Terms',
  'Sơ đồ trang web': 'Sitemap',
  'Thông tin công ty': 'Company details',
  // Language / currency modal
  'Ngôn ngữ & tiền tệ': 'Language & currency',
  'Ngôn ngữ đề xuất': 'Suggested languages',
  'Chọn loại tiền tệ': 'Choose a currency',
  // Common
  'Đặt chỗ ngay': 'Reserve now',
  'Tìm kiếm': 'Search',
  'Xem tất cả': 'See all'
};

/** Only English is hand-written so far; other languages fall back to it. */
const DICT = { en: EN };

export function t(s) {
  const code = state.language?.code || 'vi';
  if (code === 'vi') return s;
  return DICT[code]?.[s] ?? DICT.en?.[s] ?? s;
}

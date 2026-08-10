import { state } from './store.js';
import ja from './i18n/ja.js';
import ko from './i18n/ko.js';
import zh from './i18n/zh.js';
import fr from './i18n/fr.js';
import de from './i18n/de.js';
import es from './i18n/es.js';
import pagesEn from './i18n/pages-en.js';
import pagesJa from './i18n/pages-ja.js';
import pagesKo from './i18n/pages-ko.js';
import pagesZh from './i18n/pages-zh.js';
import pagesFr from './i18n/pages-fr.js';
import pagesDe from './i18n/pages-de.js';
import pagesEs from './i18n/pages-es.js';

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
  'Ngôn ngữ': 'Language',
  'Đang tải…': 'Loading…',
  'Ngôn ngữ đề xuất': 'Suggested languages',
  'Chọn loại tiền tệ': 'Choose a currency',
  // Guests summary
  'em bé': 'infants',
  'thú cưng': 'pets',
  // Listing type labels (server-provided, finite set)
  'Căn hộ': 'Apartment',
  'Cabin gỗ': 'Wood cabin',
  'Khách sạn': 'Hotel',
  'Nhà nguyên căn': 'Whole house',
  'Nguyên căn': 'Entire place',
  'Phòng riêng': 'Private room',
  'Phòng chung': 'Shared room',
  // Home rails (server-generated titles/subtitles)
  'Chỗ nghỉ được yêu thích ở ': 'Guest favourites in ',
  'Chỗ nghỉ có hồ bơi riêng': 'Stays with a private pool',
  'Bơi lúc nào cũng được, không phải chia sẻ với ai': 'Swim whenever you like — no sharing',
  'Dưới 1,2 triệu mỗi đêm': 'Under 1.2M a night',
  'Tiết kiệm mà vẫn được đánh giá cao': 'Great value, still highly rated',
  'Cho mang theo thú cưng': 'Pet-friendly',
  'Đi đâu cũng có bạn bốn chân đi cùng': 'Bring your four-legged friend along',
  // Listing cards
  'KHÁCH YÊU THÍCH': 'GUEST FAVOURITE',
  'SIÊU CHỦ NHÀ': 'SUPERHOST',
  'tại': 'in',
  'cho': 'for',
  'đêm': 'nights',
  'Mới': 'New',
  'phòng ngủ': 'bedrooms',
  'giường': 'beds',
  'phòng tắm': 'bathrooms',
  'tổng': 'total',
  'Đã gồm phí · Huỷ miễn phí': 'Fees included · Free cancellation',
  // Browse & search results
  'được yêu thích': 'favourites',
  'Chỗ nghỉ được yêu thích ở Việt Nam': 'Guest-favourite stays across Vietnam',
  'Hơn ': 'Over ',
  'chỗ nghỉ': 'stays',
  'Giá đã gồm mọi khoản phí': 'Prices include all fees',
  'Gợi ý cho chuyến đi sắp tới': 'Ideas for your next trip',
  'Hiện bản đồ': 'Show map',
  'Hiện danh sách': 'Show list',
  // Common
  'Đặt chỗ ngay': 'Reserve now',
  'Tìm kiếm': 'Search',
  'Xem tất cả': 'See all',
  // Detail page
  'Tất cả chỗ nghỉ': 'All stays',
  'đánh giá': 'reviews',
  'Chia sẻ': 'Share',
  'Đã lưu': 'Saved',
  'Lưu chỗ nghỉ': 'Save',
  'Chỗ nghỉ tương tự': 'Similar places',
  'Cùng khu vực hoặc cùng loại hình với': 'Same area or type as',
  'Đang tải chỗ nghỉ': 'Loading the listing',
  'ảnh': 'photo',
  'Hiện tất cả ảnh': 'Show all photos',
  'Ảnh': 'Photos',
  'Tiện nghi': 'Amenities',
  'Đánh giá': 'Reviews',
  'Vị trí': 'Location',
  'Mục trong trang': 'On this page',
  'Một trong những chỗ nghỉ được yêu thích nhất trên StayHost': 'One of the most loved stays on StayHost',
  'do': 'by',
  'cho thuê': '',
  'Điểm nổi bật:': 'Highlight:',
  'Chủ nhà:': 'Host:',
  'năm kinh nghiệm đón khách': 'years hosting guests',
  'là Siêu chủ nhà': 'is a Superhost',
  'Siêu chủ nhà là những chủ nhà giàu kinh nghiệm, được đánh giá cao.': 'Superhosts are experienced, highly rated hosts.',
  'Tự nhận phòng': 'Self check-in',
  'Bạn có thể tự nhận phòng bằng khoá số ở cửa.': 'You can check yourself in with the door keypad.',
  'Huỷ miễn phí trước 48 giờ': 'Free cancellation up to 48 hours before',
  'Không gian riêng để làm việc': 'A dedicated workspace',
  'Phòng có bàn làm việc và wifi tốc độ cao, phù hợp làm việc từ xa.': 'The room has a desk and fast wifi, good for remote work.',
  'Nơi bạn sẽ ngủ': "Where you'll sleep",
  'Tiện nghi tại đây': 'What this place offers',
  'Hiện tất cả': 'Show all',
  'tiện nghi': 'amenities',
  'Mức độ sạch sẽ': 'Cleanliness',
  'Độ chính xác': 'Accuracy',
  'Nhận phòng': 'Check-in',
  'Giao tiếp': 'Communication',
  'Giá trị': 'Value',
  'Sạch sẽ': 'Cleanliness',
  'Chủ nhà': 'Host',
  'Yên tĩnh': 'Quiet',
  'Đáng tiền': 'Great value',
  'Báo cáo đánh giá': 'Report review',
  'sao': 'stars',
  'Phản hồi của chủ nhà': "Host's reply",
  'Sát biển': 'By the beach',
  'Đi bộ vài phút là xuống tới bãi tắm.': 'A few minutes walk to the sand.',
  'Dễ đi lại': 'Easy to get around',
  'Có xe đạp miễn phí để khám phá quanh khu.': 'Free bikes to explore the area.',
  'Tầm nhìn đẹp': 'Great views',
  'Khung cảnh mở, ít bị che chắn.': 'An open outlook with little in the way.',
  'Đỗ xe thuận tiện': 'Easy parking',
  'Có chỗ đậu xe ngay tại chỗ nghỉ.': 'Parking right at the stay.',
  'Trung tâm': 'Central',
  'Gần quán ăn, cà phê và điểm tham quan chính.': 'Near eateries, cafés and the main sights.',
  'Vị trí chỗ nghỉ': 'Where the stay is',
  'Điểm nổi bật của khu vực': 'Neighbourhood highlights',
  'Vị trí chính xác được cung cấp sau khi đặt chỗ thành công.': 'The exact location is shared once your booking is confirmed.',
  'Gặp gỡ chủ nhà': 'Meet your host',
  'điểm trung bình': 'average rating',
  'chỗ nghỉ đang cho thuê': 'active listings',
  'Tỉ lệ phản hồi:': 'Response rate:',
  'Thời gian phản hồi:': 'Response time:',
  'Ngôn ngữ:': 'Languages:',
  'Đồng quản lý:': 'Co-hosts:',
  'Nhắn tin cho': 'Message',
  'Nhắn tin cho chủ nhà': 'Message the host',
  'Những điều cần biết': 'Things to know',
  'Nội quy nhà': 'House rules',
  'An toàn & chỗ ở': 'Safety & property',
  'Chính sách huỷ': 'Cancellation policy',
  'Lịch sử huỷ đơn của chủ nhà': 'Host cancellation history',
  'Chọn loại phòng': 'Choose a room',
  'Còn': 'Left',
  'phòng': 'rooms',
  'Hết phòng cho ngày này': 'No rooms for these dates',
  'phòng rẻ nhất': 'cheapest room',
  'NHẬN PHÒNG': 'CHECK-IN',
  'TRẢ PHÒNG': 'CHECK-OUT',
  'KHÁCH': 'GUESTS',
  'Giảm số khách': 'Fewer guests',
  'Tăng số khách': 'More guests',
  'Bạn chưa bị trừ tiền ở bước này': "You won't be charged yet",
  'Vượt quá sức chứa': 'Over capacity',
  'Chỗ nghỉ này nhận tối đa': 'This place takes at most',
  'Đang tính giá…': 'Working out the price…',
  'Đã giữ chỗ cho bạn': "We've held your spot",
  'Chủ nhà sẽ xác nhận trong 12 giờ.': 'The host will confirm within 12 hours.',
  'Không đặt được': "Couldn't book",
  'Báo cáo chỗ nghỉ này': 'Report this listing',
  // Experiences page
  'Hoạt động do người địa phương dẫn, tính theo vé chứ không theo đêm.': 'Activities led by locals, sold by the ticket rather than the night.',
  'Nấu ăn, chèo SUP, cà phê…': 'Cooking, SUP, coffee…',
  'giờ': 'hr',
  'phút': 'min',
  'tối đa': 'up to',
  'người': 'people',
  'suất': 'seats',
  'Tạm hết suất': 'Sold out for now',
  'Chưa có trải nghiệm nào khớp': 'No experiences match',
  'Thử một từ khoá khác.': 'Try another keyword.',
  'Không tìm thấy trải nghiệm này': "We couldn't find this experience",
  'Buổi này có gì': 'What to expect',
  'Vé bao gồm': 'What the ticket includes',
  'Cần biết': 'Good to know',
  'Điểm hẹn': 'Meeting point',
  'Độ tuổi': 'Age',
  'Từ': 'From',
  'tuổi': 'years old',
  'Mọi lứa tuổi': 'All ages',
  'Tối thiểu': 'Minimum',
  'người thì suất mới chạy': 'people needed for the session to run',
  'Suất không đủ': "A session that doesn't reach",
  'người trước 48 giờ sẽ bị huỷ và hoàn tiền toàn bộ. Bạn huỷ trước 24 giờ cũng được hoàn đủ.': "people 48 hours before is cancelled with a full refund. Cancel more than 24 hours ahead for a full refund too.",
  'Chọn suất': 'Choose a session',
  'còn': 'left',
  'hết chỗ': 'full',
  'Chưa có suất nào mở.': 'No sessions open yet.',
  'Số người': 'People',
  'Thuê trọn nhóm riêng': 'Book the whole group privately',
  'Chỉ nhóm bạn, không ghép với khách khác': 'Just your group, no one else joins',
  'Chưa đặt được': "Can't book yet",
  'Đang xử lý…': 'Processing…',
  'Đặt trải nghiệm': 'Book the experience',
  'Đăng nhập để xem vé của bạn': 'Log in to see your tickets',
  'Huỷ vé': 'Cancel ticket',
  'Đã đặt — mã': 'Booked — ref',
  'Đã huỷ, hoàn': 'Cancelled, refunded',
  'Đã huỷ. Huỷ sát giờ nên không hoàn tiền.': 'Cancelled. No refund for a late cancellation.',
  'chỗ': 'seats',
  'nhóm riêng': 'private group',
  'Mã': 'Ref',
  'Đã hoàn': 'Refunded',
  'Xem trải nghiệm': 'View experience',
  'Chưa có vé nào': 'No tickets yet',
  // Services page
  'Đầu bếp': 'Chef',
  'Chụp ảnh': 'Photography',
  'Đưa đón': 'Transfers',
  'Giữ hành lý': 'Luggage storage',
  'Đi chợ hộ': 'Grocery run',
  'Đầu bếp, chụp ảnh, đưa đón — đặt theo khung giờ, làm tại chỗ bạn ở.': "Chefs, photographers, transfers — booked by the slot, done where you're staying.",
  'Tới tận nơi trong': 'Comes to you within',
  'Khách tới chỗ cung cấp': 'You go to the provider',
  'qua': 'via',
  'Chưa có dịch vụ nào ở nhóm này': 'No services in this group yet',
  'Không tìm thấy dịch vụ này': "We couldn't find this service",
  'thực hiện': '',
  'Dịch vụ này gồm gì': 'What this service includes',
  'Phạm vi phục vụ': 'Service area',
  'Giờ nhận': 'Hours',
  'Nhận từ': 'Quantity',
  'đến': 'to',
  'Đặt trước': 'Lead time',
  'Ít nhất 4 giờ': 'At least 4 hours',
  'Huỷ trước 24 giờ được hoàn toàn bộ. Sau đó thì không hoàn.': 'Cancel more than 24 hours ahead for a full refund. After that, no refund.',
  'Chọn khung giờ': 'Choose a time',
  'đã kín': 'full',
  'còn trống': 'free',
  'Số': 'Number of',
  'Địa chỉ thực hiện': 'Service address',
  'Số nhà, đường, phường': 'House number, street, ward',
  'Ghi chú': 'Notes',
  '(không bắt buộc)': '(optional)',
  'Có người dị ứng hải sản…': 'Someone is allergic to seafood…',
  'Đặt dịch vụ': 'Book the service',
  'Chọn một khung giờ để xem giá.': 'Pick a time to see the price.',
  'Đăng nhập để xem dịch vụ đã đặt': 'Log in to see your booked services',
  'Huỷ đơn': 'Cancel booking',
  'Đã huỷ và hoàn tiền.': 'Cancelled and refunded.',
  'Xem dịch vụ': 'View service',
  'Huỷ': 'Cancel',
  'Chưa đặt dịch vụ nào': 'No services booked yet'
};

/**
 * English is the base dictionary; the rest are per-language tables in ./i18n/.
 * Any key missing from a language falls back to English, then to the Vietnamese
 * source — so a partially filled language stays readable rather than broken.
 */
const DICT = {
  en: { ...EN, ...pagesEn },
  ja: { ...ja, ...pagesJa },
  ko: { ...ko, ...pagesKo },
  zh: { ...zh, ...pagesZh },
  fr: { ...fr, ...pagesFr },
  de: { ...de, ...pagesDe },
  es: { ...es, ...pagesEs },
};

/** Any run of digits, including 1.234.567 and 12,5 — one number, one slot. */
const NUMBERS = /\d[\d.,]*/g;

/**
 * `context` disambiguates a Vietnamese word that means two things. "Hết hạn" is
 * "Expires" on a card and "Expired" as a booking status; keying by the Vietnamese
 * alone cannot hold both. A context looks up "Hết hạn|status" first and falls back
 * to the plain key, so adding one is always safe.
 */
export function t(s, context) {
  const code = state.language?.code || 'vi';
  if (code === 'vi' || typeof s !== 'string' || !s) return s;

  const table = DICT[code];

  if (context) {
    const keyed = `${s}|${context}`;
    const hit = table?.[keyed] ?? DICT.en?.[keyed];
    if (hit !== undefined) return hit;
  }

  const exact = table?.[s] ?? DICT.en?.[s];
  if (exact !== undefined) return exact;

  /*
   * Most of what the server composes is one sentence with a number dropped into
   * it: "2.782.500₫ × 3 đêm", "Phí di chuyển ngoài 10 km", "Còn 2 chỗ". Keying
   * every possible number would be endless, so the numbers are lifted out, the
   * shape is looked up once as "… {} …", and they are put back in order. One
   * dictionary entry then covers every value the server will ever send.
   */
  const numbers = s.match(NUMBERS);
  if (!numbers) return s;

  const shape = s.replace(NUMBERS, '{}');
  const hit = table?.[shape] ?? DICT.en?.[shape];
  if (hit === undefined) return s;

  let i = 0;
  return hit.replace(/\{\}/g, () => numbers[i++] ?? '');
}

/**
 * A few server strings are a fixed prefix followed by a proper noun — a city rail
 * title like "Chỗ nghỉ được yêu thích ở Đà Nẵng". Translate the prefix, keep the
 * name. Whole-string dictionary hits (the theme rails) win first.
 */
const PREFIXES = ['Chỗ nghỉ được yêu thích ở '];

export function tt(s) {
  if (!s) return s;
  const direct = t(s);
  if (direct !== s) return direct;
  for (const p of PREFIXES) {
    if (s.startsWith(p)) return t(p) + s.slice(p.length);
  }
  return s;
}

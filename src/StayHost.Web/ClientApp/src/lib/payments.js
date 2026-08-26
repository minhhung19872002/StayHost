/**
 * docs/07 §2 — the ways Staylio takes money, as the client last knew them.
 *
 * The live list comes from `api.paymentCatalogue()` so the checkout and the
 * saved-methods screen cannot disagree; this is what a checkout falls back to
 * when that call fails, and it is the §2.1 group — the one that has to be there
 * for the platform to work at all. Manual bank transfer used to be offered;
 * §2.4 refuses it.
 *
 * In its own module rather than beside a component, because the stay checkout
 * and the service checkout both read it and neither owns it.
 */
export const FALLBACK_METHODS = [
  { key: 'card', label: 'Thẻ tín dụng / ghi nợ', hint: 'Visa, Mastercard, JCB, American Express' },
  { key: 'napas', label: 'Thẻ ATM nội địa', hint: 'Qua NAPAS, cần đăng ký thanh toán trực tuyến' },
  { key: 'momo', label: 'Ví MoMo', hint: 'Mở ứng dụng MoMo để xác nhận' },
  { key: 'zalopay', label: 'ZaloPay', hint: 'Mở ứng dụng ZaloPay để xác nhận' }
];

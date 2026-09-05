import { useStore } from '../lib/useStore.js';
import { applyTimeZone } from '../lib/store.js';
import { t } from '../lib/i18n.js';

/*
 * docs/01 TK-09 — the display timezone, the third of "ngôn ngữ, tiền tệ, múi
 * giờ". Two of the three shipped and PLAN.md counted the code done — the
 * "soát từng vế" lesson — so this is the late half being made real, not a new
 * idea.
 *
 * A short list rather than the full IANA table: the zones the eight interface
 * languages imply, plus the device default. "Theo thiết bị" is first and null,
 * because the browser's own clock is the only honest default — a guessed zone
 * is wrong for every traveller currently away from home.
 *
 * One component for the two doors that offer it (LanguageModal and
 * /cai-dat/tuy-chinh), so the list cannot drift between them.
 */
const ZONES = [
  [null, 'Theo thiết bị', 'Đồng hồ của máy đang dùng'],
  ['Asia/Ho_Chi_Minh', 'Việt Nam', 'GMT+7'],
  ['Asia/Bangkok', 'Thái Lan', 'GMT+7'],
  ['Asia/Singapore', 'Singapore', 'GMT+8'],
  ['Asia/Shanghai', 'Trung Quốc', 'GMT+8'],
  ['Asia/Tokyo', 'Nhật Bản', 'GMT+9'],
  ['Asia/Seoul', 'Hàn Quốc', 'GMT+9'],
  ['Australia/Sydney', 'Úc — Sydney', 'GMT+10/+11'],
  ['Europe/London', 'Anh', 'GMT+0/+1'],
  ['Europe/Paris', 'Pháp', 'GMT+1/+2'],
  ['Europe/Berlin', 'Đức', 'GMT+1/+2'],
  ['Europe/Madrid', 'Tây Ban Nha', 'GMT+1/+2'],
  ['America/New_York', 'Mỹ — bờ Đông', 'GMT-5/-4'],
  ['America/Los_Angeles', 'Mỹ — bờ Tây', 'GMT-8/-7'],
];

export function TimeZonePicker() {
  const state = useStore();

  return (
    <div className="lang-grid" style={{ marginTop: 14 }}>
      {ZONES.map(([id, label, hint]) => (
        <button key={id ?? 'device'}
                className={`lang ${(state.timeZone ?? null) === id ? 'is-on' : ''}`}
                onClick={() => applyTimeZone(id)}>
          <b>{t(label)}</b><span>{id ? hint : t(hint)}</span>
        </button>
      ))}
    </div>
  );
}

import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, toast, shareListing } from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate, dateFormat } from '../lib/format.js';
import { CardCarousel } from '../components/CardCarousel.jsx';
import { PhotoMosaic } from '../components/PhotoMosaic.jsx';
import { Avatar } from '../components/Avatar.jsx';
import { Icon } from '../components/Icon.jsx';
import { DetailMap } from '../components/Maps.jsx';
import { Sheet } from '../components/modals/Sheet.jsx';
import { t } from '../lib/i18n.js';
import { TranslatedText } from '../components/TranslatedText.jsx';

// Asked for per render so they follow the chosen language (format.js LOCALE).
const TIME = () => dateFormat({ hour: '2-digit', minute: '2-digit' });
const DAY = () => dateFormat({ weekday: 'long', day: 'numeric', month: 'long' });
const MONTH = () => dateFormat({ month: 'long', year: 'numeric' });

const CATEGORIES = [
  ['', 'Tất cả'], ['chef', 'Đầu bếp'], ['photo', 'Chụp ảnh'], ['massage', 'Massage'],
  ['transfer', 'Đưa đón'], ['luggage', 'Giữ hành lý'], ['groceries', 'Đi chợ hộ']
];

/** The category as a guest reads it, for the "Đầu bếp ở Quận 1" line. */
const CATEGORY_LABEL = Object.fromEntries(CATEGORIES.slice(1));

/** Two letters for somebody with no photo, the way the server builds them. */
const initialsOf = name => (name || '?')
  .trim().split(/\s+/).slice(-2).map(w => w[0] ?? '').join('').toUpperCase() || '?';

/** "1 giờ 30 phút" — shared with the option cards and the slot rows. */
const duration = minutes =>
  minutes >= 60
    ? `${Math.floor(minutes / 60)} ${t('giờ')}${minutes % 60 ? ` ${minutes % 60} ${t('phút')}` : ''}`
    : `${minutes} ${t('phút')}`;

/**
 * docs/09 §5 — a service is scored on four headings of its own, and they are not
 * the experience's four: nobody is being led, and "tổ chức và an toàn" is not
 * what a guest judges a haircut on. Kept in the order ServiceReviews.Criteria
 * lists them.
 */
const SVC_CRITERIA = [
  ['skill', 'Tay nghề'],
  ['asDescribed', 'Đúng như mô tả'],
  ['punctuality', 'Đúng giờ'],
  ['value', 'Đáng giá tiền']
];

/** docs/01 MR-05 → MR-07 — services booked by the slot, at an address you give. */
export function Services() {
  const { slug } = useParams();
  return slug ? <Detail slug={slug} /> : <Browse />;
}

function Browse() {
  const navigate = useNavigate();
  const [category, setCategory] = useState('');
  const [items, setItems] = useState(null);

  useEffect(() => {
    api.services({ category: category || undefined })
      .then(setItems)
      .catch(e => toast(e.message));
  }, [category]);

  return (
    <div className="shell" style={{ paddingBlock: '30px 90px' }}>
      <h1 className="section-title">{t('Dịch vụ')}</h1>
      <p className="section-sub">{t('Đầu bếp, chụp ảnh, đưa đón — đặt theo khung giờ, làm tại chỗ bạn ở.')}</p>

      <div className="seg-tabs" style={{ marginTop: 16 }}>
        {CATEGORIES.map(([key, label]) => (
          <button key={key || 'all'} className={`seg-tab ${category === key ? 'is-active' : ''}`}
                  onClick={() => setCategory(key)}>{t(label)}</button>
        ))}
      </div>

      {!items ? (
        <div className="card-grid" style={{ marginTop: 24 }}>
          {Array.from({ length: 4 }, (_, i) => (
            <div className="card skeleton" key={i} style={{ height: 280, border: 0 }} />
          ))}
        </div>
      ) : items.length ? (
        <div className="card-grid" style={{ marginTop: 24 }}>
          {items.map(s => (
            <button className="card" key={s.id} onClick={() => navigate(`/services/${s.slug}`)}
                    style={{ textAlign: 'left', border: 0, background: 'none', padding: 0, cursor: 'pointer' }}>
              <CardCarousel images={s.images} alt={s.title} />
              <div className="card-body">
                <div className="card-row">
                  <h3 className="card-title"><TranslatedText as="span" text={s.title} notice={false} /></h3>
                  <div className="card-rating">
                    {s.reviewCount ? `★ ${s.rating.toFixed(2)} (${s.reviewCount})` : `★ ${t('Mới')}`}
                  </div>
                </div>
                {/* Airbnb's service cards read "who does it, where" before the
                    price; the pricing model belongs beside the number instead. */}
                <div className="card-sub card-line">
                  {s.hostName}{s.isPartner ? ` · ${s.partnerName}` : ''} · {s.city}
                </div>
                <div className="card-price">
                  <b>{money(s.basePrice)}</b> <span>/ {t(s.unit)}</span>
                  {s.durationMinutes > 0 && <span> · {duration(s.durationMinutes)}</span>}
                </div>
                <div className="card-perks card-line">
                  {s.travelsToGuest ? `${t('Tới tận nơi trong')} ${s.serviceRadiusKm} km` : t('Khách tới chỗ cung cấp')}
                </div>
              </div>
            </button>
          ))}
        </div>
      ) : (
        <div className="empty-state" style={{ marginTop: 28 }}><h3>{t('Chưa có dịch vụ nào ở nhóm này')}</h3></div>
      )}
    </div>
  );
}

/**
 * docs/09 §3.4 (MR-S-05) — the provider's working week, read the way
 * ServiceRules.WorksOn reads it: Monday is bit 0 and Sunday bit 6.
 */
function worksOn(detail, date) {
  const mask = detail.workingDaysMask ?? 127;
  const day = date.getDay();
  return (mask & (1 << (day === 0 ? 6 : day - 1))) !== 0;
}

/**
 * docs/09 §3.4 — nothing may be booked closer than this to now
 * (ServiceRules.MinimumNotice). Kept here so the picker never offers a row the
 * server is bound to refuse.
 */
const MINIMUM_NOTICE_MS = 4 * 3600_000;

/** Two-hour steps inside the provider's working day, for the next fortnight. */
function slotsFor(detail) {
  const out = [];
  const now = new Date();
  const earliest = now.getTime() + MINIMUM_NOTICE_MS;

  for (let day = 0; day < 14; day++) {
    for (let hour = detail.opensAtHour; hour < detail.closesAtHour; hour += 2) {
      const at = new Date(now);
      at.setDate(at.getDate() + day);
      at.setHours(hour, 0, 0, 0);
      if (at.getTime() < earliest) continue;
      // A day the provider does not work at all never becomes a slot to click.
      if (!worksOn(detail, at)) continue;

      const ends = new Date(at.getTime() + detail.durationMinutes * 60000);
      if (ends.getHours() > detail.closesAtHour && ends.getHours() !== 0) continue;

      const taken = detail.busy.some(b => at < new Date(b.to) && new Date(b.from) < ends);
      out.push({ at, ends, taken });
    }
  }
  return out;
}

/** "Hôm nay, 12 tháng 8" — the day a session falls on, said the short way. */
function dayLabel(date) {
  const today = new Date();
  const tomorrow = new Date(today.getTime() + 86400000);
  const same = (a, b) => a.toDateString() === b.toDateString();
  const written = DAY().format(date);

  if (same(date, today)) return `${t('Hôm nay')} · ${written}`;
  if (same(date, tomorrow)) return `${t('Ngày mai')} · ${written}`;
  return written;
}

function Detail({ slug }) {
  const state = useStore();
  const navigate = useNavigate();
  const [s, setS] = useState(null);
  const [missing, setMissing] = useState(false);
  const [when, setWhen] = useState(null);
  const [quantity, setQuantity] = useState(1);
  const [address, setAddress] = useState('');
  const [note, setNote] = useState('');
  // docs/09 §3.3 — the paid extras ticked (MR-S-03) and the guest's word that the
  // place has what the job needs (MR-S-07). Both travel with the quote as well as
  // the booking: an extra changes the price, and an unconfirmed condition is the
  // very thing that makes the job unbookable.
  const [addOnIds, setAddOnIds] = useState([]);
  const [conditionsOk, setConditionsOk] = useState(false);
  const [quote, setQuote] = useState(null);
  const [busy, setBusy] = useState(false);
  const [booking, setBooking] = useState(false);

  const load = () => api.service(slug).then(d => {
    setS(d);
    setQuantity(q => Math.max(q, d.minQuantity));
  }).catch(() => setMissing(true));

  useEffect(() => { load(); }, [slug]);

  useEffect(() => {
    if (!s || !when) { setQuote(null); return; }
    // The address decides whether the provider will come at all, so it is part
    // of the quote rather than something checked at the end (docs/01 MR-05).
    api.quoteService(s.id, {
      startsAt: when.toISOString(),
      quantity,
      address: address.trim() || null,
      latitude: s.latitude + 0.01,
      longitude: s.longitude + 0.01,
      addOnIds,
      conditionsConfirmed: conditionsOk
    }).then(setQuote).catch(e => toast(e.message));
  }, [s, when, quantity, address, addOnIds, conditionsOk]);

  if (missing) {
    return <div className="shell" style={{ paddingBlock: '40px 90px' }}>
      <div className="empty-state"><h3>{t('Không tìm thấy dịch vụ này')}</h3>
        <button className="btn btn-primary" style={{ marginTop: 18 }}
                onClick={() => navigate('/services')}>{t('Xem tất cả')}</button></div></div>;
  }

  if (!s) return <div className="shell" style={{ paddingBlock: '40px 90px' }}>
    <div className="stat skeleton" style={{ height: 300, border: 0 }} /></div>;

  const book = async () => {
    if (!state.user) { set({ authMode: 'login', authError: null, overlay: 'login' }); return; }
    setBusy(true);
    try {
      const b = await api.bookService(s.id, {
        startsAt: when.toISOString(),
        quantity,
        address: address.trim() || null,
        latitude: s.latitude + 0.01,
        longitude: s.longitude + 0.01,
        note: note.trim() || null,
        paymentMethod: 'card',
        cardLast4: '4242',
        addOnIds,
        conditionsConfirmed: conditionsOk
      });
      toast(`${t('Đã đặt — mã')} ${b.reference}`);
      navigate('/services/bookings');
    } catch (err) { toast(err.message); } finally { setBusy(false); }
  };

  const addOns = s.addOns ?? [];
  // docs/09 §3.3 (MR-S-07) — a service with conditions cannot be booked until the
  // guest has said the place meets them; that answer is what makes §3.6's "khai
  // sai điều kiện" rule fair, so it is a tick of its own, not fine print.
  const requirements = s.onSiteRequirements ?? [];
  const conditionsPending = requirements.length > 0 && !conditionsOk;
  const cover = s.images[0];
  const category = t(CATEGORY_LABEL[s.category] ?? 'Dịch vụ');

  /* An extra is picked from the list on the right as well as from inside the
     dialog, so the two share one handler rather than each keeping their own. */
  const toggleAddOn = id =>
    setAddOnIds(ids => ids.includes(id) ? ids.filter(x => x !== id) : [...ids, id]);

  const openBooking = () => setBooking(true);

  return (
    <div className="shell" style={{ paddingBlock: '26px 90px' }}>
      <button className="back-link" onClick={() => navigate('/services')}>← {t('Dịch vụ')}</button>

      {/*
        * The provider on the left, what they sell on the right. A service is
        * bought from a person who is going to turn up at your address, so the
        * rail that stays on screen is their face, their trade and their price —
        * and the column that scrolls is everything the guest reads to decide.
        */}
      <div className="svc-page">
        <aside className="svc-rail">
          <div className="svc-id">
            <button className="svc-cover" onClick={() => {
              document.getElementById('svc-portfolio')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }} aria-label={t('Xem bộ ảnh')}>
              <img src={cover} alt={s.title} decoding="async" />
              <Avatar className="avatar svc-face" url={s.hostAvatarUrl} initials={s.hostInitials} />
            </button>

            <h1><TranslatedText as="span" text={s.title} /></h1>
            <p className="svc-id-sub"><TranslatedText as="span" text={s.summary} notice={false} /></p>

            <div className="svc-id-meta">
              {s.reviewCount
                ? <span><b>★ {s.rating.toFixed(2)}</b> · {s.reviewCount} {t('đánh giá')}</span>
                : <span className="muted">{t('Chưa có đánh giá')}</span>}
              {/* "Đầu bếp ở Quận 1" — a trade and a place, the way Airbnb names
                  a provider. Two keys around the city rather than one sentence,
                  so the word order survives translation. */}
              <span className="muted">{category} · {s.city}</span>
              <span className="muted">
                {s.travelsToGuest ? t('Phục vụ tận nơi') : t('Khách tới chỗ cung cấp')}
                {s.isPartner ? ` · ${t('do')} ${s.partnerName} ${t('thực hiện')}` : ''}
              </span>
            </div>

            <div className="svc-id-tools">
              <button onClick={() => shareListing({ title: s.title })} aria-label={t('Chia sẻ')}>
                <Icon name="share" size={19} />
              </button>
            </div>
          </div>

          {/* docs/09 §3.6 — the ladder in one line, where a guest looks for it. */}
          <div className="svc-pill">
            <p><b>{t('Huỷ miễn phí')}</b> · {t('trước 72 giờ được hoàn toàn bộ')}</p>
            <span className="ic"><Icon name="calendar" size={22} /></span>
          </div>

          <div className="svc-buy">
            <span className="svc-buy-price">
              {t('Từ')} <b>{money(s.basePrice)}</b> / {t(s.unit)}
            </span>
            <button className="btn btn-primary" onClick={openBooking}>{t('Xem lịch')}</button>
          </div>
        </aside>

        <div className="svc-body">
          <section className="detail-section" style={{ paddingTop: 0 }}>
            <h2>{t('Dịch vụ này gồm gì')}</h2>
            <TranslatedText as="p" style={{ fontSize: 15.5, lineHeight: 1.75, color: 'var(--ink-body)' }}
                            text={s.description} />
          </section>

          {/*
            * docs/09 §3.3 (MR-S-03) — what is actually on sale, priced one line
            * at a time. The base job is the first card because it is the thing
            * being bought; each extra is a card of its own beneath it, so a guest
            * sees what each one costs before it lands on the bill rather than
            * after. Ticking one here and ticking it in the dialog is the same act.
            */}
          <section className="detail-section">
            <h2>{t('Chọn gói')}</h2>
            <div className="svc-options">
              <button type="button" className="svc-option is-on" onClick={openBooking}>
                <img src={cover} alt="" loading="lazy" decoding="async" />
                <span className="svc-option-body">
                  <span className="svc-option-tag">{t('Gói cơ bản')}</span>
                  <h3><TranslatedText as="span" text={s.title} notice={false} /></h3>
                  <p><TranslatedText as="span" text={s.summary} notice={false} /></p>
                  <span className="svc-option-price">
                    <b>{money(s.basePrice)}</b> / {t(s.unit)} · {duration(s.durationMinutes)}
                  </span>
                </span>
              </button>

              {addOns.map((a, i) => (
                <button type="button" key={a.id}
                        className={`svc-option ${addOnIds.includes(a.id) ? 'is-on' : ''}`}
                        onClick={() => toggleAddOn(a.id)}>
                  <img src={s.images[(i + 1) % s.images.length] ?? cover} alt="" loading="lazy" decoding="async" />
                  <span className="svc-option-body">
                    <span className="svc-option-tag">
                      {addOnIds.includes(a.id) ? t('Đã chọn') : t('Tuỳ chọn thêm')}
                    </span>
                    {/* The provider named these extras themselves. */}
                    <h3><TranslatedText as="span" text={a.name} notice={false} /></h3>
                    <span className="svc-option-price">
                      <b>+{money(a.price)}</b> · {t('cộng vào gói cơ bản')}
                    </span>
                  </span>
                </button>
              ))}
            </div>
            {/* Not "Nhắn cho {tên} nếu…": a sentence with a name dropped into the
                middle of it only reads in languages that put the object there. */}
            <p className="section-sub" style={{ marginTop: 14 }}>
              {t('Muốn điều chỉnh cho hợp hơn? Nhắn cho nhà cung cấp.')}
            </p>
          </section>

          <ServiceReviews service={s} />

          {/* docs/09 §3.2 — vetting is the whole reason this row of a stranger's
              credentials exists, so it is said with the same weight Airbnb gives
              "My qualifications" rather than buried in a fact grid. */}
          <section className="detail-section">
            <h2>{t('Trình độ của tôi')}</h2>
            <div className="svc-qual">
              <div className="svc-qual-head">
                <Avatar url={s.hostAvatarUrl} initials={s.hostInitials} />
                <div>
                  <b>{s.hostName}</b>
                  <span>
                    {category}
                    {s.hostIsSuperhost ? ` · ${t('Siêu chủ nhà')}` : ''}
                  </span>
                </div>
              </div>

              {s.hostYears > 0 && (
                <div className="svc-qual-item">
                  <b>{s.hostYears} {t('năm kinh nghiệm')}</b>
                </div>
              )}

              {s.hostBio && (
                <div className="svc-qual-item">
                  <TranslatedText as="span" text={s.hostBio} />
                </div>
              )}

              {s.certificateName && (
                <div className="svc-qual-item">
                  <b>{t('Chứng chỉ hành nghề')}</b>
                  <span>
                    <TranslatedText as="span" text={s.certificateName} notice={false} />
                    {s.certificateExpiresOn
                      ? ` · ${t('có hiệu lực đến')} ${longDate(s.certificateExpiresOn)}`
                      : ''}
                  </span>
                </div>
              )}

              <div>
                <button className="btn btn-outline" onClick={() => navigate('/messages')}>
                  {t('Nhắn cho nhà cung cấp')}
                </button>
              </div>

              <p className="svc-safe">
                {t('Để được bảo vệ, hãy luôn thanh toán và trao đổi qua StayHost.')}
              </p>
            </div>
          </section>

          {!!s.images.length && (
            <section className="detail-section" id="svc-portfolio">
              <h2>{t('Bộ ảnh của tôi')}</h2>
              <PhotoMosaic images={s.images} alt={s.title} />
            </section>
          )}

          <section className="detail-section">
            <h2>{t('Nơi thực hiện')}</h2>
            <p className="section-sub" style={{ marginBottom: 14 }}>
              {s.travelsToGuest
                ? `${t('Tới tận nơi trong')} ${s.serviceRadiusKm} km ${t('quanh')} ${s.city}`
                : `${t('Khách tới chỗ cung cấp')} · ${s.city}`}
            </p>
            <DetailMap latitude={s.latitude} longitude={s.longitude} />
          </section>

          <section className="detail-section">
            <h2>{t('Cần biết')}</h2>
            <div className="xp-know">
              <Know icon="users" title={t('Nhận việc')}
                    body={`${s.minQuantity}–${s.maxQuantity} ${t(s.unit)} ${t('mỗi lần')}`} />
              <Know icon="calendar" title={t('Giờ nhận')}
                    body={`${s.opensAtHour}:00 – ${s.closesAtHour}:00 · ${t('đặt trước ít nhất 4 giờ')}`} />
              <Know icon="pin" title={t('Phạm vi phục vụ')}
                    body={s.travelsToGuest
                      ? (s.travelFeePerKm > 0
                          ? `${s.serviceRadiusKm} km ${t('miễn phí')}, ${t('xa hơn')} ${money(s.travelFeePerKm)}/km`
                          : `${t('Tới tận nơi trong')} ${s.serviceRadiusKm} km`)
                      : t('Khách tới chỗ cung cấp')} />
              {/* The ladder as ServiceRules actually applies it: 72 hours, then
                  24, then nothing. The page used to promise a full refund at 24
                  hours, which is half of what the guest would really get. */}
              <Know icon="selfcheckin" title={t('Chính sách huỷ')}
                    body={t('Trước 72 giờ hoàn 100%, trước 24 giờ hoàn 50%, sát giờ không hoàn.')} />
            </div>

            {!!requirements.length && (
              <div style={{ marginTop: 20 }}>
                <b style={{ fontSize: 15 }}>{t('Nơi thực hiện cần có')}</b>
                <ul style={{ margin: '8px 0 0', paddingLeft: 20, lineHeight: 1.8, color: 'var(--ink-body)' }}>
                  {requirements.map(r => (
                    <li key={r}><TranslatedText as="span" text={r} notice={false} /></li>
                  ))}
                </ul>
              </div>
            )}
          </section>

          {/* docs/09 §3.2 — "duyệt thủ công, không tự động", and stricter than an
              experience. That is a selling point, so it is said out loud. */}
          <section className="detail-section">
            <div className="svc-trust">
              <h3>{t('Nhà cung cấp trên StayHost đều được thẩm định')}</h3>
              <p>
                {t('Mỗi hồ sơ được người thật xét duyệt: xác minh danh tính, kiểm tra lý lịch tư pháp với dịch vụ tới tận nhà, và chứng chỉ hành nghề còn hạn theo từng nghề.')}
              </p>
            </div>
            <p className="svc-report">
              {t('Thấy điều gì bất thường?')}{' '}
              <button className="link-btn" onClick={() => navigate('/help')}>{t('Báo cáo tin này')}</button>
            </p>
          </section>
        </div>
      </div>

      {booking && (
        <BookingSheet
          service={s} slots={slotsFor(s)} when={when} onPick={setWhen}
          quantity={quantity} setQuantity={setQuantity}
          address={address} setAddress={setAddress}
          note={note} setNote={setNote}
          addOnIds={addOnIds} toggleAddOn={toggleAddOn}
          requirements={requirements} conditionsOk={conditionsOk} setConditionsOk={setConditionsOk}
          quote={quote} busy={busy}
          blocked={conditionsPending || (!!s.requiredNote && !note.trim())}
          onBook={book} onClose={() => setBooking(false)} />
      )}
    </div>
  );
}

/**
 * docs/09 §3.5 — everything a service needs before it can be sent, in the
 * dialog the price card opens. It used to sit permanently in a side panel, which
 * meant a guest read the address field and the allergy note before they had
 * chosen a time — and the page could show only the first fourteen slots, because
 * a taller list would have pushed the price off the screen.
 */
function BookingSheet({
  service: s, slots, when, onPick, quantity, setQuantity, address, setAddress,
  note, setNote, addOnIds, toggleAddOn, requirements, conditionsOk, setConditionsOk,
  quote, busy, blocked, onBook, onClose
}) {
  // One heading per day, the slots of that day under it.
  const days = useMemo(() => {
    const groups = [];
    for (const slot of slots) {
      const key = slot.at.toDateString();
      const last = groups[groups.length - 1];
      if (last?.key === key) last.slots.push(slot);
      else groups.push({ key, at: slot.at, slots: [slot] });
    }
    return groups;
  }, [slots]);

  const addOns = s.addOns ?? [];

  return (
    <Sheet title={t('Chọn khung giờ')} onClose={onClose}
           foot={
             <>
               <span>
                 {quote
                   ? <><b style={{ fontSize: 16 }}>{money(quote.total)}</b>{' '}
                       <span style={{ color: 'var(--ink-muted)', fontSize: 13 }}>{t('tổng cộng')}</span></>
                   : <span style={{ color: 'var(--ink-muted)', fontSize: 13.5 }}>
                       {t('Chọn một khung giờ để xem giá.')}
                     </span>}
               </span>
               <button className="btn btn-primary"
                       disabled={busy || !quote || !quote.canBook || blocked}
                       onClick={onBook}>
                 {busy ? t('Đang xử lý…') : t('Đặt dịch vụ')}
               </button>
             </>
           }>
      {s.pricing !== 'PerSession' && (
        <div className="count-row" style={{ paddingTop: 0 }}>
          <div className="tx">
            <b>{quantity} {t(s.unit)}</b>
            <span>{t('Nhận từ')} {s.minQuantity} {t('đến')} {s.maxQuantity}</span>
          </div>
          <div className="count-ctl">
            <button type="button" className="round-btn" aria-label={t('Giảm')}
                    disabled={quantity <= s.minQuantity}
                    onClick={() => setQuantity(q => Math.max(s.minQuantity, q - 1))}>−</button>
            <span className="num">{quantity}</span>
            <button type="button" className="round-btn" aria-label={t('Tăng')}
                    disabled={quantity >= s.maxQuantity}
                    onClick={() => setQuantity(q => Math.min(s.maxQuantity, q + 1))}>+</button>
          </div>
        </div>
      )}

      <div className="slot-month">
        <span>{MONTH().format(when ?? new Date())}</span>
        <Icon name="calendar" size={20} />
      </div>

      {days.length ? days.map(day => (
        <div key={day.key}>
          <h3 className="slot-day">{dayLabel(day.at)}</h3>
          <div className="slot-list">
            {day.slots.map(({ at, ends, taken }) => (
              <button key={at.toISOString()} type="button" disabled={taken}
                      className={`slot-card ${when && +when === +at ? 'is-on' : ''}`}
                      onClick={() => onPick(at)}>
                <span className="slot-card-when">
                  <b>{TIME().format(at)} – {TIME().format(ends)}</b>
                  <span>{money(s.basePrice)} / {t(s.unit)} · {duration(s.durationMinutes)}</span>
                </span>
                <i className={taken ? '' : 'is-scarce'}>{taken ? t('đã kín') : t('còn trống')}</i>
              </button>
            ))}
          </div>
        </div>
      )) : <p className="slot-empty">{t('Hai tuần tới chưa có khung giờ nào trống.')}</p>}

      {when && <>
        {/* docs/09 §3.3 (MR-S-03) — each extra is priced on its own and shows up
            as its own line on the quote, so nothing grows quietly. */}
        {!!addOns.length && (
          <div className="modal-section" style={{ marginTop: 22 }}>
            <h3>{t('Tuỳ chọn thêm')}</h3>
            <div style={{ display: 'grid', gap: 8, marginTop: 10 }}>
              {addOns.map(a => (
                <label className="check-row" key={a.id}>
                  <input type="checkbox" checked={addOnIds.includes(a.id)}
                         onChange={() => toggleAddOn(a.id)} />
                  <span style={{ flex: '1 1 auto' }}>
                    <TranslatedText as="span" text={a.name} notice={false} />
                  </span>
                  <b>+{money(a.price)}</b>
                </label>
              ))}
            </div>
          </div>
        )}

        {s.travelsToGuest && (
          <label className="form-field" style={{ marginTop: 18 }}>
            <span className="cap">{t('Địa chỉ thực hiện')}</span>
            <input value={address} placeholder={t('Số nhà, đường, phường')}
                   onChange={e => setAddress(e.target.value)} />
          </label>
        )}

        {/* docs/09 §3.3 (MR-S-07) — the provider turns up expecting these. */}
        {!!requirements.length && (
          <div className="book-alert" style={{ marginTop: 14 }}>
            <b>{t('Nơi thực hiện cần có')}</b>
            <ul style={{ margin: '6px 0 0', paddingLeft: 18, fontSize: 13, lineHeight: 1.7, color: '#5c5c5c' }}>
              {requirements.map(r => (
                <li key={r}><TranslatedText as="span" text={r} notice={false} /></li>
              ))}
            </ul>
            <label className="check-row" style={{ marginTop: 10, alignItems: 'flex-start' }}>
              <input type="checkbox" checked={conditionsOk}
                     onChange={e => setConditionsOk(e.target.checked)} />
              <span style={{ fontSize: 13, lineHeight: 1.5 }}>
                {t('Tôi xác nhận nơi thực hiện có đủ những điều kiện trên.')}
              </span>
            </label>
            <span style={{ fontSize: 12.5, marginTop: 6, display: 'block', color: '#5c5c5c', lineHeight: 1.5 }}>
              {t('Khai sai điều kiện thì nhà cung cấp vẫn được nhận 50% giá trị đơn.')}
            </span>
          </div>
        )}

        <label className="form-field" style={{ marginTop: 14 }}>
          <span className="cap">
            {s.requiredNote
              ? <>{t(s.requiredNote)} <span style={{ color: 'var(--danger, #c0392b)' }}>*</span></>
              : <>{t('Ghi chú')} <span style={{ fontWeight: 400 }}>{t('(không bắt buộc)')}</span></>}
          </span>
          <input value={note} placeholder={s.requiredNote ? t(s.requiredNote) : t('Có người dị ứng hải sản…')}
                 onChange={e => setNote(e.target.value)} />
          {s.requiredNote && !note.trim() &&
            <span className="hint" style={{ color: 'var(--ink-muted)', fontSize: 13 }}>
              {t('Dịch vụ này bắt buộc điền thông tin trên trước khi đặt.')}</span>}
        </label>

        {quote && <>
          <div className="book-lines" style={{ marginTop: 18 }}>
            {quote.lines.map(l => (
              <div className="book-line" key={l.key}><span>{t(l.label)}</span><b>{money(l.amount)}</b></div>
            ))}
            <div className="book-line is-total"><span>{t('Tổng')}</span><b>{money(quote.total)}</b></div>
          </div>

          {!quote.canBook &&
            <div className="book-alert is-error"><b>{t('Chưa đặt được')}</b><span>{quote.reason}</span></div>}
        </>}
      </>}
    </Sheet>
  );
}

/** One thing worth knowing before booking: an icon, a heading and a single line. */
function Know({ icon, title, body }) {
  return (
    <div className="xp-know-item">
      <span className="xp-know-ic"><Icon name={icon} size={22} /></span>
      <b>{title}</b>
      <span>{body}</span>
    </div>
  );
}

/**
 * docs/09 §5 — what people who actually had the job done wrote, on the four
 * headings a service has. Laid out like the stay's and the experience's review
 * blocks so the site reads as one place, scored on its own criteria.
 */
function ServiceReviews({ service }) {
  const [rows, setRows] = useState(null);

  useEffect(() => {
    api.serviceReviews(service.id).then(setRows).catch(err => toast(err.message));
  }, [service.id]);

  if (!rows) return null;

  const average = key => rows.reduce((sum, r) => sum + r[key], 0) / rows.length;

  return (
    <section className="detail-section">
      <h2>{t('Đánh giá')}</h2>

      {/* The headline score comes off the offering, so it is shown whenever there
          is one — saying "chưa có đánh giá nào" under a ★4.70 the rail is already
          advertising would be the page contradicting itself. */}
      {service.reviewCount > 0 ? (
        <div className="rating-summary">
          <span className="rating-big">★ {service.rating.toFixed(2)}</span>
          <span style={{ fontSize: 15, color: 'var(--ink-muted)' }}>
            · {service.reviewCount} {t('đánh giá')}
          </span>
        </div>
      ) : (
        <p className="section-sub">
          {t('Dịch vụ này chưa có đánh giá nào. Chỉ người đã dùng mới viết được.')}
        </p>
      )}

      {!rows.length ? null : <>
      <div className="rating-bars">
        {SVC_CRITERIA.map(([key, label]) => (
          <div className="rating-bar" key={key}>
            <span>{t(label)}</span>
            <span className="track"><span className="fill" style={{ width: `${(average(key) / 5) * 100}%` }} /></span>
            <span className="val">{average(key).toFixed(1)}</span>
          </div>
        ))}
      </div>

      <div className="review-grid">
        {rows.map(r => (
          <article className="review" key={r.id}>
            <div className="review-head">
              <Avatar url={r.authorAvatarUrl} initials={initialsOf(r.authorName)} />
              <div style={{ minWidth: 0 }}>
                <div className="review-name">{r.authorName}</div>
                <div className="review-when">{longDate(r.createdAt.slice(0, 10))}</div>
              </div>
            </div>
            <div className="topic-row" style={{ margin: '4px 0 0' }}>
              {SVC_CRITERIA.map(([key, label]) => (
                <span className="topic-chip" key={key}>{t(label)} <b>★ {r[key]}</b></span>
              ))}
            </div>
            {!!r.comment && <p>{r.comment}</p>}
          </article>
        ))}
      </div>
      </>}
    </section>
  );
}

/**
 * docs/09 §5 — a service is over when its hours are up; there is no register to
 * sign the way an experience has, so the job ending is the whole test.
 */
const jobEnded = r =>
  new Date(r.startsAt).getTime() + r.durationMinutes * 60000 <= Date.now();

const canReviewJob = r =>
  jobEnded(r) && !r.hasReview && (r.status === 'Confirmed' || r.status === 'Completed');

/** docs/09 §5 — four criteria of the service's own and an optional word. */
function JobReview({ booking, onDone }) {
  const [scores, setScores] = useState({ skill: 5, asDescribed: 5, punctuality: 5, value: 5 });
  const [busy, setBusy] = useState(false);

  const stars = key => (
    <div className="star-row" data-field={key}>
      {[1, 2, 3, 4, 5].map(n => (
        <button type="button" key={n} aria-label={`${n} ${t('sao')}`}
                className={`star sm ${n <= scores[key] ? 'is-on' : ''}`}
                onClick={() => setScores(s => ({ ...s, [key]: n }))}>★</button>
      ))}
    </div>
  );

  const submit = async e => {
    e.preventDefault();
    const comment = e.currentTarget.comment.value.trim();
    setBusy(true);
    try {
      await api.reviewServiceBooking(booking.id, { ...scores, comment });
      toast(t('Đã gửi đánh giá. Cảm ơn bạn.'));
      onDone();
    } catch (err) {
      // "Buổi này chưa kết thúc nên chưa đánh giá được", "Bạn đã đánh giá đơn
      // này rồi" — the server's sentence, said as it is.
      toast(err.message);
    } finally { setBusy(false); }
  };

  return (
    <form onSubmit={submit}
          style={{ flexBasis: '100%', minWidth: 0, marginTop: 14, paddingTop: 14, borderTop: '1px solid var(--divider)' }}>
      <b style={{ fontSize: 15 }}>{t('Dịch vụ này thế nào?')}</b>
      {SVC_CRITERIA.map(([key, label]) => (
        <div className="count-row" key={key}>
          <div className="tx"><b>{t(label)}</b></div>
          {stars(key)}
        </div>
      ))}

      <label className="form-field" style={{ marginTop: 14 }}>
        <span className="cap">{t('Nhận xét')} <span style={{ fontWeight: 400 }}>{t('(không bắt buộc)')}</span></span>
        <textarea name="comment" rows={4}
                  placeholder={t('Họ làm có đúng hẹn không? Kết quả có như bạn mong đợi?')}
                  style={{ width: '100%', padding: '12px 14px', border: '1px solid var(--line)', borderRadius: 12, fontSize: 14 }} />
      </label>

      <button type="submit" className="btn btn-primary btn-sm" disabled={busy}>
        {busy ? t('Đang gửi…') : t('Gửi đánh giá')}
      </button>
    </form>
  );
}

export function ServiceBookings() {
  const state = useStore();
  const navigate = useNavigate();
  const [rows, setRows] = useState(null);
  // Which job has its review form open, and which ones this visit has sent.
  const [reviewing, setReviewing] = useState(null);
  const [sent, setSent] = useState([]);

  const load = () => api.serviceBookings().then(setRows).catch(e => toast(e.message));
  useEffect(() => { if (state.user) load(); }, [state.user]);

  if (!state.user) {
    return <div className="shell" style={{ paddingBlock: '60px 90px' }}>
      <div className="empty-state"><h3>{t('Đăng nhập để xem dịch vụ đã đặt')}</h3>
        <button className="btn btn-primary" style={{ marginTop: 18 }}
                onClick={() => set({ authMode: 'login', authError: null, overlay: 'login' })}>{t('Đăng nhập')}</button>
      </div></div>;
  }

  const cancel = async row => {
    if (!confirm(`${t('Huỷ đơn')} ${row.reference}?`)) return;
    try {
      const after = await api.cancelServiceBooking(row.id);
      toast(after.refundedAmount > 0 ? t('Đã huỷ và hoàn tiền.') : t('Đã huỷ. Huỷ sát giờ nên không hoàn tiền.'));
      load();
    } catch (err) { toast(err.message); }
  };

  return (
    <div className="shell" style={{ paddingBlock: '30px 90px' }}>
      <h1 className="section-title">{t('Dịch vụ đã đặt')}</h1>

      {!rows ? <div className="stat skeleton" style={{ height: 200, border: 0, marginTop: 24 }} />
        : rows.length ? (
          <div style={{ marginTop: 20, display: 'grid', gap: 12 }}>
            {rows.map(r => (
              <article className="host-booking" key={r.id}>
                <div style={{ minWidth: 0 }}>
                  <h3><TranslatedText as="span" text={r.title} notice={false} /></h3>
                  <div className="meta">
                    {longDate(r.startsAt.slice(0, 10))} · {TIME().format(new Date(r.startsAt))} ·
                    {' '}{r.quantity} {t(r.unit)} · {money(r.total)}
                  </div>
                  <div className="meta">{t('Mã')} {r.reference}{r.address ? ` · ${r.address}` : ''}</div>
                  {r.note && <div className="meta">{t('Ghi chú')}: {r.note}</div>}
                  {r.cancelReason && <div className="meta">{t(r.cancelReason)}</div>}
                  {r.refundedAmount > 0 && <div className="meta">{t('Đã hoàn')} {money(r.refundedAmount)}</div>}
                  <span className={`badge ${r.statusBadge}`} style={{ marginTop: 8 }}>{t(r.statusLabel)}</span>
                </div>
                <div className="host-booking-actions">
                  <button className="btn btn-outline btn-sm"
                          onClick={() => navigate(`/services/${r.slug}`)}>{t('Xem dịch vụ')}</button>
                  {(r.status === 'Confirmed' || r.status === 'Requested') &&
                    <button className="btn btn-outline btn-sm" onClick={() => cancel(r)}>{t('Huỷ')}</button>}
                  {/* docs/09 §5 — only once the job is over, and only once. */}
                  {r.hasReview || sent.includes(r.id)
                    ? (jobEnded(r) && <span className="badge confirmed">{t('Đã đánh giá')}</span>)
                    : canReviewJob(r) && (
                        <button className="btn btn-outline btn-sm"
                                onClick={() => setReviewing(id => id === r.id ? null : r.id)}>
                          {reviewing === r.id ? t('Đóng') : t('Đánh giá dịch vụ')}
                        </button>
                      )}
                </div>
                {reviewing === r.id && !sent.includes(r.id) && (
                  <JobReview booking={r}
                             onDone={() => { setSent(ids => [...ids, r.id]); setReviewing(null); load(); }} />
                )}
              </article>
            ))}
          </div>
        ) : (
          <div className="empty-state" style={{ marginTop: 24 }}>
            <h3>{t('Chưa đặt dịch vụ nào')}</h3>
            <button className="btn btn-primary" style={{ marginTop: 18 }}
                    onClick={() => navigate('/services')}>{t('Xem dịch vụ')}</button>
          </div>
        )}
    </div>
  );
}

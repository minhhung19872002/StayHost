import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, toast } from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate, dateFormat } from '../lib/format.js';
import { CardCarousel } from '../components/CardCarousel.jsx';
import { PhotoMosaic } from '../components/PhotoMosaic.jsx';
import { t } from '../lib/i18n.js';
import { TranslatedText } from '../components/TranslatedText.jsx';

// Asked for per render so it follows the chosen language (format.js LOCALE).
const TIME = () => dateFormat({ hour: '2-digit', minute: '2-digit' });

const CATEGORIES = [
  ['', 'Tất cả'], ['chef', 'Đầu bếp'], ['photo', 'Chụp ảnh'], ['massage', 'Massage'],
  ['transfer', 'Đưa đón'], ['luggage', 'Giữ hành lý'], ['groceries', 'Đi chợ hộ']
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
                <div className="card-sub card-line">{s.city} · {t(s.pricingLabel)}</div>
                <div className="card-price">
                  <b>{money(s.basePrice)}</b> <span>/ {t(s.unit)}</span>
                </div>
                <div className="card-perks card-line">
                  {s.travelsToGuest ? `${t('Tới tận nơi trong')} ${s.serviceRadiusKm} km` : t('Khách tới chỗ cung cấp')}
                  {s.isPartner ? ` · ${t('qua')} ${s.partnerName}` : ''}
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

/** Two-hour steps inside the provider's working day, for the next fortnight. */
function slotsFor(detail) {
  const out = [];
  const now = new Date();

  for (let day = 0; day < 14 && out.length < 60; day++) {
    for (let hour = detail.opensAtHour; hour < detail.closesAtHour; hour += 2) {
      const at = new Date(now);
      at.setDate(at.getDate() + day);
      at.setHours(hour, 0, 0, 0);
      if (at <= now) continue;
      // A day the provider does not work at all never becomes a slot to click.
      if (!worksOn(detail, at)) continue;

      const ends = new Date(at.getTime() + detail.durationMinutes * 60000);
      if (ends.getHours() > detail.closesAtHour && ends.getHours() !== 0) continue;

      const taken = detail.busy.some(b => at < new Date(b.to) && new Date(b.from) < ends);
      out.push({ at, taken });
    }
  }
  return out;
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

  const slots = slotsFor(s);
  const addOns = s.addOns ?? [];
  // docs/09 §3.3 (MR-S-07) — a service with conditions cannot be booked until the
  // guest has said the place meets them; that answer is what makes §3.6's "khai
  // sai điều kiện" rule fair, so it is a tick of its own, not fine print.
  const requirements = s.onSiteRequirements ?? [];
  const conditionsPending = requirements.length > 0 && !conditionsOk;

  return (
    <div className="shell" style={{ paddingBlock: '26px 90px' }}>
      <button className="back-link" onClick={() => navigate('/services')}>← {t('Dịch vụ')}</button>

      <h1 className="section-title" style={{ marginTop: 10 }}>
        <TranslatedText as="span" text={s.title} />
      </h1>
      <p className="section-sub">
        {s.city} · {t(s.pricingLabel)} · {s.durationMinutes} {t('phút')}
        {s.isPartner ? ` · ${t('do')} ${s.partnerName} ${t('thực hiện')}` : ''}
      </p>

      {!!s.images.length && <PhotoMosaic images={s.images} alt={s.title} />}

      <div className="trip-layout" style={{ marginTop: 24 }}>
        <div style={{ minWidth: 0 }}>
          <section className="detail-section" style={{ paddingTop: 0 }}>
            <h2>{t('Dịch vụ này gồm gì')}</h2>
            <TranslatedText as="p" style={{ fontSize: 15.5, lineHeight: 1.75, color: 'var(--ink-body)' }} text={s.description} />
          </section>

          <section className="detail-section">
            <h2>{t('Cần biết')}</h2>
            <div className="kv-grid">
              <Kv label={t('Phạm vi phục vụ')}
                  value={s.travelsToGuest ? `${t('Tới tận nơi trong')} ${s.serviceRadiusKm} km` : t('Khách tới chỗ cung cấp')} />
              {/* docs/09 §3.3 (MR-S-04) — past the free radius they still come, for a fee. */}
              {s.travelsToGuest && s.travelFeePerKm > 0 && (
                <Kv label={t('Đi xa hơn')}
                    value={`${t('Thêm')} ${money(s.travelFeePerKm)}/km, ${t('tối đa thêm')} ${s.maxTravelKm} km`} />
              )}
              <Kv label={t('Giờ nhận')} value={`${s.opensAtHour}:00 – ${s.closesAtHour}:00`} />
              <Kv label={t('Nhận từ')} value={`${s.minQuantity} ${t('đến')} ${s.maxQuantity} ${t(s.unit)}`} />
              <Kv label={t('Đặt trước')} value={t('Ít nhất 4 giờ')} />
            </div>
            <p style={{ fontSize: 13.5, color: 'var(--ink-muted)', marginTop: 14, lineHeight: 1.6 }}>
              {t('Huỷ trước 24 giờ được hoàn toàn bộ. Sau đó thì không hoàn.')}
            </p>
          </section>
        </div>

        <aside className="book-panel">
          <div className="book-price">
            <span className="amount">{money(s.basePrice)}</span>
            <span className="per">/ {t(s.unit)}</span>
          </div>

          <p className="cap" style={{ margin: '14px 0 8px' }}>{t('Chọn khung giờ')}</p>
          <div className="xp-slots">
            {slots.slice(0, 14).map(({ at, taken }) => (
              <button key={at.toISOString()} disabled={taken}
                      className={`xp-slot ${when && +when === +at ? 'is-on' : ''}`}
                      onClick={() => setWhen(at)}>
                <b>{at.getDate()}/{at.getMonth() + 1}</b>
                <span>{TIME().format(at)}</span>
                <i>{taken ? t('đã kín') : t('còn trống')}</i>
              </button>
            ))}
          </div>

          {s.pricing !== 'PerSession' && (
            <label className="form-field" style={{ marginTop: 14 }}>
              <span className="cap">{t('Số')} {t(s.unit)}</span>
              <input type="number" min={s.minQuantity} max={s.maxQuantity} value={quantity}
                     onChange={e => setQuantity(Math.max(s.minQuantity,
                       Math.min(s.maxQuantity, Number(e.target.value) || s.minQuantity)))} />
            </label>
          )}

          {/* docs/09 §3.3 (MR-S-03) — each extra is priced on its own and shows up
              as its own line on the quote, so nothing grows quietly. */}
          {!!addOns.length && (
            <div style={{ marginTop: 14 }}>
              <span className="cap">{t('Tuỳ chọn thêm')}</span>
              <div style={{ display: 'grid', gap: 8, marginTop: 8 }}>
                {addOns.map(a => (
                  <label className="check-row" key={a.id}>
                    <input type="checkbox" checked={addOnIds.includes(a.id)}
                           onChange={e => setAddOnIds(ids => e.target.checked
                             ? [...ids, a.id]
                             : ids.filter(x => x !== a.id))} />
                    {/* The provider named these add-ons themselves. */}
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
            <label className="form-field">
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

          <label className="form-field">
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
            <div className="book-lines" style={{ marginTop: 12 }}>
              {quote.lines.map(l => (
                <div className="book-line" key={l.key}><span>{t(l.label)}</span><b>{money(l.amount)}</b></div>
              ))}
              <div className="book-line is-total"><span>{t('Tổng')}</span><b>{money(quote.total)}</b></div>
            </div>

            {!quote.canBook && <div className="book-alert is-error"><b>{t('Chưa đặt được')}</b><span>{quote.reason}</span></div>}

            <button className="btn btn-primary" style={{ width: '100%', marginTop: 12 }}
                    disabled={busy || !quote.canBook || conditionsPending || (s.requiredNote && !note.trim())}
                    onClick={book}>
              {busy ? t('Đang xử lý…') : t('Đặt dịch vụ')}
            </button>
          </>}

          {!when && <p className="section-sub" style={{ marginTop: 12 }}>{t('Chọn một khung giờ để xem giá.')}</p>}
        </aside>
      </div>
    </div>
  );
}

function Kv({ label, value }) {
  return <div className="kv"><span className="kv-label">{label}</span><b>{value}</b></div>;
}

export function ServiceBookings() {
  const state = useStore();
  const navigate = useNavigate();
  const [rows, setRows] = useState(null);

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
                  <h3>{r.title}</h3>
                  <div className="meta">
                    {longDate(r.startsAt.slice(0, 10))} · {TIME().format(new Date(r.startsAt))} ·
                    {' '}{r.quantity} {r.unit} · {money(r.total)}
                  </div>
                  <div className="meta">{t('Mã')} {r.reference}{r.address ? ` · ${r.address}` : ''}</div>
                  {r.note && <div className="meta">{t('Ghi chú')}: {r.note}</div>}
                  {r.cancelReason && <div className="meta">{r.cancelReason}</div>}
                  {r.refundedAmount > 0 && <div className="meta">{t('Đã hoàn')} {money(r.refundedAmount)}</div>}
                  <span className={`badge ${r.statusBadge}`} style={{ marginTop: 8 }}>{r.statusLabel}</span>
                </div>
                <div className="host-booking-actions">
                  <button className="btn btn-outline btn-sm"
                          onClick={() => navigate(`/services/${r.slug}`)}>{t('Xem dịch vụ')}</button>
                  {(r.status === 'Confirmed' || r.status === 'Requested') &&
                    <button className="btn btn-outline btn-sm" onClick={() => cancel(r)}>{t('Huỷ')}</button>}
                </div>
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

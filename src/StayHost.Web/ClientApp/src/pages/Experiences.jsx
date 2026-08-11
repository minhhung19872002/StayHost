import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, toast } from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate, dateFormat, number } from '../lib/format.js';
import { CardCarousel } from '../components/CardCarousel.jsx';
import { PhotoMosaic } from '../components/PhotoMosaic.jsx';
import { Avatar } from '../components/Avatar.jsx';
import { t } from '../lib/i18n.js';
import { TranslatedText } from '../components/TranslatedText.jsx';

/**
 * docs/09 §2.10 (MR-E-11) — an experience is judged on four things of its own.
 * Deliberately not the stay's six: there is no cleanliness, no check-in and no
 * location here, and the order is the one the spec writes.
 */
const XP_CRITERIA = [
  ['host', 'Người dẫn'],
  ['asDescribed', 'Đúng như mô tả'],
  ['safety', 'Tổ chức và an toàn'],
  ['value', 'Đáng giá tiền']
];

/** Two letters for somebody with no photo, the way the server builds them. */
const initialsOf = name => (name || '?')
  .trim().split(/\s+/).slice(-2).map(w => w[0] ?? '').join('').toUpperCase() || '?';

// Dates follow the language the guest picked (see format.js LOCALE), so these
// are asked for per render rather than frozen at import.
const TIME = () => dateFormat({ hour: '2-digit', minute: '2-digit' });
const DAY = () => dateFormat({ weekday: 'short', day: '2-digit', month: '2-digit' });

// Exported so the trip page's cross-sell cards (docs/09 §4) read a session's
// length exactly the way the experience cards here do.
export const duration = minutes =>
  minutes >= 60
    ? `${Math.floor(minutes / 60)} ${t('giờ')}${minutes % 60 ? ` ${minutes % 60} ${t('phút')}` : ''}`
    : `${minutes} ${t('phút')}`;

/** docs/01 MR-01 → MR-04 — things a local runs, sold by the seat. */
export function Experiences() {
  const { slug } = useParams();
  return slug ? <Detail slug={slug} /> : <Browse />;
}

function Browse() {
  const navigate = useNavigate();
  const [q, setQ] = useState('');
  const [items, setItems] = useState(null);

  useEffect(() => {
    let live = true;
    const timer = setTimeout(() => {
      api.experiences({ q: q.trim() || undefined })
        .then(r => { if (live) setItems(r); })
        .catch(e => toast(e.message));
    }, 180);
    return () => { live = false; clearTimeout(timer); };
  }, [q]);

  return (
    <div className="shell" style={{ paddingBlock: '30px 90px' }}>
      <h1 className="section-title">{t('Trải nghiệm')}</h1>
      <p className="section-sub">{t('Hoạt động do người địa phương dẫn, tính theo vé chứ không theo đêm.')}</p>

      <div className="help-search" style={{ maxWidth: 520 }}>
        <input value={q} onChange={e => setQ(e.target.value)} autoComplete="off"
               placeholder={t('Nấu ăn, chèo SUP, cà phê…')} />
      </div>

      {!items ? (
        <div className="card-grid" style={{ marginTop: 24 }}>
          {Array.from({ length: 4 }, (_, i) => (
            <div className="card skeleton" key={i} style={{ height: 300, border: 0 }} />
          ))}
        </div>
      ) : items.length ? (
        <div className="card-grid" style={{ marginTop: 24 }}>
          {items.map(x => (
            <button className="card" key={x.id} onClick={() => navigate(`/experiences/${x.slug}`)}
                    style={{ textAlign: 'left', border: 0, background: 'none', padding: 0, cursor: 'pointer' }}>
              <CardCarousel images={x.images} alt={x.title} />
              <div className="card-body">
                <div className="card-row">
                  <h3 className="card-title"><TranslatedText as="span" text={x.title} notice={false} /></h3>
                  <div className="card-rating">
                    {x.reviewCount ? `★ ${x.rating.toFixed(2)} (${x.reviewCount})` : `★ ${t('Mới')}`}
                  </div>
                </div>
                <div className="card-sub card-line">{x.city} · {duration(x.durationMinutes)} · {t('tối đa')} {x.maxGroup} {t('người')}</div>
                <div className="card-price">
                  <b>{money(x.pricePerPerson)}</b> <span>/ {t('người')}</span>
                </div>
                <div className="card-perks card-line">
                  {x.openSlots ? `${t('Còn')} ${x.openSlots} ${t('suất')}` : t('Tạm hết suất')} · {x.hostName}
                </div>
              </div>
            </button>
          ))}
        </div>
      ) : (
        <div className="empty-state" style={{ marginTop: 28 }}>
          <h3>{t('Chưa có trải nghiệm nào khớp')}</h3>
          <p>{t('Thử một từ khoá khác.')}</p>
        </div>
      )}
    </div>
  );
}

function Detail({ slug }) {
  const state = useStore();
  const navigate = useNavigate();
  const [x, setX] = useState(null);
  const [missing, setMissing] = useState(false);
  const [slotId, setSlotId] = useState(null);
  const [seats, setSeats] = useState(2);
  const [priv, setPriv] = useState(false);
  const [quote, setQuote] = useState(null);
  const [busy, setBusy] = useState(false);

  const load = () => api.experience(slug).then(d => {
    setX(d);
    setSlotId(id => id ?? d.slots.find(s => s.status === 'Open' && s.seatsLeft > 0)?.id ?? null);
  }).catch(() => setMissing(true));

  useEffect(() => { load(); }, [slug]);

  useEffect(() => {
    if (!slotId) { setQuote(null); return; }
    api.experienceQuote(slotId, seats, priv).then(setQuote).catch(e => toast(e.message));
  }, [slotId, seats, priv]);

  if (missing) {
    return <div className="shell" style={{ paddingBlock: '40px 90px' }}>
      <div className="empty-state"><h3>{t('Không tìm thấy trải nghiệm này')}</h3>
        <button className="btn btn-primary" style={{ marginTop: 18 }}
                onClick={() => navigate('/experiences')}>{t('Xem tất cả')}</button></div>
    </div>;
  }

  if (!x) return <div className="shell" style={{ paddingBlock: '40px 90px' }}>
    <div className="stat skeleton" style={{ height: 320, border: 0 }} /></div>;

  const book = async () => {
    if (!state.user) { set({ authMode: 'login', authError: null, overlay: 'login' }); return; }
    setBusy(true);
    try {
      const b = await api.bookExperience(slotId, { seats, private: priv, paymentMethod: 'card', cardLast4: '4242' });
      toast(`${t('Đã đặt — mã')} ${b.reference}`);
      await load();
      navigate('/experiences/bookings');
    } catch (err) { toast(err.message); } finally { setBusy(false); }
  };

  const open = x.slots.filter(s => s.status === 'Open');

  return (
    <div className="shell" style={{ paddingBlock: '26px 90px' }}>
      <button className="back-link" onClick={() => navigate('/experiences')}>← {t('Trải nghiệm')}</button>

      <h1 className="section-title" style={{ marginTop: 10 }}>
        <TranslatedText as="span" text={x.title} />
      </h1>
      <p className="section-sub">
        {x.city} · {duration(x.durationMinutes)} · {t('tối đa')} {x.maxGroup} {t('người')}
        {x.reviewCount ? ` · ★ ${x.rating.toFixed(2)} (${x.reviewCount})` : ''}
      </p>

      {!!x.images.length && <PhotoMosaic images={x.images} alt={x.title} />}

      <div className="trip-layout" style={{ marginTop: 24 }}>
        <div style={{ minWidth: 0 }}>
          <section className="detail-section" style={{ paddingTop: 0 }}>
            <h2>{t('Buổi này có gì')}</h2>
            <TranslatedText as="p" style={{ fontSize: 15.5, lineHeight: 1.75, color: 'var(--ink-body)' }} text={x.description} />
          </section>

          <section className="detail-section">
            <h2>{t('Vé bao gồm')}</h2>
            <ul style={{ margin: 0, paddingLeft: 22, lineHeight: 1.9, color: 'var(--ink-body)' }}>
              {/* What the host typed, not something the platform ships — so it goes
                  the machine-translation way, like the house rules on a stay. */}
              {x.included.map((i, k) => <li key={k}><TranslatedText as="span" text={i} /></li>)}
            </ul>
          </section>

          <section className="detail-section">
            <h2>{t('Cần biết')}</h2>
            <div className="kv-grid">
              {/* An address the host wrote. notice={false} because a "translated
                  automatically" line inside a two-line key/value cell wrecks the
                  grid; the description above already says it for the page. */}
              <Kv label={t('Điểm hẹn')}
                  value={<TranslatedText as="span" text={x.meetingPoint} notice={false} />} />
              <Kv label={t('Ngôn ngữ')} value={x.languages.join(', ')} />
              <Kv label={t('Độ tuổi')} value={x.minAge ? `${t('Từ')} ${x.minAge} ${t('tuổi')}` : t('Mọi lứa tuổi')} />
              <Kv label={t('Tối thiểu')} value={`${x.minGuests} ${t('người thì suất mới chạy')}`} />
            </div>
            <p style={{ fontSize: 13.5, color: 'var(--ink-muted)', marginTop: 14, lineHeight: 1.6 }}>
              {t('Suất không đủ')} {x.minGuests} {t('người trước 48 giờ sẽ bị huỷ và hoàn tiền toàn bộ. Bạn huỷ trước 24 giờ cũng được hoàn đủ.')}
            </p>
          </section>

          <ExperienceReviews experience={x} />
        </div>

        <aside className="book-panel">
          <div className="book-price">
            <span className="amount">{money(x.pricePerPerson)}</span>
            <span className="per">/ {t('người')}</span>
          </div>

          <p className="cap" style={{ margin: '14px 0 8px' }}>{t('Chọn suất')}</p>
          <div className="xp-slots">
            {open.length ? open.slice(0, 12).map(s => (
              <button key={s.id} className={`xp-slot ${slotId === s.id ? 'is-on' : ''}`}
                      disabled={s.seatsLeft === 0}
                      onClick={() => setSlotId(s.id)}>
                <b>{DAY().format(new Date(s.startsAt))}</b>
                <span>{TIME().format(new Date(s.startsAt))}</span>
                <i>{s.seatsLeft ? `${t('còn')} ${s.seatsLeft}` : t('hết chỗ')}</i>
              </button>
            )) : <p className="section-sub">{t('Chưa có suất nào mở.')}</p>}
          </div>

          <label className="form-field" style={{ marginTop: 14 }}>
            <span className="cap">{t('Số người')}</span>
            <input type="number" min={1} max={x.maxGroup} value={seats}
                   onChange={e => setSeats(Math.max(1, Math.min(x.maxGroup, Number(e.target.value) || 1)))} />
          </label>

          {x.privateGroupPrice != null && (
            <button type="button" className={`opt ${priv ? 'is-on' : ''}`} style={{ marginTop: 10 }}
                    onClick={() => setPriv(p => !p)}>
              <b>{t('Thuê trọn nhóm riêng')} — {money(x.privateGroupPrice)}</b>
              <span>{t('Chỉ nhóm bạn, không ghép với khách khác')}</span>
            </button>
          )}

          {quote && <>
            <div className="book-lines" style={{ marginTop: 16 }}>
              {quote.lines.map(l => (
                <div className="book-line" key={l.key}><span>{t(l.label)}</span><b>{money(l.amount)}</b></div>
              ))}
              <div className="book-line is-total"><span>{t('Tổng')}</span><b>{money(quote.total)}</b></div>
            </div>

            {!quote.canBook && <div className="book-alert is-error"><b>{t('Chưa đặt được')}</b><span>{quote.reason}</span></div>}

            <button className="btn btn-primary" style={{ width: '100%', marginTop: 14 }}
                    disabled={busy || !quote.canBook} onClick={book}>
              {busy ? t('Đang xử lý…') : t('Đặt trải nghiệm')}
            </button>
          </>}
        </aside>
      </div>
    </div>
  );
}

function Kv({ label, value }) {
  return <div className="kv"><span className="kv-label">{label}</span><b>{value}</b></div>;
}

/**
 * docs/09 §2.10 (MR-E-11) — what people who were actually there wrote. Laid out
 * like the stay's review block so the page reads as one site, but scored on the
 * four headings an experience has rather than the six a stay has.
 */
function ExperienceReviews({ experience }) {
  const [rows, setRows] = useState(null);

  useEffect(() => {
    api.experienceReviews(experience.id).then(setRows).catch(err => toast(err.message));
  }, [experience.id]);

  if (!rows) return null;

  if (!rows.length) {
    return (
      <section className="detail-section">
        <h2>{t('Đánh giá')}</h2>
        <p className="section-sub">{t('Buổi này chưa có đánh giá nào. Chỉ người có mặt mới viết được.')}</p>
      </section>
    );
  }

  const average = key => rows.reduce((sum, r) => sum + r[key], 0) / rows.length;

  return (
    <section className="detail-section">
      <h2>{t('Đánh giá')}</h2>

      <div className="rating-summary">
        <span className="rating-big">★ {experience.rating.toFixed(2)}</span>
        <span style={{ fontSize: 15, color: 'var(--ink-muted)' }}>
          · {experience.reviewCount} {t('đánh giá')}
        </span>
      </div>

      <div className="rating-bars">
        {XP_CRITERIA.map(([key, label]) => (
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
              {XP_CRITERIA.map(([key, label]) => (
                <span className="topic-chip" key={key}>{t(label)} <b>★ {r[key]}</b></span>
              ))}
            </div>
            {!!r.comment && <p>{r.comment}</p>}
          </article>
        ))}
      </div>
    </section>
  );
}

/**
 * docs/09 §2.9 — a session is over once it has started and run its length. The
 * server has the last word on who may review (only somebody the host marked
 * present), so this is only about not offering a form that cannot possibly work.
 */
const sessionEnded = r =>
  new Date(r.startsAt).getTime() + r.durationMinutes * 60000 <= Date.now();

/**
 * docs/09 §2.10 — "chỉ người có mặt mới đánh giá được". The ticket now carries
 * the host's mark and whether a review already exists, so a no-show is never
 * offered a form the server would only refuse, and nobody is asked twice.
 */
const canReviewTicket = r =>
  sessionEnded(r) && r.attended === true && !r.hasReview
  && (r.status === 'Confirmed' || r.status === 'Completed');

/**
 * docs/09 §2.10 (MR-E-11) — four criteria of the experience's own and an
 * optional word. Nothing from the stay's form belongs here.
 */
function TicketReview({ booking, onDone }) {
  const [scores, setScores] = useState({ host: 5, asDescribed: 5, safety: 5, value: 5 });
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
      await api.reviewExperienceBooking(booking.id, { ...scores, comment });
      toast(t('Đã gửi đánh giá. Cảm ơn bạn.'));
      onDone();
    } catch (err) {
      // "Chỉ người có mặt trong buổi mới đánh giá được", "Bạn đã đánh giá buổi
      // này rồi" — the server's sentence, said as it is.
      toast(err.message);
    } finally { setBusy(false); }
  };

  return (
    <form onSubmit={submit}
          style={{ flexBasis: '100%', minWidth: 0, marginTop: 14, paddingTop: 14, borderTop: '1px solid var(--divider)' }}>
      <b style={{ fontSize: 15 }}>{t('Buổi này thế nào?')}</b>
      {XP_CRITERIA.map(([key, label]) => (
        <div className="count-row" key={key}>
          <div className="tx"><b>{t(label)}</b></div>
          {stars(key)}
        </div>
      ))}

      <label className="form-field" style={{ marginTop: 14 }}>
        <span className="cap">{t('Nhận xét')} <span style={{ fontWeight: 400 }}>{t('(không bắt buộc)')}</span></span>
        <textarea name="comment" rows={4}
                  placeholder={t('Người dẫn kể chuyện thế nào? Bạn có thấy an toàn không?')}
                  style={{ width: '100%', padding: '12px 14px', border: '1px solid var(--line)', borderRadius: 12, fontSize: 14 }} />
      </label>

      <button type="submit" className="btn btn-primary btn-sm" disabled={busy}>
        {busy ? t('Đang gửi…') : t('Gửi đánh giá')}
      </button>
    </form>
  );
}

/** The tickets someone holds, with the one action they have: giving one back. */
export function ExperienceBookings() {
  const state = useStore();
  const navigate = useNavigate();
  const [rows, setRows] = useState(null);
  // Which ticket has its review form open, and which ones this visit has sent.
  const [reviewing, setReviewing] = useState(null);
  const [sent, setSent] = useState([]);

  const load = () => api.experienceBookings().then(setRows).catch(e => toast(e.message));
  useEffect(() => { if (state.user) load(); }, [state.user]);

  if (!state.user) {
    return <div className="shell" style={{ paddingBlock: '60px 90px' }}>
      <div className="empty-state"><h3>{t('Đăng nhập để xem vé của bạn')}</h3>
        <button className="btn btn-primary" style={{ marginTop: 18 }}
                onClick={() => set({ authMode: 'login', authError: null, overlay: 'login' })}>{t('Đăng nhập')}</button>
      </div></div>;
  }

  const cancel = async row => {
    if (!confirm(`${t('Huỷ vé')} ${row.reference}?`)) return;
    try {
      const after = await api.cancelExperienceBooking(row.id);
      toast(after.refundedAmount > 0
        ? `${t('Đã huỷ, hoàn')} ${number(after.refundedAmount)}₫.`
        : t('Đã huỷ. Huỷ sát giờ nên không hoàn tiền.'));
      load();
    } catch (err) { toast(err.message); }
  };

  return (
    <div className="shell" style={{ paddingBlock: '30px 90px' }}>
      <h1 className="section-title">{t('Vé trải nghiệm')}</h1>

      {!rows ? <div className="stat skeleton" style={{ height: 200, border: 0, marginTop: 24 }} />
        : rows.length ? (
          <div style={{ marginTop: 20, display: 'grid', gap: 12 }}>
            {rows.map(r => (
              <article className="host-booking" key={r.id}>
                <div style={{ minWidth: 0 }}>
                  {/* The host named the experience — machine translation, not the dictionary. */}
                  <h3><TranslatedText as="span" text={r.title} notice={false} /></h3>
                  <div className="meta">
                    {longDate(r.startsAt.slice(0, 10))} · {TIME().format(new Date(r.startsAt))} ·
                    {' '}{r.seats} {t('chỗ')}{r.private ? ` · ${t('nhóm riêng')}` : ''} · {money(r.total)}
                  </div>
                  <div className="meta">{t('Mã')} {r.reference} · {r.city}</div>
                  {r.cancelReason && <div className="meta">{t(r.cancelReason)}</div>}
                  {r.refundedAmount > 0 && <div className="meta">{t('Đã hoàn')} {money(r.refundedAmount)}</div>}
                  <span className={`badge ${r.statusBadge}`} style={{ marginTop: 8 }}>{t(r.statusLabel)}</span>
                </div>
                <div className="host-booking-actions">
                  <button className="btn btn-outline btn-sm"
                          onClick={() => navigate(`/experiences/${r.slug}`)}>{t('Xem trải nghiệm')}</button>
                  {r.status === 'Confirmed' &&
                    <button className="btn btn-outline btn-sm" onClick={() => cancel(r)}>{t('Huỷ vé')}</button>}
                  {/* docs/09 §2.10 — only somebody the host marked present, and
                      only once. Both facts come off the ticket now. */}
                  {r.hasReview || sent.includes(r.id)
                    ? (sessionEnded(r) && <span className="badge confirmed">{t('Đã đánh giá')}</span>)
                    : canReviewTicket(r) && (
                        <button className="btn btn-outline btn-sm"
                                onClick={() => setReviewing(id => id === r.id ? null : r.id)}>
                          {reviewing === r.id ? t('Đóng') : t('Đánh giá buổi này')}
                        </button>
                      )}
                  {/* A no-show is told why rather than left wondering. */}
                  {sessionEnded(r) && r.attended === false && (
                    <span className="badge cancelled">{t('Vắng mặt')}</span>
                  )}
                </div>
                {reviewing === r.id && !sent.includes(r.id) && (
                  <TicketReview booking={r}
                                onDone={() => { setSent(ids => [...ids, r.id]); setReviewing(null); load(); }} />
                )}
              </article>
            ))}
          </div>
        ) : (
          <div className="empty-state" style={{ marginTop: 24 }}>
            <h3>{t('Chưa có vé nào')}</h3>
            <button className="btn btn-primary" style={{ marginTop: 18 }}
                    onClick={() => navigate('/experiences')}>{t('Xem trải nghiệm')}</button>
          </div>
        )}
    </div>
  );
}

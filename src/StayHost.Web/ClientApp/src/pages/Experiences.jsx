import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, toast } from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate } from '../lib/format.js';
import { CardCarousel } from '../components/CardCarousel.jsx';

const TIME = new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit' });
const DAY = new Intl.DateTimeFormat('vi-VN', { weekday: 'short', day: '2-digit', month: '2-digit' });

const duration = minutes =>
  minutes >= 60
    ? `${Math.floor(minutes / 60)} giờ${minutes % 60 ? ` ${minutes % 60} phút` : ''}`
    : `${minutes} phút`;

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
      <h1 className="section-title">Trải nghiệm</h1>
      <p className="section-sub">Hoạt động do người địa phương dẫn, tính theo vé chứ không theo đêm.</p>

      <div className="help-search" style={{ maxWidth: 520 }}>
        <input value={q} onChange={e => setQ(e.target.value)} autoComplete="off"
               placeholder="Nấu ăn, chèo SUP, cà phê…" />
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
                  <h3 className="card-title">{x.title}</h3>
                  <div className="card-rating">
                    {x.reviewCount ? `★ ${x.rating.toFixed(2)} (${x.reviewCount})` : '★ Mới'}
                  </div>
                </div>
                <div className="card-sub card-line">{x.city} · {duration(x.durationMinutes)} · tối đa {x.maxGroup} người</div>
                <div className="card-price">
                  <b>{money(x.pricePerPerson)}</b> <span>/ người</span>
                </div>
                <div className="card-perks card-line">
                  {x.openSlots ? `Còn ${x.openSlots} suất` : 'Tạm hết suất'} · {x.hostName}
                </div>
              </div>
            </button>
          ))}
        </div>
      ) : (
        <div className="empty-state" style={{ marginTop: 28 }}>
          <h3>Chưa có trải nghiệm nào khớp</h3>
          <p>Thử một từ khoá khác.</p>
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
      <div className="empty-state"><h3>Không tìm thấy trải nghiệm này</h3>
        <button className="btn btn-primary" style={{ marginTop: 18 }}
                onClick={() => navigate('/experiences')}>Xem tất cả</button></div>
    </div>;
  }

  if (!x) return <div className="shell" style={{ paddingBlock: '40px 90px' }}>
    <div className="stat skeleton" style={{ height: 320, border: 0 }} /></div>;

  const book = async () => {
    if (!state.user) { set({ authMode: 'login', authError: null, overlay: 'login' }); return; }
    setBusy(true);
    try {
      const b = await api.bookExperience(slotId, { seats, private: priv, paymentMethod: 'card', cardLast4: '4242' });
      toast(`Đã đặt — mã ${b.reference}`);
      await load();
      navigate('/experiences/bookings');
    } catch (err) { toast(err.message); } finally { setBusy(false); }
  };

  const open = x.slots.filter(s => s.status === 'Open');

  return (
    <div className="shell" style={{ paddingBlock: '26px 90px' }}>
      <button className="back-link" onClick={() => navigate('/experiences')}>← Trải nghiệm</button>

      <h1 className="section-title" style={{ marginTop: 10 }}>{x.title}</h1>
      <p className="section-sub">
        {x.city} · {duration(x.durationMinutes)} · tối đa {x.maxGroup} người
        {x.reviewCount ? ` · ★ ${x.rating.toFixed(2)} (${x.reviewCount})` : ''}
      </p>

      {!!x.images.length && (
        <div className="xp-gallery">
          {x.images.slice(0, 3).map((u, i) => <img src={u} alt="" key={i} loading="lazy" />)}
        </div>
      )}

      <div className="trip-layout" style={{ marginTop: 24 }}>
        <div style={{ minWidth: 0 }}>
          <section className="detail-section" style={{ paddingTop: 0 }}>
            <h2>Buổi này có gì</h2>
            <p style={{ fontSize: 15.5, lineHeight: 1.75, color: 'var(--ink-body)' }}>{x.description}</p>
          </section>

          <section className="detail-section">
            <h2>Vé bao gồm</h2>
            <ul style={{ margin: 0, paddingLeft: 22, lineHeight: 1.9, color: 'var(--ink-body)' }}>
              {x.included.map((i, k) => <li key={k}>{i}</li>)}
            </ul>
          </section>

          <section className="detail-section">
            <h2>Cần biết</h2>
            <div className="kv-grid">
              <Kv label="Điểm hẹn" value={x.meetingPoint} />
              <Kv label="Ngôn ngữ" value={x.languages.join(', ')} />
              <Kv label="Độ tuổi" value={x.minAge ? `Từ ${x.minAge} tuổi` : 'Mọi lứa tuổi'} />
              <Kv label="Tối thiểu" value={`${x.minGuests} người thì suất mới chạy`} />
            </div>
            <p style={{ fontSize: 13.5, color: 'var(--ink-muted)', marginTop: 14, lineHeight: 1.6 }}>
              Suất không đủ {x.minGuests} người trước 48 giờ sẽ bị huỷ và hoàn tiền toàn bộ.
              Bạn huỷ trước 24 giờ cũng được hoàn đủ.
            </p>
          </section>
        </div>

        <aside className="book-panel">
          <div className="book-price">
            <span className="amount">{money(x.pricePerPerson)}</span>
            <span className="per">/ người</span>
          </div>

          <p className="cap" style={{ margin: '14px 0 8px' }}>Chọn suất</p>
          <div className="xp-slots">
            {open.length ? open.slice(0, 12).map(s => (
              <button key={s.id} className={`xp-slot ${slotId === s.id ? 'is-on' : ''}`}
                      disabled={s.seatsLeft === 0}
                      onClick={() => setSlotId(s.id)}>
                <b>{DAY.format(new Date(s.startsAt))}</b>
                <span>{TIME.format(new Date(s.startsAt))}</span>
                <i>{s.seatsLeft ? `còn ${s.seatsLeft}` : 'hết chỗ'}</i>
              </button>
            )) : <p className="section-sub">Chưa có suất nào mở.</p>}
          </div>

          <label className="form-field" style={{ marginTop: 14 }}>
            <span className="cap">Số người</span>
            <input type="number" min={1} max={x.maxGroup} value={seats}
                   onChange={e => setSeats(Math.max(1, Math.min(x.maxGroup, Number(e.target.value) || 1)))} />
          </label>

          {x.privateGroupPrice != null && (
            <button type="button" className={`opt ${priv ? 'is-on' : ''}`} style={{ marginTop: 10 }}
                    onClick={() => setPriv(p => !p)}>
              <b>Thuê trọn nhóm riêng — {money(x.privateGroupPrice)}</b>
              <span>Chỉ nhóm bạn, không ghép với khách khác</span>
            </button>
          )}

          {quote && <>
            <div className="book-lines" style={{ marginTop: 16 }}>
              {quote.lines.map(l => (
                <div className="book-line" key={l.key}><span>{l.label}</span><b>{money(l.amount)}</b></div>
              ))}
              <div className="book-line is-total"><span>Tổng</span><b>{money(quote.total)}</b></div>
            </div>

            {!quote.canBook && <div className="book-alert is-error"><b>Chưa đặt được</b><span>{quote.reason}</span></div>}

            <button className="btn btn-primary" style={{ width: '100%', marginTop: 14 }}
                    disabled={busy || !quote.canBook} onClick={book}>
              {busy ? 'Đang xử lý…' : 'Đặt trải nghiệm'}
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

/** The tickets someone holds, with the one action they have: giving one back. */
export function ExperienceBookings() {
  const state = useStore();
  const navigate = useNavigate();
  const [rows, setRows] = useState(null);

  const load = () => api.experienceBookings().then(setRows).catch(e => toast(e.message));
  useEffect(() => { if (state.user) load(); }, [state.user]);

  if (!state.user) {
    return <div className="shell" style={{ paddingBlock: '60px 90px' }}>
      <div className="empty-state"><h3>Đăng nhập để xem vé của bạn</h3>
        <button className="btn btn-primary" style={{ marginTop: 18 }}
                onClick={() => set({ authMode: 'login', authError: null, overlay: 'login' })}>Đăng nhập</button>
      </div></div>;
  }

  const cancel = async row => {
    if (!confirm(`Huỷ vé ${row.reference}?`)) return;
    try {
      const after = await api.cancelExperienceBooking(row.id);
      toast(after.refundedAmount > 0
        ? `Đã huỷ, hoàn ${new Intl.NumberFormat('vi-VN').format(after.refundedAmount)}₫.`
        : 'Đã huỷ. Huỷ sát giờ nên không hoàn tiền.');
      load();
    } catch (err) { toast(err.message); }
  };

  return (
    <div className="shell" style={{ paddingBlock: '30px 90px' }}>
      <h1 className="section-title">Vé trải nghiệm</h1>

      {!rows ? <div className="stat skeleton" style={{ height: 200, border: 0, marginTop: 24 }} />
        : rows.length ? (
          <div style={{ marginTop: 20, display: 'grid', gap: 12 }}>
            {rows.map(r => (
              <article className="host-booking" key={r.id}>
                <div style={{ minWidth: 0 }}>
                  <h3>{r.title}</h3>
                  <div className="meta">
                    {longDate(r.startsAt.slice(0, 10))} · {TIME.format(new Date(r.startsAt))} ·
                    {' '}{r.seats} chỗ{r.private ? ' · nhóm riêng' : ''} · {money(r.total)}
                  </div>
                  <div className="meta">Mã {r.reference} · {r.city}</div>
                  {r.cancelReason && <div className="meta">{r.cancelReason}</div>}
                  {r.refundedAmount > 0 && <div className="meta">Đã hoàn {money(r.refundedAmount)}</div>}
                  <span className={`badge ${r.statusBadge}`} style={{ marginTop: 8 }}>{r.statusLabel}</span>
                </div>
                <div className="host-booking-actions">
                  <button className="btn btn-outline btn-sm"
                          onClick={() => navigate(`/experiences/${r.slug}`)}>Xem trải nghiệm</button>
                  {r.status === 'Confirmed' &&
                    <button className="btn btn-outline btn-sm" onClick={() => cancel(r)}>Huỷ vé</button>}
                </div>
              </article>
            ))}
          </div>
        ) : (
          <div className="empty-state" style={{ marginTop: 24 }}>
            <h3>Chưa có vé nào</h3>
            <button className="btn btn-primary" style={{ marginTop: 18 }}
                    onClick={() => navigate('/experiences')}>Xem trải nghiệm</button>
          </div>
        )}
    </div>
  );
}

import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import {
  set, loadHosting, loadHostCalendar, respondBooking, respondChange, requireAuth, toast,
  previewHostCancel
} from '../lib/store.js';
import { api } from '../lib/api.js';
import { money, longDate, todayIso, dateFormat, dateTime } from '../lib/format.js';
import { t } from '../lib/i18n.js';
import { Icon } from '../components/Icon.jsx';
import { Today } from './hosting/Today.jsx';
import { Payout, SuperhostProgress } from './hosting/Payout.jsx';
import { MultiCalendar } from './hosting/MultiCalendar.jsx';
import { Team } from './hosting/Team.jsx';

const TABS = [
  ['today', 'Hôm nay'], ['overview', 'Tổng quan'], ['listings', 'Chỗ nghỉ'],
  ['experiences', 'Trải nghiệm'],
  ['services', 'Dịch vụ'],
  ['calendar', 'Lịch'], ['bookings', 'Đơn đặt'], ['earnings', 'Doanh thu'],
  ['payout', 'Nhận tiền'], ['team', 'Đồng quản lý']
];

export function Hosting() {
  const state = useStore();
  const navigate = useNavigate();

  useEffect(() => { if (state.user) loadHosting(); }, [state.user]);

  /*
   * Arriving from the "Đăng nhà cho thuê" button on /host, which cannot open the
   * editor itself — App closes every overlay on a route change, so the intent
   * has to survive the navigation and be acted on once this page is here.
   *
   * Above the early returns below, because a hook that only sometimes runs is
   * not a hook.
   */
  const location = useLocation();
  useEffect(() => {
    if (!location.state?.newListing || !state.user) return;
    set({ editingListing: null, overlay: 'listing-editor' });
    navigate(location.pathname, { replace: true, state: null });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.state, state.user]);

  if (!state.user) {
    return (
      <div className="shell" style={{ paddingBlock: '60px 90px' }}>
        <div className="empty-state">
          <h3>{t('Đăng nhập để quản lý chỗ nghỉ')}</h3>
          <p>{t('Trang chủ nhà cho bạn xem đơn đặt, lịch và doanh thu.')}</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }}
                  onClick={() => set({ authMode: 'login', authError: null, overlay: 'login' })}>{t('Đăng nhập')}</button>
        </div>
      </div>
    );
  }

  const d = state.hosting;

  if (state.hostingLoading || !d) {
    return (
      <div className="shell" style={{ paddingBlock: '34px 90px' }}>
        <div className="sk-line skeleton" style={{ width: 260, height: 26 }} />
        <div className="stat-grid" style={{ marginTop: 24 }}>
          {Array.from({ length: 4 }, (_, i) => <div className="stat skeleton" key={i} style={{ height: 112, border: 0 }} />)}
        </div>
      </div>
    );
  }

  const newListing = () => {
    if (!requireAuth()) return;
    set({ editingListing: null, overlay: 'listing-editor' });
  };

  if (d.listingCount === 0) {
    return (
      <div className="shell" style={{ paddingBlock: '40px 90px' }}>
        <h1 className="section-title">{t('Trang chủ nhà')}</h1>
        <p className="section-sub">{t('Bạn chưa có chỗ nghỉ nào. Đăng chỗ đầu tiên để bắt đầu nhận đặt.')}</p>
        <div className="empty-state" style={{ marginTop: 22 }}>
          <h3>{t('Đăng chỗ nghỉ đầu tiên')}</h3>
          <p>{t('Mất khoảng 5 phút: mô tả không gian, thêm ảnh và đặt giá.')}</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }} onClick={newListing}>{t('+ Đăng chỗ nghỉ')}</button>
        </div>
      </div>
    );
  }

  const tab = state.hostingTab;

  return (
    <div className="shell" style={{ paddingBlock: '30px 90px' }}>
      <div className="page-head">
        <div>
          <h1 className="section-title">{t('Trang chủ nhà')}</h1>
          <p className="section-sub">
            {t('Xin chào')} {state.user.fullName} — {d.publishedCount}/{d.listingCount} {t('chỗ nghỉ đang hiển thị')}
          </p>
        </div>
        <button className="btn btn-primary btn-sm" onClick={newListing}>{t('+ Đăng chỗ nghỉ')}</button>
      </div>

      <nav className="seg-tabs" role="tablist">
        {TABS.map(([key, label]) => (
          <button role="tab" key={key} aria-selected={tab === key}
                  className={`seg-tab ${tab === key ? 'is-active' : ''}`}
                  onClick={() => set({ hostingTab: key })}>{t(label)}</button>
        ))}
      </nav>

      {tab === 'today' && <Today />}
      {tab === 'overview' && <Overview d={d} navigate={navigate} />}
      {tab === 'listings' && (
        <div className="host-listing-grid" style={{ marginTop: 24 }}>
          {d.listings.map(l => <ListingCard key={l.id} listing={l} navigate={navigate} />)}
        </div>
      )}
      {tab === 'experiences' && <HostExperiences />}
      {tab === 'services' && <HostServices />}
      {tab === 'calendar' && <MultiCalendar />}
      {tab === 'bookings' && <Bookings d={d} navigate={navigate} />}
      {tab === 'earnings' && <Earnings d={d} />}
      {tab === 'payout' && <><Payout /><SuperhostProgress /></>}
      {tab === 'team' && <Team />}
    </div>
  );
}

function Overview({ d, navigate }) {
  const cards = [
    [t('Chỗ nghỉ đang hiển thị'), `${d.publishedCount}/${d.listingCount}`, t('Bản nháp không hiện với khách')],
    [t('Lượt đặt sắp tới'), String(d.upcomingBookings), `${money(d.earningsUpcoming)} ${t('sẽ nhận')}`],
    [t('Đã nhận đến nay'), money(d.earningsToDate), t('Sau phí dịch vụ StayHost')],
    [t('Điểm đánh giá'), d.totalReviews ? `★ ${d.averageRating.toFixed(2)}` : t('Chưa có'), `${d.totalReviews} ${t('đánh giá')}`]
  ];

  const pending = d.bookings.filter(b => b.status === 'PendingHostApproval');

  return <>
    <div className="stat-grid" style={{ marginTop: 24 }}>
      {cards.map(([label, value, note]) => (
        <div className="stat" key={label}>
          <div className="value" style={{ fontSize: 'clamp(22px,2.6vw,28px)' }}>{value}</div>
          <div className="label">{label}</div>
          <div className="note">{note}</div>
        </div>
      ))}
    </div>

    {!!pending.length && (
      <section style={{ marginTop: 38 }}>
        <h2 className="section-title" style={{ fontSize: 20 }}>{t('Cần bạn xử lý')} ({pending.length})</h2>
        <p className="section-sub">{t('Khách đang chờ bạn xác nhận.')}</p>
        {pending.map(b => <BookingRow key={b.id} booking={b} navigate={navigate} />)}
      </section>
    )}

    <section style={{ marginTop: 38 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>{t('Chỗ nghỉ của bạn')}</h2>
      <div className="host-listing-grid" style={{ marginTop: 16 }}>
        {d.listings.slice(0, 4).map(l => <ListingCard key={l.id} listing={l} navigate={navigate} />)}
      </div>
    </section>
  </>;
}

function ListingCard({ listing: l, navigate }) {
  const [advice, setAdvice] = useState(null);
  const [busy, setBusy] = useState(false);

  const openCalendar = async () => {
    await loadHostCalendar(l.id);
    set({ overlay: 'host-block', hostMonthOffset: 0 });
  };

  // docs/01 QL-09 + QL-18 — pull the price suggestion and improvement checklist.
  const toggleAdvice = async () => {
    if (advice) { setAdvice(null); return; }
    try { setAdvice(await api.listingAdvice(l.id)); }
    catch (err) { toast(err.message); }
  };

  // docs/01 CN-15 — clone into a fresh draft.
  const duplicate = async () => {
    if (busy) return;
    setBusy(true);
    try { await api.duplicateListing(l.id); await loadHosting(); toast(t('Đã tạo bản sao ở dạng nháp.')); }
    catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  return (
    <article className="host-listing">
      <div className="host-listing-media">
        {l.images.length
          ? <img src={l.images[0]} alt={l.title} loading="lazy" decoding="async" />
          : <div className="skeleton" style={{ width: '100%', height: '100%' }} />}
        <span className={`badge ${l.isPublished ? 'confirmed' : 'pending'} host-listing-state`}>
          {l.isPublished ? t('Đang hiển thị') : t('Bản nháp')}
        </span>
      </div>
      <div className="host-listing-body">
        <h3>{l.title}</h3>
        {/* docs/01 AT-01 — a place still in review is not yet public. */}
        {l.reviewStatus === 'Pending' && (
          <div className="badge pending" style={{ marginBottom: 6 }}>{t('Đang chờ duyệt')}</div>
        )}
        {l.reviewStatus === 'Rejected' && (
          <div className="meta" style={{ color: 'var(--danger, #c0392b)', marginBottom: 6 }}>
            {t('Bị từ chối')}{l.reviewNote ? `: ${l.reviewNote}` : ''}. {t('Chỉnh sửa và lưu để gửi lại duyệt.')}
          </div>
        )}
        <div className="meta">{l.city} · {l.bedrooms} {t('phòng ngủ')} · {l.maxGuests} {t('khách')}</div>
        <div className="meta">
          <b style={{ color: 'var(--ink)' }}>{money(l.pricePerNight)}</b> {t('/ đêm')}
          {l.reviewCount ? ` · ★ ${l.rating.toFixed(2)} (${l.reviewCount})` : ` · ${t('Chưa có đánh giá')}`}
        </div>
        <div className="meta">{l.upcomingBookings} {t('lượt đặt sắp tới')} · {t('đã nhận')} {money(l.earningsToDate)}</div>
        <div className="host-listing-actions">
          <button className="btn btn-outline btn-sm"
                  onClick={() => set({ editingListing: l, overlay: 'listing-editor' })}>{t('Chỉnh sửa')}</button>
          <button className="btn btn-outline btn-sm" onClick={openCalendar}>{t('Lịch')}</button>
          {/* docs/01 TĐ-22 — the guidebook is post-publish content, so it lives
              beside the listing rather than inside the create wizard. */}
          <button className="btn btn-outline btn-sm"
                  onClick={() => set({ editingListing: l, overlay: 'guidebook-editor' })}>{t('Cẩm nang')}</button>
          <button className="btn btn-outline btn-sm" onClick={toggleAdvice}>
            {advice ? t('Ẩn gợi ý') : t('Gợi ý')}
          </button>
          <button className="btn btn-outline btn-sm" onClick={duplicate} disabled={busy}>{t('Nhân bản')}</button>
          <button className="btn btn-outline btn-sm" onClick={() => navigate(`/rooms/${l.slug}`)}>{t('Xem trang')}</button>
        </div>
        {advice && <ListingAdvice advice={advice} />}
      </div>
    </article>
  );
}

/** docs/01 QL-09 + QL-18 — suggested price and improvement checklist for one listing. */
function ListingAdvice({ advice }) {
  return (
    <div className="host-advice" style={{ marginTop: 12, padding: 12, background: 'var(--surface-2, #f6f6f6)', borderRadius: 10 }}>
      {/* QL-09 — a price to consider; the host applies it, nothing changes on its own. */}
      <div className="meta" style={{ marginBottom: 8 }}>
        <b style={{ color: 'var(--ink)' }}>{t('Gợi ý giá:')} </b>
        {advice.price.isFirm ? `${money(advice.price.suggestedPrice)} · ` : ''}{t(advice.price.rationale)}
      </div>
      {/* QL-18 — concrete improvements with a rough sense of impact. */}
      {advice.improvements.length === 0
        ? <div className="meta">{t('Tin đăng đang ở trạng thái tốt, chưa có gì cần cải thiện.')}</div>
        : (
          <ul style={{ margin: 0, paddingLeft: 18 }}>
            {advice.improvements.map((i, idx) => (
              <li key={idx} className="meta" style={{ marginBottom: 4 }}>
                <b style={{ color: 'var(--ink)' }}>{t(i.area)}:</b> {t(i.suggestion)}
                <span style={{ color: 'var(--brand, #e5484d)' }}> — {t(i.estimatedImpact)}</span>
              </li>
            ))}
          </ul>
        )}
    </div>
  );
}

/**
 * docs/09 §2.1–§2.3 (MR-E-01) — the host's own experiences. Submitting one sends
 * it to a reviewer rather than putting it on sale, so the list says which state
 * each is in instead of a bare published/draft.
 */
/**
 * docs/09 §2.2 — where an experience stands with the reviewer. "Not on sale"
 * covers four different situations and the host needs to tell them apart: still
 * a draft, waiting in the queue, sent back to fix, or refused.
 */
function experienceState(x) {
  if (x.isPublished) return { label: 'Đang bán vé', tone: 'confirmed' };

  switch (x.moderationStatus) {
    case 'PendingReview': return { label: 'Đang chờ duyệt', tone: 'pending' };
    case 'ChangesRequested': return { label: 'Cần chỉnh lại', tone: 'pending' };
    case 'Rejected': return { label: 'Bị từ chối', tone: 'cancelled' };
    default: return { label: 'Bản nháp', tone: 'pending' };
  }
}

/** A session stamped the way a host reads a diary: day, month, hour, minute. */
// Asked for per render so they follow the chosen language (format.js LOCALE).
const SESSION_STAMP = () => dateFormat({
  day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit'
});
const SESSION_TIME = () => dateFormat({ hour: '2-digit', minute: '2-digit' });

function HostExperiences() {
  const state = useStore();
  const [rows, setRows] = useState(null);
  // Which experience has its register open. One at a time: the register is a
  // list of names to read down, not something to compare side by side.
  const [registerFor, setRegisterFor] = useState(null);

  const load = () => api.myExperiences().then(setRows).catch(err => toast(err.message));
  // Reload when the editor closes, so a just-submitted experience shows up.
  useEffect(() => { if (!state.overlay) load(); }, [state.overlay]);

  const open = x => set({ editingExperience: x ?? null, overlay: 'experience-editor' });

  return (
    <div style={{ marginTop: 24 }}>
      <div className="page-head" style={{ marginBottom: 0 }}>
        <div>
          <h2 className="section-title" style={{ fontSize: 20 }}>{t('Trải nghiệm của bạn')}</h2>
          <p className="section-sub">
            {t('Hoạt động bán theo vé. Gửi duyệt xong, StayHost xem trong 5 ngày làm việc rồi mới mở bán.')}
          </p>
        </div>
        <button className="btn btn-primary btn-sm" onClick={() => open(null)}>{t('+ Đăng trải nghiệm')}</button>
      </div>

      {!rows ? <div className="stat skeleton" style={{ height: 160, border: 0, marginTop: 16 }} />
        : rows.length ? (
          <div style={{ marginTop: 16, display: 'grid', gap: 12 }}>
            {rows.map(x => (
              <article className="host-booking" key={x.id}>
                <div style={{ minWidth: 0 }}>
                  <h3>{x.title}</h3>
                  <div className="meta">{x.city} · {money(x.pricePerPerson)} / {t('người')} · {t('tối đa')} {x.maxGroup} {t('người')}</div>
                  <div className="meta">{x.meetingPoint || t('Chưa có điểm hẹn')}</div>
                  {/* docs/09 §2.2 — a submission waiting on a reviewer must not
                      look like one that was turned down, so the badge reads the
                      moderation state rather than the published flag alone. */}
                  <span className={`badge ${experienceState(x).tone}`} style={{ marginTop: 8 }}>
                    {t(experienceState(x).label)}
                  </span>
                  {x.reviewerNote && !x.isPublished && (
                    <div className="meta" style={{ marginTop: 6 }}>{x.reviewerNote}</div>
                  )}
                </div>
                <div className="host-booking-actions">
                  <button className="btn btn-outline btn-sm" onClick={() => open(x)}>{t('Chỉnh sửa')}</button>
                  {/* docs/09 §2.9 (MR-E-09) — the day itself: who turned up. */}
                  <button className="btn btn-outline btn-sm"
                          onClick={() => setRegisterFor(id => id === x.id ? null : x.id)}>
                    {registerFor === x.id ? t('Đóng điểm danh') : t('Điểm danh')}
                  </button>
                </div>
                {/* Full width of the card: the register is a list, not a column. */}
                {registerFor === x.id && (
                  <div style={{ flexBasis: '100%', minWidth: 0 }}>
                    <SessionRegister experience={x} />
                  </div>
                )}
              </article>
            ))}
          </div>
        ) : (
          <div className="empty-state" style={{ marginTop: 20 }}>
            <h3>{t('Chưa có trải nghiệm nào')}</h3>
            <p>{t('Kể một hoạt động bạn dẫn được và bán theo vé cho khách.')}</p>
            <button className="btn btn-primary" style={{ marginTop: 18 }}
                    onClick={() => open(null)}>{t('+ Đăng trải nghiệm')}</button>
          </div>
        )}
    </div>
  );
}

/**
 * docs/09 §3.2 (MR-S-02) — where a service stands. Unlike an experience there is
 * no reviewer queue: it is on sale or it is not, and the one thing that takes it
 * off sale by itself is a practising certificate that ran out.
 */
function serviceState(s, today) {
  const lapsed = s.certificateExpiresOn && s.certificateExpiresOn < today;
  if (lapsed) return { label: 'Chứng chỉ hết hạn — đã ẩn', tone: 'cancelled' };
  return s.isPublished
    ? { label: 'Đang bán', tone: 'confirmed' }
    : { label: 'Bản nháp', tone: 'pending' };
}

/** docs/09 §3.2 — the provider is warned thirty days before the certificate lapses. */
const CERTIFICATE_REMINDER_DAYS = 30;

function certificateWarning(s, today) {
  if (!s.certificateExpiresOn || s.certificateExpiresOn < today) return null;
  const days = Math.round(
    (new Date(`${s.certificateExpiresOn}T00:00:00`) - new Date(`${today}T00:00:00`)) / 86400000);
  return days <= CERTIFICATE_REMINDER_DAYS ? days : null;
}

/**
 * docs/09 §3.2–§3.4 (MR-S-01) — the provider's own services. Sold by the slot at
 * the guest's address, so the row says how far they travel and which days they
 * work rather than a bed count.
 */
function HostServices() {
  const state = useStore();
  const [rows, setRows] = useState(null);
  const today = todayIso();

  const load = () => api.myServices().then(setRows).catch(err => toast(err.message));
  // Reload when the editor closes, so a just-saved service shows up.
  useEffect(() => { if (!state.overlay) load(); }, [state.overlay]);

  const open = s => set({ editingService: s ?? null, overlay: 'service-editor' });

  return (
    <div style={{ marginTop: 24 }}>
      <div className="page-head" style={{ marginBottom: 0 }}>
        <div>
          <h2 className="section-title" style={{ fontSize: 20 }}>{t('Dịch vụ của bạn')}</h2>
          <p className="section-sub">
            {t('Việc bán theo khung giờ, làm tại chỗ khách ở. Chứng chỉ hành nghề hết hạn thì dịch vụ tự ẩn khỏi tìm kiếm.')}
          </p>
        </div>
        <button className="btn btn-primary btn-sm" onClick={() => open(null)}>{t('+ Đăng dịch vụ')}</button>
      </div>

      {!rows ? <div className="stat skeleton" style={{ height: 160, border: 0, marginTop: 16 }} />
        : rows.length ? (
          <div style={{ marginTop: 16, display: 'grid', gap: 12 }}>
            {rows.map(s => (
              <article className="host-booking" key={s.id}>
                <div style={{ minWidth: 0 }}>
                  <h3>{s.title}</h3>
                  <div className="meta">
                    {s.city} · {t(s.pricingLabel)} · {money(s.basePrice)} / {t(s.unit)} · {s.durationMinutes} {t('phút')}
                  </div>
                  <div className="meta">
                    {s.travelsToGuest
                      ? `${t('Tới tận nơi trong')} ${s.serviceRadiusKm} km`
                      : t('Khách tới chỗ cung cấp')}
                    {' · '}{s.opensAtHour}:00–{s.closesAtHour}:00
                    {s.maxJobsPerDay > 0 ? ` · ${t('tối đa')} ${s.maxJobsPerDay} ${t('đơn/ngày')}` : ''}
                  </div>
                  {!!(s.addOns ?? []).length && (
                    <div className="meta">{(s.addOns ?? []).length} {t('tuỳ chọn thêm')}</div>
                  )}
                  <span className={`badge ${serviceState(s, today).tone}`} style={{ marginTop: 8 }}>
                    {t(serviceState(s, today).label)}
                  </span>
                  {/* docs/09 §3.2 — the thirty-day warning, said where they will see it. */}
                  {certificateWarning(s, today) !== null && (
                    <div className="meta" style={{ marginTop: 6 }}>
                      {t('Chứng chỉ còn')} {certificateWarning(s, today)}{' '}
                      {t('ngày là hết hạn — gia hạn trước khi dịch vụ tự ẩn.')}
                    </div>
                  )}
                </div>
                <div className="host-booking-actions">
                  <button className="btn btn-outline btn-sm" onClick={() => open(s)}>{t('Chỉnh sửa')}</button>
                </div>
              </article>
            ))}
            <ProviderJobs />
          </div>
        ) : (
          <div className="empty-state" style={{ marginTop: 20 }}>
            <h3>{t('Chưa có dịch vụ nào')}</h3>
            <p>{t('Nhận nấu ăn, chụp ảnh, đưa đón — bán theo khung giờ cho khách đang ở gần bạn.')}</p>
            <button className="btn btn-primary" style={{ marginTop: 18 }}
                    onClick={() => open(null)}>{t('+ Đăng dịch vụ')}</button>
          </div>
        )}
    </div>
  );
}

/**
 * docs/09 §3.5, §3.6 — the jobs somebody has actually been booked for.
 *
 * The console used to show only the services on sale, so the note docs/09 §3.5
 * forces a guest to write — food allergies, what to avoid in a massage — was
 * collected and then shown to nobody. It leads this list for that reason.
 */
function ProviderJobs() {
  const [jobs, setJobs] = useState(null);
  const [busy, setBusy] = useState(0);

  const load = () => api.serviceJobs().then(setJobs).catch(() => setJobs([]));
  useEffect(() => { load(); }, []);

  // docs/09 §3.6 (DV-D) — the guest keeps half, the provider keeps half. It ends
  // the job, so it asks first.
  const misdeclared = async job => {
    const note = prompt(t('Thiếu gì ở nơi làm việc? (không bắt buộc)'));
    if (note === null) return;
    setBusy(job.id);
    try {
      await api.reportMisdeclared(job.id, note.trim() || null);
      await load();
      toast(t('Đã ghi nhận. Bạn được trả một nửa giá trị đơn cho công đi lại.'));
    } catch (err) { toast(err.message); }
    finally { setBusy(0); }
  };

  if (!jobs?.length) return null;

  return (
    <section style={{ marginTop: 10 }}>
      <h3 style={{ margin: '0 0 4px', fontSize: 16, fontWeight: 800 }}>{t('Đơn bạn nhận được')}</h3>
      <p className="section-sub" style={{ marginBottom: 12 }}>
        {t('Giờ hẹn, địa chỉ và ghi chú bắt buộc của khách.')}
      </p>

      <div style={{ display: 'grid', gap: 10 }}>
        {jobs.map(j => (
          <article className="host-booking" key={j.id}>
            <div style={{ minWidth: 0 }}>
              <h3>{j.offeringTitle}</h3>
              <div className="meta">
                {j.guestName} · {dateTime(j.startsAt)} · {j.quantity} {t(j.unit)}
              </div>
              <div className="meta">{j.address}</div>
              {/* The whole reason this list exists. */}
              {j.note && (
                <div className="meta" style={{ marginTop: 6, color: 'var(--ink)', fontWeight: 700 }}>
                  {t('Khách dặn:')} {j.note}
                </div>
              )}
              {j.guestPhone && <div className="meta">{t('Điện thoại:')} {j.guestPhone}</div>}
              <div className="meta">
                {money(j.total)} · {t('bạn nhận')} <b style={{ color: 'var(--ink)' }}>{money(j.providerPayout)}</b>
              </div>
              <span className={`badge ${j.statusBadge}`} style={{ marginTop: 8 }}>{t(j.statusLabel)}</span>
              {j.cancelReason && <div className="meta" style={{ marginTop: 6 }}>{j.cancelReason}</div>}
            </div>
            {j.canReportMisdeclared && (
              <div className="host-booking-actions">
                <button className="btn btn-outline btn-sm" disabled={busy === j.id}
                        onClick={() => misdeclared(j)}>{t('Không đủ điều kiện tại chỗ')}</button>
              </div>
            )}
          </article>
        ))}
      </div>
    </section>
  );
}

/**
 * docs/09 §2.9 (MR-E-09) — the register for one session. Whether it may be taken
 * at all is the server's answer (`canMark`), not a reading of this machine's
 * clock: marking somebody absent from a session that has not started is a guess.
 * So before the start time the names are shown and the two buttons are off,
 * with the reason written out rather than left to a failed click.
 */
function SessionRegister({ experience }) {
  const slots = experience.slots.filter(s => s.status !== 'Cancelled');
  // Open on the session being run right now — the latest one already under way —
  // and fall back to the next one so the host can read the names beforehand.
  const [slotId, setSlotId] = useState(() => {
    const now = Date.now();
    const started = slots.filter(s => new Date(s.startsAt).getTime() <= now);
    return (started.length ? started[started.length - 1] : slots[0])?.id ?? null;
  });
  const [roster, setRoster] = useState(null);
  const [busyId, setBusyId] = useState(null);

  useEffect(() => {
    if (!slotId) { setRoster(null); return; }
    let live = true;
    setRoster(null);
    api.experienceRoster(slotId)
      .then(r => { if (live) setRoster(r); })
      .catch(err => toast(err.message));
    return () => { live = false; };
  }, [slotId]);

  if (!slots.length) {
    return (
      <div className="notice notice-warn">
        {t('Trải nghiệm này chưa có suất nào để điểm danh. Thêm suất trước đã.')}
      </div>
    );
  }

  /* A mark also closes the ticket on the server, so the register is read back
     instead of being patched here — the screen then says what the data says. */
  const mark = async (bookingId, attended) => {
    setBusyId(bookingId);
    try {
      await api.markExperienceAttendance(bookingId, attended);
      setRoster(await api.experienceRoster(slotId));
    } catch (err) { toast(err.message); }
    finally { setBusyId(null); }
  };

  return (
    <div style={{ marginTop: 14, paddingTop: 14, borderTop: '1px solid var(--divider)' }}>
      <label className="form-field" style={{ maxWidth: 360, marginBottom: 4 }}>
        <span className="cap">{t('Suất')}</span>
        <select value={slotId ?? ''} onChange={e => setSlotId(Number(e.target.value))}
                style={{ padding: '8px 10px', border: '1px solid var(--line)', borderRadius: 10, fontSize: 14 }}>
          {slots.map(s => (
            <option key={s.id} value={s.id}>
              {SESSION_STAMP().format(new Date(s.startsAt))} · {s.seatsTaken}/{s.capacity} {t('chỗ')}
            </option>
          ))}
        </select>
      </label>
      <p className="meta">{t('Chỉ hiện suất sắp tới và suất vừa diễn ra.')}</p>

      {!roster ? <div className="stat skeleton" style={{ height: 120, border: 0, marginTop: 12 }} /> : <>
        <div className="meta" style={{ marginTop: 10 }}>
          {SESSION_STAMP().format(new Date(roster.startsAt))} → {SESSION_TIME().format(new Date(roster.endsAt))} ·
          {' '}{roster.seatsTaken}/{roster.capacity} {t('chỗ đã bán')} · {roster.guests.length} {t('vé')}
        </div>

        {/* Not yet: say so instead of letting the host click into an error. */}
        {!roster.canMark ? (
          <div className="notice notice-warn">
            <b>{t('Chưa tới giờ bắt đầu nên chưa điểm danh được.')}</b>{' '}
            {t('Danh sách dưới đây để bạn xem trước; điểm danh mở ngay khi suất bắt đầu.')}
          </div>
        ) : (
          <div className="notice notice-ok">
            {t('Khách tới muộn quá')} {roster.lateAllowanceMinutes} {t('phút thì bạn có quyền bắt đầu mà không cần chờ; khách đó không được hoàn tiền.')}
          </div>
        )}

        {roster.guests.length === 0 ? (
          <p className="meta" style={{ marginTop: 12 }}>{t('Suất này chưa có ai đặt.')}</p>
        ) : (
          <div style={{ marginTop: 12, display: 'grid', gap: 8 }}>
            {roster.guests.map(g => (
              <div className="count-row" key={g.bookingId} style={{ gap: 12 }}>
                <div className="tx" style={{ minWidth: 0 }}>
                  <b>{g.guestName}</b>
                  <span>
                    {t('Mã')} {g.reference} · {g.seats} {t('chỗ')}{g.isPrivate ? ` · ${t('nhóm riêng')}` : ''}
                    {g.attended == null
                      ? ` · ${t('Chưa điểm danh')}`
                      : ` · ${g.attended ? t('Có mặt') : t('Vắng')}${g.markedAt ? ` ${SESSION_TIME().format(new Date(g.markedAt))}` : ''}`}
                  </span>
                </div>
                <div className="host-booking-actions">
                  <button className={`btn btn-sm ${g.attended === true ? 'btn-primary' : 'btn-outline'}`}
                          disabled={!roster.canMark || busyId === g.bookingId}
                          onClick={() => mark(g.bookingId, true)}>{t('Có mặt')}</button>
                  <button className={`btn btn-sm ${g.attended === false ? 'btn-primary' : 'btn-outline'}`}
                          disabled={!roster.canMark || busyId === g.bookingId}
                          onClick={() => mark(g.bookingId, false)}>{t('Vắng')}</button>
                </div>
              </div>
            ))}
          </div>
        )}
      </>}
    </div>
  );
}

function Bookings({ d, navigate }) {
  if (!d.bookings.length) {
    return (
      <div className="empty-state" style={{ marginTop: 24 }}>
        <h3>{t('Chưa có lượt đặt nào')}</h3>
        <p>{t('Khi có khách đặt, đơn sẽ hiện ở đây.')}</p>
      </div>
    );
  }
  return <div style={{ marginTop: 24 }}>{d.bookings.map(b => <BookingRow key={b.id} booking={b} navigate={navigate} />)}</div>;
}

/** docs/03 §3 — the host has 24 hours before the request expires by itself. */
function RespondDeadline({ at }) {
  const minutes = Math.round((new Date(at) - Date.now()) / 60000);
  if (minutes <= 0) return null;

  return (
    <span className="badge pending">
      {minutes < 60 ? `${t('Còn')} ${minutes} ${t('phút để trả lời')}` : `${t('Còn')} ${Math.round(minutes / 60)} ${t('giờ để trả lời')}`}
    </span>
  );
}

function BookingRow({ booking: b, navigate }) {
  const awaitingHost = b.status === 'PendingHostApproval';

  return (
    <article className="host-booking">
      <div style={{ minWidth: 0 }}>
        <h3>{b.listingTitle}</h3>
        <div className="meta">{b.guestName}{b.guestEmail ? ` · ${b.guestEmail}` : ''} · {t('mã')} {b.reference}</div>
        <div className="meta">
          {longDate(b.checkIn)} → {longDate(b.checkOut)} · {b.nights} {t('đêm')} · {b.guests} {t('khách')}
        </div>
        <div className="meta">
          {t('Khách trả')} <b style={{ color: 'var(--ink)' }}>{money(b.total)}</b> ·
          {' '}{t('bạn nhận')} <b style={{ color: 'var(--brand)' }}>{money(b.hostPayout)}</b>
        </div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 8 }}>
          <span className={`badge ${b.statusBadge}`}>{t(b.statusLabel, 'status')}</span>
          {awaitingHost && b.requestExpiresAt && <RespondDeadline at={b.requestExpiresAt} />}
        </div>

        {/* docs/01 CĐ-06 — a change the guest asked for, with accept/reject. */}
        {b.pendingChange && (
          <div className="notice notice-warn" style={{ marginTop: 10 }}>
            <b>{t('Yêu cầu đổi lịch:')}</b> {longDate(b.pendingChange.newCheckIn)} → {longDate(b.pendingChange.newCheckOut)} ·
            {' '}{b.pendingChange.newGuests} {t('khách')} · {t(b.pendingChange.differenceLabel)}
            <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
              <button className="btn btn-primary btn-sm" onClick={() => respondChange(b.id, b.pendingChange.id, true)}>{t('Chấp nhận đổi')}</button>
              <button className="btn btn-outline btn-sm" onClick={() => respondChange(b.id, b.pendingChange.id, false)}>{t('Từ chối')}</button>
            </div>
          </div>
        )}
      </div>
      <div className="host-booking-actions">
        {awaitingHost && <>
          <button className="btn btn-primary btn-sm" onClick={() => respondBooking(b.id, 'confirm')}>{t('Xác nhận')}</button>
          <button className="btn btn-outline btn-sm" onClick={() => respondBooking(b.id, 'decline')}>{t('Từ chối')}</button>
        </>}
        {b.status === 'Completed' && (
          <button className="btn btn-outline btn-sm"
                  onClick={() => set({ guestReviewBooking: b, overlay: 'guest-review' })}>{t('Đánh giá khách')}</button>
        )}
        {/* docs/01 QL-13 — never a bare "huỷ": the warning comes first. */}
        {b.status === 'Confirmed' && (
          <button className="btn btn-outline btn-sm" onClick={() => previewHostCancel(b.id)}>{t('Huỷ đơn')}</button>
        )}
        <button className="btn btn-outline btn-sm" onClick={() => navigate('/messages')}>{t('Nhắn khách')}</button>
      </div>
    </article>
  );
}

function Earnings({ d }) {
  const state = useStore();
  const hostRate = state.meta?.fees?.hostServiceFeeRate ?? 0.03;

  if (!d.earningsByMonth.length) {
    return (
      <div className="empty-state" style={{ marginTop: 24 }}>
        <h3>{t('Chưa có doanh thu')}</h3>
        <p>{t('Biểu đồ sẽ hiện khi bạn có lượt đặt đầu tiên.')}</p>
      </div>
    );
  }

  const max = Math.max(...d.earningsByMonth.map(m => Number(m.amount)), 1);
  const pct = Math.round(hostRate * 100);

  return (
    <div style={{ marginTop: 24 }}>
      <div className="stat-grid">
        <div className="stat">
          <div className="value">{money(d.earningsToDate)}</div>
          <div className="label">{t('Đã nhận')}</div>
          <div className="note">{t('Các kỳ nghỉ đã hoàn tất')}</div>
        </div>
        <div className="stat">
          <div className="value">{money(d.earningsUpcoming)}</div>
          <div className="label">{t('Sắp nhận')}</div>
          <div className="note">{d.upcomingBookings} {t('lượt đặt sắp tới')}</div>
        </div>
        <div className="stat">
          <div className="value">{money(d.earningsToDate + d.earningsUpcoming)}</div>
          <div className="label">{t('Tổng cộng')}</div>
          <div className="note">{t('Sau phí dịch vụ chủ nhà')} {pct}%</div>
        </div>
      </div>

      <section style={{ marginTop: 34 }}>
        <div className="page-head" style={{ marginBottom: 0 }}>
          <h2 className="section-title" style={{ fontSize: 20 }}>{t('Theo tháng nhận phòng')}</h2>
          <a className="btn btn-outline btn-sm" href="/api/host/earnings.csv" download>{t('Tải file doanh thu')}</a>
        </div>
        <div className="bar-chart">
          {d.earningsByMonth.map(m => (
            <div className="bar-col" key={m.month} title={`${m.month}: ${money(m.amount)} · ${m.nights} ${t('đêm')}`}>
              <div className="bar" style={{ height: `${Math.max(4, (Number(m.amount) / max) * 100)}%` }} />
              <span className="bar-label">{m.month}</span>
              <span className="bar-value">{money(m.amount)}</span>
            </div>
          ))}
        </div>
      </section>

      <PerformanceReport />
      <TaxReport />

      <section style={{ marginTop: 34 }}>
        <h2 className="section-title" style={{ fontSize: 20 }}>{t('Cách StayHost tính tiền')}</h2>
        <div className="know-grid" style={{ marginTop: 16 }}>
          <div className="know">
            <h3><Icon name="star" size={18} /> {t('Phí dịch vụ')}</h3>
            <ul><li>{t('StayHost giữ')} {pct}% {t('trên tạm tính của mỗi lượt đặt thành công.')}</li></ul>
          </div>
          <div className="know">
            <h3><Icon name="heart" size={18} /> {t('Bạn nhận')}</h3>
            <ul><li>{t('Toàn bộ tiền phòng và phí dọn dẹp trừ phí dịch vụ, chuyển 24 giờ sau khi khách nhận phòng.')}</li></ul>
          </div>
          <div className="know">
            <h3><Icon name="globe" size={18} /> {t('Huỷ & hoàn')}</h3>
            <ul><li>{t('Phí vệ sinh luôn được hoàn 100% cho khách ở mọi chính sách huỷ.')}</li></ul>
          </div>
        </div>
      </section>
    </div>
  );
}

/**
 * docs/01 TC-04, docs/02 G7 — the year written down for whoever does the host's
 * tax. Only completed stays are in it, counted by the year they ended; the CSV
 * carries every stay behind the totals so the file can be checked rather than
 * trusted.
 */
function TaxReport() {
  const [report, setReport] = useState(null);
  const [year, setYear] = useState(null);

  useEffect(() => {
    api.taxReport(year).then(setReport).catch(err => toast(err.message));
  }, [year]);

  if (!report) return null;

  const rows = report.months.filter(m => m.stays > 0);

  return (
    <section style={{ marginTop: 34 }}>
      <div className="page-head" style={{ marginBottom: 0 }}>
        <h2 className="section-title" style={{ fontSize: 20 }}>{t('Báo cáo thuế')}</h2>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          {report.years.length > 1 && (
            <select value={report.year} onChange={e => setYear(Number(e.target.value))}
                    style={{ padding: '8px 10px', border: '1px solid var(--line)', borderRadius: 10, fontSize: 14 }}>
              {report.years.map(y => <option key={y} value={y}>{t('Năm')} {y}</option>)}
            </select>
          )}
          <a className="btn btn-outline btn-sm" download
             href={`/api/host/tax-report.csv?year=${report.year}`}>{t('Tải báo cáo thuế')}</a>
        </div>
      </div>

      <p className="section-sub">{t(report.note)}</p>

      {report.stays === 0 ? (
        <p className="section-sub" style={{ marginTop: 12 }}>{t('Năm')} {report.year} {t('chưa có đơn nào hoàn tất.')}</p>
      ) : (
        <>
          <div className="table-wrap" style={{ marginTop: 16 }}>
            <table className="admin-table">
              <thead>
                <tr>
                  <th>{t('Tháng')}</th><th>{t('Số đơn')}</th>
                  <th style={{ textAlign: 'right' }}>{t('Khách trả')}</th>
                  <th style={{ textAlign: 'right' }}>{t('Thuế')}</th>
                  <th style={{ textAlign: 'right' }}>{t('Phí dịch vụ')}</th>
                  <th style={{ textAlign: 'right' }}>{t('Bạn nhận')}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map(m => (
                  <tr key={m.month}>
                    <td>{t(m.label)}</td>
                    <td>{m.stays}</td>
                    <td style={{ textAlign: 'right' }}>{money(m.guestPaid)}</td>
                    <td style={{ textAlign: 'right' }}>{money(m.tax)}</td>
                    <td style={{ textAlign: 'right' }}>{money(m.hostServiceFee)}</td>
                    <td style={{ textAlign: 'right' }}>{money(m.hostPayout)}</td>
                  </tr>
                ))}
                <tr>
                  <td><b>{t('Cả năm')} {report.year}</b></td>
                  <td><b>{report.stays}</b></td>
                  <td style={{ textAlign: 'right' }}><b>{money(report.guestPaid)}</b></td>
                  <td style={{ textAlign: 'right' }}><b>{money(report.tax)}</b></td>
                  <td style={{ textAlign: 'right' }}><b>{money(report.hostServiceFee)}</b></td>
                  <td style={{ textAlign: 'right' }}><b>{money(report.hostPayout)}</b></td>
                </tr>
              </tbody>
            </table>
          </div>

          {!!report.taxes.length && (
            <div className="table-wrap" style={{ marginTop: 16 }}>
              <table className="admin-table">
                <thead>
                  <tr><th>{t('Loại thuế')}</th><th>{t('Số đơn')}</th><th style={{ textAlign: 'right' }}>{t('Số tiền')}</th></tr>
                </thead>
                <tbody>
                  {/* Not `t` — that name belongs to the translator in this file. */}
                  {report.taxes.map(line => (
                    <tr key={line.name}>
                      <td>{t(line.name)}</td>
                      <td>{line.stays}</td>
                      <td style={{ textAlign: 'right' }}>{money(line.amount)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </section>
  );
}

/**
 * docs/01 QL-16, docs/02 G7 — how each listing is doing. The view counts have
 * been recorded all along; this is the screen that finally reads them back, next
 * to saves, bookings and the two rates derived from them.
 */
function PerformanceReport() {
  const [rows, setRows] = useState(null);
  const [days, setDays] = useState(30);

  useEffect(() => {
    api.performance(days).then(setRows).catch(err => toast(err.message));
  }, [days]);

  if (!rows) return null;

  return (
    <section style={{ marginTop: 34 }}>
      <div className="page-head" style={{ marginBottom: 0 }}>
        <h2 className="section-title" style={{ fontSize: 20 }}>{t('Hiệu suất tin đăng')}</h2>
        <select value={days} onChange={e => setDays(Number(e.target.value))}
                style={{ padding: '8px 10px', border: '1px solid var(--line)', borderRadius: 10, fontSize: 14 }}>
          <option value={7}>{t('7 ngày qua')}</option>
          <option value={30}>{t('30 ngày qua')}</option>
          <option value={90}>{t('90 ngày qua')}</option>
        </select>
      </div>

      {rows.length === 0 ? (
        <p className="section-sub" style={{ marginTop: 12 }}>{t('Bạn chưa có tin đăng nào.')}</p>
      ) : (
        <div className="table-wrap" style={{ marginTop: 16 }}>
          <table className="admin-table">
            <thead>
              <tr>
                <th>{t('Tin đăng')}</th>
                <th style={{ textAlign: 'right' }}>{t('Lượt xem')}</th>
                <th style={{ textAlign: 'right' }}>{t('Lượt lưu')}</th>
                <th style={{ textAlign: 'right' }}>{t('Lượt đặt')}</th>
                <th style={{ textAlign: 'right' }}>{t('Xem → đặt')}</th>
                <th style={{ textAlign: 'right' }}>{t('Lấp đầy')}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map(r => (
                <tr key={r.listingId}>
                  <td>{r.title}{!r.isPublished && <span className="meta"> · {t('ẩn')}</span>}</td>
                  <td style={{ textAlign: 'right' }}>{r.views}</td>
                  <td style={{ textAlign: 'right' }}>{r.saves}</td>
                  <td style={{ textAlign: 'right' }}>{r.bookings}</td>
                  <td style={{ textAlign: 'right' }}>{r.conversionPercent}%</td>
                  <td style={{ textAlign: 'right' }}>{r.occupancyPercent}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

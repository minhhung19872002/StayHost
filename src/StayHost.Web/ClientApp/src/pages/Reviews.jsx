import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStore } from '../lib/useStore.js';
import { set, toast } from '../lib/store.js';
import { api } from '../lib/api.js';
import { longDate } from '../lib/format.js';
import { t } from '../lib/i18n.js';

/**
 * docs/02 H1 — everything one account's reviews add up to, in the three groups
 * the document names.
 *
 * The pieces were all there and gathered nowhere: a stay could only be reviewed
 * from the trip it belonged to, what you had written could not be read back
 * without opening each trip one at a time, and what hosts had said about you was
 * visible only on your own public profile — a page built for other people.
 */
const GROUPS = [
  ['todo', 'Cần viết'],
  ['written', 'Tôi đã viết'],
  ['about', 'Về tôi']
];

export function Reviews() {
  const state = useStore();
  const navigate = useNavigate();
  const [data, setData] = useState(null);
  const [group, setGroup] = useState(null);

  useEffect(() => {
    if (!state.user) return;
    api.myReviews().then(setData).catch(err => toast(err.message));
  }, [state.user]);

  if (!state.user) {
    return (
      <div className="shell" style={{ paddingBlock: '60px 90px' }}>
        <div className="empty-state">
          <h3>{t('Đăng nhập để xem đánh giá của bạn')}</h3>
          <p>{t('Đánh giá cần viết, đánh giá bạn đã viết và đánh giá về bạn nằm ở đây.')}</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }}
                  onClick={() => set({ authMode: 'login', authError: null, overlay: 'login' })}>
            {t('Đăng nhập')}
          </button>
        </div>
      </div>
    );
  }

  if (!data) {
    return (
      <div className="shell" style={{ paddingBlock: '30px 90px' }}>
        <div className="sk-line skeleton" style={{ width: 240, height: 26 }} />
        <div className="skeleton" style={{ height: 200, borderRadius: 16, marginTop: 20 }} />
      </div>
    );
  }

  const counts = { todo: data.toWrite.length, written: data.written.length, about: data.aboutMe.length };
  const active = group ?? GROUPS.find(([key]) => counts[key] > 0)?.[0] ?? 'todo';

  return (
    <div className="shell" style={{ paddingBlock: '30px 90px' }}>
      <h1 className="section-title">{t('Đánh giá')}</h1>
      <p className="section-sub">
        {t('Đánh giá mù hai chiều: bên kia chỉ đọc được khi cả hai đã gửi, hoặc sau 14 ngày.')}
      </p>

      <div className="seg-tabs" role="tablist" style={{ marginTop: 18 }}>
        {GROUPS.map(([key, label]) => (
          <button role="tab" key={key} aria-selected={active === key}
                  className={`seg-tab ${active === key ? 'is-active' : ''}`}
                  onClick={() => setGroup(key)}>
            {t(label)} ({counts[key]})
          </button>
        ))}
      </div>

      {active === 'todo' && <ToWrite rows={data.toWrite} navigate={navigate} />}
      {active === 'written' && <Written rows={data.written} navigate={navigate} />}
      {active === 'about' && <AboutMe rows={data.aboutMe} />}
    </div>
  );
}

/** docs/02 H1 — "cần viết (kèm hạn còn lại)". The deadline is the point. */
function ToWrite({ rows, navigate }) {
  if (!rows.length) {
    return (
      <div className="empty-state" style={{ marginTop: 24 }}>
        <h3>{t('Không còn đánh giá nào cần viết')}</h3>
        <p>{t('Sau mỗi chuyến đã hoàn tất, bạn có 14 ngày để viết.')}</p>
      </div>
    );
  }

  return (
    <div style={{ marginTop: 20, display: 'grid', gap: 12 }}>
      {rows.map(r => (
        <article className="host-booking" key={`${r.bookingId}-${r.side}`}>
          <div style={{ minWidth: 0 }}>
            <h3>{r.listingTitle}</h3>
            <div className="meta">
              {/* Both halves of docs/03 §7 live in one list: a host owes reviews
                  of their guests exactly as a guest owes reviews of the stay. */}
              {r.side === 'host'
                ? `${t('Đánh giá khách')} ${r.counterpartName ?? ''}`
                : `${t('Chủ nhà')} ${r.counterpartName ?? ''}`}
              {' · '}{t('mã')} {r.reference}
            </div>
            <div className="meta">{t('Trả phòng')} {longDate(r.checkOut)}</div>
            <span className={`badge ${r.daysLeft <= 3 ? 'pending' : 'confirmed'}`} style={{ marginTop: 8 }}>
              {t('Còn {} ngày để viết').replace('{}', r.daysLeft)}
            </span>
          </div>
          <div className="host-booking-actions">
            <button className="btn btn-primary btn-sm"
                    onClick={() => navigate(r.side === 'host' ? '/hosting' : `/trips/${r.bookingId}`)}>
              {t('Viết đánh giá')}
            </button>
          </div>
        </article>
      ))}
    </div>
  );
}

function Written({ rows, navigate }) {
  if (!rows.length) {
    return (
      <div className="empty-state" style={{ marginTop: 24 }}>
        <h3>{t('Bạn chưa viết đánh giá nào')}</h3>
      </div>
    );
  }

  return (
    <div style={{ marginTop: 20, display: 'grid', gap: 12 }}>
      {rows.map(r => (
        <article className="host-booking" key={`w-${r.id}-${r.wouldHostAgain === null ? 'g' : 'h'}`}>
          <div style={{ minWidth: 0, flexBasis: '100%' }}>
            <h3>★ {r.rating.toFixed(1)} · {r.listingTitle ?? t('Chuyến đi')}</h3>
            <div className="meta">
              {r.when}
              {/* docs/03 §7 — say which state it is in rather than leaving the
                  writer to guess why nobody can see it yet. */}
              {' · '}
              <span className={`badge ${r.isPublic ? 'confirmed' : 'pending'}`}>
                {r.isPublic ? t('Đã công khai') : t('Chờ bên kia gửi')}
              </span>
            </div>
            <p style={{ margin: '10px 0 0', fontSize: 14.5, lineHeight: 1.6 }}>{r.text}</p>

            {r.wouldHostAgain !== null && (
              <div className="meta" style={{ marginTop: 8 }}>
                {r.wouldHostAgain ? t('Bạn có khuyến nghị khách này') : t('Bạn không khuyến nghị khách này')}
              </div>
            )}

            {r.hostReply && (
              <div className="review-reply" style={{ marginLeft: 0, marginTop: 12 }}>
                <b>{t('Chủ nhà đã trả lời')}</b>
                <p>{r.hostReply}</p>
              </div>
            )}

            {/* docs/01 ĐG-08 — the correction window, from the one screen that
                lists what there is to correct. */}
            {r.canEdit && r.bookingId && (
              <button className="btn btn-outline btn-sm" style={{ marginTop: 12 }}
                      onClick={() => navigate(`/trips/${r.bookingId}`)}>
                {t('Sửa đánh giá')}
              </button>
            )}
          </div>
        </article>
      ))}
    </div>
  );
}

function AboutMe({ rows }) {
  if (!rows.length) {
    return (
      <div className="empty-state" style={{ marginTop: 24 }}>
        <h3>{t('Chưa có đánh giá nào về bạn')}</h3>
        <p>{t('Đánh giá chỉ hiện ở đây sau khi được công khai.')}</p>
      </div>
    );
  }

  return (
    <div style={{ marginTop: 20, display: 'grid', gap: 12 }}>
      {rows.map(r => (
        <article className="host-booking" key={`a-${r.id}-${r.wouldHostAgain === null ? 'g' : 'h'}`}>
          <div style={{ minWidth: 0, flexBasis: '100%' }}>
            <h3>★ {r.rating.toFixed(1)} · {r.authorName ?? t('Khách')}</h3>
            <div className="meta">{r.listingTitle ?? ''}{r.listingTitle ? ' · ' : ''}{r.when}</div>
            <p style={{ margin: '10px 0 0', fontSize: 14.5, lineHeight: 1.6 }}>{r.text}</p>
            {r.wouldHostAgain !== null && (
              <div className="meta" style={{ marginTop: 8 }}>
                {r.wouldHostAgain ? t('Chủ nhà khuyến nghị bạn') : t('Chủ nhà không khuyến nghị bạn')}
              </div>
            )}
            {r.hostReply && (
              <div className="review-reply" style={{ marginLeft: 0, marginTop: 12 }}>
                <b>{t('Phản hồi của bạn')}</b>
                <p>{r.hostReply}</p>
              </div>
            )}
          </div>
        </article>
      ))}
    </div>
  );
}

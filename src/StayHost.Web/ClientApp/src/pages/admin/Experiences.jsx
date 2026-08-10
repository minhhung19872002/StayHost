import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../../lib/api.js';
import { toast } from '../../lib/store.js';
import { longDate, todayIso } from '../../lib/format.js';
import { t } from '../../lib/i18n.js';

/* docs/09 §2.3 — the band comes from the activity, so the reviewer reads it
   rather than judges it. High risk is the one that must not slip past. */
const RISK_BADGE = {
  'Rủi ro cao': 'danger',
  'Rủi ro trung bình': 'pending',
  'Rủi ro thấp': 'confirmed'
};

const DONE = {
  approve: 'Đã duyệt trải nghiệm.',
  changes: 'Đã gửi yêu cầu chỉnh sửa cho chủ trải nghiệm.',
  reject: 'Đã từ chối trải nghiệm.'
};

/**
 * docs/09 §2.2 (MR-E-03) — the moderation queue for experiences. Oldest first,
 * because the five working days of TN-A start ticking when the host submits, not
 * when somebody gets round to looking.
 */
export function ExperienceReviewPanel() {
  const [rows, setRows] = useState(null);

  const load = async () => {
    try { setRows(await api.experienceReviewQueue()); }
    catch { setRows([]); }
  };
  useEffect(() => { load(); }, []);

  if (!rows) return null;

  return (
    <section style={{ marginTop: 40 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>{t('Trải nghiệm chờ kiểm duyệt')}</h2>
      <p className="section-sub">
        {rows.length
          ? `${rows.length} ${t('hồ sơ đang chờ · hạn xét 5 ngày làm việc kể từ lúc gửi')}`
          : t('Không có trải nghiệm nào đang chờ duyệt.')}
      </p>

      {rows.length > 0 && (
        <div style={{ marginTop: 16, display: 'grid', gap: 12 }}>
          {rows.map(x => <PendingExperience key={x.id} x={x} onDone={load} />)}
        </div>
      )}
    </section>
  );
}

function PendingExperience({ x, onDone }) {
  const navigate = useNavigate();
  const [mode, setMode] = useState(null); // 'changes' | 'reject', mở ô ghi lý do
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);

  const high = x.riskLabel === 'Rủi ro cao';
  const waited = daysSince(x.submittedAt);
  const due = workingDaysAfter(x.submittedAt, x.reviewWorkingDays);
  const overdue = due !== null && due < todayIso();

  const send = async decision => {
    // docs/09 §2.2 — "changes" và "reject" bắt buộc có lý do. Chặn ngay ở đây để
    // người xét không phải bấm gửi mới biết máy chủ không nhận.
    const reason = note.trim();
    if (decision !== 'approve' && !reason) {
      toast(t('Cần ghi rõ phải sửa gì hoặc vì sao từ chối.'));
      return;
    }

    setBusy(true);
    try {
      await api.reviewExperience(x.id, decision, reason || null);
      toast(t(DONE[decision]));
      setMode(null);
      setNote('');
      await onDone();
    } catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  return (
    <article className="host-booking" style={{
      alignItems: 'flex-start',
      ...(high ? { background: 'rgba(224,72,77,.06)' } : null)
    }}>
      <div style={{ minWidth: 0, flex: 1, display: 'flex', gap: 14 }}>
        {x.coverImage && (
          <img src={x.coverImage} alt="" loading="lazy" decoding="async"
               style={{ width: 96, height: 72, objectFit: 'cover', borderRadius: 12, flex: '0 0 auto' }} />
        )}

        <div style={{ minWidth: 0 }}>
          <h3>{x.title}</h3>
          <div className="meta">
            {x.city} · {x.category} · {t('chủ trải nghiệm')}{' '}
            <button className="link-btn" onClick={() => navigate(`/users/${x.hostUserId}`)}>{x.hostName}</button>
          </div>
          <div className="meta">
            {x.submittedAt
              ? `${t('Gửi lúc')} ${longDate(x.submittedAt.slice(0, 10))} · ${waited === 0 ? t('gửi hôm nay') : `${t('đã chờ')} ${waited} ${t('ngày')}`}`
              : t('Chưa rõ lúc gửi')}
            {due && ` · ${t('hạn xét')} ${longDate(due)}`}
          </div>

          <div style={{ display: 'flex', gap: 6, marginTop: 8, flexWrap: 'wrap' }}>
            <span className={`badge ${RISK_BADGE[x.riskLabel] ?? 'cancelled'}`}>{t(x.riskLabel)}</span>
            {overdue && <span className="badge danger">{t('Quá hạn xét')}</span>}
          </div>

          <Checklist x={x} />

          {mode && (
            <div style={{ marginTop: 12, maxWidth: 560 }}>
              <label className="form-field">
                <span className="cap">{mode === 'changes' ? t('Cần sửa những gì') : t('Lý do từ chối')}</span>
                <textarea rows={3} value={note} onChange={e => setNote(e.target.value)}
                          placeholder={mode === 'changes'
                            ? t('Ảnh bìa chụp chỗ khác, và lịch trình chưa có mốc thời gian.')
                            : t('Hoạt động này cần giấy phép lặn mà hồ sơ không có.')}
                          style={{ width: '100%', padding: '12px 14px', border: '1px solid var(--line)', borderRadius: 12, fontSize: 14 }} />
              </label>
              <div style={{ display: 'flex', gap: 8 }}>
                <button className="btn btn-primary btn-sm" disabled={busy || !note.trim()}
                        onClick={() => send(mode)}>
                  {busy ? t('Đang gửi…') : t('Gửi cho chủ trải nghiệm')}
                </button>
                <button className="btn btn-outline btn-sm" onClick={() => { setMode(null); setNote(''); }}>{t('Huỷ')}</button>
              </div>
            </div>
          )}
        </div>
      </div>

      <div className="host-booking-actions">
        <button className="btn btn-primary btn-sm" disabled={busy} onClick={() => send('approve')}>{t('Duyệt')}</button>
        <button className="btn btn-outline btn-sm" onClick={() => setMode(mode === 'changes' ? null : 'changes')}>{t('Yêu cầu sửa')}</button>
        <button className="btn btn-outline btn-sm" onClick={() => setMode(mode === 'reject' ? null : 'reject')}>{t('Từ chối')}</button>
        <button className="btn btn-outline btn-sm" onClick={() => navigate(`/experiences/${x.slug}`)}>{t('Xem')}</button>
      </div>
    </article>
  );
}

/**
 * docs/09 §2.3 — what the host filed. A high-risk activity needs licence,
 * insurance and an emergency number; from medium up it needs a safety plan. The
 * server refuses to approve without them, so the marks here say in advance which
 * gap is the one that will block.
 */
function Checklist({ x }) {
  const high = x.riskLabel === 'Rủi ro cao';
  const medium = x.riskLabel === 'Rủi ro trung bình';

  return (
    <div style={{ marginTop: 10, display: 'grid', gap: 4 }}>
      <Paper label={t('Giấy phép hành nghề')} value={x.licenceName} expiresOn={x.licenceExpiresOn} required={high} />
      <Paper label={t('Bảo hiểm trách nhiệm')} value={x.insurancePolicy} expiresOn={x.insuranceExpiresOn} required={high} />
      <Paper label={t('Cam kết an toàn')} value={x.safetyPlan} required={high || medium} />
      <Paper label={t('Số điện thoại khẩn cấp')} value={x.emergencyPhone} required={high} />
      <div className="meta" style={{ display: 'flex', gap: 8 }}>
        <span aria-hidden style={{ fontWeight: 700, color: 'var(--ink-muted)' }}>·</span>
        <span>
          <b style={{ color: 'var(--ink)' }}>{t('Trẻ em')}:</b>{' '}
          {x.allowsChildren ? t('có tham gia — nâng một bậc rủi ro') : t('không nhận trẻ em')}
        </span>
      </div>
    </div>
  );
}

/** One line of paperwork: what it is, what was filed, and until when. */
function Paper({ label, value, expiresOn, required }) {
  const expired = !!value && !!expiresOn && expiresOn < todayIso();
  const ok = !!value && !expired;

  return (
    <div className="meta" style={{ display: 'flex', gap: 8 }}>
      <span aria-hidden style={{
        fontWeight: 700,
        color: ok ? '#12734a' : required ? 'var(--brand)' : 'var(--ink-muted)'
      }}>{ok ? '✓' : '✕'}</span>
      <span>
        <b style={{ color: 'var(--ink)' }}>{label}:</b>{' '}
        {value || t('chưa nộp')}
        {value && expiresOn && ` · ${t('hết hạn')} ${longDate(expiresOn)}`}
        {expired && <span className="badge danger" style={{ marginLeft: 6 }}>{t('Đã hết hạn')}</span>}
        {!value && required && <span className="badge danger" style={{ marginLeft: 6 }}>{t('Bắt buộc')}</span>}
      </span>
    </div>
  );
}

const daysSince = at => (at ? Math.max(0, Math.floor((Date.now() - new Date(at)) / 86400000)) : null);

/**
 * docs/09 TN-A — the deadline is five working days from submission, so Saturday
 * and Sunday do not count against the reviewer.
 */
function workingDaysAfter(at, days) {
  if (!at || !days) return null;

  const d = new Date(at);
  for (let left = days; left > 0;) {
    d.setDate(d.getDate() + 1);
    if (d.getDay() !== 0 && d.getDay() !== 6) left--;
  }

  const iso = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  return iso;
}

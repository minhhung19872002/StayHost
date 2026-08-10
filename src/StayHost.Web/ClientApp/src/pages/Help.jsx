import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api } from '../lib/api.js';
import { toast } from '../lib/store.js';
import { longDate } from '../lib/format.js';
import { t } from '../lib/i18n.js';

const AUDIENCES = [['all', 'Tất cả'], ['guest', 'Khách'], ['host', 'Chủ nhà']];

/**
 * docs/01 AT-09 — reach a human support agent when the articles do not settle it.
 * A safety topic is flagged urgent and jumps the support queue.
 */
function ContactSupport() {
  const [topics, setTopics] = useState([]);
  const [topic, setTopic] = useState('booking');
  const [subject, setSubject] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(null);

  useEffect(() => { api.supportTopics().then(setTopics).catch(() => {}); }, []);

  const submit = async () => {
    setBusy(true);
    try {
      const r = await api.createSupportTicket({ topic, subject, message });
      setSent(r.message); setSubject(''); setMessage('');
    } catch (err) { toast(err.message); }
    finally { setBusy(false); }
  };

  return (
    <section style={{ marginTop: 44, borderTop: '1px solid var(--divider)', paddingTop: 28, maxWidth: 640 }}>
      <h2 className="section-title" style={{ fontSize: 20 }}>{t('Vẫn cần trợ giúp?')}</h2>
      <p className="section-sub">{t('Chuyển tiếp lên nhân viên hỗ trợ. Vấn đề an toàn khẩn cấp được ưu tiên cao nhất.')}</p>

      {sent ? (
        <div className="notice notice-ok" style={{ marginTop: 12 }}>{sent}</div>
      ) : (
        <div style={{ display: 'grid', gap: 10, marginTop: 12 }}>
          <label className="form-field"><span className="cap">{t('Loại vấn đề')}</span>
            <select value={topic} onChange={e => setTopic(e.target.value)}
                    style={{ padding: '10px 12px', border: '1px solid var(--line)', borderRadius: 10 }}>
              {topics.map(tp => <option key={tp.key} value={tp.key}>{tp.label}</option>)}
            </select>
          </label>
          <label className="form-field"><span className="cap">{t('Tiêu đề')}</span>
            <input value={subject} maxLength={150} onChange={e => setSubject(e.target.value)}
                   placeholder={t('Tóm tắt ngắn gọn vấn đề')} /></label>
          <label className="form-field"><span className="cap">{t('Mô tả')}</span>
            <textarea rows={4} value={message} maxLength={4000} onChange={e => setMessage(e.target.value)}
                      placeholder={t('Kể chi tiết để nhân viên hỗ trợ nắm được.')} /></label>
          <button className="btn btn-primary" disabled={busy || !subject.trim() || !message.trim()}
                  onClick={submit} style={{ justifySelf: 'start' }}>{t('Gửi cho nhân viên hỗ trợ')}</button>
        </div>
      )}
    </section>
  );
}

/**
 * docs/01 AT-07 — a help centre with real articles, a search that copes with
 * missing accents, and guest content kept apart from host content.
 */
export function Help() {
  const { slug } = useParams();
  return slug ? <Article slug={slug} /> : <Index />;
}

function Index() {
  const navigate = useNavigate();
  const [audience, setAudience] = useState('all');
  const [q, setQ] = useState('');
  const [data, setData] = useState(null);

  useEffect(() => {
    let live = true;
    // A short pause keeps every keystroke from becoming a request.
    const timer = setTimeout(() => {
      api.help({ q: q.trim() || undefined, audience: audience !== 'all' ? audience : undefined })
        .then(d => { if (live) setData(d); })
        .catch(e => toast(e.message));
    }, 180);
    return () => { live = false; clearTimeout(timer); };
  }, [q, audience]);

  const groups = data
    ? data.categories
        .map(c => ({ ...c, items: data.articles.filter(a => a.category === c.name) }))
        .filter(g => g.items.length)
    : [];

  return (
    <div className="shell" style={{ paddingBlock: '34px 90px' }}>
      <h1 className="section-title">{t('Trung tâm trợ giúp')}</h1>
      <p className="section-sub">{t('Tìm câu trả lời, hoặc nhắn cho chúng tôi nếu vẫn chưa rõ.')}</p>

      <div className="help-search">
        <input value={q} onChange={e => setQ(e.target.value)} autoComplete="off"
               placeholder={t('Bạn cần giúp gì? Ví dụ: huy dat cho, phi dich vu…')} />
      </div>

      <Assistant navigate={navigate} />

      <div className="seg-tabs" style={{ marginTop: 16 }}>
        {AUDIENCES.map(([key, label]) => (
          <button key={key} className={`seg-tab ${audience === key ? 'is-active' : ''}`}
                  onClick={() => setAudience(key)}>{t(label)}</button>
        ))}
      </div>

      {!data ? (
        <div className="stat skeleton" style={{ height: 260, border: 0, marginTop: 24 }} />
      ) : groups.length ? (
        <div style={{ marginTop: 28, display: 'grid', gap: 34 }}>
          {groups.map(g => (
            <section key={g.name}>
              <h2 className="section-title" style={{ fontSize: 20 }}>{g.name}</h2>
              <div className="help-grid">
                {g.items.map(a => (
                  <button className="help-card" key={a.slug} onClick={() => navigate(`/help/${a.slug}`)}>
                    <b>{a.title}</b>
                    <span>{a.summary}</span>
                    <i>{a.audienceLabel}</i>
                  </button>
                ))}
              </div>
            </section>
          ))}
        </div>
      ) : (
        <div className="empty-state" style={{ marginTop: 28 }}>
          <h3>{t('Không tìm thấy bài viết nào')}</h3>
          <p>{t('Thử một từ khoá khác, hoặc bỏ bớt bộ lọc bên trên.')}</p>
        </div>
      )}

      <ContactSupport />
    </div>
  );
}

function Article({ slug }) {
  const navigate = useNavigate();
  const [article, setArticle] = useState(null);
  const [missing, setMissing] = useState(false);

  useEffect(() => {
    setArticle(null);
    setMissing(false);
    api.helpArticle(slug).then(setArticle).catch(() => setMissing(true));
  }, [slug]);

  if (missing) {
    return (
      <div className="shell" style={{ paddingBlock: '34px 90px' }}>
        <div className="empty-state">
          <h3>{t('Không có bài viết này')}</h3>
          <button className="btn btn-primary" style={{ marginTop: 18 }}
                  onClick={() => navigate('/help')}>{t('Về trung tâm trợ giúp')}</button>
        </div>
      </div>
    );
  }

  if (!article) {
    return <div className="shell" style={{ paddingBlock: '34px 90px' }}>
      <div className="sk-line skeleton" style={{ width: 280, height: 26 }} />
    </div>;
  }

  return (
    <div className="shell shell-narrow" style={{ paddingBlock: '30px 90px' }}>
      <button className="back-link" onClick={() => navigate('/help')}>← {t('Trung tâm trợ giúp')}</button>

      <h1 className="section-title" style={{ marginTop: 10 }}>{article.title}</h1>
      <p className="section-sub">
        {article.category} · {article.audienceLabel} · {t('cập nhật')} {longDate(article.updatedAt.slice(0, 10))}
      </p>

      <div className="help-body">
        {article.body.split(/\n{2,}/).map((block, i) =>
          block.trimStart().startsWith('- ')
            ? <ul key={i}>{block.split('\n').map((line, j) => <li key={j}>{line.replace(/^\s*-\s*/, '')}</li>)}</ul>
            : <p key={i}>{block}</p>)}
      </div>

      <div className="help-foot">
        <b>{t('Vẫn chưa rõ?')}</b>
        <span>{t('Nhắn cho đội hỗ trợ, hoặc mở một yêu cầu trong Trung tâm giải quyết.')}</span>
        <div style={{ display: 'flex', gap: 10, marginTop: 12, flexWrap: 'wrap' }}>
          <button className="btn btn-primary btn-sm" onClick={() => navigate('/messages')}>{t('Nhắn cho hỗ trợ')}</button>
          <button className="btn btn-outline btn-sm" onClick={() => navigate('/resolutions')}>{t('Trung tâm giải quyết')}</button>
        </div>
      </div>
    </div>
  );
}

/**
 * docs/01 AT-08 — the automated assistant: reads the caller's situation and offers
 * the next useful action, each a button. Renders nothing until it has something.
 */
function Assistant({ navigate }) {
  const [rows, setRows] = useState(null);
  useEffect(() => { api.supportAssistant().then(r => setRows(r.suggestions)).catch(() => setRows([])); }, []);
  if (!rows || rows.length === 0) return null;

  return (
    <section className="assistant-card" style={{ marginTop: 20, padding: 16, background: 'var(--surface-2,#f6f6f6)', borderRadius: 14 }}>
      <h2 className="section-title" style={{ fontSize: 16, marginBottom: 4 }}>{t('Trợ lý StayHost')}</h2>
      <p className="section-sub" style={{ marginTop: 0 }}>{t('Gợi ý theo tình huống hiện tại của bạn:')}</p>
      <div style={{ display: 'grid', gap: 8, marginTop: 8 }}>
        {rows.map((s, i) => (
          <div key={i} style={{ display: 'flex', gap: 10, alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap' }}>
            <span className="meta" style={{ flex: 1, minWidth: 180 }}>{s.text}</span>
            <button className="btn btn-outline btn-sm" onClick={() => navigate(s.actionLink)}>{s.actionLabel}</button>
          </div>
        ))}
      </div>
    </section>
  );
}

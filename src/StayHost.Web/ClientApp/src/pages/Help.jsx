import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api } from '../lib/api.js';
import { toast } from '../lib/store.js';
import { longDate } from '../lib/format.js';

const AUDIENCES = [['all', 'Tất cả'], ['guest', 'Khách'], ['host', 'Chủ nhà']];

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
      <h1 className="section-title">Trung tâm trợ giúp</h1>
      <p className="section-sub">Tìm câu trả lời, hoặc nhắn cho chúng tôi nếu vẫn chưa rõ.</p>

      <div className="help-search">
        <input value={q} onChange={e => setQ(e.target.value)} autoComplete="off"
               placeholder="Bạn cần giúp gì? Ví dụ: huy dat cho, phi dich vu…" />
      </div>

      <div className="seg-tabs" style={{ marginTop: 16 }}>
        {AUDIENCES.map(([key, label]) => (
          <button key={key} className={`seg-tab ${audience === key ? 'is-active' : ''}`}
                  onClick={() => setAudience(key)}>{label}</button>
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
          <h3>Không tìm thấy bài viết nào</h3>
          <p>Thử một từ khoá khác, hoặc bỏ bớt bộ lọc bên trên.</p>
        </div>
      )}
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
          <h3>Không có bài viết này</h3>
          <button className="btn btn-primary" style={{ marginTop: 18 }}
                  onClick={() => navigate('/help')}>Về trung tâm trợ giúp</button>
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
      <button className="back-link" onClick={() => navigate('/help')}>← Trung tâm trợ giúp</button>

      <h1 className="section-title" style={{ marginTop: 10 }}>{article.title}</h1>
      <p className="section-sub">
        {article.category} · {article.audienceLabel} · cập nhật {longDate(article.updatedAt.slice(0, 10))}
      </p>

      <div className="help-body">
        {article.body.split(/\n{2,}/).map((block, i) =>
          block.trimStart().startsWith('- ')
            ? <ul key={i}>{block.split('\n').map((line, j) => <li key={j}>{line.replace(/^\s*-\s*/, '')}</li>)}</ul>
            : <p key={i}>{block}</p>)}
      </div>

      <div className="help-foot">
        <b>Vẫn chưa rõ?</b>
        <span>Nhắn cho đội hỗ trợ, hoặc mở một yêu cầu trong Trung tâm giải quyết.</span>
        <div style={{ display: 'flex', gap: 10, marginTop: 12, flexWrap: 'wrap' }}>
          <button className="btn btn-primary btn-sm" onClick={() => navigate('/messages')}>Nhắn cho hỗ trợ</button>
          <button className="btn btn-outline btn-sm" onClick={() => navigate('/resolutions')}>Trung tâm giải quyết</button>
        </div>
      </div>
    </div>
  );
}

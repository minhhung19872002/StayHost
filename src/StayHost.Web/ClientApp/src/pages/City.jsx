import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { api } from '../lib/api.js';
import { Card } from '../components/Card.jsx';
import { t } from '../lib/i18n.js';

/**
 * docs/01 TM-26 — a landing page for one city, so a visitor arriving from a
 * search engine sees real content and a way into the catalogue rather than an
 * empty search box.
 */
export function City() {
  const { city } = useParams();
  const navigate = useNavigate();
  const [page, setPage] = useState(null);
  const [missing, setMissing] = useState(false);

  useEffect(() => {
    setPage(null);
    setMissing(false);
    api.city(city).then(setPage).catch(() => setMissing(true));
  }, [city]);

  if (missing) {
    return (
      <div className="shell" style={{ paddingBlock: '40px 90px' }}>
        <div className="empty-state">
          <h3>{t('Chưa có chỗ nghỉ ở nơi này')}</h3>
          <p>{t('StayHost chưa có tin đăng nào cho thành phố bạn tìm.')}</p>
          <button className="btn btn-primary" style={{ marginTop: 18 }} onClick={() => navigate('/')}>{t('Về trang chủ')}</button>
        </div>
      </div>
    );
  }

  if (!page) {
    return <div className="shell" style={{ paddingBlock: '40px 90px' }}>
      <div className="sk-line skeleton" style={{ width: 280, height: 30 }} />
    </div>;
  }

  return (
    <div className="shell" style={{ paddingBlock: '28px 80px' }}>
      <h1 className="section-title" style={{ fontSize: 28 }}>{t('Chỗ nghỉ tại')} {page.city}</h1>
      <p className="section-sub" style={{ maxWidth: 720, fontSize: 15, lineHeight: 1.6 }}>{page.blurb}</p>
      <p className="section-sub">{page.count} {t('chỗ nghỉ đang mở đặt')}</p>

      <div className="card-grid" style={{ marginTop: 20 }}>
        {page.listings.map(c => <Card key={c.id} card={c} lazy />)}
      </div>

      <div style={{ marginTop: 28 }}>
        <button className="btn btn-dark" onClick={() => navigate(`/?q=${encodeURIComponent(page.city)}`)}>
          {t('Xem tất cả chỗ nghỉ ở')} {page.city}
        </button>
      </div>
    </div>
  );
}

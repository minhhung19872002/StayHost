import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { t } from '../lib/i18n.js';
import { setPageMeta, setNoIndex } from '../lib/seo.js';

/**
 * The page for an address with nothing behind it.
 *
 * There was none until now: an unknown address rendered the home page, and an
 * unknown listing rendered the loading skeleton forever. Both answered 200, so
 * a crawler filed them as real pages — the soft 404 described in Program.cs.
 * The server sets the status; this sets what a person sees, and repeats the
 * refusal in a meta tag for the case the server never saw the address at all
 * because the app navigated here on its own.
 */
export function NotFound({ title, body }) {
  const navigate = useNavigate();

  useEffect(() => {
    setPageMeta({ title: `${t('Không tìm thấy trang')} | Staylio` });
    setNoIndex(true);
    return () => setNoIndex(false);
  }, []);

  return (
    <div className="shell" style={{ paddingBlock: '60px 110px' }}>
      <div className="empty-state">
        <h3>{title || t('Không tìm thấy trang')}</h3>
        <p>{body || t('Địa chỉ này không còn, hoặc chưa bao giờ có. Tin đăng đã gỡ cũng dẫn tới đây.')}</p>
        <div style={{ display: 'flex', gap: 10, justifyContent: 'center', marginTop: 18, flexWrap: 'wrap' }}>
          <button className="btn btn-primary" onClick={() => navigate('/')}>{t('Về trang chủ')}</button>
          <button className="btn btn-dark" onClick={() => navigate('/help')}>{t('Trung tâm trợ giúp')}</button>
        </div>
      </div>
    </div>
  );
}

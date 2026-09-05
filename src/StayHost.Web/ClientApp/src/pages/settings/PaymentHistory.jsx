import { useEffect, useState } from 'react';
import { api } from '../../lib/api.js';
import { toast } from '../../lib/store.js';
import { money, dateTime } from '../../lib/format.js';
import { t } from '../../lib/i18n.js';

/*
 * docs/02 F1 — "lịch sử trả", the one line of the Thanh toán group that had no
 * screen anywhere. The server returns amounts exactly as stored; this renders
 * them exactly as returned. Status labels and method names are server-composed
 * Vietnamese, so they go through t() at the render site — the "Nơi này có
 * những gì" lesson, where the items translated and the group headings did not.
 */
const KIND_LABEL = {
  stay: 'Chỗ ở',
  experience: 'Trải nghiệm',
  service: 'Dịch vụ',
  'gift-card': 'Thẻ quà tặng',
};

const METHOD_LABEL = {
  card: 'Thẻ tín dụng / ghi nợ',
  napas: 'Thẻ ATM nội địa',
  momo: 'Ví MoMo',
  zalopay: 'ZaloPay',
  balance: 'Số dư',
  banktransfer: 'Chuyển khoản',
  property: 'Trả tại nơi ở',
};

export function PaymentHistory() {
  const [rows, setRows] = useState(null);

  useEffect(() => {
    api.paymentHistory().then(setRows).catch(err => { toast(err.message); setRows([]); });
  }, []);

  if (!rows) return <div className="stat skeleton" style={{ height: 120, border: 0, marginTop: 16 }} />;

  if (!rows.length) {
    return <p className="section-sub" style={{ marginTop: 12 }}>{t('Chưa có khoản thanh toán nào.')}</p>;
  }

  return (
    <div className="table-wrap" style={{ marginTop: 16 }}>
      <table className="admin-table">
        <thead>
          <tr>
            <th>{t('Thời gian')}</th>
            <th>{t('Khoản')}</th>
            <th style={{ textAlign: 'right' }}>{t('Số tiền')}</th>
            <th>{t('Phương thức')}</th>
            <th>{t('Trạng thái')}</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {rows.map(r => (
            <tr key={`${r.kind}-${r.reference}`}>
              <td style={{ whiteSpace: 'nowrap' }}>{dateTime(r.at)}</td>
              <td>
                <b>{r.title}</b>
                <div className="meta">{t(KIND_LABEL[r.kind] ?? r.kind)} · {r.reference}</div>
              </td>
              <td style={{ textAlign: 'right', whiteSpace: 'nowrap' }}><b>{money(r.amount)}</b></td>
              <td>
                {r.method ? t(METHOD_LABEL[r.method] ?? r.method) : '—'}
                {r.cardLast4 && <span className="meta"> ····{r.cardLast4}</span>}
              </td>
              <td>{t(r.statusLabel)}</td>
              <td>
                {r.bookingId && (
                  <a className="link-btn" href={`/api/bookings/${r.bookingId}/invoice`}
                     target="_blank" rel="noreferrer">{t('Xem hoá đơn')}</a>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

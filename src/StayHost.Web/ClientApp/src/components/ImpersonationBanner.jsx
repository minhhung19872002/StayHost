import { useEffect, useState } from 'react';
import { api } from '../lib/api.js';
import { toast } from '../lib/store.js';

/**
 * docs/08 §7.5 — "Trên màn hình admin luôn hiện dải cảnh báo rõ đang ở chế độ
 * thay mặt và đang là ai."
 *
 * Sits above everything and never scrolls away: the whole risk of this mode is
 * an admin forgetting which account they are in. The countdown is the same
 * 30-minute limit the server enforces (§7.3), shown rather than assumed — when
 * it reaches zero the server has already ended the session, and this asks again
 * to confirm it rather than pretending on its own.
 */
export function ImpersonationBanner() {
  const [session, setSession] = useState(null);
  const [left, setLeft] = useState(0);

  const check = async () => {
    try {
      const current = await api.adminImpersonationCurrent();
      setSession(current ?? null);
      setLeft(current?.secondsLeft ?? 0);
    } catch { setSession(null); }
  };

  useEffect(() => { check(); }, []);

  // One tick a second for the countdown; one call a minute to the server, which
  // is the only thing that can actually say the session is over.
  useEffect(() => {
    if (!session) return undefined;

    const tick = setInterval(() => setLeft(s => Math.max(0, s - 1)), 1000);
    const poll = setInterval(check, 60_000);

    return () => { clearInterval(tick); clearInterval(poll); };
  }, [session]);

  useEffect(() => {
    if (session && left === 0) check();
  }, [left, session]);

  if (!session) return null;

  const end = async () => {
    try {
      await api.adminEndImpersonation();
      setSession(null);
      toast('Đã thoát chế độ thay mặt.');
    } catch (err) { toast(err.message); }
  };

  const minutes = Math.floor(left / 60);
  const seconds = String(left % 60).padStart(2, '0');

  return (
    <div className="impersonation-banner">
      <div className="shell">
        <b>Đang thao tác THAY MẶT {session.targetName}</b>
        <span>
          Admin: {session.adminName} · hồ sơ #{session.ticketId} · còn {minutes}:{seconds}
          {session.targetNotified ? ' · người dùng đã được thông báo' : ''}
        </span>
        <span className="impersonation-forbidden">
          Không được: {session.forbidden.join(', ')}.
        </span>
        <button className="btn btn-sm" onClick={end}>Thoát chế độ thay mặt</button>
      </div>
    </div>
  );
}

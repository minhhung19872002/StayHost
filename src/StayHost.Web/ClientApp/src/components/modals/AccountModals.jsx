import { useState } from 'react';
import { useStore } from '../../lib/useStore.js';
import {
  set, login, register, loadMe, saveProfile, submitReview,
  closeOverlay, toast, loadSessions, loadBookings, loadTrip, state as store
} from '../../lib/store.js';
import { api } from '../../lib/api.js';
import { money, longDate } from '../../lib/format.js';
import { Modal } from './Modal.jsx';

export function AuthModal() {
  const state = useStore();
  if (state.authMode === 'forgot') return <ForgotModal />;
  if (state.authMode === 'reset') return <ResetModal />;

  const isRegister = state.authMode === 'register';

  const submit = async e => {
    e.preventDefault();
    const f = e.currentTarget;
    const body = {
      email: f.email.value.trim(),
      password: f.password.value,
      fullName: f.fullName?.value?.trim() ?? '',
      phone: f.phone?.value?.trim() || null
    };
    const ok = await (isRegister ? register(body) : login(body));
    if (ok) { await loadMe(); closeOverlay(); }
  };

  return (
    <Modal title={isRegister ? 'Đăng ký' : 'Đăng nhập'} size="narrow">
      <h3 style={{ margin: '0 0 4px', fontSize: 20, fontWeight: 800 }}>Chào mừng đến StayHost</h3>
      <p style={{ margin: '0 0 18px', fontSize: 13.5, color: 'var(--ink-muted)' }}>
        {isRegister ? 'Tạo tài khoản để đặt chỗ, lưu yêu thích và cho thuê nhà.' : 'Đăng nhập để tiếp tục.'}
      </p>

      <form onSubmit={submit} noValidate id="auth-form">
        {isRegister && (
          <label className="form-field">
            <span className="cap">Họ và tên</span>
            <input type="text" name="fullName" autoComplete="name" placeholder="Nguyễn Văn A" required />
          </label>
        )}
        <label className="form-field">
          <span className="cap">Email</span>
          <input type="email" name="email" autoComplete="email" placeholder="ban@email.com" required />
        </label>
        <label className="form-field">
          <span className="cap">Mật khẩu</span>
          <input type="password" name="password" autoComplete={isRegister ? 'new-password' : 'current-password'}
                 placeholder="Tối thiểu 8 ký tự" required />
        </label>
        {isRegister && (
          <label className="form-field">
            <span className="cap">Số điện thoại <span style={{ fontWeight: 400 }}>(không bắt buộc)</span></span>
            <input type="tel" name="phone" autoComplete="tel" placeholder="0912 345 678" />
          </label>
        )}

        {state.authError && <div className="form-error">{state.authError}</div>}

        <button type="submit" className="btn btn-primary btn-block" style={{ marginTop: 6 }} disabled={state.authBusy}>
          {state.authBusy ? 'Đang xử lý…' : isRegister ? 'Tạo tài khoản' : 'Đăng nhập'}
        </button>
      </form>

      {!isRegister && (
        <p style={{ textAlign: 'right', margin: '-6px 0 0' }}>
          <button className="link-btn" style={{ fontWeight: 600, fontSize: 13 }}
                  onClick={() => set({ authMode: 'forgot', authError: null })}>Quên mật khẩu?</button>
        </p>
      )}

      <p style={{ textAlign: 'center', fontSize: 13.5, color: 'var(--ink-muted)', margin: '18px 0 0' }}>
        {isRegister ? 'Đã có tài khoản?' : 'Chưa có tài khoản?'}{' '}
        <button className="link-btn" onClick={() => set({ authMode: isRegister ? 'login' : 'register', authError: null })}>
          {isRegister ? 'Đăng nhập' : 'Đăng ký ngay'}
        </button>
      </p>

      <div style={{ marginTop: 22, padding: 14, background: 'var(--surface-soft)', borderRadius: 12 }}>
        <b style={{ fontSize: 12.5 }}>Tài khoản dùng thử</b>
        <div style={{ fontSize: 12.5, color: 'var(--ink-muted)', marginTop: 6, lineHeight: 1.6 }}>
          Khách: <code>guest@stayhost.vn</code><br />
          Chủ nhà: <code>host1@stayhost.vn</code><br />
          Mật khẩu: <code>stayhost123</code>
        </div>
        <button className="btn btn-outline btn-sm btn-block" style={{ marginTop: 10 }} onClick={() => {
          const form = document.getElementById('auth-form');
          if (!form) return;
          form.email.value = 'host1@stayhost.vn';
          form.password.value = 'stayhost123';
        }}>Điền tài khoản chủ nhà</button>
      </div>
    </Modal>
  );
}

function ForgotModal() {
  const state = useStore();

  const submit = async e => {
    e.preventDefault();
    const email = e.currentTarget.email.value.trim();
    set({ authBusy: true, authError: null });
    try {
      const res = await api.forgotPassword(email);
      const token = res.resetLink ? new URLSearchParams(res.resetLink.split('?')[1]).get('token') : null;
      set({
        resetLink: res.resetLink,
        resetToken: token,
        authError: res.resetLink ? null : 'Không tìm thấy tài khoản với email này.'
      });
    } catch (err) {
      set({ authError: err.message });
    } finally {
      set({ authBusy: false });
    }
  };

  return (
    <Modal title="Quên mật khẩu" size="narrow">
      <p style={{ margin: '0 0 18px', fontSize: 14, color: 'var(--ink-muted)', lineHeight: 1.6 }}>
        Nhập email của bạn, chúng tôi sẽ gửi liên kết đặt lại mật khẩu.
      </p>
      <form onSubmit={submit}>
        <label className="form-field">
          <span className="cap">Email</span>
          <input type="email" name="email" autoComplete="email" placeholder="ban@email.com" required />
        </label>
        {state.authError && <div className="form-error">{state.authError}</div>}
        {state.resetLink ? <>
          <div className="book-alert">
            <b>Liên kết đặt lại của bạn</b>
            <span>Bản demo không gửi email thật — bấm nút dưới để tiếp tục.</span>
          </div>
          <button type="button" className="btn btn-dark btn-block" style={{ marginTop: 12 }}
                  onClick={() => set({ authMode: 'reset', authError: null })}>
            Mở trang đặt lại mật khẩu
          </button>
        </> : (
          <button type="submit" className="btn btn-primary btn-block" disabled={state.authBusy}>
            {state.authBusy ? 'Đang gửi…' : 'Gửi liên kết'}
          </button>
        )}
      </form>
      <p style={{ textAlign: 'center', margin: '18px 0 0' }}>
        <button className="link-btn" onClick={() => set({ authMode: 'login', authError: null, resetLink: null })}>
          Quay lại đăng nhập
        </button>
      </p>
    </Modal>
  );
}

function ResetModal() {
  const state = useStore();

  const submit = async e => {
    e.preventDefault();
    const f = e.currentTarget;
    if (f.newPassword.value !== f.confirmPassword.value) {
      set({ authError: 'Hai mật khẩu không khớp.' });
      return;
    }
    set({ authBusy: true, authError: null });
    try {
      const user = await api.resetPassword({ token: state.resetToken, newPassword: f.newPassword.value });
      set({ user, overlay: null, resetLink: null, resetToken: null });
      toast('Đã đổi mật khẩu và đăng nhập lại.');
    } catch (err) {
      set({ authError: err.message });
    } finally {
      set({ authBusy: false });
    }
  };

  return (
    <Modal title="Đặt mật khẩu mới" size="narrow">
      <form onSubmit={submit}>
        <label className="form-field">
          <span className="cap">Mật khẩu mới</span>
          <input type="password" name="newPassword" autoComplete="new-password" placeholder="Tối thiểu 8 ký tự" required />
        </label>
        <label className="form-field">
          <span className="cap">Nhập lại mật khẩu</span>
          <input type="password" name="confirmPassword" autoComplete="new-password" required />
        </label>
        {state.authError && <div className="form-error">{state.authError}</div>}
        <button type="submit" className="btn btn-primary btn-block" disabled={state.authBusy}>
          {state.authBusy ? 'Đang lưu…' : 'Đặt mật khẩu mới'}
        </button>
      </form>
    </Modal>
  );
}

const PROFILE_TABS = [['profile', 'Hồ sơ'], ['security', 'Bảo mật'], ['devices', 'Thiết bị']];

export function ProfileModal() {
  const state = useStore();
  const u = state.user;
  if (!u) return null;
  const tab = state.profileTab;

  const pickTab = key => {
    set({ profileTab: key, authError: null });
    if (key === 'devices') loadSessions();
  };

  const verify = async () => {
    try {
      const res = await api.sendVerification();
      if (!res.verifyLink) { toast(res.message); return; }
      const token = new URLSearchParams(res.verifyLink.split('?')[1]).get('token');
      await api.verifyEmail(token);
      await loadMe();
      toast('Email đã được xác minh.');
    } catch (err) { toast(err.message); }
  };

  const changePassword = async e => {
    e.preventDefault();
    const f = e.currentTarget;
    if (f.newPassword.value !== f.confirmPassword.value) {
      set({ authError: 'Hai mật khẩu mới không khớp.' });
      return;
    }
    try {
      await api.changePassword({ currentPassword: f.currentPassword.value, newPassword: f.newPassword.value });
      closeOverlay();
      toast('Đã đổi mật khẩu.');
    } catch (err) { set({ authError: err.message }); }
  };

  return (
    <Modal title="Tài khoản">
      <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 18 }}>
        <span className="avatar" style={{ width: 56, height: 56, fontSize: 18 }}>{u.initials}</span>
        <div style={{ minWidth: 0 }}>
          <div style={{ fontSize: 17, fontWeight: 800 }}>{u.fullName}</div>
          <div style={{ fontSize: 13, color: 'var(--ink-muted)' }}>{u.email} · {u.joinedLabel}</div>
          <div style={{ marginTop: 6 }}>
            {u.emailConfirmed
              ? <span className="badge confirmed">Email đã xác minh</span>
              : <>
                  <span className="badge pending">Chưa xác minh email</span>
                  <button className="link-btn" style={{ marginLeft: 8, fontSize: 12.5 }} onClick={verify}>Gửi liên kết</button>
                </>}
          </div>
        </div>
      </div>

      <nav className="seg-tabs" style={{ marginBottom: 18 }}>
        {PROFILE_TABS.map(([key, label]) => (
          <button key={key} className={`seg-tab ${tab === key ? 'is-active' : ''}`} onClick={() => pickTab(key)}>{label}</button>
        ))}
      </nav>

      {tab === 'profile' && (
        <form onSubmit={e => {
          e.preventDefault();
          const f = e.currentTarget;
          saveProfile({
            fullName: f.fullName.value.trim(),
            phone: f.phone.value.trim() || null,
            bio: f.bio.value.trim() || null
          });
        }}>
          <label className="form-field"><span className="cap">Họ và tên</span>
            <input type="text" name="fullName" defaultValue={u.fullName} required /></label>
          <label className="form-field"><span className="cap">Số điện thoại</span>
            <input type="tel" name="phone" defaultValue={u.phone ?? ''} /></label>
          <label className="form-field"><span className="cap">Giới thiệu</span>
            <textarea name="bio" rows={4} defaultValue={u.bio ?? ''}
              style={{ width: '100%', padding: '12px 14px', border: '1px solid var(--line)', borderRadius: 12, fontSize: 14 }} /></label>
          <button type="submit" className="btn btn-primary btn-block">Lưu thay đổi</button>
        </form>
      )}

      {tab === 'security' && (
        <form onSubmit={changePassword}>
          <label className="form-field"><span className="cap">Mật khẩu hiện tại</span>
            <input type="password" name="currentPassword" autoComplete="current-password" required /></label>
          <label className="form-field"><span className="cap">Mật khẩu mới</span>
            <input type="password" name="newPassword" autoComplete="new-password" placeholder="Tối thiểu 8 ký tự" required /></label>
          <label className="form-field"><span className="cap">Nhập lại mật khẩu mới</span>
            <input type="password" name="confirmPassword" autoComplete="new-password" required /></label>
          {state.authError && <div className="form-error">{state.authError}</div>}
          <p style={{ fontSize: 12.5, color: 'var(--ink-muted)', lineHeight: 1.5, margin: '0 0 12px' }}>
            Đổi mật khẩu sẽ đăng xuất mọi thiết bị khác.
          </p>
          <button type="submit" className="btn btn-primary btn-block">Đổi mật khẩu</button>
        </form>
      )}

      {tab === 'devices' && (
        <div style={{ display: 'grid', gap: 10 }}>
          {(state.sessions ?? []).length ? state.sessions.map(s => (
            <div className="cal-row" key={s.id}>
              <div style={{ flex: 1, minWidth: 0 }}>
                <b style={{ fontSize: 14 }}>{s.device}</b>
                {s.isCurrent && <span className="badge confirmed" style={{ marginLeft: 8 }}>Thiết bị này</span>}
                <div style={{ fontSize: 12.5, color: 'var(--ink-muted)' }}>
                  Đăng nhập {longDate(s.createdAt.slice(0, 10))}
                </div>
              </div>
              {!s.isCurrent && (
                <button className="text-btn" onClick={async () => {
                  try { await api.revokeSession(s.id); await loadSessions(); toast('Đã đăng xuất thiết bị đó.'); }
                  catch (err) { toast(err.message); }
                }}>Đăng xuất</button>
              )}
            </div>
          )) : <p style={{ fontSize: 14, color: 'var(--ink-muted)' }}>Đang tải phiên đăng nhập…</p>}
        </div>
      )}
    </Modal>
  );
}

const REVIEW_FIELDS = [
  ['cleanliness', 'Mức độ sạch sẽ'], ['accuracy', 'Độ chính xác'], ['checkIn', 'Nhận phòng'],
  ['communication', 'Giao tiếp'], ['location', 'Vị trí'], ['value', 'Giá trị']
];

const BLANK_REVIEW = {
  rating: 5, cleanliness: 5, accuracy: 5, checkIn: 5,
  communication: 5, location: 5, value: 5, text: ''
};

export function ReviewModal() {
  const state = useStore();
  const b = state.reviewBooking;
  const [draft, setDraft] = useState(() => state.reviewDraft ?? BLANK_REVIEW);
  if (!b) return null;

  const stars = (field, small) => (
    <div className="star-row" data-field={field}>
      {[1, 2, 3, 4, 5].map(n => (
        <button type="button" key={n} aria-label={`${n} sao`}
                className={`star ${small ? 'sm' : ''} ${n <= draft[field] ? 'is-on' : ''}`}
                onClick={() => setDraft(d => ({ ...d, [field]: n }))}>★</button>
      ))}
    </div>
  );

  const submit = async e => {
    e.preventDefault();
    const text = e.currentTarget.text.value.trim();
    const ok = await submitReview(b.id, { bookingId: b.id, ...draft, text });
    if (ok) closeOverlay();
  };

  return (
    <Modal title="Đánh giá chuyến đi">
      <div style={{ display: 'flex', gap: 14, alignItems: 'center', paddingBottom: 18, borderBottom: '1px solid var(--divider)' }}>
        <img src={b.listingImage} alt="" style={{ width: 88, height: 66, objectFit: 'cover', borderRadius: 12 }} />
        <div style={{ minWidth: 0 }}>
          <div style={{ fontSize: 15, fontWeight: 700 }}>{b.listingTitle}</div>
          <div style={{ fontSize: 13, color: 'var(--ink-muted)' }}>{b.listingCity} · {b.nights} đêm</div>
        </div>
      </div>

      <form onSubmit={submit}>
        <div style={{ padding: '20px 0', borderBottom: '1px solid var(--divider)' }}>
          <b style={{ fontSize: 15 }}>Điểm tổng thể</b>
          <div style={{ marginTop: 10 }}>{stars('rating', false)}</div>
        </div>

        {REVIEW_FIELDS.map(([key, label]) => (
          <div className="count-row" key={key}>
            <div className="tx"><b>{label}</b></div>
            {stars(key, true)}
          </div>
        ))}

        <label className="form-field" style={{ marginTop: 20 }}>
          <span className="cap">Cảm nhận của bạn</span>
          <textarea name="text" rows={5} required minLength={10} defaultValue={draft.text}
                    placeholder="Chỗ nghỉ thế nào? Chủ nhà hỗ trợ ra sao?"
                    style={{ width: '100%', padding: '12px 14px', border: '1px solid var(--line)', borderRadius: 12, fontSize: 14 }} />
        </label>

        <button type="submit" className="btn btn-primary btn-block">Gửi đánh giá</button>
      </form>
    </Modal>
  );
}

export function CancelTripModal() {
  const state = useStore();
  const preview = state.cancelPreview;
  if (!preview) return null;

  const confirm = async () => {
    try {
      const result = await api.cancelBooking(preview.bookingId);
      set({ overlay: null, cancelPreview: null });
      toast(`Đã huỷ. Hoàn lại ${money(result.refund)}.`);
      await loadBookings();
      if (store.trip?.id === preview.bookingId) await loadTrip(preview.bookingId);
    } catch (err) { toast(err.message); }
  };

  return (
    <Modal title="Huỷ chuyến đi" size="narrow" foot={<>
      <button className="text-btn" onClick={closeOverlay}>Giữ chuyến đi</button>
      <button className="btn btn-primary btn-sm" onClick={confirm}>Xác nhận huỷ</button>
    </>}>
      <p style={{ margin: '0 0 18px', fontSize: 14.5, lineHeight: 1.6, color: 'var(--ink-body)' }}>
        {preview.explanation}
      </p>
      <div className="book-lines">
        <div className="book-line"><span>Đã thanh toán</span><span>{money(preview.total)}</span></div>
        <div className="book-line" style={{ color: 'var(--brand-dark)' }}>
          <span>Sẽ hoàn lại</span><span>{money(preview.refund)}</span>
        </div>
        <div className="book-rule" />
        <div className="book-total"><span>Không hoàn</span><span>{money(preview.penalty)}</span></div>
      </div>
    </Modal>
  );
}

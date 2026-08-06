/**
 * docs/01 TK-02 — Google, Apple and Facebook.
 *
 * Each provider insists on raising its own window: that is the whole point, since
 * the password is typed on their page and never on ours. So all this file does is
 * load their script, ask them to open that window, and hand back the signed thing
 * they give us. The server decides who it belongs to.
 */

import { api } from './api.js';

const scripts = new Map();

/** Loads a provider SDK once, however many times the modal is opened. */
function loadScript(src) {
  if (scripts.has(src)) return scripts.get(src);

  const promise = new Promise((resolve, reject) => {
    const el = document.createElement('script');
    el.src = src;
    el.async = true;
    el.onload = () => resolve();
    el.onerror = () => {
      // A failed load must not be remembered, or a flaky network would disable
      // the button for the rest of the session.
      scripts.delete(src);
      reject(new Error('Không tải được thư viện đăng nhập. Kiểm tra kết nối mạng.'));
    };
    document.head.appendChild(el);
  });

  scripts.set(src, promise);
  return promise;
}

let configPromise = null;

/** Which providers the server has credentials for, asked once per page load. */
export function externalConfig() {
  configPromise ??= api.externalConfig().catch(() => ({}));
  return configPromise;
}

/* ------------------------------------------------------------------ Google */

/**
 * Google only issues an identity token to its own rendered button, so this draws
 * theirs into `container` rather than styling one of ours. Clicking it opens the
 * account chooser everybody recognises.
 */
export async function mountGoogleButton(container, clientId, onCredential, onError) {
  await loadScript('https://accounts.google.com/gsi/client');

  const gsi = window.google?.accounts?.id;
  if (!gsi) throw new Error('Không tải được đăng nhập Google.');

  gsi.initialize({
    client_id: clientId,
    ux_mode: 'popup',
    // Chrome is retiring third-party cookies; without this the chooser can come
    // up empty on browsers that have already switched.
    use_fedcm_for_prompt: true,
    callback: response => {
      if (response?.credential) onCredential(response.credential);
      else onError?.(new Error('Google không trả về mã xác thực.'));
    }
  });

  container.innerHTML = '';
  gsi.renderButton(container, {
    type: 'standard',
    theme: 'outline',
    size: 'large',
    shape: 'pill',
    text: 'continue_with',
    locale: 'vi',
    logo_alignment: 'center',
    width: Math.round(container.getBoundingClientRect().width) || 320
  });
}

/* ------------------------------------------------------------------- Apple */

export async function signInWithApple({ servicesId, redirectUri }) {
  await loadScript('https://appleid.cdn-apple.com/appleauth/static/jsapi/appleid/1/vi_VN/appleid.auth.js');

  const AppleID = window.AppleID;
  if (!AppleID?.auth) throw new Error('Không tải được đăng nhập Apple.');

  AppleID.auth.init({
    clientId: servicesId,
    scope: 'name email',
    // Apple checks this against the Return URLs on the Services ID, character for
    // character, and refuses the whole request if it does not match.
    redirectURI: redirectUri,
    usePopup: true
  });

  try {
    const result = await AppleID.auth.signIn();
    const token = result?.authorization?.id_token;
    if (!token) throw new Error('Apple không trả về mã xác thực.');
    return token;
  } catch (err) {
    // Closing the window is a decision, not a fault; everything else is.
    if (err?.error === 'popup_closed_by_user' || err?.error === 'user_cancelled_authorize') return null;
    throw new Error(err?.error ? `Apple từ chối: ${err.error}` : 'Đăng nhập Apple thất bại.');
  }
}

/* ---------------------------------------------------------------- Facebook */

export async function signInWithFacebook({ appId }) {
  await loadScript('https://connect.facebook.net/vi_VN/sdk.js');

  const FB = window.FB;
  if (!FB) throw new Error('Không tải được đăng nhập Facebook.');

  FB.init({ appId, cookie: false, xfbml: false, version: 'v21.0' });

  return new Promise((resolve, reject) => {
    FB.login(response => {
      if (response?.authResponse?.accessToken) resolve(response.authResponse.accessToken);
      else if (response?.status === 'unknown') resolve(null);      // window closed
      else reject(new Error('Facebook không cấp quyền đăng nhập.'));
    }, { scope: 'public_profile,email' });
  });
}

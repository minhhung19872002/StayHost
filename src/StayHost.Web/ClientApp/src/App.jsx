import { useEffect, useState } from 'react';
import { Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { useStore } from './lib/useStore.js';
import {
  loadMeta, loadMe, loadFavorites, loadNotifications, set, state as store
} from './lib/store.js';
import { queryToSearch } from './lib/urlState.js';
import { setNavigator } from './lib/nav.js';

import { Header } from './components/Header.jsx';
import { Footer } from './components/Footer.jsx';
import { Overlay } from './components/modals/Overlay.jsx';
import { Toasts } from './components/Toasts.jsx';

import { Browse } from './pages/Browse.jsx';
import { Detail } from './pages/Detail.jsx';
import { Wishlists } from './pages/Wishlists.jsx';
import { Trips } from './pages/Trips.jsx';
import { Trip } from './pages/Trip.jsx';
import { Host } from './pages/Host.jsx';
import { Hosting } from './pages/Hosting.jsx';
import { Messages } from './pages/Messages.jsx';
import { Admin } from './pages/Admin.jsx';
import { Resolutions } from './pages/Resolutions.jsx';

export function App() {
  const state = useStore();
  const location = useLocation();
  const navigate = useNavigate();
  const [booted, setBooted] = useState(false);

  setNavigator(navigate);

  // One bootstrap pass: reference data first (the URL's price bounds are
  // relative to it), then the signed-in identity and its dependent feeds.
  useEffect(() => {
    (async () => {
      await loadMeta();
      queryToSearch(location.search);
      setBooted(true);
      await loadMe();
      await Promise.all([loadFavorites(), loadNotifications()]);
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // A route change closes whatever chrome was open and returns to the top.
  useEffect(() => {
    set({ overlay: null, menu: null, suggestOpen: false, photoIndex: null });
    window.scrollTo({ top: 0, behavior: 'instant' });
  }, [location.pathname]);

  // Leaving the room page drops its detail so a stale quote can't leak into
  // the next screen's totals.
  useEffect(() => {
    if (!location.pathname.startsWith('/rooms/') && store.detail) {
      set({ detail: null, quote: null, bookingResult: null, bookingError: null });
    }
  }, [location.pathname]);

  if (state.metaError) {
    return (
      <div className="shell" style={{ padding: '60px 0' }}>
        <div className="empty-state">
          <h3>Không kết nối được máy chủ</h3>
          <p>{state.metaError}</p>
        </div>
      </div>
    );
  }

  return <>
    <div id="app">
      <header className="site-header"><Header /></header>
      <main id="main">
        {booted && (
          <Routes>
            <Route path="/" element={<Browse />} />
            <Route path="/rooms/:slug" element={<Detail />} />
            <Route path="/wishlists" element={<Wishlists />} />
            <Route path="/trips" element={<Trips />} />
            <Route path="/trips/:id" element={<Trip />} />
            <Route path="/host" element={<Host />} />
            <Route path="/hosting" element={<Hosting />} />
            <Route path="/messages" element={<Messages />} />
            <Route path="/messages/:id" element={<Messages />} />
            <Route path="/resolutions" element={<Resolutions />} />
            <Route path="/admin" element={<Admin />} />
            <Route path="*" element={<Browse />} />
          </Routes>
        )}
      </main>
      <footer className="site-footer"><Footer /></footer>
    </div>

    <Overlay />
    <Toasts />
  </>;
}

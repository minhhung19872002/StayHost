// Leaflet lives outside React's tree on purpose: the map instance owns its own
// DOM and must survive result-set changes, so the component only ever renders
// an empty div and drives the map through effects.

import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { useStore } from '../lib/useStore.js';
import { useMedia } from '../lib/useMedia.js';
import { t } from '../lib/i18n.js';
import { money } from '../lib/format.js';

/*
 * Where the map pictures come from, and why not from OpenStreetMap directly.
 *
 * openstreetmap.org is blocked at DNS by the large Vietnamese ISPs. Measured on
 * 27/08/2026 against tile.openstreetmap.org: Viettel (123.23.23.23) returns
 * nothing, FPT (210.245.0.100) returns nothing, and VNPT (203.113.131.1)
 * answers 127.0.0.1 — a poisoned record, which is a deliberate block rather
 * than an outage. Cloudflare and Google resolve it fine, which is exactly why
 * this went unnoticed: it works for anyone who changed their DNS, and most
 * developers have. Every guest on a default home connection saw a grey
 * rectangle with a red circle on it and no map at all.
 *
 * Both providers below resolve on all of those networks, and neither needs a
 * key. Carto was tried first and dropped: its free raster tiles now arrive with
 * "API KEY REQUIRED" stamped across the picture, which reads as a broken site
 * rather than a missing map — worse than the blank it replaced. Checked by
 * looking at a real tile, not by trusting the 200.
 *
 * Two of them, because a whole provider vanishing behind a national block is no
 * longer hypothetical — it is the bug this comment exists to explain. Neither is
 * a paid plan, so if the map ever carries real traffic, get a key from MapTiler
 * or Stadia and make that the first entry.
 */
const PROVIDERS = [
  {
    // Esri's public basemap. Street names come through in Vietnamese, and the
    // infrastructure is sized for far more traffic than this. Note the axis
    // order: ArcGIS asks for {z}/{y}/{x}, not {z}/{x}/{y} like everyone else,
    // and getting it wrong returns valid tiles of the wrong place.
    url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}',
    attribution: 'Tiles &copy; Esri &mdash; Esri, DeLorme, NAVTEQ',
    maxZoom: 19,
  },
  {
    // Run by the German OpenStreetMap chapter, on a host the Vietnamese ISPs do
    // not block. Standard OSM cartography, more detail than Esri at high zoom.
    url: 'https://tile.openstreetmap.de/{z}/{x}/{y}.png',
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
    maxZoom: 18,
  },
];


/**
 * Adds the base layer, and swaps to the next provider if this one turns out to
 * be unreachable.
 *
 * Leaflet raises `tileerror` per failed tile, and a real failure fails every
 * tile at once, so a handful is already conclusive — while one or two errors
 * are just the edge of the world at low zoom and must not trigger a swap. The
 * swap happens once: if the fallback is unreachable too, the honest outcome is
 * an empty map rather than an endless cycle between two dead hosts.
 */
function addBaseLayer(map, index = 0) {
  const provider = PROVIDERS[index];
  if (!provider) return null;

  const layer = L.tileLayer(provider.url, {
    attribution: provider.attribution,
    maxZoom: provider.maxZoom,
  }).addTo(map);

  let failures = 0;
  layer.on('tileerror', () => {
    if (++failures < 4 || index + 1 >= PROVIDERS.length) return;
    layer.off('tileerror');
    map.removeLayer(layer);
    addBaseLayer(map, index + 1);
  });

  return layer;
}

/*
 * Leaflet anchors the top-left when the box changes size, so the geographic
 * centre drifts by half the delta. That is why a listing's circle could end up
 * off in a corner: the map is built, setView centres it on the place, and then
 * the column settles a few pixels wider once the photos above it have loaded.
 * The same drift on the way in and out of full screen.
 *
 * A 60ms timeout was the old guess at "when the layout is done". An observer
 * does not have to guess, and it holds the centre rather than the corner.
 */
function keepCentred(map) {
  const fix = () => {
    if (!map._loaded) return;
    const centre = map.getCenter();
    map.invalidateSize({ pan: false, animate: false });
    map.setView(centre, map.getZoom(), { animate: false });
  };
  const ro = new ResizeObserver(fix);
  ro.observe(map.getContainer());
  return () => ro.disconnect();
}

/*
 * Full screen without the Fullscreen API. iOS Safari grants it to <video> and
 * nothing else, so requestFullscreen fails silently on a large share of the
 * traffic this site actually gets. A fixed wrapper works everywhere; Leaflet
 * only has to be told the box changed size, and it has to be told after the
 * class has landed, not before.
 */
function useFullMap(full, setFull, mapRef) {
  useEffect(() => {
    // Nothing to do about the wheel: every map here has it from the start.
    // No invalidateSize here either — keepCentred's observer sees the box change
    // and holds the centre, which a bare invalidateSize would not.
    if (!full) return undefined;

    const onKey = e => { if (e.key === 'Escape') setFull(false); };
    document.addEventListener('keydown', onKey);
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    /*
     * The results pane sits in .split-map, which is `isolation: isolate;
     * z-index: 0` — a stacking context, so a fixed child painted inside it goes
     * under the cards and the header no matter what z-index it claims. The
     * class lets the stylesheet stand that ancestor down for as long as the map
     * owns the screen. A body class rather than :has(), because this is load-
     * bearing and every browser has had one of these for twenty years.
     */
    document.body.classList.add('is-map-full');
    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = prev;
      document.body.classList.remove('is-map-full');
    };
  }, [full, setFull, mapRef]);
}

/**
 * The expand button, shared by the two inline maps.
 *
 * There was a hint here — "bấm vào bản đồ rồi lăn chuột" — because the wheel
 * used to need a click first. The client asked for the wheel to work on hover,
 * so the click is gone and the hint has nothing left to explain. The cost is
 * the reason the gate existed: with the pointer over one of these maps the
 * wheel zooms rather than scrolls the page.
 */
function MapChrome({ full, setFull }) {
  return (
    <button type="button" className="map-expand" onClick={() => setFull(!full)}
            aria-label={full ? t('Thu nhỏ bản đồ') : t('Mở bản đồ toàn màn hình')}>
      {full ? '✕' : '⤡'}
    </button>
  );
}

/** docs/01 TM-10 — below this zoom, nearby pins merge into a count. */

const CLUSTER_ZOOM = 11;

const markersById = new Map();

/** Card hover lifts the matching price pin; the map's own hover does the reverse. */
export function setHoveredListing(id, on) {
  const el = markersById.get(Number(id))?.getElement()?.querySelector('.price-marker');
  el?.classList.toggle('is-active', on);
}

/**
 * Groups pins that would otherwise sit on top of each other. The grid size
 * shrinks as the map zooms in, so clusters break apart naturally.
 */
function cluster(items, zoom) {
  if (zoom >= CLUSTER_ZOOM) return items.map(i => ({ items: [i], lat: i.latitude, lng: i.longitude }));

  const cell = 6 / Math.pow(2, Math.max(0, zoom - 4));
  const buckets = new Map();

  for (const item of items) {
    const key = `${Math.round(item.latitude / cell)}:${Math.round(item.longitude / cell)}`;
    const bucket = buckets.get(key) ?? { items: [], lat: 0, lng: 0 };
    bucket.items.push(item);
    buckets.set(key, bucket);
  }

  return [...buckets.values()].map(b => ({
    items: b.items,
    lat: b.items.reduce((s, i) => s + i.latitude, 0) / b.items.length,
    lng: b.items.reduce((s, i) => s + i.longitude, 0) / b.items.length
  }));
}

export function ResultsMap({ onSearchArea, onDrawArea }) {
  const state = useStore();
  const navigate = useNavigate();
  const hostRef = useRef(null);
  const mapRef = useRef(null);
  const layerRef = useRef(null);
  const navigateRef = useRef(navigate);
  navigateRef.current = navigate;

  const [zoom, setZoom] = useState(5);
  const [moved, setMoved] = useState(false);
  /*
   * Below 1100px the stylesheet used to hide .split-map outright, so "Hiện bản
   * đồ" on a phone rendered the split, hid the map half of it, and gave back
   * the same list under a button that now read "Hiện danh sách". There was no
   * way to reach the map from a phone at all — the toggle only ever changed its
   * own label. A narrow screen has no room to share, so the map takes all of it.
   */
  const narrow = useMedia('(max-width: 1099px)');
  const [full, setFull] = useState(false);
  // The result set the view was last framed to; a zoom must not count as a new one.
  const fittedKeyRef = useRef(null);

  // docs/01 TM-24 — freehand-ish area draw: tap to drop vertices, finish to search.
  const [drawing, setDrawing] = useState(false);
  const drawRef = useRef({ points: [], layer: null, drawing: false });
  const drawAreaRef = useRef(onDrawArea);
  drawAreaRef.current = onDrawArea;

  const redrawPolygon = () => {
    const map = mapRef.current;
    const d = drawRef.current;
    if (d.layer) { map.removeLayer(d.layer); d.layer = null; }
    if (d.points.length >= 2) {
      d.layer = L.polygon(d.points.map(p => [p.lat, p.lng]),
        { color: '#e5484d', weight: 2, fillOpacity: 0.08 }).addTo(map);
    } else if (d.points.length === 1) {
      d.layer = L.circleMarker([d.points[0].lat, d.points[0].lng], { radius: 4, color: '#e5484d' }).addTo(map);
    }
  };

  const startDraw = () => {
    const map = mapRef.current;
    drawRef.current.points = [];
    redrawPolygon();
    drawRef.current.drawing = true;
    setDrawing(true);
    map.getContainer().style.cursor = 'crosshair';
  };

  const clearDraw = () => {
    const map = mapRef.current;
    const d = drawRef.current;
    if (d.layer) { map.removeLayer(d.layer); d.layer = null; }
    d.points = [];
    d.drawing = false;
    setDrawing(false);
    map.getContainer().style.cursor = '';
    if (state.searchPolygon) drawAreaRef.current?.(null);
  };

  const finishDraw = () => {
    const map = mapRef.current;
    const pts = drawRef.current.points.slice();
    drawRef.current.drawing = false;
    setDrawing(false);
    map.getContainer().style.cursor = '';
    if (pts.length >= 3) drawAreaRef.current?.(pts);
  };

  const searchAreaRef = useRef(onSearchArea);
  searchAreaRef.current = onSearchArea;

  useEffect(() => {
    // The one map that owns everything under the cursor: no page scrolls behind
    // it to trap, so the wheel needs no ceremony.
    //
    // Zoom moves to the top right, under the expand button, the way airbnb.com
    // stacks them — controls on both edges of one map read as two toolbars.
    const map = L.map(hostRef.current, { scrollWheelZoom: true, zoomControl: false })
      .setView([16.0, 107.5], 5);
    L.control.zoom({ position: 'topright' }).addTo(map);
    addBaseLayer(map);

    mapRef.current = map;
    layerRef.current = L.layerGroup().addTo(map);

    /*
     * docs/01 TM-12 — the search re-runs whenever the guest moves the map, the
     * way airbnb.com/s does it. It used to be a checkbox, defaulted off, sitting
     * in a white bar across the top of the map with two buttons for company; the
     * client asked for the bar gone and this behaviour on by default.
     * `refitting` still suppresses it when it was our own fitBounds that moved
     * the map, or the search would answer itself in a loop.
     */
    const onMoveEnd = () => {
      setZoom(map.getZoom());
      if (map.__refitting) { map.__refitting = false; return; }
      setMoved(true);
    };
    map.on('moveend', onMoveEnd);

    // docs/01 TM-24 — while drawing, each tap drops a vertex.
    const onClick = e => {
      if (!drawRef.current.drawing) return;
      drawRef.current.points.push({ lat: e.latlng.lat, lng: e.latlng.lng });
      redrawPolygon();
    };
    map.on('click', onClick);

    // The pane is laid out by CSS grid and changes size on the way in and out of
    // full screen; the observer covers both without guessing at a delay.
    const stopCentring = keepCentred(map);

    return () => {
      stopCentring();
      map.off('moveend', onMoveEnd);
      map.off('click', onClick);
      map.remove();
      mapRef.current = null;
      markersById.clear();
    };
  }, []);

  const items = state.results.items.filter(i => i.latitude && i.longitude);
  // Re-pinning is keyed on the result set so unrelated state changes (a ♥ click,
  // a currency switch) never rebuild markers or re-fit the view.
  const key = items.map(i => `${i.id}:${i.pricePerNight}`).join(',');

  useEffect(() => {
    const map = mapRef.current;
    const layer = layerRef.current;
    if (!map || !layer) return undefined;

    layer.clearLayers();
    markersById.clear();
    if (!items.length) return undefined;

    for (const group of cluster(items, map.getZoom())) {
      if (group.items.length > 1) {
        const marker = L.marker([group.lat, group.lng], {
          icon: L.divIcon({ className: '', html: `<span class="cluster-marker">${group.items.length}</span>`, iconSize: null })
        });
        marker.on('click', () => map.setView([group.lat, group.lng], Math.max(map.getZoom() + 3, CLUSTER_ZOOM)));
        marker.bindTooltip(`${group.items.length} chỗ nghỉ`, { direction: 'top', offset: [0, -8] });
        marker.addTo(layer);
        continue;
      }

      const item = group.items[0];
      const marker = L.marker([item.latitude, item.longitude], {
        icon: L.divIcon({
          className: '',
          html: `<span class="price-marker">${money(item.pricePerNight)}</span>`,
          iconSize: null
        })
      });
      marker.on('click', () => navigateRef.current(`/rooms/${item.slug}`));
      marker.on('mouseover', () => highlightCard(item.id, true));
      marker.on('mouseout', () => highlightCard(item.id, false));
      marker.bindTooltip(item.title, { direction: 'top', offset: [0, -8] });
      marker.addTo(layer);
      markersById.set(item.id, marker);
    }

    /*
     * Only re-frame when the result set itself changed, never on a zoom tweak —
     * which is what the line below always claimed and the dependency array
     * never allowed. `zoom` is in the deps because the pins re-cluster as you
     * zoom, and it is state set on every moveend, so each zoom re-ran this
     * effect and fitBounds put the map straight back. The wheel, the +/- buttons
     * and a cluster tap were all equally powerless: the map moved and snapped
     * home inside one frame, which reads exactly like a map that cannot zoom.
     * Re-clustering still follows the zoom; the framing now follows the results.
     */
    if (!state.searchArea && fittedKeyRef.current !== key) {
      fittedKeyRef.current = key;
      map.__refitting = true;
      map.fitBounds(items.map(i => [i.latitude, i.longitude]), { padding: [50, 50], maxZoom: 12 });
      setMoved(false);
    }

    return undefined;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, zoom]);

  const searchHere = () => {
    const b = mapRef.current?.getBounds();
    if (!b) return;
    setMoved(false);
    searchAreaRef.current?.({
      south: b.getSouth(), west: b.getWest(), north: b.getNorth(), east: b.getEast()
    });
  };

  /*
   * Moving the map is the search. Not while a polygon is being drawn: the taps
   * that place its vertices move nothing, but finishing it does.
   *
   * Debounced, because a wheel zoom is several moveends in a row and each one
   * would otherwise be its own query — eight requests for one gesture, and the
   * results flickering through eight answers on the way to the one that counts.
   */
  useEffect(() => {
    if (!moved || drawing) return undefined;
    const t = setTimeout(searchHere, 400);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [moved, drawing]);

  // docs/01 TM-24 — the draw tool now lives in the filter sheet, which closes
  // itself and asks the map to start. A counter rather than a flag, so asking
  // twice in a row works.
  useEffect(() => {
    if (state.drawRequest) startDraw();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state.drawRequest]);

  useFullMap(full, setFull, mapRef);

  /*
   * Two different full screens, and they are not the same gesture.
   *
   * `is-full` is the expand button: the map takes everything, header included,
   * and Escape gives it back. `is-mobile` is the map *view* — the list/map pill
   * is how you leave, so the map has to stop short of the header and stay under
   * that pill. Painting over it would strand the reader on a map with no way
   * back, which is worse than no map at all.
   */
  useEffect(() => {
    if (!narrow) return undefined;
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = prev; };
  }, [narrow]);

  return (
    <div className={`map-box is-pane ${full ? 'is-full' : ''} ${narrow ? 'is-mobile' : ''}`}>
      {/* On a phone the map is already the whole screen and the list/map pill is
          the way back, so a second control saying the same thing is noise. */}
      {!narrow && (
        <button type="button" className="map-expand" onClick={() => setFull(!full)}
                aria-label={full ? t('Thu nhỏ bản đồ') : t('Mở bản đồ toàn màn hình')}>
          {full ? '✕' : '⤡'}
        </button>
      )}
      {/*
        * Nothing across the top of the map unless the moment calls for it. The
        * bar used to be permanent — two buttons and a checkbox in a white pill
        * over the part of the map you were looking at — and none of it had to
        * be: moving the map searches by itself, and drawing is started from the
        * filter sheet. What is left is only ever on screen while it applies.
        */}
      {(drawing || state.searchPolygon) && (
        <div className="map-search-again">
          {drawing && <button onClick={finishDraw}>✓ {t('Xong')} ({drawRef.current.points.length})</button>}
          {drawing && <button onClick={clearDraw}>{t('Huỷ')}</button>}
          {drawing && <span className="map-draw-hint">{t('Chạm để thêm điểm, rồi bấm Xong')}</span>}
          {!drawing && state.searchPolygon &&
            <button onClick={clearDraw}>✕ {t('Bỏ vùng đã vẽ')}</button>}
        </div>
      )}
      <div id="map" ref={hostRef} />
    </div>
  );
}

function highlightCard(id, on) {
  document.querySelector(`[data-listing="${id}"]`)?.classList.toggle('is-hovered', on);
}

/** Airbnb shows an approximate circle on the room page, never the exact address. */
/**
 * docs/01 YT-04 — a plain map of a given set of cards.
 *
 * ResultsMap cannot do this job: it reads `state.results` directly and carries
 * the whole search-as-I-move apparatus, none of which a wishlist has. This one
 * takes the cards it is handed, so any screen with a list of places can show
 * where they are.
 */
export function CardsMap({ cards, height = 320 }) {
  const hostRef = useRef(null);
  const mapRef = useRef(null);
  const [full, setFull] = useState(false);
  const navigate = useNavigate();
  const navigateRef = useRef(navigate);
  navigateRef.current = navigate;

  const pinned = (cards ?? []).filter(c => c.latitude && c.longitude);
  // Re-pin only when the set itself changes — a ♥ toggle must not rebuild the map.
  const key = pinned.map(c => c.id).join(',');

  useEffect(() => {
    if (!pinned.length) return undefined;

    const map = L.map(hostRef.current, { scrollWheelZoom: true });
    addBaseLayer(map);
    mapRef.current = map;

    for (const c of pinned) {
      const marker = L.marker([c.latitude, c.longitude], {
        icon: L.divIcon({
          className: '',
          html: `<span class="price-marker">${money(c.pricePerNight)}</span>`,
          iconSize: null
        })
      });
      marker.on('click', () => navigateRef.current(`/rooms/${c.slug}`));
      marker.bindTooltip(c.title, { direction: 'top', offset: [0, -8] });
      marker.addTo(map);
    }

    // One pin has no bounds worth fitting; centre on it instead of zooming to a point.
    if (pinned.length === 1) map.setView([pinned[0].latitude, pinned[0].longitude], 13);
    else map.fitBounds(pinned.map(c => [c.latitude, c.longitude]), { padding: [40, 40], maxZoom: 13 });

    const stopCentring = keepCentred(map);
    return () => { stopCentring(); map.remove(); mapRef.current = null; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  useFullMap(full, setFull, mapRef);

  if (!pinned.length) return null;
  return (
    <div className={`map-box ${full ? 'is-full' : ''}`}>
      <div className="cards-map" ref={hostRef} style={{ height, borderRadius: 14, overflow: 'hidden' }} />
      <MapChrome full={full} setFull={setFull} />
    </div>
  );
}

export function DetailMap({ latitude, longitude }) {
  const hostRef = useRef(null);
  const mapRef = useRef(null);
  const [full, setFull] = useState(false);

  useEffect(() => {
    if (latitude == null || longitude == null) return undefined;

    const map = L.map(hostRef.current, { scrollWheelZoom: true }).setView([latitude, longitude], 13);
    addBaseLayer(map);
    L.circle([latitude, longitude], {
      radius: 900, color: '#e01a2b', fillColor: '#e01a2b', fillOpacity: 0.15, weight: 2
    }).addTo(map);

    mapRef.current = map;
    const stopCentring = keepCentred(map);
    return () => { stopCentring(); map.remove(); mapRef.current = null; };
  }, [latitude, longitude]);

  useFullMap(full, setFull, mapRef);

  return (
    <div className={`map-box ${full ? 'is-full' : ''}`}>
      <div className="detail-map" ref={hostRef} />
      <MapChrome full={full} setFull={setFull} />
    </div>
  );
}

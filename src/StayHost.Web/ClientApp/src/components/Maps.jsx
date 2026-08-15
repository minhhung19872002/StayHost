// Leaflet lives outside React's tree on purpose: the map instance owns its own
// DOM and must survive result-set changes, so the component only ever renders
// an empty div and drives the map through effects.

import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { useStore } from '../lib/useStore.js';
import { t } from '../lib/i18n.js';
import { set } from '../lib/store.js';
import { money } from '../lib/format.js';

const TILES = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
const ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>';

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
    const map = L.map(hostRef.current, { scrollWheelZoom: false, zoomControl: true })
      .setView([16.0, 107.5], 5);
    L.tileLayer(TILES, { attribution: ATTRIBUTION, maxZoom: 18 }).addTo(map);

    mapRef.current = map;
    layerRef.current = L.layerGroup().addTo(map);

    // docs/01 TM-12 — offer to search again once the guest has moved the map.
    // `refitting` suppresses the offer when it was our own fitBounds that moved it.
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

    // The pane is laid out by CSS grid, so its final size is only known after paint.
    const t = setTimeout(() => map.invalidateSize(), 60);

    return () => {
      clearTimeout(t);
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

    // Only re-frame when the result set itself changed, never on a zoom tweak.
    if (!state.searchArea) {
      map.__refitting = true;
      map.fitBounds(items.map(i => [i.latitude, i.longitude]), { padding: [50, 50], maxZoom: 12 });
      setMoved(false);
    }

    const t = setTimeout(() => map.invalidateSize(), 60);
    return () => clearTimeout(t);
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

  // A moved map with "search as I move" on searches immediately; otherwise the
  // guest is offered the button and stays in control (docs/01 TM-12).
  useEffect(() => {
    if (moved && state.searchOnMapMove) searchHere();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [moved, state.searchOnMapMove]);

  return (
    <div style={{ position: 'relative', height: '100%' }}>
      <div className="map-search-again">
        {moved && !state.searchOnMapMove && !drawing && <button onClick={searchHere}>{t('Tìm ở khu vực này')}</button>}
        {/* docs/01 TM-24 — vẽ vùng tìm kiếm trên bản đồ. */}
        {!drawing && !state.searchPolygon && <button onClick={startDraw}>✎ {t('Vẽ vùng')}</button>}
        {!drawing && state.searchPolygon && <button onClick={clearDraw}>✕ {t('Bỏ vùng đã vẽ')}</button>}
        {drawing && <button onClick={finishDraw}>✓ {t('Xong')} ({drawRef.current.points.length})</button>}
        {drawing && <button onClick={clearDraw}>{t('Huỷ')}</button>}
        {!drawing && (
          <label>
            <input type="checkbox" checked={state.searchOnMapMove}
                   onChange={e => set({ searchOnMapMove: e.target.checked })} />
            {t('Tìm khi di chuyển bản đồ')}
          </label>
        )}
        {drawing && <span className="map-draw-hint">{t('Chạm để thêm điểm, rồi bấm Xong')}</span>}
      </div>
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
  const navigate = useNavigate();
  const navigateRef = useRef(navigate);
  navigateRef.current = navigate;

  const pinned = (cards ?? []).filter(c => c.latitude && c.longitude);
  // Re-pin only when the set itself changes — a ♥ toggle must not rebuild the map.
  const key = pinned.map(c => c.id).join(',');

  useEffect(() => {
    if (!pinned.length) return undefined;

    const map = L.map(hostRef.current, { scrollWheelZoom: false });
    L.tileLayer(TILES, { attribution: ATTRIBUTION, maxZoom: 18 }).addTo(map);

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

    const timer = setTimeout(() => map.invalidateSize(), 60);
    return () => { clearTimeout(timer); map.remove(); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  if (!pinned.length) return null;
  return <div ref={hostRef} style={{ height, borderRadius: 14, overflow: 'hidden' }} />;
}

export function DetailMap({ latitude, longitude }) {
  const hostRef = useRef(null);

  useEffect(() => {
    if (latitude == null || longitude == null) return undefined;

    const map = L.map(hostRef.current, { scrollWheelZoom: false }).setView([latitude, longitude], 13);
    L.tileLayer(TILES, { attribution: ATTRIBUTION, maxZoom: 18 }).addTo(map);
    L.circle([latitude, longitude], {
      radius: 900, color: '#e01a2b', fillColor: '#e01a2b', fillOpacity: 0.15, weight: 2
    }).addTo(map);

    const t = setTimeout(() => map.invalidateSize(), 60);
    return () => { clearTimeout(t); map.remove(); };
  }, [latitude, longitude]);

  return <div className="detail-map" ref={hostRef} />;
}

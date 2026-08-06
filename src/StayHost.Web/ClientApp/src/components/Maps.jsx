// Leaflet lives outside React's tree on purpose: the map instance owns its own
// DOM and must survive result-set changes, so the component only ever renders
// an empty div and drives the map through effects.

import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { useStore } from '../lib/useStore.js';
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

export function ResultsMap({ onSearchArea }) {
  const state = useStore();
  const navigate = useNavigate();
  const hostRef = useRef(null);
  const mapRef = useRef(null);
  const layerRef = useRef(null);
  const navigateRef = useRef(navigate);
  navigateRef.current = navigate;

  const [zoom, setZoom] = useState(5);
  const [moved, setMoved] = useState(false);

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

    // The pane is laid out by CSS grid, so its final size is only known after paint.
    const t = setTimeout(() => map.invalidateSize(), 60);

    return () => {
      clearTimeout(t);
      map.off('moveend', onMoveEnd);
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
        {moved && !state.searchOnMapMove && <button onClick={searchHere}>Tìm ở khu vực này</button>}
        <label>
          <input type="checkbox" checked={state.searchOnMapMove}
                 onChange={e => set({ searchOnMapMove: e.target.checked })} />
          Tìm khi di chuyển bản đồ
        </label>
      </div>
      <div id="map" ref={hostRef} />
    </div>
  );
}

function highlightCard(id, on) {
  document.querySelector(`[data-listing="${id}"]`)?.classList.toggle('is-hovered', on);
}

/** Airbnb shows an approximate circle on the room page, never the exact address. */
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

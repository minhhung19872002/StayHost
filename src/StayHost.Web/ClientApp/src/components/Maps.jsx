// Leaflet lives outside React's tree on purpose: the map instance owns its own
// DOM and must survive result-set changes, so the component only ever renders
// an empty div and drives the map through effects.

import { useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { useStore } from '../lib/useStore.js';
import { money } from '../lib/format.js';

const TILES = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
const ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>';

const markersById = new Map();

/** Card hover lifts the matching price pin; the map's own hover does the reverse. */
export function setHoveredListing(id, on) {
  const el = markersById.get(Number(id))?.getElement()?.querySelector('.price-marker');
  el?.classList.toggle('is-active', on);
}

export function ResultsMap() {
  const state = useStore();
  const navigate = useNavigate();
  const hostRef = useRef(null);
  const mapRef = useRef(null);
  const layerRef = useRef(null);
  const navigateRef = useRef(navigate);
  navigateRef.current = navigate;

  useEffect(() => {
    const map = L.map(hostRef.current, { scrollWheelZoom: false, zoomControl: true })
      .setView([16.0, 107.5], 5);
    L.tileLayer(TILES, { attribution: ATTRIBUTION, maxZoom: 18 }).addTo(map);

    mapRef.current = map;
    layerRef.current = L.layerGroup().addTo(map);

    // The pane is laid out by CSS grid, so its final size is only known after paint.
    const t = setTimeout(() => map.invalidateSize(), 60);

    return () => { clearTimeout(t); map.remove(); mapRef.current = null; markersById.clear(); };
  }, []);

  const items = state.results.items.filter(i => i.latitude && i.longitude);
  // Re-pinning is keyed on the result set so unrelated state changes (a ♥ click,
  // a currency switch) never rebuild markers or re-fit the view.
  const key = items.map(i => `${i.id}:${i.pricePerNight}`).join(',');

  useEffect(() => {
    const map = mapRef.current;
    const layer = layerRef.current;
    if (!map || !layer) return;

    layer.clearLayers();
    markersById.clear();
    if (!items.length) return;

    const bounds = [];
    for (const item of items) {
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
      bounds.push([item.latitude, item.longitude]);
    }

    map.fitBounds(bounds, { padding: [50, 50], maxZoom: 12 });
    const t = setTimeout(() => map.invalidateSize(), 60);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  return <div id="map" ref={hostRef} />;
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

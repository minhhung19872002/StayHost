// Leaflet is loaded from a CDN with `defer`, so every entry point guards on
// window.L being present and simply skips the map when it is not.

import { money } from '../util.js';
import { state } from '../store.js';

let resultsMap = null;
let markerLayer = null;
let detailMap = null;
const markersById = new Map();

const TILES = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
const ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>';

export function destroyMaps() {
  if (resultsMap) { resultsMap.remove(); resultsMap = null; markerLayer = null; }
  if (detailMap) { detailMap.remove(); detailMap = null; }
}

export function mountResultsMap() {
  const el = document.getElementById('map');
  if (!el || !window.L) return;

  if (resultsMap && !document.body.contains(resultsMap.getContainer())) {
    resultsMap.remove();
    resultsMap = null;
  }

  if (!resultsMap) {
    resultsMap = L.map(el, { scrollWheelZoom: false, zoomControl: true })
      .setView([16.0, 107.5], 5);
    L.tileLayer(TILES, { attribution: ATTRIBUTION, maxZoom: 18 }).addTo(resultsMap);
    markerLayer = L.layerGroup().addTo(resultsMap);
  }

  markerLayer.clearLayers();
  markersById.clear();

  const items = state.results.items.filter(i => i.latitude && i.longitude);
  if (!items.length) return;

  const bounds = [];
  for (const item of items) {
    const marker = L.marker([item.latitude, item.longitude], {
      icon: L.divIcon({
        className: '',
        html: `<span class="price-marker" data-marker="${item.id}">${money(item.pricePerNight)}</span>`,
        iconSize: null
      })
    });
    marker.on('click', () => {
      window.dispatchEvent(new CustomEvent('sh:open-listing', { detail: item.slug }));
    });
    marker.on('mouseover', () => highlightCard(item.id, true));
    marker.on('mouseout', () => highlightCard(item.id, false));
    marker.bindTooltip(item.title, { direction: 'top', offset: [0, -8] });
    marker.addTo(markerLayer);
    markersById.set(item.id, marker);
    bounds.push([item.latitude, item.longitude]);
  }

  resultsMap.fitBounds(bounds, { padding: [50, 50], maxZoom: 12 });
  setTimeout(() => resultsMap && resultsMap.invalidateSize(), 60);
}

/** Hovering a card lifts its marker, and vice versa. */
export function highlightMarker(listingId, on) {
  const marker = markersById.get(Number(listingId));
  const el = marker?.getElement()?.querySelector('.price-marker');
  if (el) el.classList.toggle('is-active', on);
}

function highlightCard(listingId, on) {
  document.querySelector(`[data-listing="${listingId}"]`)?.classList.toggle('is-hovered', on);
}

/** Re-runs the search limited to whatever the map is currently showing. */
export function currentMapBounds() {
  if (!resultsMap) return null;
  const b = resultsMap.getBounds();
  return {
    south: b.getSouth(), west: b.getWest(),
    north: b.getNorth(), east: b.getEast()
  };
}

export function onMapMoved(handler) {
  if (!resultsMap) return;
  resultsMap.off('moveend');
  resultsMap.on('moveend', handler);
}

export function mountDetailMap() {
  const el = document.getElementById('detail-map');
  const card = state.detail?.card;
  if (!el || !window.L || !card) return;

  if (detailMap) { detailMap.remove(); detailMap = null; }

  detailMap = L.map(el, { scrollWheelZoom: false }).setView([card.latitude, card.longitude], 13);
  L.tileLayer(TILES, { attribution: ATTRIBUTION, maxZoom: 18 }).addTo(detailMap);

  // Airbnb shows an approximate circle, not the exact address.
  L.circle([card.latitude, card.longitude], {
    radius: 900, color: '#e01a2b', fillColor: '#e01a2b', fillOpacity: 0.15, weight: 2
  }).addTo(detailMap);

  setTimeout(() => detailMap && detailMap.invalidateSize(), 60);
}

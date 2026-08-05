// 24×24 line icons drawn with currentColor so they inherit the active/inactive
// category colours. Kept inline to avoid an icon-font request.

const P = {
  all: '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c2.5 2.6 2.5 15.4 0 18M12 3c-2.5 2.6-2.5 15.4 0 18"/>',
  villa: '<path d="M3 11.5 12 4l9 7.5"/><path d="M5.5 10.5V20h13v-9.5"/><path d="M3 20h18"/><path d="M9.5 20v-4.5h5V20"/>',
  apartment: '<rect x="5" y="3" width="14" height="18" rx="1.5"/><path d="M9 7h1.5M13.5 7H15M9 11h1.5M13.5 11H15M9 15h1.5M13.5 15H15"/><path d="M10.5 21v-3h3v3"/>',
  homestay: '<path d="M4 12 12 5l8 7"/><path d="M6 11v9h12v-9"/><path d="M12 20v-5"/><circle cx="12" cy="12.5" r="1.8"/>',
  house: '<path d="M3.5 10.8 12 4l8.5 6.8"/><path d="M6 10v10h12V10"/><rect x="10" y="14" width="4" height="6"/>',
  cabin: '<path d="m3 19 5-9 3 5 2.5-4L21 19z"/><path d="M8 10 6.2 6.8"/><path d="M3 19h18"/>',
  boutique: '<path d="M12 3.5 13.8 9l5.7.4-4.4 3.7 1.4 5.6L12 15.6 7.5 18.7l1.4-5.6L4.5 9.4 10.2 9z"/>',
  pool: '<path d="M3 17c1.5 0 1.5 1.5 3 1.5S7.5 17 9 17s1.5 1.5 3 1.5 1.5-1.5 3-1.5 1.5 1.5 3 1.5 1.5-1.5 3-1.5"/><path d="M7 15V6a2 2 0 0 1 4 0v9M13 15V6a2 2 0 0 1 4 0v9"/><path d="M7 9h4M13 9h4"/>',
  map: '<path d="m9 4 6 2 5-2v14l-5 2-6-2-5 2V6z"/><path d="M9 4v14M15 6v14"/>',
  filter: '<path d="M3 6h18M6 12h12M10 18h4"/>',
  heart: '<path d="M12 20s-7-4.6-7-9.4A3.9 3.9 0 0 1 12 8a3.9 3.9 0 0 1 7 2.6C19 15.4 12 20 12 20Z"/>',
  globe: '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c2.4 2.7 2.4 15.3 0 18M12 3c-2.4 2.7-2.4 15.3 0 18"/>',
  menu: '<path d="M4 7h16M4 12h16M4 17h16"/>',
  search: '<circle cx="11" cy="11" r="6.5"/><path d="m20 20-4.2-4.2"/>',
  arrowRight: '<path d="M5 12h14M13 6l6 6-6 6"/>',
  chevronLeft: '<path d="m14 6-6 6 6 6"/>',
  chevronRight: '<path d="m10 6 6 6-6 6"/>',
  share: '<path d="M12 15V4M8.5 7.5 12 4l3.5 3.5"/><path d="M5 13v5.5A1.5 1.5 0 0 0 6.5 20h11a1.5 1.5 0 0 0 1.5-1.5V13"/>',
  star: '<path d="m12 4 2.3 5.1 5.7.5-4.3 3.7 1.3 5.4L12 15.9 7 18.7l1.3-5.4L4 9.6l5.7-.5z"/>',

  // amenities
  wifi: '<path d="M2.5 9a15 15 0 0 1 19 0"/><path d="M5.5 12.5a10.5 10.5 0 0 1 13 0"/><path d="M8.5 16a6 6 0 0 1 7 0"/><circle cx="12" cy="19.3" r="1.1" fill="currentColor"/>',
  kitchen: '<rect x="4" y="3" width="16" height="18" rx="2"/><path d="M4 12h16"/><path d="M7 6.5v2M7 15v2.5"/><circle cx="16" cy="7.5" r="1.4"/>',
  parking: '<rect x="3.5" y="3.5" width="17" height="17" rx="3"/><path d="M9.5 17V7.5h3.2a2.9 2.9 0 0 1 0 5.8H9.5"/>',
  ac: '<path d="M12 3v18M12 7.5 8.8 5.3M12 7.5l3.2-2.2M12 16.5l-3.2 2.2M12 16.5l3.2 2.2"/><path d="m4.2 7.5 15.6 9M6.9 6.6l.9 3.6-3.6.9M17.1 17.4l-.9-3.6 3.6-.9"/><path d="m19.8 7.5-15.6 9M17.1 6.6l-.9 3.6 3.6.9M6.9 17.4l.9-3.6-3.6-.9"/>',
  pet: '<ellipse cx="8" cy="9" rx="1.7" ry="2.3"/><ellipse cx="16" cy="9" rx="1.7" ry="2.3"/><ellipse cx="4.8" cy="13.6" rx="1.5" ry="2"/><ellipse cx="19.2" cy="13.6" rx="1.5" ry="2"/><path d="M12 13.5c2.6 0 4.6 2 4.6 4a2.6 2.6 0 0 1-3.6 2.3 2.6 2.6 0 0 0-2 0A2.6 2.6 0 0 1 7.4 17.5c0-2 2-4 4.6-4Z"/>',
  washer: '<rect x="4" y="3" width="16" height="18" rx="2"/><circle cx="12" cy="13.5" r="4.5"/><path d="M8 6.5h2"/><circle cx="16" cy="6.5" r=".8" fill="currentColor"/>',
  tv: '<rect x="3" y="5" width="18" height="12" rx="2"/><path d="M8 21h8M12 17v4"/>',
  workspace: '<path d="M3 18h18"/><path d="M5 18V9h14v9"/><rect x="8.5" y="11.5" width="7" height="4" rx="1"/><path d="M6 9V6h12v3"/>',
  gym: '<path d="M3 12h18"/><rect x="4" y="8.5" width="3" height="7" rx="1"/><rect x="17" y="8.5" width="3" height="7" rx="1"/><rect x="7" y="6.5" width="3" height="11" rx="1"/><rect x="14" y="6.5" width="3" height="11" rx="1"/>',
  bbq: '<path d="M5 5h14l-2.4 8H7.4z"/><path d="M9 13.5 7 21M15 13.5l2 7.5M8.2 17.5h7.6"/>',
  hottub: '<path d="M3 12h18v5a3 3 0 0 1-3 3H6a3 3 0 0 1-3-3z"/><path d="M8 12V6.5A2.5 2.5 0 0 1 13 6"/><circle cx="16.5" cy="8" r="1.2"/>',
  fire: '<path d="M12 3s4.5 4 4.5 8a4.5 4.5 0 0 1-9 0c0-1.2.5-2.3 1.2-3.2"/><path d="M12 21a5 5 0 0 0 5-5c0-3-2-5-2-5"/><path d="M12 21a5 5 0 0 1-5-5"/>',
  bike: '<circle cx="6" cy="16.5" r="3.5"/><circle cx="18" cy="16.5" r="3.5"/><path d="m6 16.5 4-8h5l3 8M9 8.5h4"/>',
  beach: '<path d="M3 18c1.5 0 1.5 1.5 3 1.5s1.5-1.5 3-1.5 1.5 1.5 3 1.5 1.5-1.5 3-1.5 1.5 1.5 3 1.5 1.5-1.5 3-1.5"/><path d="M12 15V8"/><path d="M5 8a7 7 0 0 1 14 0z"/>',
  breakfast: '<path d="M4 8h12v5a5 5 0 0 1-10 0z"/><path d="M16 9h2.5a2.5 2.5 0 0 1 0 5H16"/><path d="M4 20h14"/>',
  selfcheckin: '<rect x="4" y="10" width="16" height="11" rx="2"/><path d="M8 10V7a4 4 0 0 1 8 0v3"/><circle cx="12" cy="15.5" r="1.3"/>',
  crib: '<path d="M4 6v14M20 6v14M4 20h16M4 9h16"/><path d="M8 9v6M12 9v6M16 9v6"/>',
  view: '<path d="M3 20h18"/><path d="m4 20 6-11 4 6 2-3 4 8"/><circle cx="17" cy="6" r="2"/>',
  ev: '<rect x="3" y="8" width="12" height="10" rx="2"/><path d="M15 11h2.5a2 2 0 0 1 2 2v3a1.5 1.5 0 0 0 3 0v-5l-2-2"/><path d="m9.2 10.5-1.8 3h2.6l-1.6 3"/>'
};

export function icon(name, size = 24, opts = {}) {
  const path = P[name] ?? P.all;
  const fill = opts.filled ? 'currentColor' : 'none';
  return `<svg viewBox="0 0 24 24" width="${size}" height="${size}" fill="${fill}" stroke="currentColor"
    stroke-width="${opts.weight ?? 1.6}" stroke-linecap="round" stroke-linejoin="round"
    aria-hidden="true" focusable="false">${path}</svg>`;
}

/** Amenity slug → icon name; falls back to a star for anything unmapped. */
export function amenityIcon(key, size = 22) {
  return icon(P[key] ? key : 'star', size);
}

export const CATEGORY_ICON = {
  all: 'all', villa: 'villa', apartment: 'apartment',
  homestay: 'homestay', house: 'house', cabin: 'cabin', boutique: 'boutique'
};

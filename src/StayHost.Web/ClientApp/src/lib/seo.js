// Which address a page admits to living at.
//
// This is a single-page app: every address is served the same index.html. So a
// fixed <link rel="canonical"> in that file would tell Google every listing and
// every city page is a duplicate of the home page, and they would drop out of
// the index. Canonical has to follow the address, or not exist at all.
//
// It matters more here than on most sites because this is a booking site. One
// city page is reachable as /thanh-pho/da-lat, and again with dates, and again
// with a guest count, and again with a sort order — the same rooms every time.
// Left alone, Google sees a hundred near-identical pages, splits the ranking
// between them and trusts none. Pointing them all at the bare address collapses
// that back into one page.
//
// Almost the whole query string is dropped. Dates and guest counts narrow
// availability on a page that is still about the same place, so they name no new
// page and keeping them would split one page's ranking across dozens.
//
// `trang` is the one exception, and it is not a filter — page 2 of a city holds
// different places from page 1. Folding it onto the bare address would tell
// Google the later pages are duplicates, and every listing past the first twelve
// would drop out of the index: exactly the gap the paging was added to close.
// Page 1 still normalises to the bare address, because "?trang=1" and no
// parameter are the same page and two addresses for one page is the duplicate
// this whole arrangement exists to prevent.
const KEEP = ['trang'];

/**
 * The one host this site admits to living at.
 *
 * staylio.vn and www.staylio.vn both answer, and both served a canonical
 * pointing at themselves — so Google saw two complete copies of the catalogue,
 * each claiming to be the original, and split the ranking between them. The
 * redirect at the proxy is the real fix; this is the half that travels with the
 * code, and it holds even on a deployment whose proxy has not been told yet.
 *
 * Only the "www." prefix is dropped. Rewriting the host to a configured value
 * would break every preview deployment and localhost, where the address the
 * page is being read at is the only correct answer.
 */
function canonicalOrigin() {
  const { protocol, host } = window.location;
  return `${protocol}//${host.replace(/^www\./i, '')}`;
}

/** Absolute address of a path on this origin, keeping only the params that name a page. */
export function canonicalUrl(pathname, search) {
  const raw = (pathname || '/');
  const path = raw.split('?')[0].split('#')[0];
  const query = search !== undefined ? search : raw.includes('?') ? raw.slice(raw.indexOf('?')) : '';

  // "/rooms/x/" and "/rooms/x" are the same page to a person and two pages to a
  // crawler. The home page keeps its slash; everything else loses a trailing one.
  const tidy = path.length > 1 ? path.replace(/\/+$/, '') : '/';

  const from = new URLSearchParams(query);
  const kept = new URLSearchParams();
  for (const key of KEEP) {
    const value = from.get(key);
    // "1" is the bare address, and anything that is not a page number is noise.
    if (value && /^[0-9]+$/.test(value) && Number(value) > 1) kept.set(key, value);
  }

  const tail = kept.toString();
  return canonicalOrigin() + (tidy || '/') + (tail ? `?${tail}` : '');
}

function put(selector, make) {
  let el = document.head.querySelector(selector);
  if (!el) {
    el = make();
    document.head.appendChild(el);
  }
  return el;
}

/**
 * Points canonical and og:url at `pathname`. Called on every navigation, because
 * a route change in this app never reloads the document and nothing else would
 * update the tags left over from the previous page.
 */
export function applyCanonical(pathname, search) {
  const url = canonicalUrl(pathname, search);

  const link = put('link[rel="canonical"]', () => {
    const el = document.createElement('link');
    el.setAttribute('rel', 'canonical');
    return el;
  });
  link.setAttribute('href', url);

  // og:url feeds the preview card on Facebook, Zalo and Messenger — the places a
  // guest most often pastes a room into. Left at the home page, every share of
  // every room shows the same card.
  const og = put('meta[property="og:url"]', () => {
    const el = document.createElement('meta');
    el.setAttribute('property', 'og:url');
    return el;
  });
  og.setAttribute('content', url);
}

// ---------------------------------------------------------------- page meta

// Captured before anything overwrites them, so leaving a page can put the
// defaults back exactly rather than re-deriving strings that live in index.html.
const DEFAULTS = {
  title: document.title,
  description: document.head.querySelector('meta[name="description"]')?.content || '',
  image: document.head.querySelector('meta[property="og:image"]')?.content || '',
};

function meta(attr, name) {
  let el = document.head.querySelector(`meta[${attr}="${name}"]`);
  if (!el) {
    el = document.createElement('meta');
    el.setAttribute(attr, name);
    document.head.appendChild(el);
  }
  return el;
}

/**
 * Titles and descriptions for one page.
 *
 * A single-page app keeps whatever <title> index.html shipped with, so without
 * this every city page and every room carries the home page's title. Google
 * leans on the title more than on anything else in the document, so a city page
 * whose title never says "Đà Lạt" cannot rank for "khách sạn Đà Lạt" — the
 * catalogue can be crawled and still be invisible.
 */
export function setPageMeta({ title, description, image } = {}) {
  const t = title || DEFAULTS.title;
  const d = description || DEFAULTS.description;

  document.title = t;
  meta('name', 'description').setAttribute('content', d);
  meta('property', 'og:title').setAttribute('content', t);
  meta('property', 'og:description').setAttribute('content', d);

  // The picture on the card Zalo, Messenger and Facebook draw when somebody
  // pastes the link — which in Vietnam is how most of a listing's traffic
  // arrives. Until this ran there was no og:image anywhere on the site, so
  // every share came out as a bare grey rectangle with a line of text under it.
  //
  // Absolute, because the scrapers fetch the image on their own without the
  // page around it and a path beginning with "/" means nothing to them.
  const img = absolute(image || DEFAULTS.image);
  meta('property', 'og:image').setAttribute('content', img);
  meta('property', 'og:image:alt').setAttribute('content', t);
  meta('name', 'twitter:image').setAttribute('content', img);
}

/** A share picture as a full address, whatever shape it arrived in. */
function absolute(src) {
  const s = (src || '').trim();
  if (!s) return '';
  if (/^https?:\/\//i.test(s)) return s;
  return canonicalOrigin() + (s.startsWith('/') ? s : `/${s}`);
}

/**
 * Keeps this page out of the index, or lets it back in.
 *
 * The server already answers 404 for an address with nothing behind it, but a
 * route change inside the app never reaches the server — a guest who clicks
 * through to a listing that was unpublished a minute ago gets a "không tìm
 * thấy" screen at an address that returned 200 when the document loaded.
 */
export function setNoIndex(on) {
  if (!on) {
    // Queried rather than created-then-removed: meta() appends when it finds
    // nothing, and every navigation would add a tag only to delete it again.
    document.head.querySelector('meta[name="robots"]')?.remove();
    return;
  }
  // "follow" and not "none": the links on a not-found page still lead to real
  // pages, and there is no reason to make a crawler forget them too.
  meta('name', 'robots').setAttribute('content', 'noindex, follow');
}

/** Back to what index.html shipped with — called on every navigation. */
export function resetPageMeta() {
  setPageMeta(DEFAULTS);
  // A page that set noindex must not leave it behind for the next one.
  setNoIndex(false);
}

// ----------------------------------------------------------- structured data

const LD_ID = 'seo-jsonld';

/**
 * The one JSON-LD block for this page, or null to clear it.
 *
 * Kept to a single tag rather than appended to, because a leftover block from
 * the previous page describing a different room is worse than none: Google reads
 * it as a claim about the page it is on, and structured data that disagrees with
 * the visible page is what gets rich results switched off for a whole site.
 */
export function setStructuredData(data) {
  const old = document.getElementById(LD_ID);
  if (old) old.remove();
  if (!data) return;

  const el = document.createElement('script');
  el.type = 'application/ld+json';
  el.id = LD_ID;
  el.textContent = JSON.stringify(data);
  document.head.appendChild(el);
}

/**
 * A place to stay, described the way a search engine expects.
 *
 * aggregateRating is attached only when real reviews exist. A rating with a
 * count of zero is not a small inaccuracy — Google treats invented review markup
 * as spam, and the penalty lands on the whole domain rather than the one page.
 * Rating and count here come from the review table, recomputed on every review
 * (see the seeding note in CLAUDE.md §4), so "no reviews" really does mean none.
 */
export function listingJsonLd(card, { description, url } = {}) {
  if (!card) return null;

  const node = {
    '@context': 'https://schema.org',
    '@type': 'Product',
    name: card.title,
    description: (description || '').slice(0, 300) || undefined,
    image: (card.images || []).slice(0, 6),
    url,
    category: card.typeLabel,
    offers: {
      '@type': 'Offer',
      priceCurrency: 'VND',
      price: card.pricePerNight,
      availability: 'https://schema.org/InStock',
      url,
      // Spelled out as a nightly rate rather than left as a bare number, so the
      // figure in a search result means the same thing as the one on the card.
      priceSpecification: {
        '@type': 'UnitPriceSpecification',
        priceCurrency: 'VND',
        price: card.pricePerNight,
        unitText: 'đêm',
      },
    },
  };

  if (card.reviewCount > 0) {
    node.aggregateRating = {
      '@type': 'AggregateRating',
      ratingValue: card.rating,
      reviewCount: card.reviewCount,
      bestRating: 5,
      worstRating: 1,
    };
  }

  return node;
}

/** Where this page sits, so Google can draw the trail under the result. */
export function breadcrumbJsonLd(trail) {
  return {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    itemListElement: trail.map((step, i) => ({
      '@type': 'ListItem',
      position: i + 1,
      name: step.name,
      item: window.location.origin + step.path,
    })),
  };
}

/**
 * The site itself, on the home page only.
 *
 * SearchAction is what lets Google put a search box under the result for the
 * brand, and it is a promise: the address given here has to be a real search
 * page on this site, which /?q= is.
 */
export function siteJsonLd() {
  const origin = window.location.origin;
  return {
    '@context': 'https://schema.org',
    '@graph': [
      {
        '@type': 'Organization',
        '@id': `${origin}/#org`,
        name: 'Staylio',
        url: origin,
      },
      {
        '@type': 'WebSite',
        '@id': `${origin}/#site`,
        url: origin,
        name: 'Staylio',
        inLanguage: 'vi-VN',
        publisher: { '@id': `${origin}/#org` },
        potentialAction: {
          '@type': 'SearchAction',
          target: { '@type': 'EntryPoint', urlTemplate: `${origin}/?q={search_term_string}` },
          'query-input': 'required name=search_term_string',
        },
      },
    ],
  };
}

/**
 * A city landing page: the list of places on it, in the order shown.
 *
 * ItemList is the honest shape here — the page is a list of offers, not one
 * product — and it lets a result carry the places rather than only the city.
 */
export function cityJsonLd(city, cards) {
  const origin = window.location.origin;
  return {
    '@context': 'https://schema.org',
    '@type': 'ItemList',
    name: `Chỗ nghỉ tại ${city}`,
    numberOfItems: cards.length,
    itemListElement: cards.map((card, i) => ({
      '@type': 'ListItem',
      position: i + 1,
      url: `${origin}/rooms/${card.slug}`,
      name: card.title,
    })),
  };
}

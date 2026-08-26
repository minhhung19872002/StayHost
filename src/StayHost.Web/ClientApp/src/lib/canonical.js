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
// The query string is dropped entirely rather than filtered. Nothing in it names
// a different set of rooms — dates and guest counts narrow availability on a page
// that is still about the same place — so there is no parameter worth keeping,
// and a list of exceptions would be one more thing to forget to update.

/** Absolute address of a path on this origin, with no query and no fragment. */
export function canonicalUrl(pathname) {
  const path = (pathname || '/').split('?')[0].split('#')[0];

  // "/rooms/x/" and "/rooms/x" are the same page to a person and two pages to a
  // crawler. The home page keeps its slash; everything else loses a trailing one.
  const tidy = path.length > 1 ? path.replace(/\/+$/, '') : '/';

  return window.location.origin + (tidy || '/');
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
export function applyCanonical(pathname) {
  const url = canonicalUrl(pathname);

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

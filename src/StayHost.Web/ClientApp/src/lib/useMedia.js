import { useEffect, useState } from 'react';

/**
 * Re-renders the caller when the window crosses `query`.
 *
 * A media query in the stylesheet can hide a thing; it cannot tell a component
 * to render a different thing. Both the header (which swaps the search bar for
 * a summary below 720px) and the results map (which owns the screen below
 * 1100px instead of sharing it) need the second kind.
 */
export function useMedia(query) {
  const [on, setOn] = useState(() => window.matchMedia(query).matches);
  useEffect(() => {
    const mq = window.matchMedia(query);
    const onChange = e => setOn(e.matches);
    mq.addEventListener('change', onChange);
    // The query can have changed between the first render and this effect.
    setOn(mq.matches);
    return () => mq.removeEventListener('change', onChange);
  }, [query]);
  return on;
}

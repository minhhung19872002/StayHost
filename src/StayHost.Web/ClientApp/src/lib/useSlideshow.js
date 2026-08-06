import { useEffect, useState } from 'react';

/**
 * The slide-and-zoom of codepen.io/daniesy/pen/JoWOpR, shared by the photo strip
 * on a listing card and the lightbox on a listing page. Keeping one copy is what
 * stops the two drifting apart.
 *
 * The frame on its way out has to stay mounted after it stops being the current
 * one, or it disappears instead of sliding. `leaving` is what keeps it there.
 *
 * `index` and `setIndex` are the caller's, because the card holds its position in
 * local state while the lightbox holds it in the store.
 */

/** Must outlive the .35s transition in the stylesheet, or the slide is cut short. */
export const SLIDE_MS = 380;

export function useSlideshow(index, setIndex, count) {
  const [leaving, setLeaving] = useState(null);
  const idx = Math.min(Math.max(index ?? 0, 0), Math.max(count - 1, 0));

  useEffect(() => {
    if (!leaving) return;
    const timer = setTimeout(() => setLeaving(null), SLIDE_MS);
    return () => clearTimeout(timer);
  }, [leaving]);

  const move = (to, side) => {
    if (to === idx || count < 2) return;
    setLeaving({ index: idx, side });
    setIndex(to);
  };

  return {
    idx,
    leaving,

    // Wrapping round is what makes the arrows worth having: with five photos and
    // a hard stop at each end, two clicks leave you at a dead arrow.
    step: dir => move((idx + dir + count) % count, dir > 0 ? 'left' : 'right'),

    // Jumping to a thumbnail leaves in whichever direction the jump went.
    goTo: to => move(to, to > idx ? 'left' : 'right'),

    frameClass: i =>
      i === idx ? 'is-current'
        : leaving?.index === i ? `is-leaving to-${leaving.side}`
          : '',

    /** True while a frame still needs to be on screen to finish its slide. */
    isMounted: i => i === idx || leaving?.index === i
  };
}

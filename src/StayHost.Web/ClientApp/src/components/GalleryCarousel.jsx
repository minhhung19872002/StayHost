import { useState } from 'react';
import { useSlideshow } from '../lib/useSlideshow.js';

/**
 * docs/01 MR — the hero slideshow on an experience/service detail page: one large
 * frame, arrows and dots to move through every photo, and a count badge. Reuses
 * the card slide-and-zoom at a larger size so the whole product feels of a piece.
 */
export function GalleryCarousel({ images, alt = '' }) {
  const pics = images?.length ? images : [''];
  const [index, setIndex] = useState(0);
  const slides = useSlideshow(index, setIndex, pics.length);
  const { idx, frameClass } = slides;

  return (
    <div className="gallery-carousel">
      {pics.map((src, i) =>
        slides.isMounted(i) || Math.abs(i - idx) === 1 || Math.abs(i - idx) === pics.length - 1
          ? <img key={i} src={src} alt={`${alt} — ảnh ${i + 1}`}
                 className={frameClass(i)} loading={i === 0 ? 'eager' : 'lazy'} decoding="async" />
          : <img key={i} alt="" aria-hidden="true" className="is-deferred" />
      )}

      {pics.length > 1 && <>
        <button className="carousel-nav prev" onClick={() => slides.step(-1)} aria-label="Ảnh trước">‹</button>
        <button className="carousel-nav next" onClick={() => slides.step(1)} aria-label="Ảnh tiếp theo">›</button>
        <div className="gallery-count">{idx + 1} / {pics.length}</div>
        <div className="carousel-dots">
          {pics.map((_, i) => (
            <button key={i} className={`bullet ${i === idx ? 'is-on' : ''}`}
                    onClick={() => slides.goTo(i)} aria-label={`Xem ảnh ${i + 1}`} />
          ))}
        </div>
      </>}
    </div>
  );
}

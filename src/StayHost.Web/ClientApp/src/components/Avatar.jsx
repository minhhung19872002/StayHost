/**
 * docs/01 TK-04 — one circle, two ways of filling it. Everywhere a person
 * appears they get the photo they uploaded, and their initials only while they
 * have not uploaded one.
 */
export function Avatar({ url, initials, className = 'avatar', size, style }) {
  const box = size ? { width: size, height: size, fontSize: Math.round(size * 0.38), ...style } : style;

  return (
    <span className={className} style={box} aria-hidden="true">
      {url ? <img src={url} alt="" loading="lazy" /> : (initials || '?')}
    </span>
  );
}

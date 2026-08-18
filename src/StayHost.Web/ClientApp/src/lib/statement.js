/**
 * Turning a bank export pasted into a box back into columns.
 *
 * It lives here rather than beside one screen because both directions of money
 * need it: docs/07 §2.3 reads credits arriving from guests, §15.4 reads debits
 * going out to hosts, and they must read the same file the same way. Two copies
 * would drift, and the drift would show up as one screen quietly misreading
 * amounts the other got right.
 *
 * The splitting is done in the browser, in front of the person looking at their
 * own file, because banks disagree about column order, headings and decimal
 * separators; a parser tuned to one of them breaks the week they change it.
 */

/** Splits pasted text into rows of cells, tab- or comma-separated. */
export function splitRows(text) {
  return text.split(/\r?\n/)
    .map(line => line.trim())
    .filter(Boolean)
    .map(line => line.includes('\t') ? line.split('\t') : line.split(/[;,](?=(?:[^"]*"[^"]*")*[^"]*$)/))
    .map(cells => cells.map(c => c.replace(/^"|"$/g, '')));
}

/**
 * "2.672.000", "2,672,000.00" and "2672000" are the same money.
 *
 * Which separator means what is decided by what follows the last one: grouping
 * always has three digits behind it, so a separator with exactly two is the
 * decimal point and everything else is grouping. That is unambiguous in both
 * the Vietnamese and the English convention, which is why it beats guessing
 * from whether the character is a dot or a comma.
 */
export function parseAmount(cell) {
  if (!cell) return 0;

  const cleaned = String(cell).replace(/[^\d.,-]/g, '');

  const normalised = /[.,]\d{2}$/.test(cleaned)
    ? `${cleaned.slice(0, -3).replace(/[.,]/g, '')}.${cleaned.slice(-2)}`
    : cleaned.replace(/[.,]/g, '');

  const n = Number(normalised);
  return Number.isFinite(n) && n > 0 ? n : 0;
}

// Client-side price preview for result cards.
//
// docs/00 §6.8 says every money rule is defined once, server-side, in
// StayHost.Domain/Pricing.cs. The card grid can't afford a quote request per
// card, so it reads the fee constants the server publishes in /api/meta and
// applies them the same way. When the server sends `stayTotal` on a card
// (dates were part of the search) we use that number verbatim instead.

import { state } from './store.js';
import { nightsBetween } from './format.js';

const FALLBACK = { guestServiceFeeRate: 0.14, defaultCleaningFee: 350000 };

const fees = () => state.meta?.fees ?? FALLBACK;

/** Total the guest actually pays for the selected dates, fees included. */
export function stayTotal(card) {
  const nights = nightsBetween(state.checkIn, state.checkOut);
  if (card.stayTotal != null) return { nights, total: card.stayTotal };

  const f = fees();
  const subtotal = card.pricePerNight * nights;
  const cleaning = card.cleaningFee ?? f.defaultCleaningFee;
  return { nights, total: subtotal + cleaning + Math.round(subtotal * f.guestServiceFeeRate) };
}

/** Same fee model applied to the pre-discount nightly rate, for the strike-through. */
export function originalStayTotal(card) {
  if (!card.originalPricePerNight) return null;
  const f = fees();
  const nights = nightsBetween(state.checkIn, state.checkOut);
  const subtotal = card.originalPricePerNight * nights;
  const cleaning = card.cleaningFee ?? f.defaultCleaningFee;
  return subtotal + cleaning + Math.round(subtotal * f.guestServiceFeeRate);
}

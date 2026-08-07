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
  // A flexible search matches each card on its own free dates (docs/01 TM-06),
  // so the night count comes from the card when the server sent one.
  const nights = card.stayCheckIn && card.stayCheckOut
    ? nightsBetween(card.stayCheckIn, card.stayCheckOut)
    : nightsBetween(state.checkIn, state.checkOut);
  if (card.stayTotal != null) return { nights, total: card.stayTotal };

  const f = fees();
  const subtotal = card.pricePerNight * nights;
  const cleaning = card.cleaningFee ?? f.defaultCleaningFee;
  return { nights, total: subtotal + cleaning + Math.round(subtotal * f.guestServiceFeeRate) };
}

/**
 * docs/01 TM-20 — the nightly price with fees and taxes in it.
 *
 * When the search carried dates the server sent a real quote for this card, and
 * that number is the whole of docs/03 §1 including tax. Without dates it falls
 * back to the same fee model the rest of this file uses, which has no tax in it
 * — the toggle then still answers "gồm phí", just not "và thuế".
 */
export function allInPerNight(card) {
  const { nights, total } = stayTotal(card);
  return nights > 0 ? Math.round(total / nights) : card.pricePerNight;
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

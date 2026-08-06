// React Router's navigate() is only available inside components, but store
// actions (a suggestion pick, a successful "become host") also need to move the
// user. App registers the router's navigate here once so plain functions can
// use it without prop-drilling a callback through every component.

import { searchToQuery } from './urlState.js';

let navigate = () => {};

export const setNavigator = fn => { navigate = fn; };

export const go = (to, options) => navigate(to, options);

/**
 * Writes the current search criteria into the browse URL. `replace` keeps the
 * back button useful while the user is only adjusting filters.
 */
export const applySearch = ({ replace = true } = {}) =>
  navigate(`/?${searchToQuery()}`, { replace });

// Bridges the plain-JS store into React. Every component that reads `state`
// calls useStore() so a notify() re-renders exactly the subscribers, and the
// store itself stays free of React imports.

import { useSyncExternalStore } from 'react';
import { subscribe, getSnapshot, state } from './store.js';

export function useStore() {
  useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
  return state;
}

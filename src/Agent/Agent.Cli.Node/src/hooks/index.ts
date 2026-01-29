/**
 * React hooks exports
 */
export { useAgenticLoop } from './useAgenticLoop';
export type { UseAgenticLoopResult } from './useAgenticLoop';

export { useScrollable } from './useScrollable';
export type { ScrollState, UseScrollableOptions, UseScrollableReturn } from './useScrollable';

// Re-export store hooks for convenience
export {
  useStore,
  useMessages,
  useCurrentSession,
  useIsProcessing,
  useLoopStatus,
  useConnectionStatus,
  useInputHistory,
  useConfig,
} from '../store';

/*
 * Copyright (c) Microsoft Corporation. All rights reserved.
 *
 * Debug logging utility for SRE Agent browser extension.
 */

/**
 * Logs debug messages to the console with a consistent prefix.
 * @param args - Arguments to log
 */
export function debugLog(...args: unknown[]): void {
  console.log('[SREAgent Extension]', ...args);
}

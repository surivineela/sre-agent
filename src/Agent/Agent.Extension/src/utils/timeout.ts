/*
 * Copyright (c) Microsoft Corporation. All rights reserved.
 *
 * Timeout utilities for async operations.
 */

/**
 * Wraps a promise with a timeout. If the promise doesn't resolve within
 * the specified time, an error is thrown.
 *
 * @param promise - The promise to wrap
 * @param timeoutMs - Timeout in milliseconds
 * @param operation - Description of the operation (for error messages)
 * @returns The result of the promise if it resolves in time
 * @throws Error if the operation times out
 *
 * @example
 * await withTimeout(
 *   fetch('https://example.com'),
 *   5000,
 *   'Fetch request'
 * );
 */
export async function withTimeout<T>(
  promise: Promise<T>,
  timeoutMs: number,
  operation: string
): Promise<T> {
  let timeoutId: ReturnType<typeof setTimeout>;

  const timeoutPromise = new Promise<never>((_, reject) => {
    timeoutId = setTimeout(
      () => reject(new Error(`${operation} timed out after ${timeoutMs}ms`)),
      timeoutMs
    );
  });

  try {
    return await Promise.race([promise, timeoutPromise]);
  } finally {
    clearTimeout(timeoutId!);
  }
}

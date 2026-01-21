/**
 * Debounce utility with max wait time
 *
 * Debounce: Subsequent calls within the debounce period are batched together.
 * Max wait: Ensures the function executes at least once every maxWaitMs.
 */
export function createDebounce<T extends (...args: any[]) => void>(
  fn: T,
  debounceMs: number,
  maxWaitMs: number,
): [debouncedFn: T, cleanup: () => void] {
  let debounceTimeoutId: NodeJS.Timeout | undefined;
  let maxWaitTimeoutId: NodeJS.Timeout | undefined;
  let pendingArgs: Parameters<T> | undefined;

  const execute = () => {
    if (pendingArgs === undefined) {
      return;
    }

    const args = pendingArgs;
    pendingArgs = undefined;

    clearTimeout(debounceTimeoutId);
    clearTimeout(maxWaitTimeoutId);
    debounceTimeoutId = undefined;
    maxWaitTimeoutId = undefined;

    fn(...args);
  };

  const debouncedFn = ((...args: Parameters<T>) => {
    pendingArgs = args;

    // Debounce: schedule execution after debounce period
    clearTimeout(debounceTimeoutId);
    debounceTimeoutId = setTimeout(execute, debounceMs);

    // Max wait: ensure we execute at least every maxWaitMs
    if (!maxWaitTimeoutId) {
      maxWaitTimeoutId = setTimeout(execute, maxWaitMs);
    }
  }) as T;

  const cleanup = () => {
    clearTimeout(debounceTimeoutId);
    clearTimeout(maxWaitTimeoutId);
    debounceTimeoutId = undefined;
    maxWaitTimeoutId = undefined;
    pendingArgs = undefined;
  };

  return [debouncedFn, cleanup];
}

export default createDebounce;

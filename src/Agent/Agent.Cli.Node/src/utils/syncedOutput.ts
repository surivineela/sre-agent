/**
 * Synchronized Output utility for terminal rendering
 *
 * Uses the terminal synchronized output protocol (mode 2026) to prevent flickering
 * when rendering content taller than the terminal window.
 *
 * Supported terminals: Windows Terminal, iTerm2, Kitty, Alacritty, Ghostty, Warp, etc.
 * See: https://github.com/contour-terminal/vt-extensions/blob/master/synchronized-output.md
 */

// Escape sequences for synchronized output
const SYNC_START = '\x1b[?2026h'; // Begin synchronized update
const SYNC_END = '\x1b[?2026l';   // End synchronized update

/**
 * Creates a write stream wrapper that adds synchronized output sequences.
 * This tells the terminal to buffer all output between START and END,
 * then render it all at once, preventing visual tearing/flickering.
 */
export function createSyncedWriteStream(
  originalWrite: (chunk: string, encoding?: BufferEncoding, callback?: () => void) => boolean
): (chunk: string, encoding?: BufferEncoding, callback?: () => void) => boolean {
  return function syncedWrite(
    chunk: string,
    encoding?: BufferEncoding,
    callback?: () => void
  ): boolean {
    // Wrap the output with synchronized output escape sequences
    const syncedChunk = SYNC_START + chunk + SYNC_END;
    return originalWrite.call(process.stdout, syncedChunk, encoding, callback);
  };
}

/**
 * Wraps process.stdout with synchronized output
 * Call this before render() to enable flicker-free rendering
 */
export function enableSyncedOutput(): () => void {
  const originalWrite = process.stdout.write.bind(process.stdout);

  process.stdout.write = function (
    chunk: string | Uint8Array,
    encodingOrCallback?: BufferEncoding | ((err?: Error | null) => void),
    callback?: (err?: Error | null) => void
  ): boolean {
    // Handle various overload signatures
    let encoding: BufferEncoding | undefined;
    let cb: ((err?: Error | null) => void) | undefined;

    if (typeof encodingOrCallback === 'function') {
      cb = encodingOrCallback;
    } else {
      encoding = encodingOrCallback;
      cb = callback;
    }

    // Convert Uint8Array to string if needed
    const str = typeof chunk === 'string' ? chunk : Buffer.from(chunk).toString();

    // Wrap with sync sequences
    const syncedChunk = SYNC_START + str + SYNC_END;

    return originalWrite(syncedChunk, encoding, cb);
  } as typeof process.stdout.write;

  // Return a function to restore original behavior
  return () => {
    process.stdout.write = originalWrite;
  };
}

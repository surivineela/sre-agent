/**
 * Formatting utilities for terminal output
 */

/**
 * Truncate a string to a maximum length
 */
export function truncate(str: string, maxLength: number, suffix = '...'): string {
  if (str.length <= maxLength) return str;
  return str.slice(0, maxLength - suffix.length) + suffix;
}

/**
 * Format a duration in milliseconds to human-readable string
 */
export function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`;
  const minutes = Math.floor(ms / 60000);
  const seconds = ((ms % 60000) / 1000).toFixed(0);
  return `${minutes}m ${seconds}s`;
}

/**
 * Format bytes to human-readable string
 */
export function formatBytes(bytes: number): string {
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let unitIndex = 0;
  let size = bytes;

  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024;
    unitIndex++;
  }

  return `${size.toFixed(1)} ${units[unitIndex]}`;
}

/**
 * Format a date to human-readable string
 */
export function formatDate(date: Date): string {
  return date.toLocaleString();
}

/**
 * Format a relative time (e.g., "2 minutes ago")
 */
export function formatRelativeTime(date: Date): string {
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffSeconds = Math.floor(diffMs / 1000);
  const diffMinutes = Math.floor(diffSeconds / 60);
  const diffHours = Math.floor(diffMinutes / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffSeconds < 60) return 'just now';
  if (diffMinutes === 1) return '1 minute ago';
  if (diffMinutes < 60) return `${diffMinutes} minutes ago`;
  if (diffHours === 1) return '1 hour ago';
  if (diffHours < 24) return `${diffHours} hours ago`;
  if (diffDays === 1) return 'yesterday';
  if (diffDays < 7) return `${diffDays} days ago`;
  return formatDate(date);
}

/**
 * Wrap text to a maximum width
 */
export function wrapText(text: string, maxWidth: number): string[] {
  const words = text.split(' ');
  const lines: string[] = [];
  let currentLine = '';

  for (const word of words) {
    if (currentLine.length + word.length + 1 <= maxWidth) {
      currentLine += (currentLine ? ' ' : '') + word;
    } else {
      if (currentLine) lines.push(currentLine);
      currentLine = word;
    }
  }

  if (currentLine) lines.push(currentLine);
  return lines;
}

/**
 * Indent text by a number of spaces
 */
export function indent(text: string, spaces: number): string {
  const indentation = ' '.repeat(spaces);
  return text
    .split('\n')
    .map((line) => indentation + line)
    .join('\n');
}

/**
 * Strip ANSI escape codes from a string
 */
export function stripAnsi(str: string): string {
  // eslint-disable-next-line no-control-regex
  return str.replace(/\x1B\[[0-9;]*[mGKH]/g, '');
}

/**
 * Get the visual width of a string (accounting for ANSI codes)
 */
export function getVisualWidth(str: string): number {
  return stripAnsi(str).length;
}

/**
 * Pad a string to a certain width
 */
export function padRight(str: string, width: number): string {
  const visualWidth = getVisualWidth(str);
  if (visualWidth >= width) return str;
  return str + ' '.repeat(width - visualWidth);
}

/**
 * Format a table row
 */
export function formatTableRow(columns: string[], widths: number[]): string {
  return columns.map((col, i) => padRight(truncate(col, widths[i]), widths[i])).join(' │ ');
}

/**
 * Summarize tool input for display
 */
export function summarizeToolInput(input: Record<string, unknown>): string {
  const parts: string[] = [];

  for (const [key, value] of Object.entries(input)) {
    if (typeof value === 'string') {
      parts.push(`${key}: ${truncate(value, 50)}`);
    } else if (Array.isArray(value)) {
      parts.push(`${key}: [${value.length} items]`);
    } else if (typeof value === 'object' && value !== null) {
      parts.push(`${key}: {...}`);
    } else {
      parts.push(`${key}: ${String(value)}`);
    }
  }

  return parts.join(', ');
}

/**
 * Format JSON for display (with syntax highlighting placeholders)
 */
export function formatJSON(data: unknown, maxLines = 20): string {
  const json = JSON.stringify(data, null, 2);
  const lines = json.split('\n');

  if (lines.length <= maxLines) {
    return json;
  }

  return lines.slice(0, maxLines - 1).join('\n') + '\n... (truncated)';
}

/**
 * Generate a unique ID
 */
export function generateId(): string {
  return Math.random().toString(36).substring(2, 15) + Math.random().toString(36).substring(2, 15);
}

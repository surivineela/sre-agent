/**
 * Custom error classes for SRE CLI
 */

/**
 * Base error class for SRE CLI errors
 */
export class SREError extends Error {
  constructor(
    message: string,
    public readonly code: string,
    public readonly details?: Record<string, unknown>
  ) {
    super(message);
    this.name = 'SREError';
    Error.captureStackTrace?.(this, this.constructor);
  }
}

/**
 * Authentication error
 */
export class AuthenticationError extends SREError {
  constructor(message: string, details?: Record<string, unknown>) {
    super(message, 'AUTH_ERROR', details);
    this.name = 'AuthenticationError';
  }
}

/**
 * API error
 */
export class APIError extends SREError {
  constructor(
    message: string,
    public readonly statusCode?: number,
    details?: Record<string, unknown>
  ) {
    super(message, 'API_ERROR', { ...details, statusCode });
    this.name = 'APIError';
  }
}

/**
 * Configuration error
 */
export class ConfigError extends SREError {
  constructor(message: string, details?: Record<string, unknown>) {
    super(message, 'CONFIG_ERROR', details);
    this.name = 'ConfigError';
  }
}

/**
 * Tool not found error
 */
export class ToolNotFoundError extends SREError {
  constructor(toolName: string) {
    super(`Tool not found: ${toolName}`, 'TOOL_NOT_FOUND', { toolName });
    this.name = 'ToolNotFoundError';
  }
}

/**
 * Tool execution error
 */
export class ToolExecutionError extends SREError {
  constructor(
    toolName: string,
    message: string,
    details?: Record<string, unknown>
  ) {
    super(`Tool '${toolName}' failed: ${message}`, 'TOOL_EXECUTION_ERROR', {
      ...details,
      toolName,
    });
    this.name = 'ToolExecutionError';
  }
}

/**
 * Permission denied error
 */
export class PermissionDeniedError extends SREError {
  constructor(toolName: string, reason?: string) {
    super(
      `Permission denied for tool '${toolName}'${reason ? `: ${reason}` : ''}`,
      'PERMISSION_DENIED',
      { toolName, reason }
    );
    this.name = 'PermissionDeniedError';
  }
}

/**
 * Dangerous command error
 */
export class DangerousCommandError extends SREError {
  constructor(command: string, reason: string) {
    super(
      `Dangerous command blocked: ${reason}`,
      'DANGEROUS_COMMAND',
      { command, reason }
    );
    this.name = 'DangerousCommandError';
  }
}

/**
 * Validation error
 */
export class ValidationError extends SREError {
  constructor(message: string, field?: string, details?: Record<string, unknown>) {
    super(message, 'VALIDATION_ERROR', { ...details, field });
    this.name = 'ValidationError';
  }
}

/**
 * Connection error
 */
export class ConnectionError extends SREError {
  constructor(message: string, details?: Record<string, unknown>) {
    super(message, 'CONNECTION_ERROR', details);
    this.name = 'ConnectionError';
  }
}

/**
 * Timeout error
 */
export class TimeoutError extends SREError {
  constructor(operation: string, timeoutMs: number) {
    super(
      `Operation '${operation}' timed out after ${timeoutMs}ms`,
      'TIMEOUT_ERROR',
      { operation, timeoutMs }
    );
    this.name = 'TimeoutError';
  }
}

/**
 * MCP error
 */
export class MCPError extends SREError {
  constructor(message: string, serverName?: string, details?: Record<string, unknown>) {
    super(message, 'MCP_ERROR', { ...details, serverName });
    this.name = 'MCPError';
  }
}

/**
 * Format an error for display (simple string)
 */
export function formatError(error: unknown): string {
  if (error instanceof SREError) {
    return `${error.name}: ${error.message}`;
  }
  if (error instanceof Error) {
    return error.message;
  }
  return String(error);
}

/**
 * Check if an error is retryable
 */
export function isRetryableError(error: unknown): boolean {
  if (error instanceof ConnectionError) return true;
  if (error instanceof TimeoutError) return true;
  if (error instanceof APIError && error.statusCode) {
    return error.statusCode >= 500 || error.statusCode === 429;
  }
  return false;
}

// ============================================================================
// SPEC-012: Enhanced Error Formatting
// ============================================================================

/**
 * Error categories for display formatting
 */
export type ErrorCategory =
  | 'connection'
  | 'auth'
  | 'validation'
  | 'api'
  | 'file'
  | 'timeout'
  | 'unknown';

/**
 * Formatted error with user-friendly details
 */
export interface FormattedError {
  category: ErrorCategory;
  title: string;
  message: string;
  details?: string[];
  suggestions?: string[];
  actions?: ErrorAction[];
  raw?: Error;
}

/**
 * Error action for user interaction
 */
export interface ErrorAction {
  key: string;
  label: string;
  command?: string;
  handler?: () => void;
}

/**
 * Error pattern for matching and formatting
 */
interface ErrorPattern {
  pattern: RegExp;
  category: ErrorCategory;
  title: string;
  suggestions: string[];
}

/**
 * Known error patterns with user-friendly messages
 */
const ERROR_PATTERNS: ErrorPattern[] = [
  {
    pattern: /ECONNREFUSED|connection refused/i,
    category: 'connection',
    title: 'Connection Error',
    suggestions: [
      'Run /status to check connection',
      'Run /init to reconfigure server URL',
      'Check if server is running',
    ],
  },
  {
    pattern: /401|unauthorized|authentication/i,
    category: 'auth',
    title: 'Authentication Error',
    suggestions: [
      'Run /auth to re-authenticate',
      'Check if your token has expired',
      'Verify your credentials',
    ],
  },
  {
    pattern: /403|forbidden/i,
    category: 'auth',
    title: 'Permission Denied',
    suggestions: [
      'You may not have access to this resource',
      'Contact your administrator',
    ],
  },
  {
    pattern: /404|not found/i,
    category: 'api',
    title: 'Not Found',
    suggestions: [
      'The resource may have been deleted',
      'Check if the name/ID is correct',
    ],
  },
  {
    pattern: /timeout|ETIMEDOUT/i,
    category: 'timeout',
    title: 'Request Timeout',
    suggestions: [
      'The server is taking too long to respond',
      'Try again in a moment',
      'Check your network connection',
    ],
  },
  {
    pattern: /yaml|parse error|invalid format|invalid json/i,
    category: 'validation',
    title: 'Validation Error',
    suggestions: [
      'Check the syntax of your input',
      'Verify required fields are present',
      'Run with --dry-run to validate',
    ],
  },
  {
    pattern: /ENOENT|file not found|no such file/i,
    category: 'file',
    title: 'File Not Found',
    suggestions: [
      'Check the file path is correct',
      'Verify the file exists',
    ],
  },
  {
    pattern: /EACCES|permission denied|access denied/i,
    category: 'file',
    title: 'Access Denied',
    suggestions: [
      'Check file permissions',
      'Run with appropriate privileges',
    ],
  },
  {
    pattern: /network|ENETUNREACH|EHOSTUNREACH/i,
    category: 'connection',
    title: 'Network Error',
    suggestions: [
      'Check your internet connection',
      'Verify the server is accessible',
    ],
  },
  {
    pattern: /socket hang up|ECONNRESET/i,
    category: 'connection',
    title: 'Connection Reset',
    suggestions: [
      'The server closed the connection unexpectedly',
      'Try again in a moment',
    ],
  },
];

/**
 * Extract the main message from an error string
 */
function extractMainMessage(message: string): string {
  return message
    .replace(/^Error:\s*/i, '')
    .replace(/^[A-Z_]+:\s*/i, '')
    .trim();
}

/**
 * Format an error into a user-friendly FormattedError object (SPEC-012)
 */
export function formatErrorDetailed(error: Error | string): FormattedError {
  const message = typeof error === 'string' ? error : error.message;
  const rawError = typeof error === 'string' ? undefined : error;

  // Check for SREError types first
  if (rawError instanceof SREError) {
    const category = getCategoryFromSREError(rawError);
    return {
      category,
      title: getTitleFromCategory(category),
      message: extractMainMessage(message),
      suggestions: getSuggestionsForCategory(category),
      raw: rawError,
    };
  }

  // Find matching pattern
  for (const pattern of ERROR_PATTERNS) {
    if (pattern.pattern.test(message)) {
      return {
        category: pattern.category,
        title: pattern.title,
        message: extractMainMessage(message),
        suggestions: pattern.suggestions,
        raw: rawError,
      };
    }
  }

  // Unknown error
  return {
    category: 'unknown',
    title: 'Error',
    message: extractMainMessage(message),
    raw: rawError,
  };
}

/**
 * Get error category from SREError type
 */
function getCategoryFromSREError(error: SREError): ErrorCategory {
  if (error instanceof ConnectionError) return 'connection';
  if (error instanceof AuthenticationError) return 'auth';
  if (error instanceof ValidationError) return 'validation';
  if (error instanceof APIError) return 'api';
  if (error instanceof TimeoutError) return 'timeout';
  return 'unknown';
}

/**
 * Get title from error category
 */
function getTitleFromCategory(category: ErrorCategory): string {
  switch (category) {
    case 'connection': return 'Connection Error';
    case 'auth': return 'Authentication Error';
    case 'validation': return 'Validation Error';
    case 'api': return 'API Error';
    case 'file': return 'File Error';
    case 'timeout': return 'Timeout Error';
    default: return 'Error';
  }
}

/**
 * Get suggestions for error category
 */
function getSuggestionsForCategory(category: ErrorCategory): string[] {
  switch (category) {
    case 'connection':
      return ['Run /status to check connection', 'Run /init to reconfigure'];
    case 'auth':
      return ['Run /auth to re-authenticate'];
    case 'validation':
      return ['Check your input syntax', 'Verify required fields'];
    case 'api':
      return ['Try again in a moment', 'Check server logs'];
    case 'file':
      return ['Check file path and permissions'];
    case 'timeout':
      return ['Try again', 'Check network connection'];
    default:
      return [];
  }
}

/**
 * Get color for error category
 */
export function getErrorCategoryColor(category: ErrorCategory): string {
  switch (category) {
    case 'connection': return 'red';
    case 'auth': return 'yellow';
    case 'validation': return 'magenta';
    case 'api': return 'red';
    case 'file': return 'yellow';
    case 'timeout': return 'yellow';
    default: return 'red';
  }
}

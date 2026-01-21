/**
 * Logger utility for SRE CLI
 */
import { appendFileSync } from 'fs';

export type LogLevel = 'debug' | 'info' | 'warn' | 'error';

interface LoggerConfig {
  level: LogLevel;
  enabled: boolean;
  logFile?: string;
  timestamps: boolean;
}

const LOG_LEVELS: Record<LogLevel, number> = {
  debug: 0,
  info: 1,
  warn: 2,
  error: 3,
};

class Logger {
  private config: LoggerConfig = {
    level: 'info',
    enabled: process.env.DEBUG === 'true' || process.env.SRE_DEBUG === 'true',
    timestamps: true,
  };

  configure(options: Partial<LoggerConfig>): void {
    this.config = { ...this.config, ...options };
  }

  private shouldLog(level: LogLevel): boolean {
    if (!this.config.enabled) return false;
    return LOG_LEVELS[level] >= LOG_LEVELS[this.config.level];
  }

  private formatMessage(level: LogLevel, message: string, data?: unknown): string {
    const parts: string[] = [];

    if (this.config.timestamps) {
      parts.push(`[${new Date().toISOString()}]`);
    }

    parts.push(`[${level.toUpperCase()}]`);
    parts.push(message);

    if (data !== undefined) {
      try {
        parts.push(JSON.stringify(data, null, 2));
      } catch {
        parts.push(String(data));
      }
    }

    return parts.join(' ');
  }

  private writeLog(level: LogLevel, message: string, data?: unknown): void {
    if (!this.shouldLog(level)) return;

    const formatted = this.formatMessage(level, message, data);

    // Write to console (stderr to not interfere with UI)
    if (level === 'error') {
      console.error(formatted);
    } else if (this.config.enabled) {
      console.error(formatted);
    }

    // Write to file if configured
    if (this.config.logFile) {
      try {
        appendFileSync(this.config.logFile, formatted + '\n');
      } catch (err) {
        console.error(`Failed to write to log file: ${err}`);
      }
    }
  }

  debug(message: string, data?: unknown): void {
    this.writeLog('debug', message, data);
  }

  info(message: string, data?: unknown): void {
    this.writeLog('info', message, data);
  }

  warn(message: string, data?: unknown): void {
    this.writeLog('warn', message, data);
  }

  error(message: string, data?: unknown): void {
    this.writeLog('error', message, data);
  }

  /**
   * Log an API request
   */
  apiRequest(method: string, url: string, data?: unknown): void {
    this.debug(`API Request: ${method} ${url}`, data);
  }

  /**
   * Log an API response
   */
  apiResponse(method: string, url: string, status: number, duration: number): void {
    this.debug(`API Response: ${method} ${url} - ${status} (${duration}ms)`);
  }

  /**
   * Log tool execution
   */
  toolExecution(toolName: string, input: unknown): void {
    this.debug(`Tool: ${toolName}`, input);
  }

  /**
   * Log tool result
   */
  toolResult(toolName: string, success: boolean, duration: number): void {
    this.debug(`Tool Result: ${toolName} - ${success ? 'OK' : 'FAILED'} (${duration}ms)`);
  }

  /**
   * Create a child logger with a prefix
   */
  child(prefix: string): ChildLogger {
    return new ChildLogger(this, prefix);
  }
}

class ChildLogger {
  constructor(
    private parent: Logger,
    private prefix: string
  ) {}

  debug(message: string, data?: unknown): void {
    this.parent.debug(`[${this.prefix}] ${message}`, data);
  }

  info(message: string, data?: unknown): void {
    this.parent.info(`[${this.prefix}] ${message}`, data);
  }

  warn(message: string, data?: unknown): void {
    this.parent.warn(`[${this.prefix}] ${message}`, data);
  }

  error(message: string, data?: unknown): void {
    this.parent.error(`[${this.prefix}] ${message}`, data);
  }
}

// Singleton logger instance
export const logger = new Logger();

// Export Logger class for testing
export { Logger };

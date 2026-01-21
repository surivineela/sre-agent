/**
 * System message helper utilities
 * Factory functions for creating formatted system messages
 */
import { v4 as uuid } from 'uuid';
import type { SystemMessage, SystemMessageType } from '../types';

/**
 * Create a system message with the specified type
 */
export const createSystemMessage = (
  type: SystemMessageType,
  content: string,
  options?: {
    title?: string;
    action?: { label: string; command: string };
  }
): SystemMessage => ({
  id: uuid(),
  type,
  content,
  title: options?.title,
  action: options?.action,
  timestamp: new Date(),
});

/**
 * Create an info message (centered, subtle)
 */
export const info = (content: string): SystemMessage =>
  createSystemMessage('info', content);

/**
 * Create a success message (green checkmark)
 */
export const success = (content: string): SystemMessage =>
  createSystemMessage('success', content);

/**
 * Create a warning message (yellow, bordered)
 */
export const warning = (content: string, title?: string): SystemMessage =>
  createSystemMessage('warning', content, { title });

/**
 * Create an error message (red, bordered)
 */
export const error = (content: string, title?: string): SystemMessage =>
  createSystemMessage('error', content, { title });

/**
 * Create a divider (session boundary)
 */
export const divider = (content: string): SystemMessage =>
  createSystemMessage('divider', content);

/**
 * Create a hint message (lightbulb icon)
 */
export const hint = (content: string): SystemMessage =>
  createSystemMessage('hint', content);

/**
 * All system message factory functions
 */
export const systemMessages = {
  info,
  success,
  warning,
  error,
  divider,
  hint,
  create: createSystemMessage,
};

export default systemMessages;

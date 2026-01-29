/**
 * Command Types - Shared type definitions to avoid circular dependencies
 */

import type { Services } from '../types';

/**
 * Command execution context
 */
export interface CommandContext {
  services: Services;
  args: string[];
  rawInput: string;
  // Callbacks for UI interaction
  onOutput: (content: string) => void;
  onClear: () => void;
  onExit: () => void;
  onStateChange: (state: Partial<CommandState>) => void;
}

/**
 * State that commands can modify
 */
export interface CommandState {
  compactMode: boolean;
  verboseMode: boolean;
  showTimestamps: boolean;
  currentPlan: string | null;
}

/**
 * Editor configuration for inline editing
 */
export interface EditorConfig {
  content: string;
  filename: string;
  filePath: string;
  fileType?: 'yaml' | 'json' | 'python' | 'text';
  readOnly?: boolean;
}

/**
 * Wizard option for multi-step flows
 */
export interface WizardOption {
  key: string;
  label: string;
  description?: string;
}

/**
 * Wizard step configuration
 */
export interface WizardStep {
  id: string;
  title: string;
  prompt: string;
  type: 'select' | 'input' | 'confirm';
  options?: WizardOption[];
  placeholder?: string;
  defaultValue?: string;
}

/**
 * Wizard configuration for multi-step interactive flows
 */
export interface WizardConfig {
  id: string;
  title: string;
  steps: WizardStep[];
  currentStep: number;
  data: Record<string, string>;
  onComplete: (data: Record<string, string>) => Promise<CommandResult>;
}

/**
 * Trace view configuration for full-screen trace display
 */
export interface TraceViewConfig {
  threadId?: string;
  agentName?: string;
}

/**
 * Command result
 */
export interface CommandResult {
  success: boolean;
  message?: string;
  silent?: boolean; // Don't show any output
  shouldExit?: boolean;
  editor?: EditorConfig; // Open inline editor
  wizard?: WizardConfig; // Start multi-step wizard
  traceView?: TraceViewConfig; // Open full-screen trace view
}

/**
 * Slash command definition
 */
export interface SlashCommand {
  name: string;
  aliases?: string[];
  description: string;
  usage?: string;
  examples?: string[];
  execute: (ctx: CommandContext) => Promise<CommandResult>;
}

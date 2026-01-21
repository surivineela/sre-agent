/**
 * Configuration schema using Zod for validation and type inference
 */
import { z } from 'zod';

// Server configuration schema
export const ServerConfigSchema = z.object({
  url: z.string().url().default('http://localhost:5000'),
  authRequired: z.boolean().default(true),
  timeout: z.number().min(1000).max(300000).default(30000),
});

// Profile schema
export const ProfileSchema = z.object({
  name: z.string().min(1),
  serverUrl: z.string().url(),
  authRequired: z.boolean().default(true),
  isDefault: z.boolean().default(false),
});

// UI configuration schema
export const UIConfigSchema = z.object({
  colorScheme: z.enum(['auto', 'dark', 'light']).default('auto'),
  unicodeSupport: z.boolean().default(true),
  animationsEnabled: z.boolean().default(true),
  compactMode: z.boolean().default(false),
});

// Agent behavior configuration schema
export const AgentConfigSchema = z.object({
  maxIterations: z.number().min(1).max(100).default(10),
  maxTokens: z.number().min(1000).max(100000).default(8192),
  temperature: z.number().min(0).max(2).default(0.7),
  enableExtendedThinking: z.boolean().default(true),
});

// Permissions configuration schema
export const PermissionsConfigSchema = z.object({
  allowlist: z.array(z.string()).default([]),
  denylist: z.array(z.string()).default([]),
  bashAllowPatterns: z.array(z.string()).default([
    'git *',
    'npm *',
    'kubectl get *',
    'az account show',
    'ls *',
    'cat *',
    'pwd',
    'echo *',
  ]),
});

// MCP server configuration schema
export const MCPServerConfigSchema = z.object({
  name: z.string().min(1),
  command: z.string().min(1),
  args: z.array(z.string()).optional(),
  env: z.record(z.string()).optional(),
  enabled: z.boolean().default(true),
});

// Debug configuration schema
export const DebugConfigSchema = z.object({
  enabled: z.boolean().default(false),
  logFile: z.string().optional(),
  verboseApi: z.boolean().default(false),
});

// Main configuration schema
export const ConfigSchema = z.object({
  server: ServerConfigSchema.default({}),
  profiles: z.record(ProfileSchema).default({}),
  currentProfile: z.string().optional(),
  ui: UIConfigSchema.default({}),
  agent: AgentConfigSchema.default({}),
  permissions: PermissionsConfigSchema.default({}),
  mcpServers: z.record(MCPServerConfigSchema.omit({ name: true })).default({}),
  debug: DebugConfigSchema.default({}),
});

// Export types inferred from schemas
export type ServerConfig = z.infer<typeof ServerConfigSchema>;
export type Profile = z.infer<typeof ProfileSchema>;
export type UIConfig = z.infer<typeof UIConfigSchema>;
export type AgentConfig = z.infer<typeof AgentConfigSchema>;
export type PermissionsConfig = z.infer<typeof PermissionsConfigSchema>;
export type MCPServerConfig = z.infer<typeof MCPServerConfigSchema>;
export type DebugConfig = z.infer<typeof DebugConfigSchema>;
export type Config = z.infer<typeof ConfigSchema>;

/**
 * Get default configuration
 */
export function getDefaultConfig(): Config {
  return ConfigSchema.parse({});
}

/**
 * Validate configuration
 */
export function validateConfig(config: unknown): Config {
  return ConfigSchema.parse(config);
}

/**
 * Merge configurations (deep merge)
 */
export function mergeConfig(base: Config, updates: Partial<Config>): Config {
  return ConfigSchema.parse({
    ...base,
    ...updates,
    server: { ...base.server, ...updates.server },
    ui: { ...base.ui, ...updates.ui },
    agent: { ...base.agent, ...updates.agent },
    permissions: { ...base.permissions, ...updates.permissions },
    debug: { ...base.debug, ...updates.debug },
    profiles: { ...base.profiles, ...updates.profiles },
    mcpServers: { ...base.mcpServers, ...updates.mcpServers },
  });
}

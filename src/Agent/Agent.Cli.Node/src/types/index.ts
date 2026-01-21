/**
 * Core types for SRE CLI
 */

// ============================================================================
// Message Types
// ============================================================================

export type MessageRole = 'user' | 'assistant' | 'system' | 'tool';

export interface Message {
  id: string;
  role: MessageRole;
  content: string;
  timestamp: Date;
  toolCalls?: ToolCall[];
  toolResults?: ToolResult[];
  isStreaming?: boolean;
  reasoningLines?: string[];
}

// ============================================================================
// Tool Types
// ============================================================================

export interface ToolCall {
  id: string;
  name: string;
  input: Record<string, unknown>;
}

export interface ToolResult {
  id: string;
  success: boolean;
  output?: unknown;
  error?: string;
  duration?: number;
}

export type ToolCategory =
  | 'file_system'
  | 'api_call'
  | 'agent_management'
  | 'shell_execution'
  | 'information'
  | 'mcp';

export type PermissionLevel = 'none' | 'once' | 'session' | 'always';

export interface ToolDefinition {
  name: string;
  description: string;
  inputSchema: Record<string, unknown>;
  execute: (input: unknown, context: ToolContext) => Promise<ToolOutput>;
  requiresPermission?: PermissionLevel;
  category: ToolCategory;
}

export interface ToolContext {
  api: APIServiceInterface;
  config: Config;
  cwd: string;
  abortSignal?: AbortSignal;
}

export interface ToolOutput {
  success?: boolean;
  [key: string]: unknown;
}

// ============================================================================
// API Types
// ============================================================================

export interface Agent {
  name: string;
  instructions: string;
  tools?: string[];
  handoffs?: string[];
  temperature?: number;
  model?: string;
  createdAt?: Date;
  updatedAt?: Date;
}

export interface AgentSpec {
  name: string;
  instructions: string;
  tools?: string[];
  handoffs?: string[];
  temperature?: number;
  model?: string;
}

export interface Tool {
  name: string;
  description: string;
  type: string;
  inputSchema?: Record<string, unknown>;
}

export interface Thread {
  id: string;
  agentName: string;
  status: ThreadStatus;
  createdAt: Date;
  updatedAt?: Date;
}

export type ThreadStatus = 'active' | 'completed' | 'failed' | 'cancelled';

export interface ThreadMessage {
  id: string;
  role: MessageRole;
  content: string;
  timestamp: Date;
  metadata?: Record<string, unknown>;
}

export interface Skill {
  name: string;
  description: string;
  agentName?: string;
}

export interface ScheduledTask {
  id: string;
  name: string;
  schedule: string;
  agentName: string;
  enabled: boolean;
}

export interface ScheduledTaskSpec {
  name: string;
  schedule: string;
  agentName: string;
  message: string;
}

export interface SmartAgentResult {
  name: string;
  instructions: string;
  tools: string[];
}

// ============================================================================
// Configuration Types
// ============================================================================

export interface ServerConfig {
  url: string;
  authRequired: boolean;
  timeout: number;
}

export interface Profile {
  name: string;
  serverUrl: string;
  authRequired: boolean;
  isDefault: boolean;
}

export interface UIConfig {
  colorScheme: 'auto' | 'dark' | 'light';
  unicodeSupport: boolean;
  animationsEnabled: boolean;
  compactMode: boolean;
}

export interface AgentConfig {
  maxIterations: number;
  maxTokens: number;
  temperature: number;
  enableExtendedThinking: boolean;
}

export interface PermissionsConfig {
  allowlist: string[];
  denylist: string[];
  bashAllowPatterns: string[];
}

export interface MCPServerConfig {
  name?: string; // Name is the key in mcpServers record, so optional in value
  command: string;
  args?: string[];
  env?: Record<string, string>;
  enabled: boolean;
}

export interface DebugConfig {
  enabled: boolean;
  logFile?: string;
  verboseApi: boolean;
}

export interface Config {
  server: ServerConfig;
  profiles: Record<string, Profile>;
  currentProfile?: string;
  ui: UIConfig;
  agent: AgentConfig;
  permissions: PermissionsConfig;
  mcpServers: Record<string, MCPServerConfig>;
  debug: DebugConfig;
}

// ============================================================================
// Session Types
// ============================================================================

export interface Session {
  id: string;
  messages: Message[];
  agentName?: string;
  threadId?: string;
  startedAt: Date;
}

// ============================================================================
// Permission Types
// ============================================================================

export interface Permission {
  tool: string;
  scope: 'once' | 'session' | 'always';
  grantedAt: Date;
  expiresAt?: Date;
}

export interface PermissionResult {
  granted: boolean;
  reason?: string;
  requiresConfirmation?: boolean;
  warningLevel?: 'normal' | 'high';
}

export type PermissionChoice = 'once' | 'session' | 'deny';

// ============================================================================
// CLI Types
// ============================================================================

export interface CLIOptions {
  debug?: boolean;
  quiet?: boolean;
  noColor?: boolean;
  batch?: boolean;
  config?: string;
  profile?: string;
  output?: 'json' | 'text';
}

export type ConnectionStatus = 'connected' | 'disconnected' | 'connecting';

// ============================================================================
// Enhanced Connection State (SPEC-011)
// ============================================================================

export interface EnhancedConnectionState {
  status: 'connected' | 'connecting' | 'disconnected' | 'reconnecting' | 'auth-required';
  attempt: number;
  maxAttempts: number;
  nextRetryIn: number; // seconds
  lastError?: string;
  serverUrl?: string;
}

// ============================================================================
// Stream Types
// ============================================================================

export interface StreamChunk {
  type: 'text' | 'tool_call' | 'tool_result' | 'done' | 'error';
  content?: string;
  toolCall?: ToolCall;
  toolResult?: ToolResult;
  error?: string;
}

// ============================================================================
// Agentic Loop Types
// ============================================================================

export interface AgenticLoopConfig {
  maxIterations: number;
  maxTokens: number;
  timeoutMs: number;
  enableExtendedThinking: boolean;
}

export type LoopStatus =
  | 'idle'
  | 'thinking'
  | 'tool_execution'
  | 'streaming'
  | 'awaiting_permission';

export interface LoopState {
  messages: Message[];
  pendingTools: ToolCall[];
  iteration: number;
  status: LoopStatus;
}

export interface AgenticLoopCallbacks {
  onThinking?: () => void;
  onStream?: (chunk: string, messageId: string) => void;
  onToolStart?: (tool: ToolCall) => void;
  onToolComplete?: (tool: ToolCall, result: ToolResult) => void;
  onPermissionRequest?: (tool: ToolCall) => Promise<boolean>;
  onError?: (error: Error) => void;
}

// ============================================================================
// API Context Types
// ============================================================================

export interface APIContext {
  systemPrompt: string;
  messages: APIMessage[];
  tools: APIToolDefinition[];
  maxTokens: number;
  temperature: number;
}

export interface APIMessage {
  role: 'user' | 'assistant' | 'system';
  content: string | APIContentBlock[];
}

export type APIContentBlock =
  | { type: 'text'; text: string }
  | { type: 'tool_use'; id: string; name: string; input: Record<string, unknown> }
  | { type: 'tool_result'; tool_use_id: string; content: string };

export interface APIToolDefinition {
  name: string;
  description: string;
  input_schema: Record<string, unknown>;
}

// ============================================================================
// Service Interfaces
// ============================================================================

export interface AgentMetadata {
  name: string;
  subscriptionId: string;
  resourceGroup: string;
}

export interface APIServiceInterface {
  connectionStatus: ConnectionStatus;
  connect(): Promise<void>;
  testConnection(): Promise<{ success: boolean; message: string }>;
  getAgentMetadata(): Promise<AgentMetadata | null>;
  getAppInsightsAppIdFromArm(): Promise<string | null>;
  createAgent(spec: AgentSpec): Promise<Agent>;
  listAgents(): Promise<Agent[]>;
  getAgent(name: string): Promise<Agent>;
  deleteAgent(name: string): Promise<void>;
  generateSmartAgent(name: string, instructions?: string): Promise<SmartAgentResult>;
  listTools(): Promise<Tool[]>;
  createThread(agentName: string, message: string): Promise<Thread>;
  sendMessage(threadId: string, message: string): Promise<ThreadMessage>;
  trackThread(threadId: string, maxWaitSeconds?: number): Promise<ThreadMessage[]>;
  streamThreadMessages(threadId: string): AsyncGenerator<ThreadMessage>;
  listSkills(): Promise<Skill[]>;
  convertAgentToSkill(agentName: string): Promise<Skill>;
  listScheduledTasks(): Promise<ScheduledTask[]>;
  createScheduledTask(task: ScheduledTaskSpec): Promise<ScheduledTask>;
}

export interface AuthServiceInterface {
  getToken(): Promise<string>;
  storeApiKey(apiKey: string): Promise<void>;
  getStoredApiKey(): Promise<string | null>;
  clearCredentials(): Promise<void>;
}

export interface ConfigServiceInterface {
  load(): Promise<Config>;
  get(): Config;
  save(updates: Partial<Config>): Promise<void>;
  initialize(serverUrl: string): Promise<void>;
  getServerUrl(): string;
}

export interface Services {
  api: APIServiceInterface;
  auth: AuthServiceInterface;
  config: ConfigServiceInterface;
  tools: ToolRegistryInterface;
}

export interface ToolRegistryInterface {
  register(tool: ToolDefinition): void;
  get(name: string): ToolDefinition | undefined;
  getAll(): ToolDefinition[];
  getToolsForAPI(): APIToolDefinition[];
  execute(name: string, input: unknown, context: ToolContext): Promise<ToolOutput>;
  connectMCPServer(config: MCPServerConfig): Promise<void>;
}

// ============================================================================
// Slash Command Types
// ============================================================================

export interface SlashCommand {
  name: string;
  description: string;
  category: string;
  execute?: (args: string[], services: Services) => Promise<void>;
}

// ============================================================================
// MCP Types
// ============================================================================

export interface MCPTool {
  name: string;
  description?: string;
  inputSchema: Record<string, unknown>;
}

// ============================================================================
// Batch Mode Types
// ============================================================================

export interface BatchOptions {
  input?: string;
  output?: 'json' | 'text';
  timeout?: number;
  exitOnError?: boolean;
}

export interface BatchResult {
  success: boolean;
  response?: string;
  toolCalls?: ToolCall[];
  error?: string;
}

// ============================================================================
// Event Types
// ============================================================================

export type AppEvent =
  | { type: 'status_change'; status: LoopStatus }
  | { type: 'tool_start'; tool: ToolCall }
  | { type: 'tool_complete'; tool: ToolCall; result: ToolResult }
  | { type: 'tool_error'; tool: ToolCall; error: Error }
  | { type: 'tool_denied'; tool: ToolCall }
  | { type: 'stream_chunk'; chunk: string }
  | { type: 'error'; error: Error };

// ============================================================================
// System Message Types (for formatted system notifications)
// ============================================================================

export type SystemMessageType =
  | 'info'      // General information (centered, subtle)
  | 'success'   // Operation succeeded (green checkmark)
  | 'warning'   // Warning/caution (yellow, bordered)
  | 'error'     // Error occurred (red, bordered)
  | 'divider'   // Session/section divider
  | 'hint';     // Helpful tip

export interface SystemMessage {
  id: string;
  type: SystemMessageType;
  content: string;
  title?: string;  // For bordered messages
  timestamp: Date;
  action?: {
    label: string;
    command: string;
  };
}

/**
 * Permission system for tool execution
 */
import type {
  Permission,
  PermissionResult,
  ToolCall,
  ToolDefinition,
  Config,
} from '../types';
import { logger } from '../utils/logger';

/**
 * Permission manager for controlling tool access
 */
export class PermissionManager {
  private grantedPermissions: Map<string, Permission> = new Map();
  private config: Config;
  private toolRegistry: Map<string, ToolDefinition> = new Map();

  constructor(config: Config) {
    this.config = config;
  }

  /**
   * Update configuration
   */
  setConfig(config: Config): void {
    this.config = config;
  }

  /**
   * Register a tool for permission checking
   */
  registerTool(tool: ToolDefinition): void {
    this.toolRegistry.set(tool.name, tool);
  }

  /**
   * Check if a tool call is permitted
   */
  async checkPermission(tool: ToolCall): Promise<PermissionResult> {
    const toolName = tool.name;

    // 1. Check deny list first
    if (this.isDenied(toolName)) {
      return {
        granted: false,
        reason: 'Tool is blocked by policy',
      };
    }

    // 2. Check allow list
    if (this.isAllowed(toolName)) {
      return { granted: true };
    }

    // 3. Check already granted session permissions
    const existing = this.getGrantedPermission(tool);
    if (existing && this.isValid(existing)) {
      return { granted: true };
    }

    // 4. Get tool definition
    const toolDef = this.toolRegistry.get(toolName);

    // 5. Check if tool requires no permission
    if (!toolDef || toolDef.requiresPermission === 'none') {
      return { granted: true };
    }

    // 6. Static analysis for dangerous patterns
    const analysis = await this.analyzeToolCall(tool);
    if (analysis.dangerous) {
      return {
        granted: false,
        requiresConfirmation: true,
        warningLevel: 'high',
        reason: analysis.reason,
      };
    }

    // 7. Permission required
    return {
      granted: false,
      requiresConfirmation: true,
      warningLevel: 'normal',
    };
  }

  /**
   * Grant permission for a tool
   */
  grantPermission(tool: ToolCall, scope: 'once' | 'session'): void {
    const key = this.getPermissionKey(tool);
    const permission: Permission = {
      tool: tool.name,
      scope,
      grantedAt: new Date(),
    };

    this.grantedPermissions.set(key, permission);
    logger.debug('Permission granted', { tool: tool.name, scope });
  }

  /**
   * Revoke a permission
   */
  revokePermission(key: string): void {
    this.grantedPermissions.delete(key);
    logger.debug('Permission revoked', { key });
  }

  /**
   * Clear all permissions
   */
  clearPermissions(): void {
    this.grantedPermissions.clear();
    logger.debug('All permissions cleared');
  }

  /**
   * Check if a tool is in the deny list
   */
  private isDenied(toolName: string): boolean {
    return this.config.permissions.denylist.some((pattern) =>
      this.matchesPattern(toolName, pattern)
    );
  }

  /**
   * Check if a tool is in the allow list
   */
  private isAllowed(toolName: string): boolean {
    return this.config.permissions.allowlist.some((pattern) =>
      this.matchesPattern(toolName, pattern)
    );
  }

  /**
   * Check if a bash command matches allowed patterns
   */
  isBashCommandAllowed(command: string): boolean {
    return this.config.permissions.bashAllowPatterns.some((pattern) =>
      this.matchesBashPattern(command, pattern)
    );
  }

  /**
   * Match tool name against a pattern (supports wildcards)
   */
  private matchesPattern(name: string, pattern: string): boolean {
    if (pattern === '*') return true;
    if (pattern.endsWith('*')) {
      const prefix = pattern.slice(0, -1);
      return name.startsWith(prefix);
    }
    return name === pattern;
  }

  /**
   * Match bash command against a pattern
   */
  private matchesBashPattern(command: string, pattern: string): boolean {
    // Simple pattern matching: "git *" matches "git status"
    if (pattern.endsWith(' *')) {
      const prefix = pattern.slice(0, -2);
      return command.startsWith(prefix + ' ') || command === prefix;
    }
    return command === pattern || command.startsWith(pattern + ' ');
  }

  /**
   * Get permission key for a tool call
   */
  private getPermissionKey(tool: ToolCall): string {
    // For bash commands, include a hash of the command
    if (tool.name === 'bash' && tool.input && typeof tool.input.command === 'string') {
      const cmd = tool.input.command.split(' ')[0]; // First word only
      return `${tool.name}:${cmd}`;
    }
    return tool.name;
  }

  /**
   * Get granted permission for a tool
   */
  private getGrantedPermission(tool: ToolCall): Permission | undefined {
    const key = this.getPermissionKey(tool);
    return this.grantedPermissions.get(key);
  }

  /**
   * Check if a permission is still valid
   */
  private isValid(permission: Permission): boolean {
    // 'once' permissions are consumed immediately
    if (permission.scope === 'once') {
      return false;
    }

    // Session permissions are valid for the session
    if (permission.scope === 'session') {
      return true;
    }

    // Check expiration
    if (permission.expiresAt && new Date() > permission.expiresAt) {
      return false;
    }

    return true;
  }

  /**
   * Analyze a tool call for dangerous patterns
   */
  private async analyzeToolCall(
    tool: ToolCall
  ): Promise<{ dangerous: boolean; reason?: string }> {
    if (tool.name === 'bash') {
      return this.analyzeBashCommand(tool.input as { command: string });
    }

    if (tool.name === 'write_file') {
      return this.analyzeFileWrite(tool.input as { path: string });
    }

    return { dangerous: false };
  }

  /**
   * Analyze a bash command for dangerous patterns
   */
  private analyzeBashCommand(input: { command: string }): {
    dangerous: boolean;
    reason?: string;
  } {
    const command = input.command.toLowerCase();

    // Dangerous patterns
    const dangerousPatterns = [
      { pattern: 'rm -rf /', reason: 'Recursive deletion of root directory' },
      { pattern: 'rm -rf ~', reason: 'Recursive deletion of home directory' },
      { pattern: '> /dev/sda', reason: 'Direct disk write' },
      { pattern: 'dd if=/dev/zero', reason: 'Disk overwrite' },
      { pattern: 'mkfs', reason: 'Filesystem format' },
      { pattern: ':(){:|:&};:', reason: 'Fork bomb' },
      { pattern: 'chmod -r 777 /', reason: 'Recursive permission change on root' },
      { pattern: 'curl | sh', reason: 'Remote script execution' },
      { pattern: 'wget | sh', reason: 'Remote script execution' },
      { pattern: '| bash', reason: 'Piped script execution' },
    ];

    for (const { pattern, reason } of dangerousPatterns) {
      if (command.includes(pattern)) {
        return { dangerous: true, reason };
      }
    }

    return { dangerous: false };
  }

  /**
   * Analyze a file write for dangerous patterns
   */
  private analyzeFileWrite(input: { path: string }): {
    dangerous: boolean;
    reason?: string;
  } {
    const path = input.path.toLowerCase();

    // Protected paths
    const protectedPaths = [
      { pattern: '/etc/', reason: 'System configuration directory' },
      { pattern: '/usr/', reason: 'System binaries directory' },
      { pattern: '/bin/', reason: 'System binaries directory' },
      { pattern: '/sbin/', reason: 'System admin binaries directory' },
      { pattern: '/boot/', reason: 'Boot directory' },
      { pattern: 'c:\\windows', reason: 'Windows system directory' },
      { pattern: 'c:\\program files', reason: 'Program files directory' },
    ];

    for (const { pattern, reason } of protectedPaths) {
      if (path.startsWith(pattern)) {
        return { dangerous: true, reason };
      }
    }

    return { dangerous: false };
  }

  /**
   * Get all granted permissions
   */
  getGrantedPermissions(): Permission[] {
    return Array.from(this.grantedPermissions.values());
  }

  /**
   * Check if any permission is granted for a tool
   */
  hasAnyPermission(toolName: string): boolean {
    for (const [key, permission] of this.grantedPermissions) {
      if (key.startsWith(toolName) && this.isValid(permission)) {
        return true;
      }
    }
    return false;
  }
}

/**
 * Create a new permission manager
 */
export function createPermissionManager(config: Config): PermissionManager {
  return new PermissionManager(config);
}

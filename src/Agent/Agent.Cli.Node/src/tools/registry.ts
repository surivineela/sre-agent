/**
 * Tool registry - Manages all available tools
 */
import { z } from 'zod';
import type {
  ToolDefinition,
  ToolContext,
  ToolOutput,
  ToolRegistryInterface,
  APIToolDefinition,
  MCPServerConfig,
} from '../types';
import { ToolNotFoundError, ToolExecutionError } from '../utils/errors';
import { logger } from '../utils/logger';

/**
 * Convert Zod schema to JSON Schema
 */
function zodToJsonSchema(schema: z.ZodSchema): Record<string, unknown> {
  // Simple conversion - in production, use a library like zod-to-json-schema
  if (schema instanceof z.ZodObject) {
    const shape = schema.shape;
    const properties: Record<string, unknown> = {};
    const required: string[] = [];

    for (const [key, value] of Object.entries(shape)) {
      const zodSchema = value as z.ZodSchema;
      properties[key] = zodFieldToJsonSchema(zodSchema);

      // Check if required (not optional)
      if (!(zodSchema instanceof z.ZodOptional)) {
        required.push(key);
      }
    }

    return {
      type: 'object',
      properties,
      required: required.length > 0 ? required : undefined,
    };
  }

  return { type: 'object' };
}

/**
 * Convert a Zod field to JSON Schema
 */
function zodFieldToJsonSchema(schema: z.ZodSchema): Record<string, unknown> {
  if (schema instanceof z.ZodString) {
    return { type: 'string' };
  }
  if (schema instanceof z.ZodNumber) {
    return { type: 'number' };
  }
  if (schema instanceof z.ZodBoolean) {
    return { type: 'boolean' };
  }
  if (schema instanceof z.ZodArray) {
    return {
      type: 'array',
      items: zodFieldToJsonSchema(schema.element),
    };
  }
  if (schema instanceof z.ZodOptional) {
    return zodFieldToJsonSchema(schema.unwrap());
  }
  if (schema instanceof z.ZodDefault) {
    const inner = zodFieldToJsonSchema(schema._def.innerType);
    return { ...inner, default: schema._def.defaultValue() };
  }
  if (schema instanceof z.ZodEnum) {
    return {
      type: 'string',
      enum: schema._def.values,
    };
  }
  if (schema instanceof z.ZodObject) {
    return zodToJsonSchema(schema);
  }

  return { type: 'string' };
}

/**
 * Tool registry for managing all available tools
 */
export class ToolRegistry implements ToolRegistryInterface {
  private tools: Map<string, ToolDefinition> = new Map();
  private mcpClients: Map<string, unknown> = new Map(); // MCP clients will be typed later

  /**
   * Register a tool
   */
  register(tool: ToolDefinition): void {
    this.tools.set(tool.name, tool);
    logger.debug('Registered tool', { name: tool.name, category: tool.category });
  }

  /**
   * Register multiple tools
   */
  registerAll(tools: ToolDefinition[]): void {
    for (const tool of tools) {
      this.register(tool);
    }
  }

  /**
   * Get a tool by name
   */
  get(name: string): ToolDefinition | undefined {
    return this.tools.get(name);
  }

  /**
   * Get all registered tools
   */
  getAll(): ToolDefinition[] {
    return Array.from(this.tools.values());
  }

  /**
   * Get tools formatted for API
   */
  getToolsForAPI(): APIToolDefinition[] {
    return this.getAll().map((tool) => ({
      name: tool.name,
      description: tool.description,
      input_schema: tool.inputSchema,
    }));
  }

  /**
   * Execute a tool
   */
  async execute(
    name: string,
    input: unknown,
    context: ToolContext
  ): Promise<ToolOutput> {
    const tool = this.tools.get(name);

    if (!tool) {
      throw new ToolNotFoundError(name);
    }

    logger.toolExecution(name, input);
    const startTime = Date.now();

    try {
      const result = await tool.execute(input, context);
      logger.toolResult(name, true, Date.now() - startTime);
      return result;
    } catch (error) {
      logger.toolResult(name, false, Date.now() - startTime);
      const message = error instanceof Error ? error.message : String(error);
      throw new ToolExecutionError(name, message);
    }
  }

  /**
   * Check if a tool exists
   */
  has(name: string): boolean {
    return this.tools.has(name);
  }

  /**
   * Remove a tool
   */
  remove(name: string): boolean {
    return this.tools.delete(name);
  }

  /**
   * Get tools by category
   */
  getByCategory(category: string): ToolDefinition[] {
    return this.getAll().filter((tool) => tool.category === category);
  }

  /**
   * Connect to an MCP server and import its tools
   */
  async connectMCPServer(config: MCPServerConfig): Promise<void> {
    logger.info('Connecting to MCP server', { name: config.name });

    // TODO: Implement MCP client connection
    // This will be implemented when we add full MCP support
    logger.warn('MCP server connection not yet implemented');
  }

  /**
   * Disconnect from an MCP server
   */
  async disconnectMCPServer(name: string): Promise<void> {
    const client = this.mcpClients.get(name);
    if (client) {
      // TODO: Properly disconnect
      this.mcpClients.delete(name);

      // Remove tools from this server
      for (const [toolName] of this.tools) {
        if (toolName.startsWith(`${name}__`)) {
          this.tools.delete(toolName);
        }
      }

      logger.info('Disconnected from MCP server', { name });
    }
  }

  /**
   * Get count of registered tools
   */
  get count(): number {
    return this.tools.size;
  }

  /**
   * Clear all tools
   */
  clear(): void {
    this.tools.clear();
    this.mcpClients.clear();
  }
}

/**
 * Create a new tool registry
 */
export function createToolRegistry(): ToolRegistry {
  return new ToolRegistry();
}

// Export helper for creating tools
export { zodToJsonSchema };

/**
 * Tools module exports
 */
import type { ToolRegistryInterface } from '../types';
import { ToolRegistry, createToolRegistry } from './registry';
import { allBuiltinTools } from './builtins';

export { ToolRegistry, createToolRegistry, zodToJsonSchema } from './registry';
export * from './builtins';

/**
 * Initialize the tool registry with built-in tools
 */
export function initializeToolRegistry(): ToolRegistry {
  const registry = createToolRegistry();

  // Register all built-in tools
  registry.registerAll(allBuiltinTools);

  return registry;
}

/**
 * Create a full tool registry interface for services
 */
export function createToolRegistryWithServices(): ToolRegistryInterface {
  const registry = initializeToolRegistry();
  return registry;
}

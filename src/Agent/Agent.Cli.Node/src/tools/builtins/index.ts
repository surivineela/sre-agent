/**
 * Built-in tools exports
 */
import type { ToolDefinition } from '../../types';
import { agentTools } from './agent';
import { fileTools } from './file';
import { shellTools } from './shell';
import { threadTools } from './thread';

export { agentTools } from './agent';
export { fileTools } from './file';
export { shellTools } from './shell';
export { threadTools } from './thread';

/**
 * All built-in tools
 */
export const allBuiltinTools: ToolDefinition[] = [
  ...agentTools,
  ...fileTools,
  ...shellTools,
  ...threadTools,
];

/**
 * Get built-in tools by category
 */
export function getBuiltinToolsByCategory(category: string): ToolDefinition[] {
  return allBuiltinTools.filter((tool) => tool.category === category);
}

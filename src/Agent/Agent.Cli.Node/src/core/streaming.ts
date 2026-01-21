/**
 * Streaming response handler for SSE and WebSocket streams
 */
import type { StreamChunk, ToolCall, ToolResult } from '../types';

/**
 * Parse SSE (Server-Sent Events) stream
 */
export async function* parseSSEStream(
  response: Response
): AsyncGenerator<StreamChunk> {
  const reader = response.body?.getReader();
  const decoder = new TextDecoder();

  if (!reader) {
    throw new Error('No response body');
  }

  let buffer = '';

  try {
    while (true) {
      const { done, value } = await reader.read();

      if (done) break;

      buffer += decoder.decode(value, { stream: true });

      // Parse SSE events
      const lines = buffer.split('\n');
      buffer = lines.pop() || ''; // Keep incomplete line in buffer

      for (const line of lines) {
        // Skip empty lines and comments
        if (!line || line.startsWith(':')) continue;

        if (line.startsWith('data: ')) {
          const data = line.slice(6);

          // Handle end of stream
          if (data === '[DONE]') {
            yield { type: 'done' };
            return;
          }

          try {
            const parsed = JSON.parse(data);
            yield parseStreamData(parsed);
          } catch {
            // Skip invalid JSON
          }
        }
      }
    }
  } finally {
    reader.releaseLock();
  }
}

/**
 * Parse stream data into StreamChunk
 */
function parseStreamData(data: unknown): StreamChunk {
  if (!data || typeof data !== 'object') {
    return { type: 'error', error: 'Invalid stream data' };
  }

  const obj = data as Record<string, unknown>;

  // Text content
  if ('text' in obj || 'content' in obj || 'delta' in obj) {
    const text =
      (obj.text as string) ||
      (obj.content as string) ||
      ((obj.delta as Record<string, string>)?.text as string) ||
      '';
    return { type: 'text', content: text };
  }

  // Tool call
  if ('tool_call' in obj || 'tool_use' in obj) {
    const toolCall = (obj.tool_call || obj.tool_use) as ToolCall;
    return { type: 'tool_call', toolCall };
  }

  // Tool result
  if ('tool_result' in obj) {
    const toolResult = obj.tool_result as ToolResult;
    return { type: 'tool_result', toolResult };
  }

  // Error
  if ('error' in obj) {
    return { type: 'error', error: String(obj.error) };
  }

  // Unknown - treat as text if possible
  if ('message' in obj) {
    return { type: 'text', content: String(obj.message) };
  }

  return { type: 'error', error: 'Unknown stream format' };
}

/**
 * Create a text stream from an async generator
 */
export async function* createTextStream(
  generator: AsyncGenerator<StreamChunk>
): AsyncGenerator<string> {
  for await (const chunk of generator) {
    if (chunk.type === 'text' && chunk.content) {
      yield chunk.content;
    }
  }
}

/**
 * Collect all chunks from a stream into a single result
 */
export async function collectStream(
  generator: AsyncGenerator<StreamChunk>
): Promise<{
  text: string;
  toolCalls: ToolCall[];
  toolResults: ToolResult[];
  error?: string;
}> {
  let text = '';
  const toolCalls: ToolCall[] = [];
  const toolResults: ToolResult[] = [];
  let error: string | undefined;

  for await (const chunk of generator) {
    switch (chunk.type) {
      case 'text':
        text += chunk.content || '';
        break;
      case 'tool_call':
        if (chunk.toolCall) {
          toolCalls.push(chunk.toolCall);
        }
        break;
      case 'tool_result':
        if (chunk.toolResult) {
          toolResults.push(chunk.toolResult);
        }
        break;
      case 'error':
        error = chunk.error;
        break;
      case 'done':
        // Stream complete
        break;
    }
  }

  return { text, toolCalls, toolResults, error };
}

/**
 * Create a mock stream for testing/development
 */
export async function* createMockStream(
  text: string,
  delay = 50
): AsyncGenerator<StreamChunk> {
  const words = text.split(' ');

  for (const word of words) {
    yield { type: 'text', content: word + ' ' };
    await new Promise((resolve) => setTimeout(resolve, delay));
  }

  yield { type: 'done' };
}

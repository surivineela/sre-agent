/**
 * React hook for the agentic loop
 */
import { useCallback, useRef } from 'react';
import type { Services, Message, ToolCall, ToolResult } from '../types';
import { AgenticLoop } from '../core/agenticLoop';
import { storeActions, useStore } from '../store';
import { generateId } from '../utils/formatting';

export interface UseAgenticLoopResult {
  messages: Message[];
  isProcessing: boolean;
  sendMessage: (content: string) => Promise<void>;
  cancelProcessing: () => void;
}

export function useAgenticLoop(services: Services): UseAgenticLoopResult {
  const loopRef = useRef<AgenticLoop | null>(null);
  const messages = useStore((state) => state.currentSession?.messages ?? []);
  const isProcessing = useStore((state) => state.isProcessing);

  // Initialize loop on first use
  if (!loopRef.current) {
    loopRef.current = new AgenticLoop(services);
  }

  const sendMessage = useCallback(
    async (content: string): Promise<void> => {
      if (!content.trim() || isProcessing) return;

      const loop = loopRef.current!;

      // Add user message
      storeActions.addMessage({ role: 'user', content });

      // Set processing state
      storeActions.setProcessing(true);
      storeActions.setLoopStatus('thinking');

      // Create a placeholder for the assistant response
      const assistantMessageId = generateId();
      storeActions.addMessage({
        role: 'assistant',
        content: '',
        isStreaming: true,
      });

      try {
        await loop.processUserInput(content, {
          onThinking: () => {
            storeActions.setLoopStatus('thinking');
          },
          onStream: (chunk, _messageId) => {
            storeActions.setLoopStatus('streaming');
            // Update the assistant message with streamed content
            storeActions.updateMessage(assistantMessageId, {
              content: chunk,
              isStreaming: true,
            });
          },
          onToolStart: (tool: ToolCall) => {
            storeActions.setLoopStatus('tool_execution');
            // Update assistant message with tool call
            storeActions.updateMessage(assistantMessageId, {
              toolCalls: [tool],
            });
          },
          onToolComplete: (_tool: ToolCall, result: ToolResult) => {
            // Update with tool result
            storeActions.updateMessage(assistantMessageId, {
              toolResults: [result],
            });
          },
          onPermissionRequest: async (tool: ToolCall) => {
            storeActions.setLoopStatus('awaiting_permission');
            // TODO: Show permission prompt UI
            // For now, auto-grant for safe tools
            return tool.name.startsWith('agent_') ||
                   tool.name === 'glob' ||
                   tool.name === 'list_dir' ||
                   tool.name === 'file_exists' ||
                   tool.name === 'pwd';
          },
          onError: (error: Error) => {
            storeActions.setError(error.message);
          },
        });

        // Mark streaming as complete
        storeActions.updateMessage(assistantMessageId, {
          isStreaming: false,
        });
      } catch (error) {
        const errorMessage = error instanceof Error ? error.message : String(error);
        storeActions.updateMessage(assistantMessageId, {
          content: `Error: ${errorMessage}`,
          isStreaming: false,
        });
      } finally {
        storeActions.setProcessing(false);
        storeActions.setLoopStatus('idle');
      }
    },
    [isProcessing]
  );

  const cancelProcessing = useCallback(() => {
    loopRef.current?.cancel();
    storeActions.setProcessing(false);
    storeActions.setLoopStatus('idle');
  }, []);

  return {
    messages,
    isProcessing,
    sendMessage,
    cancelProcessing,
  };
}

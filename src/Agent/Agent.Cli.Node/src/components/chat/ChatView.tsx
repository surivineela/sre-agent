/**
 * Chat view component
 * Uses Ink's Static component to prevent flickering on completed messages
 *
 * - Static entries: Header (once ready) + completed messages rendered ONCE via <Static>
 * - Dynamic entries: In-progress content rendered normally
 */
import React, { useMemo, useState } from 'react';
import { Box, Text, Static } from 'ink';
import type { Message, SystemMessage as SystemMessageType } from '../../types';
import { UserMessage } from './UserMessage';
import { AssistantMessage } from './AssistantMessage';
import { ExecutionMessage, type ExecutionStatus, type ExecutionType } from './ExecutionMessage';
import { SystemMessage } from './SystemMessage';
import { ThinkingIndicator } from '../feedback/ThinkingIndicator';

export interface ChatViewProps {
  messages: Message[];
  systemMessages?: SystemMessageType[];
  executions?: ExecutionItem[];
  isProcessing?: boolean;
  showTimestamps?: boolean;
  currentTask?: string;
  streamingText?: string;
  reasoningLines?: string[];
  reasoningExpanded?: boolean;
  lastTokenTime?: number;
  executionOutputExpanded?: boolean;
  // Header props
  header?: React.ReactNode;
  headerReady?: boolean;
  staticKey?: number;
}

export interface ExecutionItem {
  id: string;
  description: string;
  command: string;
  type: ExecutionType;
  status: ExecutionStatus;
  output?: string;
  error?: string;
  executedBy?: string;
  timestamp: Date;
}

// Combined message type for unified rendering
type CombinedMessage =
  | { _type: 'chat'; message: Message; timestamp: Date; id: string }
  | { _type: 'system'; message: SystemMessageType; timestamp: Date; id: string }
  | { _type: 'execution'; execution: ExecutionItem; timestamp: Date; id: string };

// Check if a message is in a "dynamic" (in-progress) state
function isDynamicEntry(item: CombinedMessage): boolean {
  if (item._type === 'chat') {
    return item.message.isStreaming === true;
  }
  if (item._type === 'execution') {
    const status = item.execution.status;
    return status === 'Pending' || status === 'PendingAuthorization' || status === 'Running';
  }
  return false;
}

// Split messages into static (completed) and dynamic (in-progress)
interface MessageSplit {
  staticEntries: CombinedMessage[];
  dynamicEntries: CombinedMessage[];
}

function splitMessagesForRendering(messages: CombinedMessage[]): MessageSplit {
  const lastCompletedAssistantIndex = messages.findLastIndex(
    (item) => item._type === 'chat' && item.message.role === 'assistant' && !item.message.isStreaming
  );

  const searchStart = lastCompletedAssistantIndex + 1;
  const dynamicIndex = messages.slice(searchStart).findIndex(isDynamicEntry);

  if (dynamicIndex === -1) {
    return { staticEntries: messages, dynamicEntries: [] };
  }

  const actualSplitIndex = searchStart + dynamicIndex;
  return {
    staticEntries: messages.slice(0, actualSplitIndex),
    dynamicEntries: messages.slice(actualSplitIndex),
  };
}

// Render a single message item
const renderMessageItem = (
  item: CombinedMessage,
  executionOutputExpanded: boolean,
  reasoningExpanded: boolean
): React.ReactNode => {
  if (item._type === 'system') {
    return (
      <Box key={item.id}>
        <SystemMessage message={item.message} />
      </Box>
    );
  }
  if (item._type === 'execution') {
    return (
      <Box key={item.id}>
        <ExecutionMessage
          id={item.execution.id}
          description={item.execution.description}
          command={item.execution.command}
          type={item.execution.type}
          status={item.execution.status}
          output={item.execution.output}
          error={item.execution.error}
          executedBy={item.execution.executedBy}
          showOutput={executionOutputExpanded}
        />
      </Box>
    );
  }
  const message = item.message;
  if (message.role === 'user') {
    return (
      <Box key={item.id}>
        <UserMessage message={message} />
      </Box>
    );
  }
  if (message.role === 'assistant') {
    return (
      <Box key={item.id}>
        <AssistantMessage
          message={message}
          reasoningExpanded={reasoningExpanded}
        />
      </Box>
    );
  }
  if (message.role === 'system') {
    return (
      <Box key={item.id}>
        <Text color="gray" dimColor>{message.content}</Text>
      </Box>
    );
  }
  return null;
};

export const ChatView: React.FC<ChatViewProps> = ({
  messages,
  systemMessages = [],
  executions = [],
  isProcessing = false,
  currentTask,
  streamingText,
  reasoningLines = [],
  reasoningExpanded = false,
  lastTokenTime,
  executionOutputExpanded = false,
  header,
  headerReady = false,
  staticKey = 0,
}) => {
  // Combine and sort all messages by timestamp
  const allMessages = useMemo((): CombinedMessage[] => {
    const combined: CombinedMessage[] = [
      ...messages.map(m => ({
        _type: 'chat' as const,
        message: m,
        timestamp: m.timestamp,
        id: m.id,
      })),
      ...systemMessages.map(m => ({
        _type: 'system' as const,
        message: m,
        timestamp: m.timestamp,
        id: m.id,
      })),
      ...executions.map(exec => ({
        _type: 'execution' as const,
        execution: exec,
        timestamp: exec.timestamp,
        id: exec.id,
      })),
    ];
    return combined.sort((a, b) => a.timestamp.getTime() - b.timestamp.getTime());
  }, [messages, systemMessages, executions]);

  // Split into static and dynamic entries
  const { staticEntries, dynamicEntries } = useMemo(
    () => splitMessagesForRendering(allMessages),
    [allMessages]
  );

  // Memoize reasoning content calculation
  const reasoningContent = useMemo(() => {
    if (reasoningLines.length === 0) return null;

    const skipWords = ['operation', 'processing', 'thinking', 'working', ''];
    const filteredLines = reasoningLines
      .map(line => line.replace(/\*\*/g, '').trim())
      .filter(line => {
        const lower = line.toLowerCase();
        return line && !skipWords.includes(lower) && lower !== '**';
      });

    if (filteredLines.length === 0) return null;

    return { filteredLines, totalLines: filteredLines.length };
  }, [reasoningLines]);

  const hasStreamingContent = streamingText && streamingText.length > 0;

  // Pre-render static message items
  const staticMessageItems = useMemo(() =>
    staticEntries.map((item) => (
      <Box key={item.id}>
        {renderMessageItem(item, executionOutputExpanded, reasoningExpanded)}
      </Box>
    )),
    [staticEntries, executionOutputExpanded, reasoningExpanded]
  );

  // Pre-render dynamic items
  const dynamicItems = useMemo(() =>
    dynamicEntries.map((item) => (
      <Box key={item.id}>
        {renderMessageItem(item, executionOutputExpanded, reasoningExpanded)}
      </Box>
    )),
    [dynamicEntries, executionOutputExpanded, reasoningExpanded]
  );

  // Build static items array (header + messages)
  const staticItems = useMemo(() => {
    const items: React.ReactNode[] = [];

    // Include header in static items when ready
    if (header && headerReady) {
      items.push(<Box key="header">{header}</Box>);
    }

    // Add static message items
    items.push(...staticMessageItems);

    return items;
  }, [header, headerReady, staticMessageItems]);

  return (
    <Box flexDirection="column" flexGrow={1}>
      {/* Header shown dynamically when NOT ready (still connecting) */}
      {header && !headerReady && header}

      {/* Static content - rendered ONCE via Ink's Static component */}
      {headerReady && staticItems.length > 0 && (
        <Static key={staticKey} items={staticItems}>
          {(item) => item}
        </Static>
      )}

      {/* Welcome message when empty */}
      {messages.length === 0 && systemMessages.length === 0 && !isProcessing && headerReady && (
        <Box flexDirection="column" marginTop={1}>
          <Box>
            <Text color="gray">Ready. </Text>
            <Text color="cyan">/help</Text>
            <Text color="gray"> for commands</Text>
          </Box>
        </Box>
      )}

      {/* Dynamic messages - rendered normally (can update/re-render) */}
      {dynamicItems}

      {/* Reasoning tree display */}
      {reasoningContent && (
        reasoningExpanded ? (
          <Box flexDirection="column">
            <Box>
              <Text color="gray" dimColor>◆ Thinking...</Text>
            </Box>
            {reasoningContent.filteredLines.map((line, index) => (
              <Box key={index}>
                <Text color="gray" dimColor>  ⎿ {line}</Text>
              </Box>
            ))}
            <Box>
              <Text color="gray" dimColor>     (ctrl+o to collapse)</Text>
            </Box>
          </Box>
        ) : (
          <Box>
            <Text color="gray" dimColor>
              ◆ Thinking... {reasoningContent.totalLines} lines (ctrl+o to expand)
            </Text>
          </Box>
        )
      )}

      {/* Streaming response */}
      {hasStreamingContent && (
        <Box>
          <AssistantMessage
            message={{
              id: 'streaming',
              role: 'assistant',
              content: streamingText,
              timestamp: new Date(),
              isStreaming: true,
            }}
            lastTokenTime={lastTokenTime}
          />
        </Box>
      )}

      {/* Thinking indicator when processing but no streaming content yet */}
      {isProcessing && !hasStreamingContent && (
        <Box>
          <ThinkingIndicator task={currentTask} />
        </Box>
      )}

      {/* Hidden character to force re-render when staticKey changes */}
      <Text>{staticKey % 2 ? "\u200C" : "\u200D"}</Text>
    </Box>
  );
};

ChatView.displayName = 'ChatView';

export default ChatView;

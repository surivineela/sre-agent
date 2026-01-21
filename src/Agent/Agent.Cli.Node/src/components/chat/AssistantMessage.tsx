/**
 * Assistant message display
 */
import React from 'react';
import { Box, Text } from 'ink';
import type { Message } from '../../types';
import { StreamingText } from './StreamingText';
import { ToolInvocation } from './ToolInvocation';
import { BABY_PINK, theme } from '../../theme';

export interface AssistantMessageProps {
  message: Message;
  showTimestamp?: boolean;
  compact?: boolean;
  reasoningExpanded?: boolean;
  lastTokenTime?: number;
}

export const AssistantMessage: React.FC<AssistantMessageProps> = ({
  message,
  compact = true,
  reasoningExpanded = false,
  lastTokenTime,
}) => {
  const hasToolCalls = message.toolCalls && message.toolCalls.length > 0;
  const hasContent = message.content && message.content.length > 0;
  const hasReasoning = message.reasoningLines && message.reasoningLines.length > 0;

  // Render reasoning in simple format
  const renderReasoning = () => {
    if (!hasReasoning || !message.reasoningLines) return null;

    const totalLines = message.reasoningLines.length;

    if (reasoningExpanded) {
      return (
        <Box flexDirection="column">
          <Box>
            <Text color="gray" dimColor>◆ Thinking...</Text>
          </Box>
          {message.reasoningLines.map((line, index) => (
            <Box key={index}>
              <Text color="gray" dimColor>  ⎿ {line}</Text>
            </Box>
          ))}
          <Box>
            <Text color="gray" dimColor>     (ctrl+o to collapse)</Text>
          </Box>
        </Box>
      );
    }

    return (
      <Box>
        <Text color="gray" dimColor>◆ Thinking... {totalLines} lines (ctrl+o to expand)</Text>
      </Box>
    );
  };

  return (
    <Box flexDirection="column">
      {/* Reasoning tree */}
      {renderReasoning()}

      {/* Tool invocations */}
      {hasToolCalls && message.toolCalls?.map((tool, index) => (
        <ToolInvocation
          key={tool.id}
          tool={tool}
          status={
            message.toolResults?.[index]
              ? message.toolResults[index].success
                ? 'complete'
                : 'error'
              : 'running'
          }
          result={message.toolResults?.[index]}
          compact={compact}
        />
      ))}

      {/* Message content with pink dot prefix */}
      {hasContent && (
        <Box flexDirection="row" gap={1} marginTop={hasToolCalls && !compact ? 1 : 0}>
          <Box width={1}>
            <Text color={BABY_PINK}>●</Text>
          </Box>
          <Box flexDirection="column" flexGrow={1}>
            <StreamingText
              text={message.content}
              isStreaming={message.isStreaming}
              lastTokenTime={lastTokenTime}
              textColor="white"
            />
          </Box>
        </Box>
      )}
    </Box>
  );
};

export default AssistantMessage;

/**
 * Trace Detail Panel - Shows details for selected span
 * Right pane of the trace view with expandable sections
 * Supports keyboard navigation when focused
 */
import React, { useState, useEffect, useCallback, memo } from 'react';
import { Box, Text, useInput } from 'ink';
import type { ISpan } from '../../types/trace';
import {
  getSpanTitle,
  getSpanIcon,
  getSpanColor,
  getSpanDuration,
  formatDuration,
} from '../../types/trace';
import { theme, BABY_PINK, BABY_BLUE } from '../../theme';

/**
 * Try to pretty-print JSON, return original if not valid JSON
 */
const formatJsonOrText = (input?: string): string => {
  if (!input || input === '-') return '-';
  try {
    const parsed = JSON.parse(input);
    return JSON.stringify(parsed, null, 2);
  } catch {
    return input;
  }
};

export interface TraceDetailPanelProps {
  span: ISpan | null;
  maxHeight?: number;
  isFocused?: boolean;
}

// Section data structure
interface Section {
  id: string;
  title: string;
  content: string;
  defaultExpanded?: boolean;
}

// Expandable section component
const ExpandableSection: React.FC<{
  title: string;
  content: string;
  isExpanded: boolean;
  isSelected: boolean;
  maxLines?: number;
}> = memo(({ title, content, isExpanded, isSelected, maxLines = 8 }) => {
  const lines = content.split('\n');
  const shouldTruncate = lines.length > maxLines && !isExpanded;
  const displayLines = shouldTruncate ? lines.slice(0, maxLines) : lines;

  return (
    <Box flexDirection="column" marginTop={1}>
      <Box>
        <Text color={isSelected ? 'white' : BABY_BLUE} bold inverse={isSelected}>
          {isExpanded ? '▼' : '▶'} {title}
        </Text>
        {shouldTruncate && (
          <Text color="gray"> (+{lines.length - maxLines} lines)</Text>
        )}
      </Box>
      <Box
        flexDirection="column"
        borderStyle="round"
        borderColor={isSelected ? BABY_BLUE : theme.ink.muted}
        paddingX={1}
        marginLeft={2}
      >
        {displayLines.map((line, i) => (
          <Text key={i} color="white" wrap="wrap">{line || ' '}</Text>
        ))}
        {shouldTruncate && (
          <Text color="gray" dimColor>... ({lines.length - maxLines} more lines)</Text>
        )}
      </Box>
    </Box>
  );
});

ExpandableSection.displayName = 'ExpandableSection';

// Key-value row
const DetailRow: React.FC<{
  label: string;
  value: string | number | undefined;
  color?: string;
}> = memo(({ label, value, color }) => {
  if (value === undefined || value === '') return null;
  return (
    <Box>
      <Text color="gray">{label}: </Text>
      <Text color={color}>{value}</Text>
    </Box>
  );
});

DetailRow.displayName = 'DetailRow';

// Build sections for a span
function buildSectionsForSpan(span: ISpan): Section[] {
  const sections: Section[] = [];

  switch (span.kind) {
    case 'ModelGeneration': {
      const usage = span.usage_info;
      if (usage?.systemPrompt) {
        sections.push({ id: 'systemPrompt', title: 'System Prompt', content: usage.systemPrompt });
      }
      if (usage?.model_input && usage.model_input.length > 0) {
        sections.push({ id: 'modelInput', title: 'Input Messages', content: JSON.stringify(usage.model_input, null, 2) });
      }
      if (usage?.modelThinking) {
        sections.push({ id: 'modelThinking', title: 'Model Thinking', content: usage.modelThinking });
      }
      if (usage?.reasoning) {
        sections.push({ id: 'reasoning', title: 'Reasoning', content: usage.reasoning });
      }
      if (usage?.response) {
        sections.push({ id: 'response', title: 'Response', content: usage.response, defaultExpanded: true });
      } else if (usage?.model_output && usage.model_output.length > 0) {
        sections.push({ id: 'modelOutput', title: 'Output', content: JSON.stringify(usage.model_output, null, 2), defaultExpanded: true });
      }
      break;
    }
    case 'Execution':
    case 'Tool': {
      const attrs = span.attributes;
      if (attrs?.toolInput) {
        sections.push({ id: 'toolInput', title: 'Input Arguments', content: formatJsonOrText(attrs.toolInput), defaultExpanded: true });
      }
      if (attrs?.command) {
        sections.push({ id: 'command', title: 'Command', content: attrs.command, defaultExpanded: true });
      }
      if (attrs?.toolOutput) {
        sections.push({ id: 'toolOutput', title: 'Output Result', content: formatJsonOrText(attrs.toolOutput), defaultExpanded: true });
      }
      if (span.error) {
        sections.push({ id: 'error', title: 'Error', content: span.error, defaultExpanded: true });
      }
      break;
    }
    case 'Agent':
    case 'SubAgent': {
      const attrs = span.attributes;
      if (attrs?.message) {
        sections.push({ id: 'message', title: 'Message', content: attrs.message, defaultExpanded: true });
      }
      break;
    }
    case 'AgentThinking': {
      const steps = span.attributes?.thinkingSteps || [];
      if (steps.length > 0) {
        const content = steps.map((step, i) => `${i + 1}. ${step.message}`).join('\n');
        sections.push({ id: 'thinkingSteps', title: `Thinking Steps (${steps.length})`, content, defaultExpanded: true });
      }
      break;
    }
    case 'AgentHandoff':
    case 'AgentHandback': {
      const attrs = span.attributes;
      if (attrs?.handoffReasoning) {
        sections.push({ id: 'handoffReasoning', title: 'Reasoning', content: attrs.handoffReasoning, defaultExpanded: true });
      }
      break;
    }
    case 'UserMessage': {
      const attrs = span.attributes;
      if (attrs?.message) {
        sections.push({ id: 'userMessage', title: 'Message', content: attrs.message, defaultExpanded: true });
      }
      break;
    }
    case 'AgentResponse': {
      const attrs = span.attributes;
      if (attrs?.message) {
        sections.push({ id: 'responseMessage', title: 'Response', content: attrs.message, defaultExpanded: true });
      }
      break;
    }
    default: {
      if (span.attributes?.message) {
        sections.push({ id: 'details', title: 'Details', content: span.attributes.message, defaultExpanded: true });
      }
    }
  }

  // Add error section if not already added and span has error
  if (span.error && span.kind !== 'Execution' && span.kind !== 'Tool') {
    sections.push({ id: 'spanError', title: 'Error', content: span.error, defaultExpanded: true });
  }

  return sections;
}

export const TraceDetailPanel: React.FC<TraceDetailPanelProps> = memo(({
  span,
  maxHeight,
  isFocused = false,
}) => {
  // Track expanded state for each section
  const [expandedSections, setExpandedSections] = useState<Set<string>>(new Set());
  // Track selected section index
  const [selectedIndex, setSelectedIndex] = useState(0);

  // Build sections for current span
  const sections = span ? buildSectionsForSpan(span) : [];

  // Reset selection and expanded state when span changes
  useEffect(() => {
    if (span) {
      const newExpanded = new Set<string>();
      const newSections = buildSectionsForSpan(span);
      for (const section of newSections) {
        if (section.defaultExpanded) {
          newExpanded.add(section.id);
        }
      }
      setExpandedSections(newExpanded);
      setSelectedIndex(0);
    }
  }, [span?.context.span_id]);

  // Handle keyboard input when focused
  useInput(useCallback((input: string, key: { upArrow?: boolean; downArrow?: boolean; leftArrow?: boolean; rightArrow?: boolean; return?: boolean }) => {
    if (!isFocused || sections.length === 0) return;

    // Navigate up/down through sections
    if (key.upArrow) {
      setSelectedIndex(prev => Math.max(0, prev - 1));
      return;
    }
    if (key.downArrow) {
      setSelectedIndex(prev => Math.min(sections.length - 1, prev + 1));
      return;
    }

    // Toggle expand/collapse with right arrow, Enter, or Space
    if ((key.rightArrow || key.return || input === ' ') && sections[selectedIndex]) {
      const sectionId = sections[selectedIndex].id;
      setExpandedSections(prev => {
        const next = new Set(prev);
        if (next.has(sectionId)) {
          next.delete(sectionId);
        } else {
          next.add(sectionId);
        }
        return next;
      });
      return;
    }

    // Collapse with left arrow
    if (key.leftArrow && sections[selectedIndex]) {
      const sectionId = sections[selectedIndex].id;
      setExpandedSections(prev => {
        const next = new Set(prev);
        next.delete(sectionId);
        return next;
      });
      return;
    }
  }, [isFocused, sections, selectedIndex]));

  if (!span) {
    return (
      <Box
        flexDirection="column"
        padding={1}
        height={maxHeight}
        justifyContent="center"
        alignItems="center"
      >
        <Text color="gray">Select a span to view details</Text>
        <Text color="gray" dimColor>Use ↑/↓ to navigate the tree</Text>
      </Box>
    );
  }

  const icon = getSpanIcon(span);
  const color = getSpanColor(span);
  const title = getSpanTitle(span);
  const duration = getSpanDuration(span);

  // Render header info based on span kind
  const renderHeaderInfo = () => {
    switch (span.kind) {
      case 'ModelGeneration': {
        const usage = span.usage_info;
        if (!usage) return null;
        const totalTokens = usage.total_tokens ?? (
          usage.prompt_tokens && usage.completion_tokens
            ? usage.prompt_tokens + usage.completion_tokens
            : undefined
        );
        return (
          <>
            <DetailRow label="Model" value={usage.modelName} color="cyan" />
            <DetailRow label="Temperature" value={usage.temperature} />
            <Box>
              <Text color="gray">Tokens: </Text>
              <Text color="green">{usage.prompt_tokens ?? '-'}</Text>
              <Text color="gray"> in / </Text>
              <Text color="yellow">{usage.completion_tokens ?? '-'}</Text>
              <Text color="gray"> out</Text>
              {totalTokens && (
                <>
                  <Text color="gray"> = </Text>
                  <Text color="white">{totalTokens}</Text>
                  <Text color="gray"> total</Text>
                </>
              )}
            </Box>
          </>
        );
      }
      case 'Execution':
      case 'Tool': {
        const attrs = span.attributes;
        if (!attrs) return null;
        return (
          <>
            <DetailRow label="Tool" value={attrs.toolName} color="cyan" />
            <DetailRow label="Description" value={attrs.toolDescription} />
            <DetailRow label="Executed By" value={attrs.executedBy} />
          </>
        );
      }
      case 'Agent':
      case 'SubAgent': {
        const attrs = span.attributes;
        if (!attrs) return null;
        return (
          <>
            <DetailRow label="Agent" value={attrs.agentName} color="cyan" />
            <DetailRow label="Result" value={attrs.result} />
          </>
        );
      }
      case 'AgentHandoff':
      case 'AgentHandback': {
        const attrs = span.attributes;
        if (!attrs) return null;
        const isHandoff = span.kind === 'AgentHandoff';
        return (
          <Box>
            <Text color="cyan">{attrs.fromAgent}</Text>
            <Text color="gray"> {isHandoff ? '→' : '←'} </Text>
            <Text color="magenta">{attrs.toAgent}</Text>
          </Box>
        );
      }
      case 'UserMessage': {
        const attrs = span.attributes;
        if (!attrs) return null;
        return <DetailRow label="User" value={attrs.displayName || attrs.userId} />;
      }
      default:
        return null;
    }
  };

  return (
    <Box
      flexDirection="column"
      paddingX={1}
      height={maxHeight}
      overflowY="hidden"
    >
      {/* Header */}
      <Box flexDirection="column" marginBottom={1}>
        <Box>
          <Text color={color} bold>{icon} {title}</Text>
          {isFocused && <Text color={BABY_BLUE}> (focused)</Text>}
        </Box>
        <Box>
          <Text color="gray">ID: </Text>
          <Text dimColor>{span.context.span_id.slice(0, 20)}...</Text>
        </Box>
        <Box>
          <Text color="gray">Started: </Text>
          <Text>{span.start_time.toLocaleTimeString()}</Text>
          {duration !== undefined && (
            <>
              <Text color="gray"> • Duration: </Text>
              <Text>{formatDuration(duration)}</Text>
            </>
          )}
        </Box>
        {span.status && (
          <Box>
            <Text color="gray">Status: </Text>
            <Text
              color={
                span.status === 'completed' ? 'green' :
                span.status === 'failed' ? 'red' :
                span.status === 'running' ? 'yellow' : 'gray'
              }
            >
              {span.status}
            </Text>
          </Box>
        )}
        {renderHeaderInfo()}
      </Box>

      {/* Separator */}
      <Text color="gray">{'─'.repeat(40)}</Text>

      {/* Expandable sections */}
      {sections.length > 0 ? (
        <Box flexDirection="column">
          {sections.map((section, index) => (
            <ExpandableSection
              key={section.id}
              title={section.title}
              content={section.content}
              isExpanded={expandedSections.has(section.id)}
              isSelected={isFocused && index === selectedIndex}
            />
          ))}
        </Box>
      ) : (
        <Box marginTop={1}>
          <Text color="gray" dimColor>No additional details available</Text>
        </Box>
      )}

      {/* Navigation hint when focused */}
      {isFocused && sections.length > 0 && (
        <Box marginTop={1}>
          <Text color="gray" dimColor>↑/↓ select • →/Space expand • ← collapse</Text>
        </Box>
      )}
    </Box>
  );
});

TraceDetailPanel.displayName = 'TraceDetailPanel';

export default TraceDetailPanel;

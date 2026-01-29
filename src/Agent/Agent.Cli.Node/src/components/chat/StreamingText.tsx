/**
 * Streaming text component with blinking cursor and markdown support
 */
import React, { useState, useEffect } from 'react';
import { Box, Text } from 'ink';
import { Table, parseMarkdownTable, containsMarkdownTable } from '../display';

export interface StreamingTextProps {
  text: string;
  isStreaming?: boolean;
  lastTokenTime?: number; // When last token was received
  cursorColor?: string;
  textColor?: string;
}

// Parse text with basic markdown support (bold, italic, code)
interface TextSegment {
  text: string;
  bold?: boolean;
  italic?: boolean;
  code?: boolean;
}

// Split text into table, code block, and non-table sections
interface ContentSection {
  type: 'text' | 'table' | 'codeblock';
  content: string;
  language?: string; // For code blocks
}

// Split content into code blocks, tables, and text sections
const splitContent = (text: string): ContentSection[] => {
  const sections: ContentSection[] = [];
  const lines = text.split('\n');
  let currentSection: ContentSection | null = null;
  let inCodeBlock = false;
  let codeBlockLanguage = '';
  let codeBlockLines: string[] = [];
  let inTable = false;
  let tableLines: string[] = [];

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    // Check for fenced code block start/end (``` or ```)
    const codeBlockMatch = line.match(/^```(\w*)$/);

    if (codeBlockMatch && !inCodeBlock) {
      // Start of code block
      // Flush current text section
      if (currentSection && currentSection.type === 'text' && currentSection.content.trim()) {
        sections.push(currentSection);
        currentSection = null;
      }
      // End table if active
      if (inTable && tableLines.length > 0) {
        sections.push({ type: 'table', content: tableLines.join('\n') });
        inTable = false;
        tableLines = [];
      }
      inCodeBlock = true;
      codeBlockLanguage = codeBlockMatch[1] || '';
      codeBlockLines = [];
      continue;
    }

    if (inCodeBlock && line.match(/^```$/)) {
      // End of code block
      sections.push({
        type: 'codeblock',
        content: codeBlockLines.join('\n'),
        language: codeBlockLanguage
      });
      inCodeBlock = false;
      codeBlockLanguage = '';
      codeBlockLines = [];
      continue;
    }

    if (inCodeBlock) {
      codeBlockLines.push(line);
      continue;
    }

    const isTableLine = line.includes('|');
    const isSeparatorLine = /^\s*\|?\s*[-:]+[-|\s:]*\|/.test(line);

    // Detect table start (header + separator)
    if (!inTable && isTableLine && i + 1 < lines.length) {
      const nextLine = lines[i + 1];
      if (/^\s*\|?\s*[-:]+[-|\s:]*\|/.test(nextLine)) {
        // Flush current text section
        if (currentSection && currentSection.type === 'text' && currentSection.content.trim()) {
          sections.push(currentSection);
        }
        inTable = true;
        tableLines = [line];
        currentSection = null;
        continue;
      }
    }

    if (inTable) {
      if (isTableLine || isSeparatorLine) {
        tableLines.push(line);
      } else {
        // End of table
        sections.push({ type: 'table', content: tableLines.join('\n') });
        inTable = false;
        tableLines = [];
        // Start new text section with current line
        if (line.trim()) {
          currentSection = { type: 'text', content: line };
        } else {
          currentSection = null;
        }
      }
    } else {
      if (!currentSection) {
        currentSection = { type: 'text', content: line };
      } else {
        currentSection.content += '\n' + line;
      }
    }
  }

  // Flush remaining content
  if (inCodeBlock && codeBlockLines.length > 0) {
    // Unclosed code block - still render it
    sections.push({
      type: 'codeblock',
      content: codeBlockLines.join('\n'),
      language: codeBlockLanguage
    });
  } else if (inTable && tableLines.length > 0) {
    sections.push({ type: 'table', content: tableLines.join('\n') });
  } else if (currentSection && currentSection.content.trim()) {
    sections.push(currentSection);
  }

  return sections;
};

const parseMarkdown = (text: string): TextSegment[] => {
  const segments: TextSegment[] = [];
  let remaining = text;

  while (remaining.length > 0) {
    // Check for bold **text**
    const boldMatch = remaining.match(/^\*\*(.+?)\*\*/);
    if (boldMatch) {
      segments.push({ text: boldMatch[1], bold: true });
      remaining = remaining.slice(boldMatch[0].length);
      continue;
    }

    // Check for italic *text* or _text_
    const italicMatch = remaining.match(/^(?:\*([^*]+?)\*|_([^_]+?)_)/);
    if (italicMatch) {
      segments.push({ text: italicMatch[1] || italicMatch[2], italic: true });
      remaining = remaining.slice(italicMatch[0].length);
      continue;
    }

    // Check for inline code `text`
    const codeMatch = remaining.match(/^`([^`]+?)`/);
    if (codeMatch) {
      segments.push({ text: codeMatch[1], code: true });
      remaining = remaining.slice(codeMatch[0].length);
      continue;
    }

    // Find next markdown marker
    const nextMarker = remaining.search(/\*\*|\*|_|`/);
    if (nextMarker === -1) {
      // No more markers, add rest as plain text
      segments.push({ text: remaining });
      break;
    } else if (nextMarker === 0) {
      // Marker at start but didn't match pattern, treat as plain char
      segments.push({ text: remaining[0] });
      remaining = remaining.slice(1);
    } else {
      // Add text before marker as plain
      segments.push({ text: remaining.slice(0, nextMarker) });
      remaining = remaining.slice(nextMarker);
    }
  }

  return segments;
};

// Render text segment with markdown formatting
const renderTextSegment = (seg: TextSegment, index: number, textColor: string) => {
  if (seg.code) {
    return <Text key={index} color="cyan">{seg.text}</Text>;
  }
  if (seg.bold && seg.italic) {
    return <Text key={index} bold italic color={textColor}>{seg.text}</Text>;
  }
  if (seg.bold) {
    return <Text key={index} bold color={textColor}>{seg.text}</Text>;
  }
  if (seg.italic) {
    return <Text key={index} italic color={textColor}>{seg.text}</Text>;
  }
  return <Text key={index} color={textColor}>{seg.text}</Text>;
};

export const StreamingText: React.FC<StreamingTextProps> = ({
  text,
  isStreaming = false,
  cursorColor = 'cyan',
  textColor = 'white',
}) => {
  // SIMPLIFIED: No cursor blinking animation - just show solid cursor when streaming
  // This eliminates interval-based re-renders that cause flickering
  // Claude Code style: solid cursor during streaming, hidden when complete

  // Memoize content parsing to avoid recalculating on every render
  const hasTable = React.useMemo(() => containsMarkdownTable(text), [text]);
  const hasCodeBlock = React.useMemo(() => text.includes('```'), [text]);

  // Helper to render the cursor - always visible when streaming
  const renderCursor = () => {
    if (!isStreaming) return null;
    return <Text color={cursorColor}>▋</Text>;
  };

  // If no tables or code blocks, use simple rendering
  if (!hasTable && !hasCodeBlock) {
    // Memoize parsed segments
    const segments = React.useMemo(() => parseMarkdown(text), [text]);
    return (
      <Text color={textColor}>
        {segments.map((seg, i) => renderTextSegment(seg, i, textColor))}
        {renderCursor()}
      </Text>
    );
  }

  // Memoize content split for complex content
  const sections = React.useMemo(() => splitContent(text), [text]);

  return (
    <Box flexDirection="column">
      {sections.map((section, sectionIndex) => {
        const isLast = sectionIndex === sections.length - 1;

        if (section.type === 'codeblock') {
          // Render code block with syntax highlighting styling
          const langLabel = section.language ? ` ${section.language}` : '';
          return (
            <Box key={sectionIndex} flexDirection="column" marginY={1}>
              <Text color="gray" dimColor>{'```'}{langLabel}</Text>
              <Box
                borderStyle="single"
                borderColor="gray"
                paddingX={1}
                flexDirection="column"
              >
                {section.content.split('\n').map((line, lineIdx) => (
                  <Text key={lineIdx} color="cyan">{line}</Text>
                ))}
              </Box>
              <Text color="gray" dimColor>{'```'}</Text>
              {isLast && renderCursor()}
            </Box>
          );
        }

        if (section.type === 'table') {
          const tableData = parseMarkdownTable(section.content);
          if (tableData) {
            return (
              <React.Fragment key={sectionIndex}>
                <Box marginY={1}>
                  <Table
                    columns={tableData.columns}
                    data={tableData.data}
                    headerColor="cyan"
                    borderColor="gray"
                  />
                </Box>
                {isLast && renderCursor()}
              </React.Fragment>
            );
          }
          // Fallback to plain text if table parsing fails
          return (
            <Text key={sectionIndex} color={textColor}>
              {section.content}
              {isLast && renderCursor()}
            </Text>
          );
        }

        // Text section
        const segments = parseMarkdown(section.content);
        return (
          <Text key={sectionIndex} color={textColor}>
            {segments.map((seg, i) => renderTextSegment(seg, i, textColor))}
            {isLast && renderCursor()}
          </Text>
        );
      })}
    </Box>
  );
};

export default StreamingText;

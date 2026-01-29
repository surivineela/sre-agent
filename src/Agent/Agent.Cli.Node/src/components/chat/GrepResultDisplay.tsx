/**
 * Grep result display - CLI version
 * Shows search results with file paths and highlighted matches
 * Ported from Agent.Web\Client GrepResultMessage.tsx
 */
import React, { useState } from 'react';
import { Box, Text } from 'ink';
import { theme } from '../../theme';

export interface MatchRange {
  start: number;
  end: number;
}

export interface GrepLineMatch {
  lineNumber: number;
  content: string;
  matchRanges: MatchRange[];
  isContext?: boolean;
}

export interface GrepFileResult {
  filePath: string;
  matchCount: number;
  matches: GrepLineMatch[];
}

export interface GrepSearchResult {
  query: string;
  totalMatches: number;
  files: GrepFileResult[];
  isRegex?: boolean;
}

export interface GrepResultDisplayProps {
  result: GrepSearchResult;
  maxFilesToShow?: number;
  maxLinesPerFile?: number;
}

/**
 * Renders line content with match highlighting
 */
const HighlightedContent: React.FC<{
  content: string;
  matchRanges: MatchRange[];
  isContext?: boolean;
}> = ({ content, matchRanges, isContext }) => {
  if (isContext || matchRanges.length === 0) {
    return <Text color={theme.ink.muted}>{content}</Text>;
  }

  const parts: React.ReactNode[] = [];
  let lastEnd = 0;

  // Sort ranges by start position
  const sortedRanges = [...matchRanges].sort((a, b) => a.start - b.start);

  sortedRanges.forEach((range, index) => {
    // Add text before match
    if (range.start > lastEnd) {
      parts.push(
        <Text key={`pre-${index}`} color={theme.ink.text}>
          {content.slice(lastEnd, range.start)}
        </Text>
      );
    }

    // Add highlighted match
    parts.push(
      <Text key={`match-${index}`} backgroundColor="yellow" color="black">
        {content.slice(range.start, range.end)}
      </Text>
    );

    lastEnd = range.end;
  });

  // Add remaining text
  if (lastEnd < content.length) {
    parts.push(
      <Text key="suffix" color={theme.ink.text}>
        {content.slice(lastEnd)}
      </Text>
    );
  }

  return <>{parts}</>;
};

/**
 * Single file result with code lines
 */
const FileResultItem: React.FC<{
  file: GrepFileResult;
  expanded: boolean;
  maxLines?: number;
}> = ({ file, expanded, maxLines = 5 }) => {
  const displayMatches = expanded
    ? file.matches.slice(0, maxLines)
    : file.matches.filter(m => !m.isContext).slice(0, 2);

  return (
    <Box flexDirection="column">
      {/* File header */}
      <Box>
        <Text color={theme.ink.muted}>│ </Text>
        <Text color={theme.ink.info}>{file.filePath}</Text>
        <Text color={theme.ink.muted}> ({file.matchCount} match{file.matchCount !== 1 ? 'es' : ''})</Text>
      </Box>

      {/* Code lines */}
      {expanded && (
        <Box flexDirection="column" marginLeft={2}>
          {displayMatches.map((match, idx) => (
            <Box key={`${match.lineNumber}-${idx}`}>
              <Text color={theme.ink.muted}>│ </Text>
              <Text color={theme.ink.muted}>
                {String(match.lineNumber).padStart(4, ' ')}
              </Text>
              <Text color={theme.ink.muted}>│ </Text>
              <HighlightedContent
                content={match.content.length > 80 ? match.content.slice(0, 77) + '...' : match.content}
                matchRanges={match.matchRanges}
                isContext={match.isContext}
              />
            </Box>
          ))}
          {file.matches.length > maxLines && (
            <Box>
              <Text color={theme.ink.muted}>│      ... {file.matches.length - maxLines} more lines</Text>
            </Box>
          )}
        </Box>
      )}
    </Box>
  );
};

export const GrepResultDisplay: React.FC<GrepResultDisplayProps> = ({
  result,
  maxFilesToShow = 5,
  maxLinesPerFile = 5,
}) => {
  const [expanded, setExpanded] = useState(false);
  const hasResults = result.files.length > 0;

  const displayFiles = expanded
    ? result.files.slice(0, maxFilesToShow)
    : result.files.slice(0, 3);

  return (
    <Box flexDirection="column" marginY={1}>
      {/* Summary header */}
      <Box>
        <Text color={theme.ink.muted}>┌─ </Text>
        <Text color={theme.ink.info}>Search: </Text>
        <Text color={theme.ink.warning}>{result.query}</Text>
        {result.isRegex && (
          <Text color={theme.ink.muted}> (regex)</Text>
        )}
      </Box>

      {/* Results count */}
      <Box>
        <Text color={theme.ink.muted}>│ </Text>
        {hasResults ? (
          <>
            <Text color={theme.ink.success}>
              {result.totalMatches} match{result.totalMatches !== 1 ? 'es' : ''}
            </Text>
            <Text color={theme.ink.muted}>
              {' '}in {result.files.length} file{result.files.length !== 1 ? 's' : ''}
            </Text>
            <Text color={theme.ink.muted}> [{expanded ? '−' : '+'}]</Text>
          </>
        ) : (
          <Text color={theme.ink.muted} italic>No results found</Text>
        )}
      </Box>

      {/* File results */}
      {hasResults && (
        <Box flexDirection="column">
          {displayFiles.map((file) => (
            <FileResultItem
              key={file.filePath}
              file={file}
              expanded={expanded}
              maxLines={maxLinesPerFile}
            />
          ))}
          {result.files.length > maxFilesToShow && expanded && (
            <Box>
              <Text color={theme.ink.muted}>│ ... {result.files.length - maxFilesToShow} more files</Text>
            </Box>
          )}
        </Box>
      )}

      {/* Footer */}
      <Box>
        <Text color={theme.ink.muted}>└─</Text>
      </Box>
    </Box>
  );
};

/**
 * Compact single-line grep summary
 */
export const GrepResultSummary: React.FC<{
  query: string;
  matchCount: number;
  fileCount: number;
}> = ({ query, matchCount, fileCount }) => {
  return (
    <Box>
      <Text color={theme.ink.info}>Search </Text>
      <Text color={theme.ink.warning}>"{query}"</Text>
      <Text color={theme.ink.muted}>: </Text>
      {matchCount > 0 ? (
        <Text color={theme.ink.success}>
          {matchCount} match{matchCount !== 1 ? 'es' : ''} in {fileCount} file{fileCount !== 1 ? 's' : ''}
        </Text>
      ) : (
        <Text color={theme.ink.muted}>no results</Text>
      )}
    </Box>
  );
};

export default GrepResultDisplay;

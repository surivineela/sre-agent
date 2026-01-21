/**
 * Table component - renders tabular data in CLI
 * Supports both structured data and markdown table parsing
 */
import React from 'react';
import { Box, Text, useStdout } from 'ink';

export interface TableColumn {
  key: string;
  header: string;
  width?: number;
  align?: 'left' | 'right' | 'center';
}

export interface TableProps {
  columns: TableColumn[];
  data: Record<string, string | number | undefined>[];
  borderColor?: string;
  headerColor?: string;
  compact?: boolean;
  maxWidth?: number;
}

// Calculate column widths based on content and terminal constraints
function calculateColumnWidths(
  columns: TableColumn[],
  data: Record<string, string | number | undefined>[],
  maxTotalWidth: number
): number[] {
  // First pass: calculate natural widths
  const naturalWidths = columns.map((col) => {
    if (col.width) return col.width;

    // Get max width from header and all data cells
    const headerWidth = col.header.length;
    const maxDataWidth = data.reduce((max, row) => {
      const cellValue = String(row[col.key] ?? '');
      return Math.max(max, cellValue.length);
    }, 0);

    return Math.max(headerWidth, maxDataWidth);
  });

  // Calculate total width needed (including borders: | col | col | = 3 chars per col + 1)
  const borderOverhead = columns.length * 3 + 1;
  const availableWidth = maxTotalWidth - borderOverhead;
  const totalNaturalWidth = naturalWidths.reduce((sum, w) => sum + w, 0);

  // If fits, return natural widths
  if (totalNaturalWidth <= availableWidth) {
    return naturalWidths;
  }

  // Need to shrink columns proportionally
  // Minimum width is header length (at least 4 chars)
  const minWidths = columns.map((col) => Math.max(4, col.header.length));
  const minTotal = minWidths.reduce((sum, w) => sum + w, 0);

  // If even minimums don't fit, use minimums anyway
  if (minTotal >= availableWidth) {
    return minWidths;
  }

  // Distribute remaining space proportionally
  const remainingSpace = availableWidth - minTotal;
  const excessWidths = naturalWidths.map((w, i) => Math.max(0, w - minWidths[i]));
  const totalExcess = excessWidths.reduce((sum, w) => sum + w, 0);

  if (totalExcess === 0) {
    return minWidths;
  }

  return naturalWidths.map((naturalWidth, i) => {
    const minWidth = minWidths[i];
    const excess = excessWidths[i];
    const share = (excess / totalExcess) * remainingSpace;
    return Math.floor(minWidth + share);
  });
}

// Pad string to width with alignment
// Adds '…' truncation indicator when content is cut off
function padCell(value: string, width: number, align: 'left' | 'right' | 'center' = 'left'): string {
  if (value.length > width) {
    // Truncate with ellipsis indicator
    if (width <= 1) return value.slice(0, width);
    return value.slice(0, width - 1) + '…';
  }

  if (value.length === width) return value;

  const padding = width - value.length;
  switch (align) {
    case 'right':
      return ' '.repeat(padding) + value;
    case 'center':
      const leftPad = Math.floor(padding / 2);
      const rightPad = padding - leftPad;
      return ' '.repeat(leftPad) + value + ' '.repeat(rightPad);
    default:
      return value + ' '.repeat(padding);
  }
}

export const Table: React.FC<TableProps> = ({
  columns,
  data,
  borderColor = 'gray',
  headerColor = 'cyan',
  compact = false,
  maxWidth,
}) => {
  const { stdout } = useStdout();
  const terminalWidth = maxWidth ?? stdout?.columns ?? 80;
  const widths = calculateColumnWidths(columns, data, terminalWidth);

  // Build separator line
  const separatorLine = columns.map((_, i) => '─'.repeat(widths[i] + 2)).join('┼');
  const topBorder = '┌' + columns.map((_, i) => '─'.repeat(widths[i] + 2)).join('┬') + '┐';
  const bottomBorder = '└' + columns.map((_, i) => '─'.repeat(widths[i] + 2)).join('┴') + '┘';
  const headerSeparator = '├' + separatorLine + '┤';

  // Render header row
  const renderHeader = () => (
    <Box>
      <Text color={borderColor}>│</Text>
      {columns.map((col, i) => (
        <React.Fragment key={col.key}>
          <Text color={headerColor} bold>
            {' ' + padCell(col.header, widths[i], col.align) + ' '}
          </Text>
          <Text color={borderColor}>│</Text>
        </React.Fragment>
      ))}
    </Box>
  );

  // Render data row
  const renderRow = (row: Record<string, string | number | undefined>, rowIndex: number) => (
    <Box key={rowIndex}>
      <Text color={borderColor}>│</Text>
      {columns.map((col, i) => (
        <React.Fragment key={col.key}>
          <Text>
            {' ' + padCell(String(row[col.key] ?? ''), widths[i], col.align) + ' '}
          </Text>
          <Text color={borderColor}>│</Text>
        </React.Fragment>
      ))}
    </Box>
  );

  if (compact) {
    // Compact mode: no borders, just aligned columns
    return (
      <Box flexDirection="column">
        <Box>
          {columns.map((col, i) => (
            <Text key={col.key} color={headerColor} bold>
              {padCell(col.header, widths[i] + 2, col.align)}
            </Text>
          ))}
        </Box>
        {data.map((row, i) => (
          <Box key={i}>
            {columns.map((col, j) => (
              <Text key={col.key}>
                {padCell(String(row[col.key] ?? ''), widths[j] + 2, col.align)}
              </Text>
            ))}
          </Box>
        ))}
      </Box>
    );
  }

  return (
    <Box flexDirection="column">
      <Text color={borderColor}>{topBorder}</Text>
      {renderHeader()}
      <Text color={borderColor}>{headerSeparator}</Text>
      {data.map(renderRow)}
      <Text color={borderColor}>{bottomBorder}</Text>
    </Box>
  );
};

/**
 * Parse markdown table string into Table props
 */
export function parseMarkdownTable(markdown: string): { columns: TableColumn[]; data: Record<string, string>[] } | null {
  const lines = markdown.trim().split('\n').filter(line => line.trim());

  if (lines.length < 2) return null;

  // Parse header row
  const headerLine = lines[0];
  const headers = headerLine
    .split('|')
    .map(h => h.trim())
    .filter(h => h.length > 0);

  if (headers.length === 0) return null;

  // Skip separator line (line with dashes)
  const dataStartIndex = lines[1].includes('-') ? 2 : 1;

  // Create columns from headers
  const columns: TableColumn[] = headers.map((header, i) => ({
    key: `col${i}`,
    header,
  }));

  // Parse data rows
  const data: Record<string, string>[] = [];
  for (let i = dataStartIndex; i < lines.length; i++) {
    const cells = lines[i]
      .split('|')
      .map(c => c.trim())
      .filter(c => c.length > 0 || lines[i].startsWith('|'));

    // Handle lines that start with |
    if (lines[i].trim().startsWith('|')) {
      const allCells = lines[i].split('|');
      allCells.shift(); // Remove empty first element
      if (allCells[allCells.length - 1].trim() === '') allCells.pop();

      const row: Record<string, string> = {};
      allCells.forEach((cell, j) => {
        if (j < columns.length) {
          row[`col${j}`] = cell.trim();
        }
      });
      data.push(row);
    } else if (cells.length > 0) {
      const row: Record<string, string> = {};
      cells.forEach((cell, j) => {
        if (j < columns.length) {
          row[`col${j}`] = cell;
        }
      });
      data.push(row);
    }
  }

  return { columns, data };
}

/**
 * Detect if text contains a markdown table
 */
export function containsMarkdownTable(text: string): boolean {
  const lines = text.split('\n');
  let hasHeaderSeparator = false;
  let hasPipes = false;
  let dataLineWithPipes = false;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (line.includes('|')) hasPipes = true;
    if (/^\s*\|?\s*[-:]+\s*\|/.test(line) || /\|\s*[-:]+\s*\|/.test(line)) {
      hasHeaderSeparator = true;
      // Check if there's a data row with pipes after the separator
      for (let j = i + 1; j < lines.length; j++) {
        const dataLine = lines[j].trim();
        if (!dataLine) continue;
        if (dataLine.includes('|')) {
          dataLineWithPipes = true;
        }
        break;
      }
    }
  }

  return hasPipes && hasHeaderSeparator && dataLineWithPipes;
}

export default Table;

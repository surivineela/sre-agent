/**
 * VimEditor - Full-screen multiline YAML/text editor
 *
 * Features:
 * - Syntax highlighting for YAML (using cli-highlight)
 * - Line numbers
 * - Cursor movement (hjkl or arrows)
 * - Insert/Normal mode toggle (i/Esc)
 * - Save (:w or Ctrl+S) and quit (:q or Ctrl+Q)
 * - Undo/redo (u/Ctrl+R)
 * - Status bar showing mode, line:col, filename
 */
import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Box, Text, useInput, useApp, useStdout } from 'ink';
import { theme } from '../../theme';

export type EditorMode = 'normal' | 'insert' | 'command';

export interface VimEditorProps {
  /** Initial content to edit */
  initialContent: string;
  /** File name for display */
  filename?: string;
  /** File type for syntax highlighting */
  fileType?: 'yaml' | 'json' | 'python' | 'text';
  /** Called when user saves */
  onSave: (content: string) => void;
  /** Called when user quits */
  onQuit: () => void;
  /** Called when user saves and quits */
  onSaveAndQuit?: (content: string) => void;
  /** Whether content has been modified */
  modified?: boolean;
  /** Read-only mode */
  readOnly?: boolean;
  /** Show line numbers */
  showLineNumbers?: boolean;
  /** Maximum visible lines */
  maxLines?: number;
}

interface EditorState {
  lines: string[];
  cursorRow: number;
  cursorCol: number;
  scrollOffset: number;
  mode: EditorMode;
  commandBuffer: string;
  modified: boolean;
  message: string;
  messageType: 'info' | 'error' | 'success';
  undoStack: string[][];
  redoStack: string[][];
}

/**
 * Simple YAML syntax highlighting
 */
function highlightYaml(line: string): React.ReactNode {
  const parts: React.ReactNode[] = [];
  let remaining = line;
  let key = 0;

  // Comments
  if (remaining.trim().startsWith('#')) {
    return <Text key={key} color="gray">{line}</Text>;
  }

  // Key: value pattern
  const keyMatch = remaining.match(/^(\s*)([a-zA-Z_][a-zA-Z0-9_-]*)(:\s*)/);
  if (keyMatch) {
    const [, indent, keyName, colon] = keyMatch;
    parts.push(<Text key={key++}>{indent}</Text>);
    parts.push(<Text key={key++} color="cyan">{keyName}</Text>);
    parts.push(<Text key={key++} color="white">{colon}</Text>);
    remaining = remaining.slice(keyMatch[0].length);

    // Value
    if (remaining) {
      // String value (quoted)
      if (remaining.startsWith('"') || remaining.startsWith("'")) {
        parts.push(<Text key={key++} color="green">{remaining}</Text>);
      }
      // Boolean/null
      else if (/^(true|false|null|yes|no)$/i.test(remaining.trim())) {
        parts.push(<Text key={key++} color="yellow">{remaining}</Text>);
      }
      // Number
      else if (/^-?\d+(\.\d+)?$/.test(remaining.trim())) {
        parts.push(<Text key={key++} color="magenta">{remaining}</Text>);
      }
      // Reference
      else if (remaining.trim().startsWith('*') || remaining.trim().startsWith('&')) {
        parts.push(<Text key={key++} color="red">{remaining}</Text>);
      }
      else {
        parts.push(<Text key={key++}>{remaining}</Text>);
      }
    }
    return <>{parts}</>;
  }

  // List item
  const listMatch = remaining.match(/^(\s*)(-)(\s*)/);
  if (listMatch) {
    const [, indent, dash, space] = listMatch;
    parts.push(<Text key={key++}>{indent}</Text>);
    parts.push(<Text key={key++} color="yellow">{dash}</Text>);
    parts.push(<Text key={key++}>{space}</Text>);
    parts.push(<Text key={key++}>{remaining.slice(listMatch[0].length)}</Text>);
    return <>{parts}</>;
  }

  // Block scalar indicators
  if (remaining.trim() === '|' || remaining.trim() === '>') {
    return <Text key={key} color="yellow">{line}</Text>;
  }

  return <Text>{line}</Text>;
}

/**
 * Get highlighted line based on file type
 */
function getHighlightedLine(line: string, fileType: string): React.ReactNode {
  switch (fileType) {
    case 'yaml':
      return highlightYaml(line);
    default:
      return <Text>{line}</Text>;
  }
}

export const VimEditor: React.FC<VimEditorProps> = ({
  initialContent,
  filename = 'untitled',
  fileType = 'yaml',
  onSave,
  onQuit,
  onSaveAndQuit,
  readOnly = false,
  showLineNumbers = true,
  maxLines = 20,
}) => {
  const { exit } = useApp();
  const { stdout } = useStdout();

  // Calculate available lines based on terminal height
  const terminalHeight = stdout?.rows || 24;
  const availableLines = Math.min(maxLines, terminalHeight - 4); // Reserve space for status bar

  const [state, setState] = useState<EditorState>(() => ({
    lines: initialContent.split('\n'),
    cursorRow: 0,
    cursorCol: 0,
    scrollOffset: 0,
    mode: 'normal',
    commandBuffer: '',
    modified: false,
    message: readOnly ? '-- READ ONLY --' : '-- NORMAL --',
    messageType: 'info',
    undoStack: [],
    redoStack: [],
  }));

  // Save undo state
  const saveUndo = useCallback(() => {
    setState((s) => ({
      ...s,
      undoStack: [...s.undoStack.slice(-50), [...s.lines]],
      redoStack: [],
    }));
  }, []);

  // Handle cursor movement
  const moveCursor = useCallback(
    (rowDelta: number, colDelta: number) => {
      setState((s) => {
        let newRow = s.cursorRow + rowDelta;
        let newCol = s.cursorCol + colDelta;

        // Clamp row
        newRow = Math.max(0, Math.min(s.lines.length - 1, newRow));

        // Clamp column to line length
        const lineLength = s.lines[newRow]?.length || 0;
        newCol = Math.max(0, Math.min(lineLength, newCol));

        // Adjust scroll offset
        let newScrollOffset = s.scrollOffset;
        if (newRow < s.scrollOffset) {
          newScrollOffset = newRow;
        } else if (newRow >= s.scrollOffset + availableLines) {
          newScrollOffset = newRow - availableLines + 1;
        }

        return {
          ...s,
          cursorRow: newRow,
          cursorCol: newCol,
          scrollOffset: newScrollOffset,
        };
      });
    },
    [availableLines]
  );

  // Insert character at cursor
  const insertChar = useCallback(
    (char: string) => {
      if (readOnly) return;

      setState((s) => {
        const line = s.lines[s.cursorRow] || '';
        const newLine =
          line.slice(0, s.cursorCol) + char + line.slice(s.cursorCol);
        const newLines = [...s.lines];
        newLines[s.cursorRow] = newLine;

        return {
          ...s,
          lines: newLines,
          cursorCol: s.cursorCol + char.length,
          modified: true,
        };
      });
    },
    [readOnly]
  );

  // Delete character before cursor
  const deleteChar = useCallback(() => {
    if (readOnly) return;

    setState((s) => {
      if (s.cursorCol > 0) {
        const line = s.lines[s.cursorRow] || '';
        const newLine =
          line.slice(0, s.cursorCol - 1) + line.slice(s.cursorCol);
        const newLines = [...s.lines];
        newLines[s.cursorRow] = newLine;
        return {
          ...s,
          lines: newLines,
          cursorCol: s.cursorCol - 1,
          modified: true,
        };
      } else if (s.cursorRow > 0) {
        // Join with previous line
        const prevLine = s.lines[s.cursorRow - 1] || '';
        const currentLine = s.lines[s.cursorRow] || '';
        const newLines = [...s.lines];
        newLines[s.cursorRow - 1] = prevLine + currentLine;
        newLines.splice(s.cursorRow, 1);
        return {
          ...s,
          lines: newLines,
          cursorRow: s.cursorRow - 1,
          cursorCol: prevLine.length,
          modified: true,
        };
      }
      return s;
    });
  }, [readOnly]);

  // Insert new line
  const insertNewLine = useCallback(() => {
    if (readOnly) return;

    setState((s) => {
      const line = s.lines[s.cursorRow] || '';
      const beforeCursor = line.slice(0, s.cursorCol);
      const afterCursor = line.slice(s.cursorCol);

      // Auto-indent: match leading whitespace
      const indent = beforeCursor.match(/^\s*/)?.[0] || '';

      const newLines = [...s.lines];
      newLines[s.cursorRow] = beforeCursor;
      newLines.splice(s.cursorRow + 1, 0, indent + afterCursor);

      return {
        ...s,
        lines: newLines,
        cursorRow: s.cursorRow + 1,
        cursorCol: indent.length,
        modified: true,
      };
    });
  }, [readOnly]);

  // Execute command
  const executeCommand = useCallback(
    (cmd: string) => {
      const trimmedCmd = cmd.trim();

      if (trimmedCmd === 'w' || trimmedCmd === 'write') {
        if (!readOnly) {
          onSave(state.lines.join('\n'));
          setState((s) => ({
            ...s,
            modified: false,
            message: `"${filename}" written`,
            messageType: 'success',
            mode: 'normal',
            commandBuffer: '',
          }));
        } else {
          setState((s) => ({
            ...s,
            message: 'Cannot write: read-only mode',
            messageType: 'error',
            mode: 'normal',
            commandBuffer: '',
          }));
        }
      } else if (trimmedCmd === 'q' || trimmedCmd === 'quit') {
        if (state.modified && !readOnly) {
          setState((s) => ({
            ...s,
            message: 'No write since last change (use :q! to override)',
            messageType: 'error',
            mode: 'normal',
            commandBuffer: '',
          }));
        } else {
          onQuit();
        }
      } else if (trimmedCmd === 'q!' || trimmedCmd === 'quit!') {
        onQuit();
      } else if (trimmedCmd === 'wq' || trimmedCmd === 'x') {
        if (!readOnly) {
          const content = state.lines.join('\n');
          if (onSaveAndQuit) {
            onSaveAndQuit(content);
          } else {
            onSave(content);
            onQuit();
          }
        } else {
          onQuit();
        }
      } else {
        setState((s) => ({
          ...s,
          message: `Unknown command: ${trimmedCmd}`,
          messageType: 'error',
          mode: 'normal',
          commandBuffer: '',
        }));
      }
    },
    [state.lines, state.modified, filename, readOnly, onSave, onQuit, onSaveAndQuit]
  );

  // Undo
  const undo = useCallback(() => {
    setState((s) => {
      if (s.undoStack.length === 0) {
        return { ...s, message: 'Already at oldest change', messageType: 'info' };
      }
      const prevLines = s.undoStack[s.undoStack.length - 1];
      return {
        ...s,
        lines: prevLines,
        undoStack: s.undoStack.slice(0, -1),
        redoStack: [...s.redoStack, [...s.lines]],
        message: 'Undo',
        messageType: 'info',
      };
    });
  }, []);

  // Redo
  const redo = useCallback(() => {
    setState((s) => {
      if (s.redoStack.length === 0) {
        return { ...s, message: 'Already at newest change', messageType: 'info' };
      }
      const nextLines = s.redoStack[s.redoStack.length - 1];
      return {
        ...s,
        lines: nextLines,
        redoStack: s.redoStack.slice(0, -1),
        undoStack: [...s.undoStack, [...s.lines]],
        message: 'Redo',
        messageType: 'info',
      };
    });
  }, []);

  // Handle keyboard input
  useInput((input, key) => {
    // Command mode
    if (state.mode === 'command') {
      if (key.return) {
        executeCommand(state.commandBuffer);
      } else if (key.escape) {
        setState((s) => ({
          ...s,
          mode: 'normal',
          commandBuffer: '',
          message: '-- NORMAL --',
          messageType: 'info',
        }));
      } else if (key.backspace || key.delete) {
        setState((s) => ({
          ...s,
          commandBuffer: s.commandBuffer.slice(0, -1),
        }));
      } else if (input && !key.ctrl && !key.meta) {
        setState((s) => ({
          ...s,
          commandBuffer: s.commandBuffer + input,
        }));
      }
      return;
    }

    // Insert mode
    if (state.mode === 'insert') {
      if (key.escape) {
        setState((s) => ({
          ...s,
          mode: 'normal',
          message: '-- NORMAL --',
          messageType: 'info',
        }));
      } else if (key.return) {
        saveUndo();
        insertNewLine();
      } else if (key.backspace || key.delete) {
        saveUndo();
        deleteChar();
      } else if (key.tab) {
        saveUndo();
        insertChar('  '); // 2-space indent
      } else if (key.upArrow) {
        moveCursor(-1, 0);
      } else if (key.downArrow) {
        moveCursor(1, 0);
      } else if (key.leftArrow) {
        moveCursor(0, -1);
      } else if (key.rightArrow) {
        moveCursor(0, 1);
      } else if (input && !key.ctrl && !key.meta) {
        insertChar(input);
      }
      return;
    }

    // Normal mode
    if (state.mode === 'normal') {
      // Mode switching
      if (input === 'i') {
        setState((s) => ({
          ...s,
          mode: 'insert',
          message: '-- INSERT --',
          messageType: 'info',
        }));
      } else if (input === 'a') {
        setState((s) => ({
          ...s,
          mode: 'insert',
          cursorCol: Math.min(s.cursorCol + 1, (s.lines[s.cursorRow]?.length || 0)),
          message: '-- INSERT --',
          messageType: 'info',
        }));
      } else if (input === 'o') {
        saveUndo();
        setState((s) => {
          const indent = s.lines[s.cursorRow]?.match(/^\s*/)?.[0] || '';
          const newLines = [...s.lines];
          newLines.splice(s.cursorRow + 1, 0, indent);
          return {
            ...s,
            lines: newLines,
            cursorRow: s.cursorRow + 1,
            cursorCol: indent.length,
            mode: 'insert',
            message: '-- INSERT --',
            messageType: 'info',
            modified: true,
          };
        });
      } else if (input === 'O') {
        saveUndo();
        setState((s) => {
          const indent = s.lines[s.cursorRow]?.match(/^\s*/)?.[0] || '';
          const newLines = [...s.lines];
          newLines.splice(s.cursorRow, 0, indent);
          return {
            ...s,
            lines: newLines,
            cursorCol: indent.length,
            mode: 'insert',
            message: '-- INSERT --',
            messageType: 'info',
            modified: true,
          };
        });
      }
      // Command mode
      else if (input === ':') {
        setState((s) => ({
          ...s,
          mode: 'command',
          commandBuffer: '',
        }));
      }
      // Movement
      else if (input === 'h' || key.leftArrow) {
        moveCursor(0, -1);
      } else if (input === 'j' || key.downArrow) {
        moveCursor(1, 0);
      } else if (input === 'k' || key.upArrow) {
        moveCursor(-1, 0);
      } else if (input === 'l' || key.rightArrow) {
        moveCursor(0, 1);
      }
      // Page movement
      else if (key.ctrl && input === 'd') {
        moveCursor(Math.floor(availableLines / 2), 0);
      } else if (key.ctrl && input === 'u') {
        moveCursor(-Math.floor(availableLines / 2), 0);
      }
      // Line movement
      else if (input === '0') {
        setState((s) => ({ ...s, cursorCol: 0 }));
      } else if (input === '$') {
        setState((s) => ({
          ...s,
          cursorCol: s.lines[s.cursorRow]?.length || 0,
        }));
      } else if (input === 'g') {
        // Go to start (simplified: just gg)
        setState((s) => ({ ...s, cursorRow: 0, cursorCol: 0, scrollOffset: 0 }));
      } else if (input === 'G') {
        setState((s) => ({
          ...s,
          cursorRow: s.lines.length - 1,
          cursorCol: 0,
          scrollOffset: Math.max(0, s.lines.length - availableLines),
        }));
      }
      // Undo/redo
      else if (input === 'u') {
        undo();
      } else if (key.ctrl && input === 'r') {
        redo();
      }
      // Quick save
      else if (key.ctrl && input === 's') {
        if (!readOnly) {
          onSave(state.lines.join('\n'));
          setState((s) => ({
            ...s,
            modified: false,
            message: `"${filename}" written`,
            messageType: 'success',
          }));
        }
      }
      // Quick quit
      else if (key.ctrl && input === 'q') {
        if (state.modified && !readOnly) {
          setState((s) => ({
            ...s,
            message: 'No write since last change (use Ctrl+Q again to force quit)',
            messageType: 'error',
          }));
        } else {
          onQuit();
        }
      }
      // Delete line
      else if (input === 'd') {
        // Simplified: dd to delete line
        saveUndo();
        setState((s) => {
          if (s.lines.length === 1) {
            return { ...s, lines: [''], modified: true };
          }
          const newLines = [...s.lines];
          newLines.splice(s.cursorRow, 1);
          return {
            ...s,
            lines: newLines,
            cursorRow: Math.min(s.cursorRow, newLines.length - 1),
            modified: true,
          };
        });
      }
    }
  });

  // Visible lines
  const visibleLines = state.lines.slice(
    state.scrollOffset,
    state.scrollOffset + availableLines
  );

  // Line number width
  const lineNumberWidth = String(state.lines.length).length + 1;

  return (
    <Box flexDirection="column" width="100%">
      {/* Editor content */}
      <Box flexDirection="column" borderStyle="single" borderColor="gray">
        {visibleLines.map((line, index) => {
          const lineNumber = state.scrollOffset + index;
          const isCurrentLine = lineNumber === state.cursorRow;

          return (
            <Box key={lineNumber}>
              {/* Line number */}
              {showLineNumbers && (
                <Box width={lineNumberWidth} justifyContent="flex-end">
                  <Text color={isCurrentLine ? theme.ink.brand : 'gray'}>
                    {lineNumber + 1}
                  </Text>
                  <Text> </Text>
                </Box>
              )}

              {/* Line content */}
              <Box flexGrow={1}>
                {isCurrentLine ? (
                  // Current line with cursor
                  <Text>
                    {line.slice(0, state.cursorCol)}
                    <Text
                      backgroundColor={
                        state.mode === 'insert' ? theme.ink.brand : 'white'
                      }
                      color="black"
                    >
                      {line[state.cursorCol] || ' '}
                    </Text>
                    {line.slice(state.cursorCol + 1)}
                  </Text>
                ) : (
                  // Other lines with syntax highlighting
                  getHighlightedLine(line, fileType)
                )}
              </Box>
            </Box>
          );
        })}
      </Box>

      {/* Status bar */}
      <Box justifyContent="space-between" paddingX={1}>
        {/* Left: mode/command */}
        <Box>
          {state.mode === 'command' ? (
            <Text>
              :{state.commandBuffer}
              <Text backgroundColor="white" color="black">
                {' '}
              </Text>
            </Text>
          ) : (
            <Text
              color={
                state.messageType === 'error'
                  ? theme.ink.error
                  : state.messageType === 'success'
                    ? theme.ink.success
                    : theme.ink.muted
              }
            >
              {state.message}
            </Text>
          )}
        </Box>

        {/* Right: position info */}
        <Box>
          <Text color="gray">
            {filename}
            {state.modified ? ' [+]' : ''}
          </Text>
          <Text color="gray"> | </Text>
          <Text color="gray">
            Ln {state.cursorRow + 1}, Col {state.cursorCol + 1}
          </Text>
        </Box>
      </Box>

      {/* Help hint */}
      <Box paddingX={1}>
        <Text color="gray" dimColor>
          {state.mode === 'normal'
            ? 'i=insert :w=save :q=quit :wq=save+quit h/j/k/l=move'
            : state.mode === 'insert'
              ? 'Esc=normal mode'
              : 'Enter=execute Esc=cancel'}
        </Text>
      </Box>
    </Box>
  );
};

export default VimEditor;

/**
 * Enhanced text input component - refined terminal UX
 * Clean prompt with smooth cursor, history support, and intelligent autocomplete
 */
import React, { useState, useMemo } from 'react';
import { Box, Text, useInput, useStdout } from 'ink';
import { theme, BABY_PINK, BABY_BLUE } from '../../theme';
import { getCommandSuggestionsWithDescriptions, type CommandSuggestion } from '../../commands';

export interface TextInputProps {
  value: string;
  onChange: (value: string) => void;
  onSubmit?: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  prompt?: string;
  promptColor?: string;
  cursorColor?: string;
  showCursor?: boolean;
  onHistoryPrev?: () => void;
  onHistoryNext?: () => void;
  enableAutocomplete?: boolean;
}

export const TextInput: React.FC<TextInputProps> = ({
  value,
  onChange,
  onSubmit,
  placeholder = 'Type a message or / for commands',
  disabled = false,
  prompt = '›',
  promptColor = BABY_PINK,
  cursorColor = BABY_BLUE,
  showCursor = true,
  onHistoryPrev,
  onHistoryNext,
  enableAutocomplete = true,
}) => {
  const { stdout } = useStdout();
  const terminalWidth = stdout?.columns || 80;
  const [cursorVisible, setCursorVisible] = useState(true);
  const [cursorPosition, setCursorPosition] = useState(value.length);
  const [suggestionIndex, setSuggestionIndex] = useState(0);

  // Get autocomplete suggestions for slash commands (with descriptions)
  const suggestions: CommandSuggestion[] = useMemo(() => {
    if (!enableAutocomplete || !value.startsWith('/')) {
      return [];
    }
    return getCommandSuggestionsWithDescriptions(value);
  }, [value, enableAutocomplete]);

  // Reset suggestion index when suggestions change
  React.useEffect(() => {
    setSuggestionIndex(0);
  }, [suggestions.length]);

  // Cursor blink effect
  React.useEffect(() => {
    if (!showCursor || disabled) return;

    const timer = setInterval(() => {
      setCursorVisible((v) => !v);
    }, 530);

    return () => clearInterval(timer);
  }, [showCursor, disabled]);

  // Keep cursor position in sync with value length
  React.useEffect(() => {
    setCursorPosition(value.length);
  }, [value]);

  useInput(
    (input, key) => {
      if (disabled) return;

      // Escape to close suggestions
      if (key.escape && suggestions.length > 0) {
        onChange('');
        setCursorPosition(0);
        return;
      }

      // Tab for autocomplete
      if (key.tab && suggestions.length > 0) {
        if (key.shift) {
          // Shift+Tab cycles backwards through suggestions
          setSuggestionIndex((prev) => (prev - 1 + suggestions.length) % suggestions.length);
        } else if (suggestions.length === 1) {
          // Single suggestion - complete it
          const cmd = '/' + suggestions[0].name + ' ';
          onChange(cmd);
          setCursorPosition(cmd.length);
        } else {
          // Multiple suggestions - cycle through them
          const nextIndex = (suggestionIndex + 1) % suggestions.length;
          setSuggestionIndex(nextIndex);
          // Apply the suggestion
          const cmd = '/' + suggestions[nextIndex].name;
          onChange(cmd);
          setCursorPosition(cmd.length);
        }
        return;
      }

      // Submit on Enter - apply suggestion if navigated, otherwise submit
      if (key.return) {
        if (suggestions.length > 0 && value.startsWith('/') && !value.includes(' ')) {
          // Apply the selected suggestion and add space
          const cmd = '/' + suggestions[suggestionIndex].name + ' ';
          onChange(cmd);
          setCursorPosition(cmd.length);
        } else {
          onSubmit?.(value);
        }
        return;
      }

      // Up/Down arrows: navigate suggestions if visible, otherwise history
      if (key.upArrow) {
        if (suggestions.length > 0) {
          // Navigate suggestions up
          const newIndex = (suggestionIndex - 1 + suggestions.length) % suggestions.length;
          setSuggestionIndex(newIndex);
        } else if (onHistoryPrev) {
          onHistoryPrev();
        }
        return;
      }

      if (key.downArrow) {
        if (suggestions.length > 0) {
          // Navigate suggestions down
          const newIndex = (suggestionIndex + 1) % suggestions.length;
          setSuggestionIndex(newIndex);
        } else if (onHistoryNext) {
          onHistoryNext();
        }
        return;
      }

      // Backspace
      if (key.backspace || key.delete) {
        if (cursorPosition > 0) {
          const newValue = value.slice(0, cursorPosition - 1) + value.slice(cursorPosition);
          onChange(newValue);
          setCursorPosition(cursorPosition - 1);
        }
        return;
      }

      // Left arrow
      if (key.leftArrow) {
        setCursorPosition(Math.max(0, cursorPosition - 1));
        return;
      }

      // Right arrow
      if (key.rightArrow) {
        setCursorPosition(Math.min(value.length, cursorPosition + 1));
        return;
      }

      // Home (Ctrl+A)
      if (key.ctrl && input === 'a') {
        setCursorPosition(0);
        return;
      }

      // End (Ctrl+E)
      if (key.ctrl && input === 'e') {
        setCursorPosition(value.length);
        return;
      }

      // Clear line (Ctrl+U)
      if (key.ctrl && input === 'u') {
        onChange('');
        setCursorPosition(0);
        return;
      }

      // Kill to end (Ctrl+K)
      if (key.ctrl && input === 'k') {
        onChange(value.slice(0, cursorPosition));
        return;
      }

      // Regular character input
      if (input && !key.ctrl && !key.meta) {
        const newValue = value.slice(0, cursorPosition) + input + value.slice(cursorPosition);
        onChange(newValue);
        setCursorPosition(cursorPosition + input.length);
      }
    },
    { isActive: !disabled }
  );

  const displayValue = value || '';
  const showPlaceholder = displayValue.length === 0 && placeholder;

  // Build the displayed text with cursor
  const beforeCursor = displayValue.slice(0, cursorPosition);
  const cursorChar = displayValue[cursorPosition] || ' ';
  const afterCursor = displayValue.slice(cursorPosition + 1);

  // Get ghost text for autocomplete (the part that would be completed)
  const ghostText = useMemo(() => {
    if (suggestions.length === 0 || !value.startsWith('/')) return '';
    const currentSuggestion = suggestions[suggestionIndex];
    if (currentSuggestion) {
      const fullCmd = '/' + currentSuggestion.name;
      if (fullCmd.startsWith(value)) {
        return fullCmd.slice(value.length);
      }
    }
    return '';
  }, [suggestions, suggestionIndex, value]);

  // Calculate max command name length for alignment
  const maxCmdLen = useMemo(() => {
    if (suggestions.length === 0) return 0;
    return Math.max(...suggestions.map(s => s.name.length + 1)); // +1 for the /
  }, [suggestions]);

  return (
    <Box flexDirection="column">
      <Box>
        <Text color={promptColor} bold>{prompt}</Text>
        <Text> </Text>
        {showPlaceholder ? (
          <Text color={theme.ink.muted}>{placeholder}</Text>
        ) : (
          <>
            <Text>{beforeCursor}</Text>
            {showCursor && cursorVisible ? (
              <Text backgroundColor={cursorColor} color="black">
                {cursorChar}
              </Text>
            ) : (
              <Text>{cursorChar}</Text>
            )}
            <Text>{afterCursor}</Text>
            {/* Ghost text for autocomplete suggestion */}
            {ghostText && <Text color={theme.ink.muted}>{ghostText}</Text>}
          </>
        )}
      </Box>
      {/* Show autocomplete suggestions as vertical list with descriptions */}
      {suggestions.length > 0 && (
        <Box flexDirection="column" marginTop={1}>
          {suggestions.slice(0, 10).map((s, i) => {
            const isSelected = i === suggestionIndex;
            const cmdName = '/' + s.name;
            const padding = ' '.repeat(Math.max(0, maxCmdLen + 4 - cmdName.length));
            // Truncate description to fit terminal
            const maxDescLen = Math.max(20, terminalWidth - maxCmdLen - 10);
            const desc = s.description.length > maxDescLen
              ? s.description.slice(0, maxDescLen - 3) + '...'
              : s.description;

            return (
              <Box key={s.name}>
                <Text color={theme.ink.muted}>  </Text>
                {isSelected ? (
                  <>
                    <Text color={theme.ink.brand} bold>{cmdName}</Text>
                    <Text>{padding}</Text>
                    <Text color="white">{desc}</Text>
                  </>
                ) : (
                  <>
                    <Text color={theme.ink.muted}>{cmdName}</Text>
                    <Text>{padding}</Text>
                    <Text color={theme.ink.muted}>{desc}</Text>
                  </>
                )}
              </Box>
            );
          })}
          {suggestions.length > 10 && (
            <Box>
              <Text color={theme.ink.muted}>  ... and {suggestions.length - 10} more</Text>
            </Box>
          )}
        </Box>
      )}
    </Box>
  );
};

export default TextInput;

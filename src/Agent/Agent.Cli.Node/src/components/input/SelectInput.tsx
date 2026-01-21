/**
 * Select input component for menu selection
 */
import React, { useState } from 'react';
import { Box, Text, useInput } from 'ink';

export interface SelectItem<T = string> {
  label: string;
  value: T;
  description?: string;
}

export interface SelectInputProps<T = string> {
  items: SelectItem<T>[];
  onSelect: (item: SelectItem<T>) => void;
  onCancel?: () => void;
  initialIndex?: number;
  indicatorColor?: string;
  selectedColor?: string;
  limit?: number;
}

export function SelectInput<T = string>({
  items,
  onSelect,
  onCancel,
  initialIndex = 0,
  indicatorColor = 'cyan',
  selectedColor = 'cyan',
  limit,
}: SelectInputProps<T>): React.ReactElement {
  const [selectedIndex, setSelectedIndex] = useState(initialIndex);

  const visibleItems = limit ? items.slice(0, limit) : items;

  useInput((input, key) => {
    // Navigate up
    if (key.upArrow) {
      setSelectedIndex((prev) => Math.max(0, prev - 1));
      return;
    }

    // Navigate down
    if (key.downArrow) {
      setSelectedIndex((prev) => Math.min(visibleItems.length - 1, prev + 1));
      return;
    }

    // Select on Enter
    if (key.return) {
      const selectedItem = visibleItems[selectedIndex];
      if (selectedItem) {
        onSelect(selectedItem);
      }
      return;
    }

    // Cancel on Escape
    if (key.escape) {
      onCancel?.();
      return;
    }

    // Number shortcuts
    const num = parseInt(input, 10);
    if (!isNaN(num) && num >= 1 && num <= visibleItems.length) {
      setSelectedIndex(num - 1);
      return;
    }
  });

  return (
    <Box flexDirection="column">
      {visibleItems.map((item, index) => {
        const isSelected = index === selectedIndex;
        return (
          <Box key={String(item.value)}>
            <Text color={isSelected ? indicatorColor : undefined}>
              {isSelected ? '› ' : '  '}
            </Text>
            <Text
              color={isSelected ? selectedColor : undefined}
              bold={isSelected}
            >
              {item.label}
            </Text>
            {item.description && (
              <Text color="gray"> - {item.description}</Text>
            )}
          </Box>
        );
      })}
    </Box>
  );
}

export default SelectInput;

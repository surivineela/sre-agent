/**
 * Alert component for displaying messages with different severity levels
 */
import React from 'react';
import { Box, Text } from 'ink';

export type AlertType = 'info' | 'success' | 'warning' | 'error';

export interface AlertProps {
  type?: AlertType;
  title?: string;
  children?: React.ReactNode;
  bordered?: boolean;
}

const getAlertConfig = (type: AlertType) => {
  switch (type) {
    case 'success':
      return { icon: '✓', color: 'green', borderColor: 'green' };
    case 'warning':
      return { icon: '⚠', color: 'yellow', borderColor: 'yellow' };
    case 'error':
      return { icon: '✗', color: 'red', borderColor: 'red' };
    case 'info':
    default:
      return { icon: 'ℹ', color: 'blue', borderColor: 'blue' };
  }
};

export const Alert: React.FC<AlertProps> = ({
  type = 'info',
  title,
  children,
  bordered = false,
}) => {
  const config = getAlertConfig(type);

  const content = (
    <Box flexDirection="column">
      <Box>
        <Text color={config.color}>{config.icon} </Text>
        {title && <Text bold>{title}</Text>}
      </Box>
      {children && (
        <Box marginLeft={2} marginTop={title ? 1 : 0}>
          <Text>{children}</Text>
        </Box>
      )}
    </Box>
  );

  if (bordered) {
    return (
      <Box borderStyle="round" borderColor={config.borderColor} paddingX={1}>
        {content}
      </Box>
    );
  }

  return content;
};

export default Alert;

/**
 * Badge component for status indicators
 */
import React from 'react';
import { Text } from 'ink';

export type BadgeVariant = 'default' | 'success' | 'warning' | 'error' | 'info';

export interface BadgeProps {
  variant?: BadgeVariant;
  label: string;
  icon?: string;
}

const getVariantConfig = (variant: BadgeVariant) => {
  switch (variant) {
    case 'success':
      return { color: 'green', bgColor: 'greenBright', icon: '●' };
    case 'warning':
      return { color: 'yellow', bgColor: 'yellowBright', icon: '●' };
    case 'error':
      return { color: 'red', bgColor: 'redBright', icon: '●' };
    case 'info':
      return { color: 'blue', bgColor: 'blueBright', icon: '●' };
    default:
      return { color: 'gray', bgColor: 'white', icon: '○' };
  }
};

export const Badge: React.FC<BadgeProps> = ({
  variant = 'default',
  label,
  icon,
}) => {
  const config = getVariantConfig(variant);
  const displayIcon = icon ?? config.icon;

  return (
    <Text>
      <Text color={config.color}>{displayIcon}</Text>
      <Text color={config.color}> {label}</Text>
    </Text>
  );
};

export default Badge;

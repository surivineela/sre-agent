/**
 * Animated spinner component
 */
import React, { useState, useEffect } from 'react';
import { Text } from 'ink';

// Braille spinner - smooth 8-frame rotation
const SPINNER_FRAMES = ['⣾', '⣽', '⣻', '⢿', '⡿', '⣟', '⣯', '⣷'];
const DOTS_FRAMES = ['·', '✢', '✳', '∗', '✻', '✽'];
const SIMPLE_FRAMES = ['|', '/', '-', '\\'];

export type SpinnerType = 'dots' | 'simple' | 'default';

export interface SpinnerProps {
  type?: SpinnerType;
  color?: string;
  label?: string;
  interval?: number;
}

const getFrames = (type: SpinnerType): string[] => {
  switch (type) {
    case 'dots':
      return DOTS_FRAMES;
    case 'simple':
      return SIMPLE_FRAMES;
    default:
      return SPINNER_FRAMES;
  }
};

export const Spinner: React.FC<SpinnerProps> = ({
  type = 'default',
  color = 'cyan',
  label,
  interval = 80,
}) => {
  const [frame, setFrame] = useState(0);
  const frames = getFrames(type);

  useEffect(() => {
    const timer = setInterval(() => {
      setFrame((prev) => (prev + 1) % frames.length);
    }, interval);

    return () => clearInterval(timer);
  }, [frames.length, interval]);

  return (
    <Text>
      <Text color={color}>{frames[frame]}</Text>
      {label && <Text> {label}</Text>}
    </Text>
  );
};

export default Spinner;

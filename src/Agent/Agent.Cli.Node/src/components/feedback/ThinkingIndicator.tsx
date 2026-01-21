/**
 * Thinking indicator with shimmer animation effect
 * Color scheme: dark pink → baby pink → white shimmer
 */
import React, { useState, useEffect } from 'react';
import { Box, Text } from 'ink';
import { theme, BABY_PINK } from '../../theme';

// Braille spinner - smooth 8-frame rotation
const SPINNER_FRAMES = ['⣾', '⣽', '⣻', '⢿', '⡿', '⣟', '⣯', '⣷'];

// Shimmer colors: baby pink -> white
const SHIMMER_COLORS = [
  BABY_PINK,  // Baby pink (#F8BBD9)
  '#FCD5E8',  // Very light pink
  '#FFFFFF',  // White
];

// Animation speeds
const SPINNER_SPEED = 80; // milliseconds
const SHIMMER_SPEED = 60; // milliseconds - faster shimmer wave
const WAVE_WIDTH = 3; // Number of characters in the shimmer wave

// Shimmer text component - creates a wave effect across text with pink-white gradient
const ShimmerText: React.FC<{ text: string }> = ({ text }) => {
  const [wavePosition, setWavePosition] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setWavePosition((prev) => (prev + 1) % (text.length + WAVE_WIDTH + 5));
    }, SHIMMER_SPEED);
    return () => clearInterval(interval);
  }, [text.length]);

  // Get color for character based on distance from wave position
  const getCharColor = (charIndex: number): string => {
    const distance = charIndex - wavePosition;

    if (distance >= 0 && distance < WAVE_WIDTH) {
      // Character is in the wave - use brighter colors (baby pink -> white)
      const colorIndex = Math.min(distance, SHIMMER_COLORS.length - 1);
      return SHIMMER_COLORS[colorIndex];
    }

    // Base color (baby pink)
    return SHIMMER_COLORS[0];
  };

  return (
    <Text>
      {text.split('').map((char, index) => (
        <Text key={index} color={getCharColor(index)}>
          {char}
        </Text>
      ))}
    </Text>
  );
};

/**
 * Animated spinner component
 */
export const Spinner: React.FC<{ color?: string }> = ({ color = 'gray' }) => {
  const [index, setIndex] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setIndex((prev) => (prev + 1) % SPINNER_FRAMES.length);
    }, SPINNER_SPEED);
    return () => clearInterval(interval);
  }, []);

  return <Text color={color}>{SPINNER_FRAMES[index]}</Text>;
};

export interface ThinkingIndicatorProps {
  task?: string;
  mode?: 'thinking' | 'cancelling' | 'executing';
  showHints?: boolean;
}

export const ThinkingIndicator: React.FC<ThinkingIndicatorProps> = ({
  task,
  mode = 'thinking',
  showHints = true,
}) => {
  const [spinnerIndex, setSpinnerIndex] = useState(0);

  // Spinner animation
  useEffect(() => {
    const interval = setInterval(() => {
      setSpinnerIndex((prev) => (prev + 1) % SPINNER_FRAMES.length);
    }, SPINNER_SPEED);
    return () => clearInterval(interval);
  }, []);

  const text = mode === 'executing'
    ? 'Executing'
    : mode === 'cancelling'
      ? 'Cancelling'
      : (task || 'Thinking');

  const showHelper = mode !== 'cancelling' && showHints;

  return (
    <Box flexDirection="row" gap={1}>
      <Box flexDirection="row">
        <Text color={SHIMMER_COLORS[0]}>{SPINNER_FRAMES[spinnerIndex]} </Text>
        <ShimmerText text={text} />
      </Box>
      {showHelper && <Text color={theme.ink.muted}>(esc to cancel)</Text>}
    </Box>
  );
};

export default ThinkingIndicator;

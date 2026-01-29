/**
 * useScrollable hook - manages scrolling state for chat view
 * Provides scroll position, navigation functions, and auto-scroll behavior
 */
import { useState, useCallback, useRef, useEffect } from 'react';
import { useStdout } from 'ink';

export interface ScrollState {
  scrollTop: number;
  maxScroll: number;
  viewportHeight: number;
  contentHeight: number;
  isAtBottom: boolean;
  isAtTop: boolean;
}

export interface UseScrollableOptions {
  contentHeight: number;
  autoScroll?: boolean;
  headerHeight?: number;
  footerHeight?: number;
}

export interface UseScrollableReturn {
  state: ScrollState;
  scrollTo: (position: number) => void;
  scrollBy: (delta: number) => void;
  scrollToTop: () => void;
  scrollToBottom: () => void;
  pageUp: () => void;
  pageDown: () => void;
  autoScrollPaused: boolean;
  resumeAutoScroll: () => void;
}

export const useScrollable = (options: UseScrollableOptions): UseScrollableReturn => {
  const { stdout } = useStdout();
  const terminalRows = stdout?.rows || 24;

  // Calculate viewport height (terminal height minus header/footer)
  const headerHeight = options.headerHeight ?? 12; // Default header size
  const footerHeight = options.footerHeight ?? 4;  // Input area
  const viewportHeight = Math.max(5, terminalRows - headerHeight - footerHeight);

  const [scrollTop, setScrollTop] = useState(0);
  const [autoScrollPaused, setAutoScrollPaused] = useState(false);
  const lastContentHeight = useRef(options.contentHeight);

  const maxScroll = Math.max(0, options.contentHeight - viewportHeight);

  // Auto-scroll when content grows (unless paused)
  useEffect(() => {
    if (options.autoScroll && !autoScrollPaused && options.contentHeight > lastContentHeight.current) {
      setScrollTop(maxScroll);
    }
    lastContentHeight.current = options.contentHeight;
  }, [options.contentHeight, options.autoScroll, autoScrollPaused, maxScroll]);

  const scrollTo = useCallback((position: number) => {
    const newPosition = Math.max(0, Math.min(maxScroll, position));
    setScrollTop(newPosition);

    // Pause auto-scroll if user scrolls up
    if (newPosition < maxScroll) {
      setAutoScrollPaused(true);
    }
  }, [maxScroll]);

  const scrollBy = useCallback((delta: number) => {
    scrollTo(scrollTop + delta);
  }, [scrollTop, scrollTo]);

  const scrollToTop = useCallback(() => {
    scrollTo(0);
  }, [scrollTo]);

  const scrollToBottom = useCallback(() => {
    scrollTo(maxScroll);
    setAutoScrollPaused(false);
  }, [scrollTo, maxScroll]);

  const pageUp = useCallback(() => {
    scrollBy(-(viewportHeight - 2));
  }, [scrollBy, viewportHeight]);

  const pageDown = useCallback(() => {
    scrollBy(viewportHeight - 2);
  }, [scrollBy, viewportHeight]);

  const resumeAutoScroll = useCallback(() => {
    setAutoScrollPaused(false);
    scrollToBottom();
  }, [scrollToBottom]);

  const state: ScrollState = {
    scrollTop,
    maxScroll,
    viewportHeight,
    contentHeight: options.contentHeight,
    isAtBottom: scrollTop >= maxScroll - 1,
    isAtTop: scrollTop <= 0,
  };

  return {
    state,
    scrollTo,
    scrollBy,
    scrollToTop,
    scrollToBottom,
    pageUp,
    pageDown,
    autoScrollPaused,
    resumeAutoScroll,
  };
};

export default useScrollable;

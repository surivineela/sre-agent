/*
 * Copyright (c) Microsoft Corporation. All rights reserved.
 *
 * Color utilities for tab group management.
 */

/**
 * Generates a consistent color for a thread's tab group based on thread ID.
 * The same thread ID will always produce the same color.
 *
 * @param threadId - The thread/conversation ID
 * @returns A Chrome tab group color
 */
export function getGroupColor(threadId: string): chrome.tabGroups.ColorEnum {
  const colors: chrome.tabGroups.ColorEnum[] = [
    'blue', 'cyan', 'green', 'yellow', 'orange', 'pink', 'purple', 'red', 'grey'
  ];

  // Simple hash function for consistent color assignment
  let hash = 0;
  const maxChars = Math.min(threadId.length, 10);
  for (let i = 0; i < maxChars; i++) {
    hash = ((hash << 5) - hash) + threadId.charCodeAt(i);
    hash = hash | 0;  // Convert to 32-bit integer
  }

  return colors[Math.abs(hash) % colors.length];
}

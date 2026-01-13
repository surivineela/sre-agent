/*
Copyright (c) Microsoft Corporation. All rights reserved.
*/

import { defineConfig } from '@playwright/test';

import type { TestOptions } from '../tests/fixtures';

export default defineConfig<TestOptions>({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'list',
  projects: [
    { name: 'chromium', use: { mcpBrowser: 'chromium' } },
  ],
});

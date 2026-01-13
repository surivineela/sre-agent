# SRE Agent Browser Extension

## What is this?

The SRE Agent Browser Extension enables SRE Agent to control your browser for web automation tasks. When you ask the agent to navigate websites or interact with web pages, this extension provides the bridge between the agent and your browser.

Key capabilities:
- Navigate to URLs
- Interact with web page elements
- Use your existing browser session (cookies, logins, etc.)

## Prerequisites

- Node.js 18 or later
- Chrome, Edge, or Chromium-based browser

## Building the Extension

```bash
# Navigate to the extension directory
cd src/Agent/Agent.Extension

# Install dependencies
npm install

# Build the extension
npm run build
```

This creates a `dist/` folder containing the built extension.

## Installing the Extension

1. Open Chrome and go to `chrome://extensions/`
2. Enable **Developer mode** (toggle in the top right corner)
3. Click **Load unpacked**
4. Select the `dist` folder inside `src/Agent/Agent.Extension/`
5. The extension should now appear in your extensions list

## How it Works

1. You ask SRE Agent to perform a web task (e.g., "Navigate to example.com")
2. The agent calls the `BrowserConnect` tool
3. A connection approval card appears in the chat UI
4. Click **Allow** to grant browser access
5. A new tab opens where you select which browser tab to control
6. The agent can now navigate and interact with your browser

## Extension ID

The extension uses a fixed key in `manifest.json` to ensure a consistent extension ID across installations:

```
jakfalbnbhgkpmoaakfflhflbfpkailf
```

This ID is used by the SRE Agent frontend to communicate with the extension.

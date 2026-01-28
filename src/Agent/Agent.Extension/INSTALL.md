# Installing the SRE Agent Browser Extension

## Quick Install from Release

### Step 1: Download the Extension

1. Go to [SRE Agent Releases](https://github.com/serverless-paas-balam/sreagent-runtime/releases)
2. Find the latest release
3. Download the `sre-agent-extension-*.zip` file

### Step 2: Extract the ZIP

1. Extract the downloaded ZIP file to a folder on your computer
2. You should see the extension files including `manifest.json` in the extracted folder

### Step 3: Install in Chrome/Edge

#### For Google Chrome:

1. Open Chrome and navigate to `chrome://extensions/`
2. Enable **Developer mode** (toggle in the top-right corner)
3. Click **Load unpacked**
4. Select the extracted folder (the one containing `manifest.json`)
5. The extension should now appear in your extensions list

#### For Microsoft Edge:

1. Open Edge and navigate to `edge://extensions/`
2. Enable **Developer mode** (toggle in the left sidebar)
3. Click **Load unpacked**
4. Select the extracted folder (the one containing `manifest.json`)
5. The extension should now appear in your extensions list

### Step 4: Verify Installation

1. You should see "Azure SRE Agent" in your browser's extension list
2. The extension icon may appear in your browser toolbar

## Troubleshooting

### Extension not detected by SRE Agent

- Make sure Developer mode is enabled
- Try removing and re-adding the extension
- Refresh the SRE Agent page

### "Manifest file is missing or unreadable"

- Make sure you selected the folder containing `manifest.json`
- Ensure the ZIP was fully extracted

## Building from Source

If you prefer to build from source instead of using a pre-built release, see [README.md](./README.md) for build instructions.

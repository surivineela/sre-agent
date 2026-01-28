/*
Copyright (c) Microsoft Corporation. All rights reserved.
*/

import { debugLog } from './utils/debug';
import { parseExtensionEndpoint } from './utils/endpoint';
import { getGroupColor } from './utils/colors';
import { withTimeout } from './utils/timeout';
import { SignalRRelayConnection } from './signalRRelayConnection';

type PageMessage = {
  type: 'connectToMCPRelay';
  mcpRelayUrl: string;
} | {
  type: 'getTabs';
} | {
  type: 'connectToTab';
  tabId?: number;
  windowId?: number;
  mcpRelayUrl: string;
} | {
  type: 'getConnectionStatus';
} | {
  type: 'disconnect';
};

// Message type for external connections from SRE Agent web UI
type BrowserConnectMessage = {
  type: 'sreagent-connect-browser';
  sessionId: string;
  threadId?: string;           // Thread ID for tab grouping and persistence
  threadTitle?: string;        // Thread title to use for tab group name
  extensionEndpoint: string;   // WebSocket URL: ws://127.0.0.1:5073/browser/extension/{sessionId}
  autoConnect?: boolean;       // If true, create a new tab and auto-connect (no user selection)
  targetUrl?: string;          // Optional URL to navigate the new tab to
  accessToken?: string;        // JWT token for YARP authentication (production mode)
};

type ExternalMessage = BrowserConnectMessage | {
  type: 'sreagent-ping';
} | {
  type: 'sreagent-get-status';
};

// Stored mapping of threadId -> tabId for session persistence
interface ThreadTabMapping {
  threadId: string;
  tabId: number;
  groupId?: number;
  createdAt: number;
}

// Helper to validate loopback URLs
function isLoopbackUrl(url: string): boolean {
  try {
    const parsed = new URL(url);
    return parsed.hostname === '127.0.0.1' ||
      parsed.hostname === 'localhost' ||
      parsed.hostname === '[::1]';
  } catch {
    return false;
  }
}

// Pending connection config for manual tab selection flow
interface PendingConnectionConfig {
  hubUrl: string;
  sessionId: string;
  accessToken?: string;
  timerId?: number;
}

class TabShareExtension {
  private _activeConnection: SignalRRelayConnection | undefined;
  private _activeSessionId: string | null = null;  // Session ID for SignalR reconnections
  private _connectedTabId: number | null = null;
  private _currentThreadId: string | null = null;
  private _pendingTabSelection = new Map<number, PendingConnectionConfig>();
  private _threadTabMappings: Map<string, ThreadTabMapping> = new Map();

  constructor() {
    chrome.tabs.onRemoved.addListener(this._onTabRemoved.bind(this));
    chrome.tabs.onUpdated.addListener(this._onTabUpdated.bind(this));
    chrome.tabs.onActivated.addListener(this._onTabActivated.bind(this));
    chrome.runtime.onMessage.addListener(this._onMessage.bind(this));
    chrome.runtime.onMessageExternal.addListener(this._onExternalMessage.bind(this));
    chrome.action.onClicked.addListener(this._onActionClicked.bind(this));

    // Load persisted thread-tab mappings on startup
    this._loadThreadTabMappings();
  }

  // Load thread-tab mappings from chrome.storage.local
  private async _loadThreadTabMappings(): Promise<void> {
    try {
      const result = await chrome.storage.local.get('threadTabMappings');
      if (result.threadTabMappings) {
        const mappings: ThreadTabMapping[] = result.threadTabMappings;
        this._threadTabMappings = new Map(mappings.map(m => [m.threadId, m]));
        debugLog(`Loaded ${this._threadTabMappings.size} thread-tab mappings`);

        // Clean up mappings for tabs that no longer exist
        await this._cleanupStaleMappings();
      }
    } catch (error) {
      debugLog('Failed to load thread-tab mappings:', error);
    }
  }

  // Save thread-tab mappings to chrome.storage.local
  private async _saveThreadTabMappings(): Promise<void> {
    try {
      const mappings = Array.from(this._threadTabMappings.values());
      await chrome.storage.local.set({ threadTabMappings: mappings });
      debugLog(`Saved ${mappings.length} thread-tab mappings`);
    } catch (error) {
      debugLog('Failed to save thread-tab mappings:', error);
    }
  }

  // Remove mappings for tabs that no longer exist
  private async _cleanupStaleMappings(): Promise<void> {
    const existingTabs = await chrome.tabs.query({});
    const existingTabIds = new Set(existingTabs.map(t => t.id));

    let cleaned = 0;
    for (const [threadId, mapping] of this._threadTabMappings) {
      if (!existingTabIds.has(mapping.tabId)) {
        this._threadTabMappings.delete(threadId);
        cleaned++;
      }
    }

    if (cleaned > 0) {
      debugLog(`Cleaned up ${cleaned} stale thread-tab mappings`);
      await this._saveThreadTabMappings();
    }
  }

  /**
   * Creates a SignalR connection and attaches to a tab.
   * This consolidates the repeated connection setup pattern.
   */
  private async _createAndConnectToTab(
    config: { hubUrl: string; sessionId: string; accessToken?: string },
    tabId: number
  ): Promise<SignalRRelayConnection> {
    const connection = new SignalRRelayConnection(config);

    connection.onclose = () => {
      debugLog('SignalR connection closed');
      this._activeConnection = undefined;
      this._activeSessionId = null;
      void this._setConnectedTabId(null);
    };

    await connection.connect();
    await connection.attachToTab(tabId);

    return connection;
  }

  /**
   * Clears the active connection state without closing the connection.
   */
  private async _clearConnectionState(): Promise<void> {
    this._activeConnection = undefined;
    this._activeSessionId = null;
    this._currentThreadId = null;
    await this._setConnectedTabId(null);
  }

  /**
   * Safely closes the active connection with error handling.
   */
  private async _safelyCloseConnection(reason: string): Promise<void> {
    if (!this._activeConnection) return;
    try {
      await this._activeConnection.close(reason);
    } catch (error) {
      debugLog('Failed to close connection gracefully:', error);
    }
    await this._clearConnectionState();
  }

  // Promise-based message handling is not supported in Chrome: https://issues.chromium.org/issues/40753031
  private _onMessage(message: PageMessage, sender: chrome.runtime.MessageSender, sendResponse: (response: any) => void) {
    switch (message.type) {
      case 'connectToMCPRelay':
        this._connectToRelay(sender.tab!.id!, message.mcpRelayUrl).then(
          () => sendResponse({ success: true }),
          (error: any) => sendResponse({ success: false, error: error.message }));
        return true;
      case 'getTabs':
        this._getTabs().then(
          tabs => sendResponse({ success: true, tabs, currentTabId: sender.tab?.id }),
          (error: any) => sendResponse({ success: false, error: error.message }));
        return true;
      case 'connectToTab':
        const tabId = message.tabId || sender.tab?.id!;
        const windowId = message.windowId || sender.tab?.windowId!;
        this._connectTab(sender.tab!.id!, tabId, windowId, message.mcpRelayUrl!).then(
          () => sendResponse({ success: true }),
          (error: any) => sendResponse({ success: false, error: error.message }));
        return true; // Return true to indicate that the response will be sent asynchronously
      case 'getConnectionStatus':
        sendResponse({
          connectedTabId: this._connectedTabId
        });
        return false;
      case 'disconnect':
        this._disconnect().then(
          () => sendResponse({ success: true }),
          (error: any) => sendResponse({ success: false, error: error.message }));
        return true;
    }
    return false;
  }

  // Handle external messages from SRE Agent web UI
  private _onExternalMessage(
    message: ExternalMessage,
    sender: chrome.runtime.MessageSender,
    sendResponse: (response: any) => void
  ): boolean {
    // Handle ping - simple extension detection (no security check needed)
    if (message.type === 'sreagent-ping') {
      debugLog('Received ping from SRE Agent');
      sendResponse({
        installed: true,
        version: chrome.runtime.getManifest().version,
        name: 'SRE Agent Browser Extension'
      });
      return false;
    }

    // Handle status check - return current connection state
    if (message.type === 'sreagent-get-status') {
      debugLog('Received status request from SRE Agent');
      sendResponse({
        installed: true,
        version: chrome.runtime.getManifest().version,
        connectedTabId: this._connectedTabId,
        isConnected: this._activeConnection !== undefined
      });
      return false;
    }

    if (message.type !== 'sreagent-connect-browser') {
      sendResponse({ success: false, error: 'Unknown message type' });
      return false;
    }

    // Determine mode based on presence of accessToken
    // - Production mode (with token): allows non-localhost origins and endpoints
    // - Development mode (no token): requires localhost for security
    const isProductionMode = !!message.accessToken;

    // Validate sender origin
    // - In dev mode: require localhost
    // - In prod mode: sender will be the Azure portal domain (allowed by manifest)
    if (!isProductionMode && (!sender.url || !isLoopbackUrl(sender.url))) {
      debugLog(`External connection rejected: sender URL ${sender.url} is not localhost`);
      sendResponse({ success: false, error: 'Connection only allowed from localhost' });
      return false;
    }

    // Validate extension endpoint
    // - In dev mode: require localhost (ws://127.0.0.1:...)
    // - In prod mode: allow wss:// to YARP proxy
    if (!isProductionMode && !isLoopbackUrl(message.extensionEndpoint)) {
      debugLog(`External connection rejected: endpoint ${message.extensionEndpoint} is not localhost`);
      sendResponse({ success: false, error: 'Extension endpoint must be localhost' });
      return false;
    }

    debugLog(`External connection request from SRE Agent: sessionId=${message.sessionId}, threadId=${message.threadId}, threadTitle=${message.threadTitle}, mode=${isProductionMode ? 'production' : 'development'}`);

    // Open the connect.html page with the relay URL
    // This allows the user to select which tab to share
    this._handleExternalConnect(message).then(
      () => sendResponse({ success: true }),
      (error: any) => sendResponse({ success: false, error: error.message })
    );

    return true; // Keep channel open for async response
  }

  private async _handleExternalConnect(message: BrowserConnectMessage): Promise<void> {
    // Auto-connect mode: create a new tab or reuse existing one for the thread
    if (message.autoConnect) {
      debugLog(`Auto-connect mode: threadId=${message.threadId}, sessionId=${message.sessionId}`);
      await withTimeout(
        this._autoConnectForThread(message.extensionEndpoint, message.threadId, message.targetUrl, message.threadTitle, message.accessToken),
        30000,  // 30 second timeout
        'Auto-connect to browser'
      );
      return;
    }

    // Manual mode: open connect.html for user to select a tab
    const connectUrl = new URL(chrome.runtime.getURL('connect.html'));
    connectUrl.searchParams.set('mcpRelayUrl', message.extensionEndpoint);
    connectUrl.searchParams.set('client', JSON.stringify({ name: 'SREAgent', version: '1.0' }));
    connectUrl.searchParams.set('protocolVersion', '2');  // SignalR protocol

    debugLog(`Opening connect page: ${connectUrl.toString()}`);

    // Open the connect page in a new tab
    await chrome.tabs.create({
      url: connectUrl.toString(),
      active: true
    });
  }

  // Auto-connect for a thread - reuses existing tab if available
  private async _autoConnectForThread(extensionEndpoint: string, threadId?: string, _targetUrl?: string, threadTitle?: string, accessToken?: string): Promise<void> {
    try {
      // Check if we have an existing tab for this thread
      if (threadId) {
        const existingMapping = this._threadTabMappings.get(threadId);
        if (existingMapping) {
          // Verify the tab still exists
          try {
            const tab = await chrome.tabs.get(existingMapping.tabId);
            if (tab) {
              debugLog(`Found existing tab ${existingMapping.tabId} for thread ${threadId}, reconnecting...`);
              await this._reconnectToExistingTab(extensionEndpoint, existingMapping.tabId, threadId, accessToken);
              return;
            }
          } catch {
            // Tab no longer exists, remove mapping
            debugLog(`Tab ${existingMapping.tabId} for thread ${threadId} no longer exists, creating new tab`);
            this._threadTabMappings.delete(threadId);
            await this._saveThreadTabMappings();
          }
        }
      }

      // No existing tab - create a new one
      await this._createNewTabForThread(extensionEndpoint, threadId, threadTitle, accessToken);
    } catch (error: any) {
      debugLog(`Auto-connect failed: ${error.message}`);
      throw error;
    }
  }

  // Reconnect to an existing tab for a thread
  private async _reconnectToExistingTab(extensionEndpoint: string, tabId: number, threadId: string, accessToken?: string): Promise<void> {
    // Close any existing connection to a different tab
    if (this._activeConnection && this._connectedTabId !== tabId) {
      await this._safelyCloseConnection('Switching to different thread');
    }

    // If already connected to this tab, just show the animation
    if (this._connectedTabId === tabId && this._activeConnection) {
      debugLog(`Already connected to tab ${tabId}, showing focus animation`);
      await this._showFocusAnimation(tabId);
      return;
    }

    // Parse endpoint to get hub URL and session ID
    const { hubUrl, sessionId } = parseExtensionEndpoint(extensionEndpoint);
    debugLog(`Reconnecting to tab ${tabId}: hubUrl=${hubUrl}, sessionId=${sessionId}`);

    // Create connection and attach to tab
    const connection = await this._createAndConnectToTab({ hubUrl, sessionId, accessToken }, tabId);

    this._activeConnection = connection;
    this._activeSessionId = sessionId;
    this._currentThreadId = threadId;
    await this._setConnectedTabId(tabId);

    // Show focus animation
    await this._showFocusAnimation(tabId);

    debugLog(`Reconnected to existing tab ${tabId} for thread ${threadId} via SignalR`);
  }

  // Create a new tab for a thread with tab group
  private async _createNewTabForThread(extensionEndpoint: string, threadId?: string, threadTitle?: string, accessToken?: string): Promise<void> {
    // Close any existing connection
    await this._safelyCloseConnection('New auto-connect requested');

    // Create a new tab with about:blank - Playwright will handle navigation
    // active: false keeps the user on the agent page
    const newTab = await chrome.tabs.create({
      url: 'about:blank',
      active: false
    });

    if (!newTab.id) {
      throw new Error('Failed to create new tab');
    }

    debugLog(`Created new tab ${newTab.id} for thread ${threadId}`);

    // Create or add to tab group for this thread
    let groupId: number | undefined;
    if (threadId) {
      try {
        groupId = await this._addTabToGroup(newTab.id, threadId, threadTitle);
      } catch (error) {
        debugLog(`Failed to create tab group: ${error}`);
      }
    }

    // Parse endpoint to get hub URL and session ID
    const { hubUrl, sessionId } = parseExtensionEndpoint(extensionEndpoint);
    debugLog(`Connecting new tab: hubUrl=${hubUrl}, sessionId=${sessionId}`);

    // Create connection and attach to tab
    const connection = await this._createAndConnectToTab({ hubUrl, sessionId, accessToken }, newTab.id);

    this._activeConnection = connection;
    this._activeSessionId = sessionId;
    this._currentThreadId = threadId || null;
    await this._setConnectedTabId(newTab.id);

    // Save the thread-tab mapping
    if (threadId) {
      this._threadTabMappings.set(threadId, {
        threadId,
        tabId: newTab.id,
        groupId,
        createdAt: Date.now()
      });
      await this._saveThreadTabMappings();
    }

    // Show focus animation
    await this._showFocusAnimation(newTab.id);

    debugLog(`Auto-connected to new tab ${newTab.id} for thread ${threadId} via SignalR`);
  }

  // Add a tab to a group for a thread (creates group if needed)
  private async _addTabToGroup(tabId: number, threadId: string, threadTitle?: string): Promise<number> {
    debugLog(`_addTabToGroup called: tabId=${tabId}, threadId=${threadId}, threadTitle=${threadTitle}`);

    // Check if we already have a group for this thread
    const existingMapping = this._threadTabMappings.get(threadId);
    if (existingMapping?.groupId) {
      try {
        // Try to add to existing group
        await chrome.tabs.group({ tabIds: tabId, groupId: existingMapping.groupId });
        debugLog(`Added tab ${tabId} to existing group ${existingMapping.groupId}`);
        return existingMapping.groupId;
      } catch {
        // Group may have been deleted, create new one
        debugLog(`Existing group ${existingMapping.groupId} not found, creating new one`);
      }
    }

    // Create a new group
    const groupId = await chrome.tabs.group({ tabIds: tabId });

    // Use thread title if provided, otherwise fall back to short ID
    const groupTitle = threadTitle || (threadId.length > 4 ? threadId.slice(-4) : threadId);

    debugLog(`Setting group ${groupId} title to: "${groupTitle}" (threadTitle was: ${threadTitle})`);

    await chrome.tabGroups.update(groupId, {
      title: groupTitle,
      color: 'blue',  // Azure branding
      collapsed: false
    });

    debugLog(`Created tab group ${groupId} for thread ${threadId} with title: ${groupTitle}`);
    return groupId;
  }

  // Show a blue pulse animation to indicate agent is taking control
  private async _showFocusAnimation(tabId: number): Promise<void> {
    try {
      // Inject CSS and JS to show a blue pulse border animation
      await chrome.scripting.executeScript({
        target: { tabId },
        func: () => {
          // Remove any existing animation
          const existing = document.getElementById('sre-agent-focus-overlay');
          if (existing) existing.remove();

          // Create overlay element
          const overlay = document.createElement('div');
          overlay.id = 'sre-agent-focus-overlay';
          overlay.innerHTML = `
            <style>
              @keyframes sre-pulse {
                0% { box-shadow: inset 0 0 0 4px rgba(0, 120, 212, 0.8); }
                50% { box-shadow: inset 0 0 0 8px rgba(0, 120, 212, 0.4); }
                100% { box-shadow: inset 0 0 0 4px rgba(0, 120, 212, 0); }
              }
              #sre-agent-focus-overlay {
                position: fixed;
                top: 0;
                left: 0;
                right: 0;
                bottom: 0;
                pointer-events: none;
                z-index: 2147483647;
                animation: sre-pulse 1.5s ease-out forwards;
              }
            </style>
          `;
          document.body.appendChild(overlay);

          // Remove after animation completes
          setTimeout(() => overlay.remove(), 1500);
        }
      });
      debugLog(`Showed focus animation on tab ${tabId}`);
    } catch (error) {
      // Ignore errors (e.g., can't inject into chrome:// pages)
      debugLog(`Failed to show focus animation: ${error}`);
    }
  }

  private async _connectToRelay(selectorTabId: number, mcpRelayUrl: string): Promise<void> {
    try {
      debugLog(`Preparing relay connection at ${mcpRelayUrl}`);

      // Parse the endpoint to get hub URL and session ID
      const { hubUrl, sessionId } = parseExtensionEndpoint(mcpRelayUrl);

      // Store the config for when user selects a tab
      this._pendingTabSelection.set(selectorTabId, {
        hubUrl,
        sessionId,
        accessToken: undefined  // Manual flow doesn't use access token
      });

      debugLog(`Prepared SignalR connection config: hubUrl=${hubUrl}, sessionId=${sessionId}`);
    } catch (error: any) {
      const message = `Failed to prepare relay connection: ${error.message}`;
      debugLog(message);
      throw new Error(message);
    }
  }

  private async _connectTab(selectorTabId: number, tabId: number, windowId: number, _mcpRelayUrl: string): Promise<void> {
    try {
      debugLog(`Connecting tab ${tabId} via SignalR`);

      // Close any existing connection
      await this._safelyCloseConnection('Another connection is requested');

      // Get the pending connection config
      const pendingConfig = this._pendingTabSelection.get(selectorTabId);
      if (!pendingConfig) {
        throw new Error('No pending connection config found');
      }
      this._pendingTabSelection.delete(selectorTabId);

      // Create connection and attach to tab
      const connection = await this._createAndConnectToTab(
        {
          hubUrl: pendingConfig.hubUrl,
          sessionId: pendingConfig.sessionId,
          accessToken: pendingConfig.accessToken
        },
        tabId
      );

      this._activeConnection = connection;
      this._activeSessionId = pendingConfig.sessionId;

      await Promise.all([
        this._setConnectedTabId(tabId),
        chrome.tabs.update(tabId, { active: true }),
        chrome.windows.update(windowId, { focused: true }),
      ]);

      debugLog(`Connected to tab ${tabId} via SignalR`);
    } catch (error: any) {
      await this._setConnectedTabId(null);
      debugLog(`Failed to connect tab ${tabId}:`, error.message);
      throw error;
    }
  }

  private async _setConnectedTabId(tabId: number | null): Promise<void> {
    const oldTabId = this._connectedTabId;
    this._connectedTabId = tabId;
    if (oldTabId && oldTabId !== tabId)
      await this._updateBadge(oldTabId, { text: '' });
    if (tabId)
      await this._updateBadge(tabId, { text: '✓', color: '#4CAF50', title: 'Connected to MCP client' });
  }

  private async _updateBadge(tabId: number, { text, color, title }: { text: string; color?: string, title?: string }): Promise<void> {
    try {
      await chrome.action.setBadgeText({ tabId, text });
      await chrome.action.setTitle({ tabId, title: title || '' });
      if (color)
        await chrome.action.setBadgeBackgroundColor({ tabId, color });
    } catch (error: any) {
      // Ignore errors as the tab may be closed already.
    }
  }

  private async _onTabRemoved(tabId: number): Promise<void> {
    // Clean up pending connection config if this was a selector tab
    if (this._pendingTabSelection.has(tabId)) {
      const pending = this._pendingTabSelection.get(tabId);
      if (pending?.timerId) {
        clearTimeout(pending.timerId);
      }
      this._pendingTabSelection.delete(tabId);
      debugLog(`Removed pending connection config for closed selector tab ${tabId}`);
      return;
    }

    // Clean up thread-tab mapping for this tab
    for (const [threadId, mapping] of this._threadTabMappings) {
      if (mapping.tabId === tabId) {
        debugLog(`Tab ${tabId} for thread ${threadId} was closed, removing mapping`);
        this._threadTabMappings.delete(threadId);
        await this._saveThreadTabMappings();
        break;
      }
    }

    if (this._connectedTabId !== tabId)
      return;
    await this._activeConnection?.close('Browser tab closed');
    this._activeConnection = undefined;
    this._activeSessionId = null;
    this._connectedTabId = null;
    this._currentThreadId = null;
  }

  private _onTabActivated(activeInfo: chrome.tabs.TabActiveInfo) {
    for (const [tabId, pending] of this._pendingTabSelection) {
      if (tabId === activeInfo.tabId) {
        // User returned to the selector tab, clear timeout
        if (pending.timerId) {
          clearTimeout(pending.timerId);
          pending.timerId = undefined;
        }
        continue;
      }
      // User navigated away from selector tab, start timeout
      if (!pending.timerId) {
        pending.timerId = setTimeout(() => {
          const existed = this._pendingTabSelection.delete(tabId);
          if (existed) {
            // With SignalR, we just remove the pending config (no active connection to close)
            debugLog(`Pending connection for tab ${tabId} timed out after 5s of inactivity`);
            chrome.tabs.sendMessage(tabId, { type: 'connectionTimeout' }).catch(() => { });
          }
        }, 5000) as unknown as number;
        return;
      }
    }
  }

  private _onTabUpdated(tabId: number, changeInfo: chrome.tabs.TabChangeInfo, tab: chrome.tabs.Tab) {
    if (this._connectedTabId === tabId)
      void this._setConnectedTabId(tabId);
  }

  private async _getTabs(): Promise<chrome.tabs.Tab[]> {
    const tabs = await chrome.tabs.query({});
    return tabs.filter(tab => tab.url && !['chrome:', 'edge:', 'devtools:'].some(scheme => tab.url!.startsWith(scheme)));
  }

  private async _onActionClicked(): Promise<void> {
    await chrome.tabs.create({
      url: chrome.runtime.getURL('status.html'),
      active: true
    });
  }

  private async _disconnect(): Promise<void> {
    this._activeConnection?.close('User disconnected');
    this._activeConnection = undefined;
    await this._setConnectedTabId(null);
  }
}

new TabShareExtension();

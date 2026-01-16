/*
Copyright (c) Microsoft Corporation. All rights reserved.
*/

import { RelayConnection, debugLog } from './relayConnection';

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

// Generate a color for tab group based on thread ID (consistent color per thread)
function getGroupColor(threadId: string): chrome.tabGroups.ColorEnum {
  const colors: chrome.tabGroups.ColorEnum[] = ['blue', 'cyan', 'green', 'yellow', 'orange', 'pink', 'purple', 'red', 'grey'];
  let hash = 0;
  for (let i = 0; i < threadId.length; i++) {
    hash = ((hash << 5) - hash) + threadId.charCodeAt(i);
    hash = hash & hash;
  }
  return colors[Math.abs(hash) % colors.length];
}

class TabShareExtension {
  private _activeConnection: RelayConnection | undefined;
  private _connectedTabId: number | null = null;
  private _currentThreadId: string | null = null;
  private _pendingTabSelection = new Map<number, { connection: RelayConnection, timerId?: number }>();
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
      await this._autoConnectForThread(message.extensionEndpoint, message.threadId, message.targetUrl, message.threadTitle, message.accessToken);
      return;
    }

    // Manual mode: open connect.html for user to select a tab
    const connectUrl = new URL(chrome.runtime.getURL('connect.html'));
    connectUrl.searchParams.set('mcpRelayUrl', message.extensionEndpoint);
    connectUrl.searchParams.set('client', JSON.stringify({ name: 'SREAgent', version: '1.0' }));
    connectUrl.searchParams.set('protocolVersion', '1');

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
      this._activeConnection.close('Switching to different thread');
      this._activeConnection = undefined;
      await this._setConnectedTabId(null);
    }

    // If already connected to this tab, just show the animation
    if (this._connectedTabId === tabId && this._activeConnection) {
      debugLog(`Already connected to tab ${tabId}, showing focus animation`);
      await this._showFocusAnimation(tabId);
      return;
    }

    // Build WebSocket URL, appending access token if provided (production mode)
    let wsUrl = extensionEndpoint;
    if (accessToken) {
      const separator = wsUrl.includes('?') ? '&' : '?';
      wsUrl = `${wsUrl}${separator}access_token=${encodeURIComponent(accessToken)}`;
    }

    // Connect to the relay
    const socket = new WebSocket(wsUrl);
    await new Promise<void>((resolve, reject) => {
      socket.onopen = () => resolve();
      socket.onerror = () => reject(new Error('WebSocket error'));
      setTimeout(() => reject(new Error('Connection timeout')), 10000);
    });

    const connection = new RelayConnection(socket);
    connection.setTabId(tabId);
    connection.onclose = () => {
      debugLog('MCP connection closed (reconnect)');
      this._activeConnection = undefined;
      void this._setConnectedTabId(null);
    };

    this._activeConnection = connection;
    this._currentThreadId = threadId;
    await this._setConnectedTabId(tabId);

    // Show focus animation
    await this._showFocusAnimation(tabId);

    debugLog(`Reconnected to existing tab ${tabId} for thread ${threadId}`);
  }

  // Create a new tab for a thread with tab group
  private async _createNewTabForThread(extensionEndpoint: string, threadId?: string, threadTitle?: string, accessToken?: string): Promise<void> {
    // Close any existing connection
    if (this._activeConnection) {
      this._activeConnection.close('New auto-connect requested');
      this._activeConnection = undefined;
      await this._setConnectedTabId(null);
    }

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

    // Build WebSocket URL, appending access token if provided (production mode)
    let wsUrl = extensionEndpoint;
    if (accessToken) {
      const separator = wsUrl.includes('?') ? '&' : '?';
      wsUrl = `${wsUrl}${separator}access_token=${encodeURIComponent(accessToken)}`;
    }

    // Connect to the relay
    const socket = new WebSocket(wsUrl);
    await new Promise<void>((resolve, reject) => {
      socket.onopen = () => resolve();
      socket.onerror = () => reject(new Error('WebSocket error'));
      setTimeout(() => reject(new Error('Connection timeout')), 10000);
    });

    const connection = new RelayConnection(socket);
    connection.setTabId(newTab.id);
    connection.onclose = () => {
      debugLog('MCP connection closed (new tab)');
      this._activeConnection = undefined;
      void this._setConnectedTabId(null);
    };

    this._activeConnection = connection;
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

    debugLog(`Auto-connected to new tab ${newTab.id} for thread ${threadId}`);
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
      debugLog(`Connecting to relay at ${mcpRelayUrl}`);
      const socket = new WebSocket(mcpRelayUrl);
      await new Promise<void>((resolve, reject) => {
        socket.onopen = () => resolve();
        socket.onerror = () => reject(new Error('WebSocket error'));
        setTimeout(() => reject(new Error('Connection timeout')), 5000);
      });

      const connection = new RelayConnection(socket);
      connection.onclose = () => {
        debugLog('Connection closed');
        this._pendingTabSelection.delete(selectorTabId);
        // TODO: show error in the selector tab?
      };
      this._pendingTabSelection.set(selectorTabId, { connection });
      debugLog(`Connected to MCP relay`);
    } catch (error: any) {
      const message = `Failed to connect to MCP relay: ${error.message}`;
      debugLog(message);
      throw new Error(message);
    }
  }

  private async _connectTab(selectorTabId: number, tabId: number, windowId: number, mcpRelayUrl: string): Promise<void> {
    try {
      debugLog(`Connecting tab ${tabId} to relay at ${mcpRelayUrl}`);
      try {
        this._activeConnection?.close('Another connection is requested');
      } catch (error: any) {
        debugLog(`Error closing active connection:`, error);
      }
      await this._setConnectedTabId(null);

      this._activeConnection = this._pendingTabSelection.get(selectorTabId)?.connection;
      if (!this._activeConnection)
        throw new Error('No active MCP relay connection');
      this._pendingTabSelection.delete(selectorTabId);

      this._activeConnection.setTabId(tabId);
      this._activeConnection.onclose = () => {
        debugLog('MCP connection closed');
        this._activeConnection = undefined;
        void this._setConnectedTabId(null);
      };

      await Promise.all([
        this._setConnectedTabId(tabId),
        chrome.tabs.update(tabId, { active: true }),
        chrome.windows.update(windowId, { focused: true }),
      ]);
      debugLog(`Connected to MCP bridge`);
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
    const pendingConnection = this._pendingTabSelection.get(tabId)?.connection;
    if (pendingConnection) {
      this._pendingTabSelection.delete(tabId);
      pendingConnection.close('Browser tab closed');
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
    this._activeConnection?.close('Browser tab closed');
    this._activeConnection = undefined;
    this._connectedTabId = null;
    this._currentThreadId = null;
  }

  private _onTabActivated(activeInfo: chrome.tabs.TabActiveInfo) {
    for (const [tabId, pending] of this._pendingTabSelection) {
      if (tabId === activeInfo.tabId) {
        if (pending.timerId) {
          clearTimeout(pending.timerId);
          pending.timerId = undefined;
        }
        continue;
      }
      if (!pending.timerId) {
        pending.timerId = setTimeout(() => {
          const existed = this._pendingTabSelection.delete(tabId);
          if (existed) {
            pending.connection.close('Tab has been inactive for 5 seconds');
            chrome.tabs.sendMessage(tabId, { type: 'connectionTimeout' });
          }
        }, 5000);
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

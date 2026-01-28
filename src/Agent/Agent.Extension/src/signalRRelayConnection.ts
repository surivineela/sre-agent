/*
 * Copyright (c) Microsoft Corporation. All rights reserved.
 *
 * SignalR-based relay connection for SRE Agent browser extension.
 */

import * as signalR from '@microsoft/signalr';
import { debugLog } from './utils/debug';

/** Result from AttachToSession hub method */
interface AttachToSessionResult {
    success: boolean;
    error?: string;
}

/** Result from AttachToTab hub method */
interface AttachToTabResult {
    success: boolean;
    error?: string;
}

/** Configuration for SignalR relay connection */
export interface SignalRRelayConfig {
    /** SignalR hub URL (e.g., ws://127.0.0.1:5073/browser/extension) */
    hubUrl: string;
    /** Relay session ID */
    sessionId: string;
    /** Optional JWT access token for production mode (YARP auth) */
    accessToken?: string;
}

/** Connection state for external consumers */
export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

/**
 * SignalR-based relay connection that bridges the browser extension with the SRE Agent relay server.
 */
export class SignalRRelayConnection {
    private readonly _config: SignalRRelayConfig;
    private readonly _connection: signalR.HubConnection;
    private _debuggee: chrome.debugger.Debuggee = {};
    private _eventListener?: (source: chrome.debugger.DebuggerSession, method: string, params: unknown) => void;
    private _detachListener?: (source: chrome.debugger.Debuggee, reason: string) => void;
    private _closed = false;
    private _tabAttached = false;

    /** Called when the connection is permanently closed */
    onclose?: () => void;

    /** Called when connection state changes */
    onstatechange?: (state: ConnectionState) => void;

    constructor(config: SignalRRelayConfig) {
        this._config = config;

        // Build SignalR connection with automatic reconnection
        const builder = new signalR.HubConnectionBuilder()
            .withUrl(this._buildHubUrl(), {
                // Use WebSockets transport for best performance in extension context
                transport: signalR.HttpTransportType.WebSockets,
                skipNegotiation: true,
                // Access token factory for YARP authentication (production mode)
                accessTokenFactory: config.accessToken ? () => config.accessToken! : undefined
            })
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: (retryContext) => {
                    // Exponential backoff: 0s, 1s, 2s, 5s, 10s, then 30s max
                    if (retryContext.previousRetryCount === 0) return 0;
                    if (retryContext.previousRetryCount === 1) return 1000;
                    if (retryContext.previousRetryCount === 2) return 2000;
                    if (retryContext.previousRetryCount === 3) return 5000;
                    if (retryContext.previousRetryCount === 4) return 10000;
                    return 30000;
                }
            })
            .configureLogging(signalR.LogLevel.Information);

        this._connection = builder.build();
        this._setupConnectionHandlers();
        this._setupClientMethods();
    }

    /** Current connection state */
    get state(): ConnectionState {
        switch (this._connection.state) {
            case signalR.HubConnectionState.Disconnected:
                return 'disconnected';
            case signalR.HubConnectionState.Connecting:
                return 'connecting';
            case signalR.HubConnectionState.Connected:
                return 'connected';
            case signalR.HubConnectionState.Reconnecting:
                return 'reconnecting';
            default:
                return 'disconnected';
        }
    }

    /** Whether a tab is currently attached */
    get isTabAttached(): boolean {
        return this._tabAttached && this._debuggee.tabId !== undefined;
    }

    /**
     * Establishes the SignalR connection and attaches to the relay session.
     */
    async connect(): Promise<void> {
        if (this._closed) {
            throw new Error('Connection has been closed');
        }

        debugLog('[SignalR] Starting connection to', this._config.hubUrl);
        this.onstatechange?.('connecting');

        try {
            await this._connection.start();
            debugLog('[SignalR] Connected, attaching to session', this._config.sessionId);

            // Attach to the relay session
            const result = await this._connection.invoke<AttachToSessionResult>(
                'AttachToSession',
                this._config.sessionId
            );

            if (!result.success) {
                throw new Error(result.error || 'Failed to attach to session');
            }

            debugLog('[SignalR] Successfully attached to session', this._config.sessionId);
            this.onstatechange?.('connected');
        } catch (error) {
            debugLog('[SignalR] Connection failed:', error);
            this.onstatechange?.('disconnected');
            throw error;
        }
    }

    /**
     * Attaches the chrome.debugger to a specific tab and notifies the server.
     */
    async attachToTab(tabId: number): Promise<chrome.debugger.TargetInfo> {
        if (this._closed) {
            throw new Error('Connection has been closed');
        }

        if (this._connection.state !== signalR.HubConnectionState.Connected) {
            throw new Error('Not connected to relay');
        }

        this._debuggee = { tabId };
        debugLog('[SignalR] Attaching chrome.debugger to tab', tabId);

        // Attach chrome.debugger
        await chrome.debugger.attach(this._debuggee, '1.3');

        // Register event listeners
        this._setupDebuggerListeners();

        // Get target info
        const result = await chrome.debugger.sendCommand(this._debuggee, 'Target.getTargetInfo') as {
            targetInfo: chrome.debugger.TargetInfo;
        };

        debugLog('[SignalR] Got target info:', result?.targetInfo);

        // Notify server that tab is attached
        const attachResult = await this._connection.invoke<AttachToTabResult>(
            'AttachToTab',
            this._config.sessionId,
            result?.targetInfo
        );

        if (!attachResult.success) {
            // Detach debugger if server rejected
            await chrome.debugger.detach(this._debuggee).catch(() => { });
            throw new Error(attachResult.error || 'Server rejected tab attachment');
        }

        this._tabAttached = true;
        debugLog('[SignalR] Tab attached successfully');

        return result?.targetInfo;
    }

    /**
     * Closes the connection and detaches the debugger.
     */
    async close(reason?: string): Promise<void> {
        if (this._closed) return;

        debugLog('[SignalR] Closing connection:', reason || 'User requested');
        this._closed = true;

        // Clean up debugger listeners
        this._cleanupDebuggerListeners();

        // Detach debugger if attached
        if (this._debuggee.tabId) {
            await chrome.debugger.detach(this._debuggee).catch(() => { });
            this._debuggee = {};
            this._tabAttached = false;
        }

        // Stop SignalR connection
        await this._connection.stop();

        this.onstatechange?.('disconnected');
        this.onclose?.();
    }

    private _buildHubUrl(): string {
        // SignalR handles the access token via accessTokenFactory, not query params
        return this._config.hubUrl;
    }

    private _setupConnectionHandlers(): void {
        this._connection.onreconnecting((error) => {
            debugLog('[SignalR] Reconnecting...', error?.message);
            this.onstatechange?.('reconnecting');
        });

        this._connection.onreconnected(async (connectionId) => {
            debugLog('[SignalR] Reconnected with ID:', connectionId);

            try {
                // Re-attach to session after reconnection
                const result = await this._connection.invoke<AttachToSessionResult>(
                    'AttachToSession',
                    this._config.sessionId
                );

                if (!result.success) {
                    debugLog('[SignalR] Failed to re-attach to session:', result.error);
                    await this.close('Failed to re-attach to session');
                    return;
                }

                // If tab was attached, notify server (debugger attachment is still valid)
                if (this._tabAttached && this._debuggee.tabId) {
                    try {
                        const targetInfo = await chrome.debugger.sendCommand(
                            this._debuggee,
                            'Target.getTargetInfo'
                        ) as { targetInfo: chrome.debugger.TargetInfo };

                        await this._connection.invoke<AttachToTabResult>(
                            'AttachToTab',
                            this._config.sessionId,
                            targetInfo?.targetInfo
                        );
                    } catch (error) {
                        debugLog('[SignalR] Failed to re-attach tab info:', error);
                        // Tab may have been closed, mark as not attached
                        this._tabAttached = false;
                    }
                }

                this.onstatechange?.('connected');
            } catch (error) {
                debugLog('[SignalR] Error during reconnection handling:', error);
                await this.close('Reconnection error');
            }
        });

        this._connection.onclose((error) => {
            debugLog('[SignalR] Connection closed:', error?.message);
            if (!this._closed) {
                this._closed = true;
                this._cleanupDebuggerListeners();
                this.onstatechange?.('disconnected');
                this.onclose?.();
            }
        });
    }

    private _setupClientMethods(): void {
        // Server calls this to execute CDP commands
        this._connection.on('ExecuteCDPCommand', async (
            commandId: number,
            method: string,
            params: unknown
        ) => {
            debugLog('[SignalR] Received CDP command:', commandId, method);

            let result: unknown = null;
            let error: string | null = null;

            try {
                if (!this._debuggee.tabId) {
                    throw new Error('No tab attached');
                }

                // Execute CDP command via chrome.debugger
                result = await chrome.debugger.sendCommand(
                    this._debuggee,
                    method,
                    params as object
                );

                debugLog('[SignalR] CDP command result:', commandId, result);
            } catch (err: unknown) {
                error = err instanceof Error ? err.message : String(err);
                debugLog('[SignalR] CDP command error:', commandId, error);
            }

            // Send response back to server
            try {
                await this._connection.invoke(
                    'SendCDPResponse',
                    this._config.sessionId,
                    commandId,
                    result,
                    error
                );
            } catch (sendError) {
                debugLog('[SignalR] Failed to send CDP response:', sendError);
            }
        });

        // Server calls this when session is closed
        this._connection.on('SessionClosed', (reason: string) => {
            debugLog('[SignalR] Session closed by server:', reason);
            void this.close(reason);
        });
    }

    private _setupDebuggerListeners(): void {
        // Forward CDP events to server
        this._eventListener = (
            source: chrome.debugger.DebuggerSession,
            method: string,
            params: unknown
        ) => {
            if (source.tabId !== this._debuggee.tabId) return;

            // Forward to server
            this._connection.invoke(
                'ForwardCDPEvent',
                this._config.sessionId,
                source.sessionId || null,  // CDP session ID (may be null for main target)
                method,
                params
            ).catch(e => debugLog('[SignalR] Failed to forward CDP event:', e));
        };

        // Handle debugger detach
        this._detachListener = (
            source: chrome.debugger.Debuggee,
            reason: string
        ) => {
            if (source.tabId !== this._debuggee.tabId) return;

            debugLog('[SignalR] Debugger detached:', reason);
            this._tabAttached = false;
            this._debuggee = {};
            void this.close(`Debugger detached: ${reason}`);
        };

        chrome.debugger.onEvent.addListener(this._eventListener);
        chrome.debugger.onDetach.addListener(this._detachListener);
    }

    private _cleanupDebuggerListeners(): void {
        if (this._eventListener) {
            chrome.debugger.onEvent.removeListener(this._eventListener);
            this._eventListener = undefined;
        }
        if (this._detachListener) {
            chrome.debugger.onDetach.removeListener(this._detachListener);
            this._detachListener = undefined;
        }
    }
}

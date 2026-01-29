/*
 * Copyright (c) Microsoft Corporation. All rights reserved.
 *
 * Endpoint parsing utilities for SignalR hub connections.
 */

export interface ParsedEndpoint {
  hubUrl: string;
  sessionId: string;
}

/**
 * Parses a WebSocket extension endpoint URL into SignalR hub URL and session ID.
 *
 * The extension endpoint format is: ws(s)://host:port/browser/extension/{sessionId}
 * SignalR needs the hub URL (without sessionId) and sessionId separately.
 *
 * @param extensionEndpoint - WebSocket URL in format: ws(s)://host:port/browser/extension/{sessionId}
 * @returns Object with hubUrl (HTTP/HTTPS) and sessionId
 * @throws Error with descriptive message if format is invalid
 *
 * @example
 * parseExtensionEndpoint('ws://127.0.0.1:5073/browser/extension/abc123')
 * // Returns: { hubUrl: 'http://127.0.0.1:5073/browser/extension', sessionId: 'abc123' }
 *
 * @example
 * parseExtensionEndpoint('wss://agent.example.com/browser/extension/xyz789')
 * // Returns: { hubUrl: 'https://agent.example.com/browser/extension', sessionId: 'xyz789' }
 */
export function parseExtensionEndpoint(extensionEndpoint: string): ParsedEndpoint {
  let parsed: URL;
  try {
    parsed = new URL(extensionEndpoint);
  } catch {
    throw new Error(`Invalid URL format: ${extensionEndpoint}`);
  }

  const pathParts = parsed.pathname.split('/').filter(p => p.length > 0);

  // Expected format: /browser/extension/{sessionId} = at least 3 parts
  if (pathParts.length < 3) {
    throw new Error(
      `Invalid extension endpoint path. Expected format: /browser/extension/{sessionId}, ` +
      `got: ${parsed.pathname}`
    );
  }

  const sessionId = pathParts[pathParts.length - 1];
  if (!sessionId) {
    throw new Error('Session ID cannot be empty');
  }

  // Build hub URL without the sessionId (e.g., /browser/extension)
  const hubPath = '/' + pathParts.slice(0, -1).join('/');

  // Convert ws:// to http:// and wss:// to https://
  const protocol = parsed.protocol === 'wss:' ? 'https:' : 'http:';
  const hubUrl = `${protocol}//${parsed.host}${hubPath}`;

  return { hubUrl, sessionId };
}

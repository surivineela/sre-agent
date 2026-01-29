/**
 * Authentication service for Azure and API key authentication
 */
import type { AuthServiceInterface } from '../types';
import { AuthenticationError } from '../utils/errors';
import { logger } from '../utils/logger';

const SERVICE_NAME = 'sre-cli';
const TOKEN_EXPIRY_BUFFER_MS = 5 * 60 * 1000; // 5 minutes

// SRE Agent API audience - the app registration client ID
const SRE_API_AUDIENCE = '59f0a04a-b322-4310-adc9-39ac41e9631e';

interface TokenCache {
  token: string;
  expiresAt: Date;
}

export class AuthService implements AuthServiceInterface {
  private cachedToken: TokenCache | null = null;
  private azureCredential: unknown = null;

  /**
   * Get an authentication token
   * Tries in order:
   * 1. Cached token (if valid)
   * 2. Azure CLI credential
   * 3. Stored API key
   * 4. Environment variable
   */
  async getToken(): Promise<string> {
    // Check cache first
    if (this.cachedToken && this.isTokenValid(this.cachedToken)) {
      logger.debug('Using cached token');
      return this.cachedToken.token;
    }

    // Try Azure credential
    try {
      const token = await this.getAzureToken();
      if (token) {
        logger.debug('Using Azure CLI token');
        return token;
      }
    } catch (err) {
      logger.debug('Azure credential failed', err);
    }

    // Try stored API key
    const apiKey = await this.getStoredApiKey();
    if (apiKey) {
      logger.debug('Using stored API key');
      return apiKey;
    }

    throw new AuthenticationError(
      'Failed to authenticate. Run "az login" or set SRE_API_KEY environment variable.'
    );
  }

  /**
   * Get token from Azure CLI credential
   */
  private async getAzureToken(): Promise<string | null> {
    try {
      // Dynamically import @azure/identity to avoid startup cost
      const { DefaultAzureCredential } = await import('@azure/identity');

      if (!this.azureCredential) {
        this.azureCredential = new DefaultAzureCredential();
      }

      // Use the SRE Agent API audience for token scope
      const tokenResponse = await (this.azureCredential as InstanceType<typeof DefaultAzureCredential>).getToken(
        `${SRE_API_AUDIENCE}/.default`
      );

      if (tokenResponse) {
        const expiresAt = tokenResponse.expiresOnTimestamp
          ? new Date(tokenResponse.expiresOnTimestamp)
          : new Date(Date.now() + 3600000); // Default 1 hour

        this.cachedToken = {
          token: tokenResponse.token,
          expiresAt,
        };

        return tokenResponse.token;
      }
    } catch (err) {
      // Azure credential not available or failed
      logger.debug('Azure credential error', err);
    }

    return null;
  }

  /**
   * Check if a cached token is still valid
   */
  private isTokenValid(cache: TokenCache): boolean {
    const now = new Date();
    const bufferTime = new Date(cache.expiresAt.getTime() - TOKEN_EXPIRY_BUFFER_MS);
    return now < bufferTime;
  }

  /**
   * Store an API key securely
   */
  async storeApiKey(apiKey: string): Promise<void> {
    try {
      // Try to use keytar for secure storage
      const keytar = await import('keytar');
      await keytar.setPassword(SERVICE_NAME, 'api-key', apiKey);
      logger.info('API key stored securely');
    } catch (err) {
      // Fallback: warn user to use environment variable
      logger.warn('Could not store API key securely. Use SRE_API_KEY environment variable instead.', err);
      throw new AuthenticationError('Secure storage not available. Use SRE_API_KEY environment variable.');
    }
  }

  /**
   * Get stored API key
   */
  async getStoredApiKey(): Promise<string | null> {
    // Check environment variable first
    const envKey = process.env.SRE_API_KEY;
    if (envKey) {
      return envKey;
    }

    // Try keytar
    try {
      const keytar = await import('keytar');
      const storedKey = await keytar.getPassword(SERVICE_NAME, 'api-key');
      return storedKey;
    } catch (err) {
      logger.debug('Could not access keytar', err);
      return null;
    }
  }

  /**
   * Clear all stored credentials
   */
  async clearCredentials(): Promise<void> {
    this.cachedToken = null;
    this.azureCredential = null;

    try {
      const keytar = await import('keytar');
      await keytar.deletePassword(SERVICE_NAME, 'api-key');
      logger.info('Credentials cleared');
    } catch (err) {
      logger.debug('Could not clear keytar credentials', err);
    }
  }

  /**
   * Check if authentication is available
   */
  async isAuthenticated(): Promise<boolean> {
    try {
      await this.getToken();
      return true;
    } catch {
      return false;
    }
  }
}

// Singleton instance
let authServiceInstance: AuthService | null = null;

export function getAuthService(): AuthService {
  if (!authServiceInstance) {
    authServiceInstance = new AuthService();
  }
  return authServiceInstance;
}

/**
 * Services layer exports and initialization
 */
import type { Services } from '../types';
import { getAuthService } from './auth';
import { getConfigService } from './config';
import { createAPIService } from './api';
import { initializeToolRegistry } from '../tools';
import { logger } from '../utils/logger';

export { type AuthService, getAuthService } from './auth';
export { type ConfigService, getConfigService } from './config';
export { type APIService, createAPIService } from './api';

/**
 * Initialize all services
 */
export async function initializeServices(): Promise<Services> {
  logger.debug('Initializing services');

  // Initialize config service first
  const configService = getConfigService();
  const config = await configService.load();

  // Configure logger based on config
  logger.configure({
    enabled: config.debug.enabled,
    logFile: config.debug.logFile,
  });

  // Initialize auth service
  const authService = getAuthService();

  // Initialize API service
  const serverUrl = configService.getServerUrl();
  const apiService = createAPIService(
    {
      baseUrl: serverUrl,
      timeout: config.server.timeout,
    },
    authService
  );

  // Initialize tool registry with built-in tools
  const toolRegistry = initializeToolRegistry();

  logger.debug('Services initialized', {
    serverUrl,
    toolCount: toolRegistry.count,
  });

  return {
    api: apiService,
    auth: authService,
    config: configService,
    tools: toolRegistry,
  };
}

/**
 * Connect to the backend API
 */
export async function connectServices(services: Services): Promise<void> {
  try {
    await services.api.connect();
    logger.info('Connected to backend API');
  } catch (err) {
    logger.warn('Failed to connect to backend API', err);
    // Don't throw - allow offline mode
  }
}

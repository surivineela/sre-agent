/**
 * Configuration service with hierarchical config loading
 */
import { cosmiconfig } from 'cosmiconfig';
import * as fs from 'fs/promises';
import * as path from 'path';
import * as os from 'os';
import type { Config, ConfigServiceInterface } from '../types';
import { ConfigSchema, getDefaultConfig, mergeConfig } from '../config/schema';
import { ConfigError } from '../utils/errors';
import { logger } from '../utils/logger';

const CONFIG_MODULE_NAME = 'sre';

export class ConfigService implements ConfigServiceInterface {
  private config: Config | null = null;
  private configPath: string | null = null;
  private explorer = cosmiconfig(CONFIG_MODULE_NAME, {
    searchPlaces: [
      'package.json',
      '.srerc',
      '.srerc.json',
      '.srerc.yaml',
      '.srerc.yml',
      '.sre/config.json',
      'sre.config.js',
      'sre.config.mjs',
    ],
  });

  /**
   * Load configuration from all sources
   * Priority (highest to lowest):
   * 1. Environment variables
   * 2. Project config (current directory)
   * 3. User config (~/.sre/config.json)
   * 4. Default values
   */
  async load(): Promise<Config> {
    logger.debug('Loading configuration');

    // Start with defaults
    let config = getDefaultConfig();

    // Load user config
    const userConfig = await this.loadUserConfig();
    if (userConfig) {
      config = mergeConfig(config, userConfig);
      logger.debug('Merged user config');
    }

    // Load project config
    const projectConfig = await this.loadProjectConfig();
    if (projectConfig) {
      config = mergeConfig(config, projectConfig.config);
      this.configPath = projectConfig.path;
      logger.debug('Merged project config', { path: projectConfig.path });
    }

    // Apply environment variable overrides
    config = this.applyEnvOverrides(config);

    // Validate final config
    try {
      this.config = ConfigSchema.parse(config);
    } catch (err) {
      throw new ConfigError(`Invalid configuration: ${err}`);
    }

    logger.debug('Configuration loaded', { profile: this.config.currentProfile });
    return this.config;
  }

  /**
   * Get the current configuration
   */
  get(): Config {
    if (!this.config) {
      throw new ConfigError('Configuration not loaded. Call load() first.');
    }
    return this.config;
  }

  /**
   * Save configuration updates
   */
  async save(updates: Partial<Config>): Promise<void> {
    if (!this.config) {
      throw new ConfigError('Configuration not loaded. Call load() first.');
    }

    const targetPath = this.configPath || path.join(process.cwd(), '.sre', 'config.json');

    // Ensure directory exists
    const dir = path.dirname(targetPath);
    await fs.mkdir(dir, { recursive: true });

    // Merge and save
    const merged = mergeConfig(this.config, updates);

    try {
      await fs.writeFile(targetPath, JSON.stringify(merged, null, 2), 'utf-8');
      this.config = ConfigSchema.parse(merged);
      this.configPath = targetPath;
      logger.info('Configuration saved', { path: targetPath });
    } catch (err) {
      throw new ConfigError(`Failed to save configuration: ${err}`);
    }
  }

  /**
   * Get the path to the user config directory
   */
  getUserConfigDir(): string {
    return path.join(os.homedir(), '.sre');
  }

  /**
   * Get the path to the user config file
   */
  getUserConfigPath(): string {
    return path.join(this.getUserConfigDir(), 'config.json');
  }

  /**
   * Load user-level configuration
   */
  private async loadUserConfig(): Promise<Partial<Config> | null> {
    const userConfigPath = this.getUserConfigPath();

    try {
      const content = await fs.readFile(userConfigPath, 'utf-8');
      return JSON.parse(content);
    } catch (err) {
      if ((err as NodeJS.ErrnoException).code !== 'ENOENT') {
        logger.warn('Failed to load user config', err);
      }
      return null;
    }
  }

  /**
   * Load project-level configuration
   */
  private async loadProjectConfig(): Promise<{ config: Partial<Config>; path: string } | null> {
    try {
      const result = await this.explorer.search();
      if (result && !result.isEmpty) {
        return {
          config: result.config as Partial<Config>,
          path: result.filepath,
        };
      }
    } catch (err) {
      logger.warn('Failed to load project config', err);
    }
    return null;
  }

  /**
   * Apply environment variable overrides
   */
  private applyEnvOverrides(config: Config): Config {
    const result = { ...config };

    // Server URL
    if (process.env.SRE_SERVER_URL) {
      result.server = { ...result.server, url: process.env.SRE_SERVER_URL };
    }

    // Debug mode
    if (process.env.SRE_DEBUG !== undefined) {
      result.debug = { ...result.debug, enabled: process.env.SRE_DEBUG === 'true' };
    }

    // Profile
    if (process.env.SRE_PROFILE) {
      result.currentProfile = process.env.SRE_PROFILE;
    }

    // Verbose API logging
    if (process.env.SRE_VERBOSE_API !== undefined) {
      result.debug = { ...result.debug, verboseApi: process.env.SRE_VERBOSE_API === 'true' };
    }

    // Max iterations
    if (process.env.SRE_MAX_ITERATIONS) {
      const maxIterations = parseInt(process.env.SRE_MAX_ITERATIONS, 10);
      if (!isNaN(maxIterations)) {
        result.agent = { ...result.agent, maxIterations };
      }
    }

    return result;
  }

  /**
   * Initialize configuration with a setup wizard
   */
  async initialize(serverUrl: string): Promise<void> {
    const configDir = this.getUserConfigDir();
    const configPath = this.getUserConfigPath();

    // Create directory
    await fs.mkdir(configDir, { recursive: true });

    // Create initial config
    const initialConfig: Partial<Config> = {
      server: {
        url: serverUrl,
        authRequired: true,
        timeout: 30000,
      },
      profiles: {
        default: {
          name: 'default',
          serverUrl,
          authRequired: true,
          isDefault: true,
        },
      },
      currentProfile: 'default',
    };

    await fs.writeFile(configPath, JSON.stringify(initialConfig, null, 2), 'utf-8');
    logger.info('Configuration initialized', { path: configPath });

    // Reload config
    await this.load();
  }

  /**
   * Get the effective server URL (considering profiles)
   */
  getServerUrl(): string {
    const config = this.get();

    if (config.currentProfile && config.profiles[config.currentProfile]) {
      return config.profiles[config.currentProfile].serverUrl;
    }

    return config.server.url;
  }

  /**
   * Switch to a different profile
   */
  async switchProfile(profileName: string): Promise<void> {
    const config = this.get();

    if (!config.profiles[profileName]) {
      throw new ConfigError(`Profile '${profileName}' not found`);
    }

    await this.save({ currentProfile: profileName });
    logger.info('Switched to profile', { profile: profileName });
  }

  /**
   * Add or update a profile
   */
  async setProfile(name: string, serverUrl: string, isDefault = false): Promise<void> {
    const config = this.get();

    // If setting as default, unset other defaults
    const profiles = { ...config.profiles };
    if (isDefault) {
      for (const key of Object.keys(profiles)) {
        profiles[key] = { ...profiles[key], isDefault: false };
      }
    }

    profiles[name] = {
      name,
      serverUrl,
      authRequired: true,
      isDefault,
    };

    await this.save({
      profiles,
      currentProfile: isDefault ? name : config.currentProfile,
    });

    logger.info('Profile saved', { name, serverUrl, isDefault });
  }

  /**
   * Delete a profile
   */
  async deleteProfile(name: string): Promise<void> {
    const config = this.get();

    if (!config.profiles[name]) {
      throw new ConfigError(`Profile '${name}' not found`);
    }

    const profiles = { ...config.profiles };
    delete profiles[name];

    const updates: Partial<Config> = { profiles };

    // If deleting current profile, switch to default or first available
    if (config.currentProfile === name) {
      const defaultProfile = Object.values(profiles).find((p) => p.isDefault);
      const firstProfile = Object.keys(profiles)[0];
      updates.currentProfile = defaultProfile?.name || firstProfile;
    }

    await this.save(updates);
    logger.info('Profile deleted', { name });
  }

  /**
   * Check if a valid configuration exists
   */
  async hasValidConfiguration(): Promise<boolean> {
    try {
      const userConfigPath = this.getUserConfigPath();
      const content = await fs.readFile(userConfigPath, 'utf-8');
      const config = JSON.parse(content);
      return !!(config.server?.url || config.profiles?.default?.serverUrl);
    } catch {
      return false;
    }
  }

  /**
   * Check if URL is localhost (for auth detection)
   */
  static isLocalhost(url: string): boolean {
    const lower = url.toLowerCase();
    return lower.includes('localhost') || lower.includes('127.0.0.1');
  }

  /**
   * Get the SREAgent config directory (different from user config)
   * This is where workspace config is stored: ~/.sreagent/
   */
  getSreAgentConfigDir(): string {
    return path.join(os.homedir(), '.sreagent');
  }

  /**
   * Get SREAgent config file path
   */
  getSreAgentConfigPath(): string {
    return path.join(this.getSreAgentConfigDir(), 'config.json');
  }

  /**
   * Save SREAgent workspace configuration (matches Agent.Cli behavior)
   */
  async saveSreAgentConfig(config: {
    resourceUrl: string;
    authRequired: boolean;
    lastUpdated: Date;
    createdAt: Date;
  }): Promise<void> {
    const configDir = this.getSreAgentConfigDir();
    const configPath = this.getSreAgentConfigPath();

    await fs.mkdir(configDir, { recursive: true });

    const configData = {
      resource_url: config.resourceUrl,
      auth_required: config.authRequired,
      last_updated: config.lastUpdated.toISOString(),
      created_at: config.createdAt.toISOString(),
    };

    await fs.writeFile(configPath, JSON.stringify(configData, null, 2), 'utf-8');
    logger.info('SREAgent config saved', { path: configPath });
  }

  /**
   * Load SREAgent workspace configuration
   */
  async loadSreAgentConfig(): Promise<{
    resourceUrl: string;
    authRequired: boolean;
    lastUpdated: Date;
    createdAt: Date;
  } | null> {
    try {
      const configPath = this.getSreAgentConfigPath();
      const content = await fs.readFile(configPath, 'utf-8');
      const data = JSON.parse(content);
      return {
        resourceUrl: data.resource_url,
        authRequired: data.auth_required,
        lastUpdated: new Date(data.last_updated),
        createdAt: new Date(data.created_at),
      };
    } catch {
      return null;
    }
  }
}

// Singleton instance
let configServiceInstance: ConfigService | null = null;

export function getConfigService(): ConfigService {
  if (!configServiceInstance) {
    configServiceInstance = new ConfigService();
  }
  return configServiceInstance;
}

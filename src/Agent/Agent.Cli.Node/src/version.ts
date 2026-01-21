/**
 * Version information for SRE CLI
 */

export const VERSION = '0.0.1';
export const NAME = 'sre';
export const DESCRIPTION = 'Interactive SRE Agent CLI';

export const BUILD_INFO = {
  version: VERSION,
  name: NAME,
  nodeVersion: process.version,
  platform: process.platform,
  arch: process.arch,
};

export function getVersionString(): string {
  return `${NAME} v${VERSION}`;
}

export function getFullVersionInfo(): string {
  return [
    `${NAME} v${VERSION}`,
    `Node.js ${process.version}`,
    `Platform: ${process.platform} (${process.arch})`,
  ].join('\n');
}

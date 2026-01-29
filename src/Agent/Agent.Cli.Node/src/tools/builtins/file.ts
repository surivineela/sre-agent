/**
 * File system tools
 */
import * as fs from 'fs/promises';
import * as path from 'path';
import { glob } from 'glob';
import type { ToolDefinition, ToolContext } from '../../types';

/**
 * Read file tool
 */
export const readFile: ToolDefinition = {
  name: 'read_file',
  description: 'Read the contents of a file',
  inputSchema: {
    type: 'object',
    properties: {
      path: {
        type: 'string',
        description: 'Path to the file to read',
      },
      encoding: {
        type: 'string',
        enum: ['utf-8', 'base64'],
        description: 'Encoding to use',
      },
    },
    required: ['path'],
  },
  execute: async (input, context: ToolContext) => {
    const { path: filePath, encoding = 'utf-8' } = input as {
      path: string;
      encoding?: 'utf-8' | 'base64';
    };

    const resolvedPath = path.resolve(context.cwd, filePath);
    const content = await fs.readFile(resolvedPath, encoding);

    return {
      content,
      path: resolvedPath,
      encoding,
    };
  },
  requiresPermission: 'session',
  category: 'file_system',
};

/**
 * Write file tool
 */
export const writeFile: ToolDefinition = {
  name: 'write_file',
  description: 'Write content to a file',
  inputSchema: {
    type: 'object',
    properties: {
      path: {
        type: 'string',
        description: 'Path to the file to write',
      },
      content: {
        type: 'string',
        description: 'Content to write to the file',
      },
    },
    required: ['path', 'content'],
  },
  execute: async (input, context: ToolContext) => {
    const { path: filePath, content } = input as {
      path: string;
      content: string;
    };

    const resolvedPath = path.resolve(context.cwd, filePath);

    // Ensure directory exists
    const dir = path.dirname(resolvedPath);
    await fs.mkdir(dir, { recursive: true });

    await fs.writeFile(resolvedPath, content, 'utf-8');

    return {
      success: true,
      path: resolvedPath,
      bytesWritten: Buffer.byteLength(content),
    };
  },
  requiresPermission: 'always',
  category: 'file_system',
};

/**
 * Glob file search tool
 */
export const globFiles: ToolDefinition = {
  name: 'glob',
  description: 'Find files matching a glob pattern',
  inputSchema: {
    type: 'object',
    properties: {
      pattern: {
        type: 'string',
        description: 'Glob pattern to match files (e.g., "**/*.ts")',
      },
      cwd: {
        type: 'string',
        description: 'Working directory for the search',
      },
      ignore: {
        type: 'array',
        items: { type: 'string' },
        description: 'Patterns to ignore',
      },
    },
    required: ['pattern'],
  },
  execute: async (input, context: ToolContext) => {
    const {
      pattern,
      cwd = context.cwd,
      ignore = ['node_modules/**', '.git/**'],
    } = input as {
      pattern: string;
      cwd?: string;
      ignore?: string[];
    };

    const files = await glob(pattern, {
      cwd,
      ignore,
      nodir: true,
    });

    return {
      files,
      count: files.length,
      pattern,
      cwd,
    };
  },
  requiresPermission: 'none',
  category: 'file_system',
};

/**
 * List directory tool
 */
export const listDir: ToolDefinition = {
  name: 'list_dir',
  description: 'List contents of a directory',
  inputSchema: {
    type: 'object',
    properties: {
      path: {
        type: 'string',
        description: 'Directory path to list',
      },
    },
    required: ['path'],
  },
  execute: async (input, context: ToolContext) => {
    const { path: dirPath } = input as { path: string };
    const resolvedPath = path.resolve(context.cwd, dirPath);

    const entries = await fs.readdir(resolvedPath, { withFileTypes: true });

    const items = entries.map((entry) => ({
      name: entry.name,
      type: entry.isDirectory() ? 'directory' : 'file',
    }));

    return {
      path: resolvedPath,
      items,
      count: items.length,
    };
  },
  requiresPermission: 'none',
  category: 'file_system',
};

/**
 * File exists tool
 */
export const fileExists: ToolDefinition = {
  name: 'file_exists',
  description: 'Check if a file or directory exists',
  inputSchema: {
    type: 'object',
    properties: {
      path: {
        type: 'string',
        description: 'Path to check',
      },
    },
    required: ['path'],
  },
  execute: async (input, context: ToolContext) => {
    const { path: filePath } = input as { path: string };
    const resolvedPath = path.resolve(context.cwd, filePath);

    try {
      const stats = await fs.stat(resolvedPath);
      return {
        exists: true,
        isFile: stats.isFile(),
        isDirectory: stats.isDirectory(),
        size: stats.size,
        modifiedAt: stats.mtime.toISOString(),
      };
    } catch {
      return {
        exists: false,
      };
    }
  },
  requiresPermission: 'none',
  category: 'file_system',
};

/**
 * All file tools
 */
export const fileTools: ToolDefinition[] = [
  readFile,
  writeFile,
  globFiles,
  listDir,
  fileExists,
];

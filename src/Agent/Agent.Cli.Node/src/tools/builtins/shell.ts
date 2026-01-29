/**
 * Shell execution tools
 */
import { spawn } from 'child_process';
import type { ToolDefinition, ToolContext } from '../../types';
import { DangerousCommandError, TimeoutError } from '../../utils/errors';

/**
 * Dangerous command patterns to block
 */
const DANGEROUS_PATTERNS = [
  /rm\s+-rf\s+\/(?!\s)/i,
  /rm\s+-rf\s+~(?!\S)/i,
  />\s*\/dev\/sd[a-z]/i,
  /dd\s+if=\/dev\/zero/i,
  /mkfs\./i,
  /:$$:$\|:&$;:/,
  /chmod\s+-[rR]\s+777\s+\//i,
  /curl.*\|\s*(?:sh|bash)/i,
  /wget.*\|\s*(?:sh|bash)/i,
  /\|\s*(?:sh|bash)\s*$/i,
];

/**
 * Check if a command is dangerous
 */
function analyzeCommand(command: string): { dangerous: boolean; reason?: string } {
  for (const pattern of DANGEROUS_PATTERNS) {
    if (pattern.test(command)) {
      return {
        dangerous: true,
        reason: `Command matches dangerous pattern: ${pattern.source}`,
      };
    }
  }
  return { dangerous: false };
}

/**
 * Execute a shell command
 */
async function execCommand(
  command: string,
  options: {
    cwd?: string;
    timeout?: number;
    signal?: AbortSignal;
  } = {}
): Promise<{ stdout: string; stderr: string; exitCode: number }> {
  const { cwd = process.cwd(), timeout = 120000, signal } = options;

  return new Promise((resolve, reject) => {
    const isWindows = process.platform === 'win32';
    const shell = isWindows ? 'cmd.exe' : '/bin/bash';
    const shellArgs = isWindows ? ['/c', command] : ['-c', command];

    const child = spawn(shell, shellArgs, {
      cwd,
      env: process.env,
      stdio: ['pipe', 'pipe', 'pipe'],
    });

    let stdout = '';
    let stderr = '';

    child.stdout?.on('data', (data) => {
      stdout += data.toString();
    });

    child.stderr?.on('data', (data) => {
      stderr += data.toString();
    });

    // Handle timeout
    const timeoutId = setTimeout(() => {
      child.kill();
      reject(new TimeoutError(`Command: ${command.slice(0, 50)}`, timeout));
    }, timeout);

    // Handle abort signal
    if (signal) {
      signal.addEventListener('abort', () => {
        child.kill();
        reject(new Error('Command aborted'));
      });
    }

    child.on('error', (error) => {
      clearTimeout(timeoutId);
      reject(error);
    });

    child.on('close', (code) => {
      clearTimeout(timeoutId);
      resolve({
        stdout: stdout.slice(0, 100000), // Limit output size
        stderr: stderr.slice(0, 100000),
        exitCode: code ?? -1,
      });
    });
  });
}

/**
 * Bash command execution tool
 */
export const bash: ToolDefinition = {
  name: 'bash',
  description: 'Execute a bash/shell command',
  inputSchema: {
    type: 'object',
    properties: {
      command: {
        type: 'string',
        description: 'The command to execute',
      },
      cwd: {
        type: 'string',
        description: 'Working directory for the command',
      },
      timeout: {
        type: 'number',
        description: 'Timeout in milliseconds (default: 120000)',
      },
    },
    required: ['command'],
  },
  execute: async (input, context: ToolContext) => {
    const { command, cwd, timeout = 120000 } = input as {
      command: string;
      cwd?: string;
      timeout?: number;
    };

    // Static analysis for dangerous commands
    const analysis = analyzeCommand(command);
    if (analysis.dangerous) {
      throw new DangerousCommandError(command, analysis.reason!);
    }

    const result = await execCommand(command, {
      cwd: cwd || context.cwd,
      timeout,
      signal: context.abortSignal,
    });

    return {
      stdout: result.stdout,
      stderr: result.stderr,
      exitCode: result.exitCode,
      success: result.exitCode === 0,
    };
  },
  requiresPermission: 'always',
  category: 'shell_execution',
};

/**
 * Get current working directory tool
 */
export const pwd: ToolDefinition = {
  name: 'pwd',
  description: 'Get the current working directory',
  inputSchema: {
    type: 'object',
    properties: {},
  },
  execute: async (_, context: ToolContext) => {
    return {
      cwd: context.cwd,
    };
  },
  requiresPermission: 'none',
  category: 'shell_execution',
};

/**
 * All shell tools
 */
export const shellTools: ToolDefinition[] = [bash, pwd];

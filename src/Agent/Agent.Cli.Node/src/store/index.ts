/**
 * Zustand store for SRE CLI state management
 */
import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import type {
  Config,
  Message,
  Session,
  Permission,
  ConnectionStatus,
  LoopStatus,
} from '../types';
import { getDefaultConfig } from '../config/schema';
import { generateId } from '../utils/formatting';

// ============================================================================
// Store State Types
// ============================================================================

interface AppState {
  // Configuration
  config: Config;
  setConfig: (config: Partial<Config>) => void;

  // Session management
  currentSession: Session | null;
  sessions: Session[];
  createSession: (agentName?: string) => Session;
  switchSession: (sessionId: string) => void;
  clearSession: () => void;
  setAgentName: (agentName?: string) => void;

  // Messages
  addMessage: (message: Omit<Message, 'id' | 'timestamp'>) => void;
  updateMessage: (id: string, update: Partial<Message>) => void;
  deleteMessage: (id: string) => void;

  // UI state
  isProcessing: boolean;
  setProcessing: (processing: boolean) => void;
  loopStatus: LoopStatus;
  setLoopStatus: (status: LoopStatus) => void;

  // Input history
  inputHistory: string[];
  addToHistory: (input: string) => void;
  clearHistory: () => void;

  // Permissions
  grantedPermissions: Record<string, Permission>;
  grantPermission: (permission: Permission) => void;
  revokePermission: (key: string) => void;
  clearPermissions: () => void;
  hasPermission: (toolName: string) => boolean;

  // Connection
  connectionStatus: ConnectionStatus;
  setConnectionStatus: (status: ConnectionStatus) => void;

  // Error state
  lastError: string | null;
  setError: (error: string | null) => void;
}

// ============================================================================
// Custom Storage for Node.js
// ============================================================================

const getStoragePath = (): string => {
  return path.join(os.homedir(), '.sre', 'store.json');
};

const nodeStorage = {
  getItem: (name: string): string | null => {
    try {
      const filePath = getStoragePath();
      if (fs.existsSync(filePath)) {
        const data = fs.readFileSync(filePath, 'utf-8');
        const parsed = JSON.parse(data);
        return JSON.stringify(parsed[name]);
      }
    } catch {
      // Ignore errors
    }
    return null;
  },
  setItem: (name: string, value: string): void => {
    try {
      const filePath = getStoragePath();
      const dir = path.dirname(filePath);

      if (!fs.existsSync(dir)) {
        fs.mkdirSync(dir, { recursive: true });
      }

      let data: Record<string, unknown> = {};
      if (fs.existsSync(filePath)) {
        try {
          data = JSON.parse(fs.readFileSync(filePath, 'utf-8'));
        } catch {
          // Start fresh
        }
      }

      data[name] = JSON.parse(value);
      fs.writeFileSync(filePath, JSON.stringify(data, null, 2));
    } catch {
      // Ignore errors
    }
  },
  removeItem: (name: string): void => {
    try {
      const filePath = getStoragePath();
      if (fs.existsSync(filePath)) {
        const data = JSON.parse(fs.readFileSync(filePath, 'utf-8'));
        delete data[name];
        fs.writeFileSync(filePath, JSON.stringify(data, null, 2));
      }
    } catch {
      // Ignore errors
    }
  },
};

// ============================================================================
// Store Creation
// ============================================================================

export const useStore = create<AppState>()(
  persist(
    (set, get) => ({
      // ========================================================================
      // Configuration
      // ========================================================================
      config: getDefaultConfig(),
      setConfig: (updates) =>
        set((state) => ({
          config: { ...state.config, ...updates },
        })),

      // ========================================================================
      // Session Management
      // ========================================================================
      currentSession: null,
      sessions: [],

      createSession: (agentName) => {
        const session: Session = {
          id: generateId(),
          messages: [],
          agentName,
          startedAt: new Date(),
        };
        set((state) => ({
          currentSession: session,
          sessions: [...state.sessions, session].slice(-10), // Keep last 10
        }));
        return session;
      },

      switchSession: (sessionId) => {
        const session = get().sessions.find((s) => s.id === sessionId);
        if (session) {
          set({ currentSession: session });
        }
      },

      clearSession: () => {
        set((state) => ({
          currentSession: state.currentSession
            ? {
                ...state.currentSession,
                messages: [],
              }
            : null,
        }));
      },

      setAgentName: (agentName?: string) => {
        set((state) => ({
          currentSession: state.currentSession
            ? {
                ...state.currentSession,
                agentName,
              }
            : null,
        }));
      },

      // ========================================================================
      // Messages
      // ========================================================================
      addMessage: (message) => {
        const newMessage: Message = {
          ...message,
          id: generateId(),
          timestamp: new Date(),
        };
        set((state) => ({
          currentSession: state.currentSession
            ? {
                ...state.currentSession,
                messages: [...state.currentSession.messages, newMessage],
              }
            : null,
        }));
      },

      updateMessage: (id, update) => {
        set((state) => ({
          currentSession: state.currentSession
            ? {
                ...state.currentSession,
                messages: state.currentSession.messages.map((m) =>
                  m.id === id ? { ...m, ...update } : m
                ),
              }
            : null,
        }));
      },

      deleteMessage: (id) => {
        set((state) => ({
          currentSession: state.currentSession
            ? {
                ...state.currentSession,
                messages: state.currentSession.messages.filter((m) => m.id !== id),
              }
            : null,
        }));
      },

      // ========================================================================
      // UI State
      // ========================================================================
      isProcessing: false,
      setProcessing: (processing) => set({ isProcessing: processing }),

      loopStatus: 'idle',
      setLoopStatus: (status) => set({ loopStatus: status }),

      // ========================================================================
      // Input History
      // ========================================================================
      inputHistory: [],

      addToHistory: (input) => {
        if (!input.trim()) return;
        set((state) => ({
          inputHistory: [...state.inputHistory.filter((h) => h !== input), input].slice(-100),
        }));
      },

      clearHistory: () => set({ inputHistory: [] }),

      // ========================================================================
      // Permissions
      // ========================================================================
      grantedPermissions: {},

      grantPermission: (permission) => {
        const key = `${permission.tool}:${permission.scope}`;
        set((state) => ({
          grantedPermissions: {
            ...state.grantedPermissions,
            [key]: permission,
          },
        }));
      },

      revokePermission: (key) => {
        set((state) => {
          const { [key]: _, ...rest } = state.grantedPermissions;
          return { grantedPermissions: rest };
        });
      },

      clearPermissions: () => set({ grantedPermissions: {} }),

      hasPermission: (toolName) => {
        const permissions = get().grantedPermissions;
        return Object.keys(permissions).some((key) => key.startsWith(`${toolName}:`));
      },

      // ========================================================================
      // Connection
      // ========================================================================
      connectionStatus: 'disconnected',
      setConnectionStatus: (status) => set({ connectionStatus: status }),

      // ========================================================================
      // Error State
      // ========================================================================
      lastError: null,
      setError: (error) => set({ lastError: error }),
    }),
    {
      name: 'sre-cli-store',
      storage: createJSONStorage(() => nodeStorage),
      partialize: (state) => ({
        // Only persist these fields
        sessions: state.sessions.slice(-5), // Keep last 5 sessions
        inputHistory: state.inputHistory.slice(-50), // Keep last 50 inputs
        config: state.config,
      }),
    }
  )
);

// ============================================================================
// Selector Hooks
// ============================================================================

export const useMessages = () => useStore((state) => state.currentSession?.messages ?? []);
export const useCurrentSession = () => useStore((state) => state.currentSession);
export const useIsProcessing = () => useStore((state) => state.isProcessing);
export const useLoopStatus = () => useStore((state) => state.loopStatus);
export const useConnectionStatus = () => useStore((state) => state.connectionStatus);
export const useInputHistory = () => useStore((state) => state.inputHistory);
export const useConfig = () => useStore((state) => state.config);

// ============================================================================
// Actions
// ============================================================================

export const storeActions = {
  createSession: (agentName?: string) => useStore.getState().createSession(agentName),
  addMessage: (message: Omit<Message, 'id' | 'timestamp'>) =>
    useStore.getState().addMessage(message),
  updateMessage: (id: string, update: Partial<Message>) =>
    useStore.getState().updateMessage(id, update),
  setProcessing: (processing: boolean) => useStore.getState().setProcessing(processing),
  setLoopStatus: (status: LoopStatus) => useStore.getState().setLoopStatus(status),
  setConnectionStatus: (status: ConnectionStatus) =>
    useStore.getState().setConnectionStatus(status),
  setError: (error: string | null) => useStore.getState().setError(error),
  addToHistory: (input: string) => useStore.getState().addToHistory(input),
  grantPermission: (permission: Permission) => useStore.getState().grantPermission(permission),
  clearSession: () => useStore.getState().clearSession(),
  setAgentName: (agentName?: string) => useStore.getState().setAgentName(agentName),
};

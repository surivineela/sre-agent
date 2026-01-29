/**
 * Main App component
 * Optimized for minimal re-renders and flicker-free terminal output
 */
import React, { useState, useEffect, useCallback, useRef, useMemo, startTransition } from 'react';
import { Box, Text, useApp, useInput, useStdout } from 'ink';
import ansiEscapes from 'ansi-escapes';
import * as fs from 'fs/promises';
import type { Services, SystemMessage } from '../types';
import { useStore, storeActions } from '../store';
import { Header } from './layout';
import { ChatView } from './chat/ChatView';
import { ExecutionMessage, type ExecutionStatus, type ExecutionType } from './chat/ExecutionMessage';
import { TextInput } from './input/TextInput';
import { VimEditor } from './editor/VimEditor';
import { WizardPrompt } from './input/WizardPrompt';
import { executeSlashCommand, initCommands, type CommandContext, type EditorConfig, type WizardConfig, type TraceViewConfig } from '../commands';
import { TraceView } from './trace/TraceView';
import type { ISpan, SpanKind } from '../types/trace';
import { StreamingService, type StreamingMessage, type EnhancedConnectionState } from '../services/streaming';
import { InlineApproval } from './approval';
import { ConnectionStatus, StreamingInterrupted, ProgressSteps, ErrorBanner } from './feedback';
import { systemMessages as sysMsg } from '../utils';
import { formatErrorDetailed, type FormattedError } from '../utils/errors';
import { useProgress } from '../services/progress';
import { createDebounce } from '../utils/debounce';

// Pending command approval state
interface PendingCommandApproval {
  id: string;
  command: string;
  description?: string;
  workingDirectory?: string;
  threadId: string;
  executionType?: 'azcli' | 'kubectl' | 'psql' | 'approval'; // For correct API endpoint
}

// Active execution tracking for inline status display
interface ActiveExecution {
  id: string;
  command: string;
  description: string;
  status: ExecutionStatus;
  type: ExecutionType;
  output?: string;
  error?: string;
  executedBy?: string;
  timestamp: Date;
}

// Initialize commands early to avoid circular dependency issues
initCommands();

export interface AppProps {
  initialPrompt?: string;
  services: Services;
}

export const App: React.FC<AppProps> = ({
  initialPrompt,
  services,
}) => {
  const { exit } = useApp();
  const [inputValue, setInputValue] = useState('');
  const [historyIndex, setHistoryIndex] = useState(-1);
  const [currentTask, setCurrentTask] = useState<string | undefined>();
  const [currentThreadId, setCurrentThreadId] = useState<string | null>(null);
  // Combined streaming state to batch updates and reduce re-renders
  const [streamingState, setStreamingState] = useState({
    text: '',
    reasoningLines: [] as string[],
    lastTokenTime: Date.now(),
  });
  // Single expanded state for both reasoning and execution output
  const [detailsExpanded, setDetailsExpanded] = useState(false);
  const [editorConfig, setEditorConfig] = useState<EditorConfig | null>(null);
  const [wizardConfig, setWizardConfig] = useState<WizardConfig | null>(null);
  const [traceViewConfig, setTraceViewConfig] = useState<TraceViewConfig | null>(null);
  // Trace spans for trace view
  const [traceSpans, setTraceSpans] = useState<ISpan[]>([]);
  const spanMapRef = useRef<Map<string, ISpan>>(new Map());
  const [pendingApproval, setPendingApproval] = useState<PendingCommandApproval | null>(null);
  const [sessionApprovedCommands, setSessionApprovedCommands] = useState<Set<string>>(new Set());
  const [systemMessageList, setSystemMessageList] = useState<SystemMessage[]>([]);
  // Active executions for inline status display (keyed by execution id)
  const [activeExecutions, setActiveExecutions] = useState<Map<string, ActiveExecution>>(new Map());
  // SPEC-011: Enhanced connection state
  const [enhancedConnectionState, setEnhancedConnectionState] = useState<EnhancedConnectionState | null>(null);
  const [interruptedResponse, setInterruptedResponse] = useState<string | null>(null);
  const [lastUserMessage, setLastUserMessage] = useState<string>('');

  // SPEC-012: Enhanced error display
  const [currentError, setCurrentError] = useState<FormattedError | null>(null);

  // App Insights App ID fetched from server metadata (for trace queries)
  const [serverAppInsightsAppId, setServerAppInsightsAppId] = useState<string | null>(null);

  // SPEC-008: Progress tracking for long operations
  const progressState = useProgress();

  // Helper to add system messages with proper formatting
  const addSystemMessage = useCallback((message: SystemMessage) => {
    setSystemMessageList(prev => [...prev, message]);
  }, []);

  // Streaming service ref
  const streamingServiceRef = useRef<StreamingService | null>(null);
  // Refs for values used in event handlers (to avoid stale closures)
  const currentThreadIdRef = useRef<string | null>(null);
  const streamingTextRef = useRef<string>('');
  const reasoningLinesRef = useRef<string[]>([]);
  // SPEC-005: Track last token time for adaptive cursor behavior
  const lastTokenTimeRef = useRef<number>(Date.now());
  // Track processed message content to avoid duplicates
  const processedContentRef = useRef<Set<string>>(new Set());
  // Track last streaming message ID to detect new messages
  const lastStreamingMessageIdRef = useRef<string | null>(null);

  // Get stdout for screen control
  const { stdout } = useStdout();

  // Static key for forcing re-render of Static component when needed
  const [staticKey, setStaticKey] = useState(0);

  // Refresh static content (clear screen and force re-render)
  const refreshStatic = useCallback(() => {
    process.stdout.write(ansiEscapes.cursorTo(0, 0) + ansiEscapes.eraseScreen);
    setStaticKey((prev) => prev + 1);
  }, []);

  // Debounced state sync - batches rapid updates, uses startTransition for non-urgent updates
  const [syncStateFromRefs, cleanupDebounce] = useMemo(() => {
    return createDebounce(() => {
      startTransition(() => {
        setStreamingState({
          text: streamingTextRef.current,
          reasoningLines: reasoningLinesRef.current,
          lastTokenTime: lastTokenTimeRef.current,
        });
      });
    }, 50, 100); // 50ms debounce, 100ms max wait
  }, []);

  // Cleanup debounce on unmount
  useEffect(() => {
    return () => cleanupDebounce();
  }, [cleanupDebounce]);

  // Store state
  const currentSession = useStore((state) => state.currentSession);
  const messages = currentSession?.messages ?? [];
  const isProcessing = useStore((state) => state.isProcessing);
  const loopStatus = useStore((state) => state.loopStatus);
  const connectionStatus = useStore((state) => state.connectionStatus);
  const inputHistory = useStore((state) => state.inputHistory);

  // Initialize session on mount
  useEffect(() => {
    if (!currentSession) {
      storeActions.createSession();
    }
  }, [currentSession]);

  // Keep threadId ref in sync with state (for use in event handlers)
  useEffect(() => {
    currentThreadIdRef.current = currentThreadId;
  }, [currentThreadId]);
  // Note: streamingText and reasoningLines are now ref-first (refs are source of truth, state syncs from refs)

  // Initialize streaming service and connect to API (only once)
  useEffect(() => {
    const serverUrl = services.config.get().server?.url;
    if (!serverUrl) {
      storeActions.setConnectionStatus('disconnected');
      return;
    }

    // Create streaming service
    const streamingService = new StreamingService(serverUrl);
    streamingServiceRef.current = streamingService;

    // Handle message updates from streaming (uses refs to avoid stale closures)
    const handleMessageUpdate = ({ threadId, message }: { threadId: string; message: StreamingMessage }) => {
      if (threadId !== currentThreadIdRef.current) return;

      // Flexible message parsing - handle multiple API formats (PascalCase and camelCase)
      const msgAny = message as Record<string, unknown>;

      // Get contents array (handle both cases)
      const contents = message.contents || message.Contents || [];

      // Try to extract text content from various possible locations
      let textContent: string | undefined;

      // Format 1: contents array with $type or type discriminator
      if (contents.length) {
        for (const content of contents) {
          const contentAny = content as Record<string, unknown>;
          const contentType = content['$type'] || content.type || contentAny.Type || contentAny['$Type'];

          // Check if this is text content
          if (contentType === 'text' || contentType === 'TextContent' || !contentType) {
            // Extract text from various fields
            const text = content.text || content.Text || contentAny.text || contentAny.Text;
            if (typeof text === 'string' && text) {
              textContent = text;
              break;
            }
          }
        }
      }

      // Format 2: Direct content/text properties
      if (!textContent && typeof msgAny.content === 'string') {
        textContent = msgAny.content;
      }
      if (!textContent && typeof msgAny.Content === 'string') {
        textContent = msgAny.Content;
      }
      if (!textContent && typeof msgAny.text === 'string') {
        textContent = msgAny.text;
      }
      if (!textContent && typeof msgAny.Text === 'string') {
        textContent = msgAny.Text;
      }
      if (!textContent && typeof msgAny.message === 'string') {
        textContent = msgAny.message;
      }
      if (!textContent && typeof msgAny.Message === 'string') {
        textContent = msgAny.Message;
      }

      // Get role from various possible fields
      const role = message.role || message.Role || (msgAny.type as string) || 'assistant';

      // Check for cancellation
      const additionalProps = message.additionalProperties || message.AdditionalProperties as Record<string, unknown> || {};
      const isCancelled = additionalProps.isCancelled || additionalProps.IsCancelled;
      if (isCancelled) {
        // Clear pending updates and refs
        cleanupDebounce();
        streamingTextRef.current = '';
        reasoningLinesRef.current = [];
        setStreamingState({ text: '', reasoningLines: [], lastTokenTime: Date.now() });
        storeActions.setProcessing(false);
        storeActions.setLoopStatus('idle');
        setCurrentTask(undefined);
        return;
      }

      // Get streamMessageType for context - this tells us what kind of content this is
      const streamMessageType = (additionalProps.streamMessageType || additionalProps.StreamMessageType || '') as string;
      const streamMessageTypeLower = streamMessageType.toLowerCase();

      // Check for finish reason early - need to process this even for execution messages
      const finishReason = message.finishReason || message.FinishReason;

      // Handle special message types: azcli, kubectl, approval, psql
      // These contain JSON in the text field with execution/approval details
      if (streamMessageTypeLower === 'azcli' || streamMessageTypeLower === 'kubectl' ||
          streamMessageTypeLower === 'approval' || streamMessageTypeLower === 'psql') {
        // Extract JSON content from text field
        let jsonContent: string | undefined;
        for (const content of contents) {
          const text = (content as Record<string, unknown>).text || (content as Record<string, unknown>).Text;
          if (typeof text === 'string' && text) {
            jsonContent = text;
            break;
          }
        }

        if (jsonContent) {
          try {
            const execution = JSON.parse(jsonContent) as {
              id: string;
              command?: string;
              description?: string;
              status?: string;
              title?: string;
              output?: string;
              error?: string;
              executedBy?: { displayName?: string };
            };

            const status = (execution.status || '').toLowerCase();
            const isPending = status === 'pending' || status === 'pendingauthorization';
            const command = execution.command || execution.description || execution.title || streamMessageType;

            // Map streamMessageType to ExecutionType
            const execType: ExecutionType = streamMessageTypeLower === 'azcli' ? 'azCli' :
              streamMessageTypeLower === 'kubectl' ? 'kubectl' :
              streamMessageTypeLower === 'psql' ? 'psql' : 'bash';

            // Map status string to ExecutionStatus
            const execStatus: ExecutionStatus = status === 'pending' ? 'Pending' :
              status === 'pendingauthorization' ? 'PendingAuthorization' :
              status === 'running' ? 'Running' :
              status === 'completed' ? 'Completed' :
              status === 'failed' ? 'Failed' :
              status === 'cancelled' ? 'Cancelled' : 'Pending';

            // Update active executions for inline status display (only if changed to reduce flickering)
            // Uses startTransition to mark this as non-urgent
            if (execution.id) {
              startTransition(() => {
                setActiveExecutions(prev => {
                  const existing = prev.get(execution.id);
                  // Give executions a slight future timestamp so they sort AFTER the message that triggered them
                  // (Messages get their timestamp when committed, executions arrive before that)
                  const newExec = {
                    id: execution.id,
                    command,
                    description: execution.description || `${streamMessageType} execution`,
                    status: execStatus,
                    type: execType,
                    output: execution.output,
                    error: execution.error,
                    executedBy: execution.executedBy?.displayName,
                    timestamp: existing?.timestamp ?? new Date(Date.now() + 1000),
                  };

                  // Skip update if nothing changed
                  if (existing &&
                      existing.status === newExec.status &&
                      existing.output === newExec.output &&
                      existing.error === newExec.error) {
                    return prev;
                  }

                  const updated = new Map(prev);
                  updated.set(execution.id, newExec);
                  return updated;
                });
              });
            }

            // Show approval dialog for pending executions
            if (isPending && execution.id) {
              const threadId = (additionalProps.threadId || additionalProps.ThreadId || currentThreadIdRef.current || '') as string;

              setPendingApproval({
                id: execution.id,
                command,
                description: execution.description || `${streamMessageType} execution`,
                threadId,
                executionType: streamMessageTypeLower as 'azcli' | 'kubectl' | 'psql' | 'approval',
              });
              setCurrentTask('Awaiting approval...');
              storeActions.setLoopStatus('awaiting_permission');
            }

            // Handle message completion if this execution message also signals end of stream
            if (finishReason === 'stop') {
              // Clear pending updates
              cleanupDebounce();

              // Finalize any streaming message
              const finalText = streamingTextRef.current;
              const finalReasoning = reasoningLinesRef.current;
              if (finalText) {
                storeActions.addMessage({
                  role: 'assistant',
                  content: finalText,
                  reasoningLines: finalReasoning.length > 0 ? [...finalReasoning] : undefined,
                });
              }

              // Clear refs and state
              streamingTextRef.current = '';
              reasoningLinesRef.current = [];
              processedContentRef.current.clear();
              setStreamingState({ text: '', reasoningLines: [], lastTokenTime: Date.now() });
              setDetailsExpanded(false);

              storeActions.setProcessing(false);
              storeActions.setLoopStatus('idle');
              setCurrentTask(undefined);
            }

            // Don't process execution messages as regular chat text - they are handled by ExecutionMessage component
            return;
          } catch (e) {
            // Not valid JSON, continue with normal processing
          }
        }
      }

      // Types that should go to reasoning display (not main chat)
      const reasoningTypes = ['thinking', 'reasoning', 'status', 'task', 'internal', 'thought', 'plan', 'planning'];
      const isReasoningType = reasoningTypes.some(t => streamMessageTypeLower.includes(t));

      // Handle reasoning/thinking content from multiple sources
      let reasoningContent: string | undefined;
      let isReasoningContent = false;

      // Source 1: Direct reasoning/thinking properties
      const directReasoning = msgAny.reasoning || msgAny.thinking || msgAny.Reasoning || msgAny.Thinking;
      if (directReasoning && typeof directReasoning === 'string') {
        reasoningContent = directReasoning;
        isReasoningContent = true;
      }

      // Source 2: streamMessageType indicates thinking/reasoning/status
      if (!reasoningContent && isReasoningType) {
        // Get text from contents for reasoning
        for (const content of contents) {
          const text = (content as Record<string, unknown>).text || (content as Record<string, unknown>).Text;
          if (typeof text === 'string' && text) {
            reasoningContent = text;
            isReasoningContent = true;
            break;
          }
        }
      }

      // Source 3: Content type indicates thinking
      if (!reasoningContent) {
        for (const content of contents) {
          const contentAny = content as Record<string, unknown>;
          const contentType = content['$type'] || content.type || contentAny.Type;
          if (contentType === 'thinking' || contentType === 'reasoning') {
            const text = content.text || content.Text || contentAny.text || contentAny.Text;
            if (typeof text === 'string' && text) {
              reasoningContent = text;
              isReasoningContent = true;
              break;
            }
          }
        }
      }

      // Helper to join streaming chunks - handles word fragments from tokenization
      const smartJoin = (prev: string, next: string): string => {
        if (!prev) return next;
        if (!next) return prev;

        const prevEndsWithSpace = /\s$/.test(prev);
        const nextStartsWithSpace = /^\s/.test(next);

        // If either has space, just concatenate
        if (prevEndsWithSpace || nextStartsWithSpace) {
          return prev + next;
        }

        // Check if next is likely a word fragment (suffix) vs a new word
        // Common suffixes that should be concatenated without space
        const suffixPatterns = /^(ing|ed|er|est|ly|tion|sion|ment|ness|ful|less|able|ible|ive|ous|ious|al|ial|ic|ical|'s|'t|'d|'ll|'ve|'re|'m|n't)$/i;
        const isLikelySuffix = suffixPatterns.test(next) && next.length <= 5;

        // If it looks like a suffix and prev ends with a letter, concatenate
        if (isLikelySuffix && /[a-zA-Z]$/.test(prev)) {
          return prev + next;
        }

        // Otherwise, add a space between chunks
        return prev + ' ' + next;
      };

      // Add reasoning to display - handle block markers and streaming word fragments
      // Use refs for accumulation, then throttled sync to state
      // NOTE: Don't return early - tool calls might also be in this message
      if (reasoningContent) {
        const newLines = [...reasoningLinesRef.current];
        let content = reasoningContent;

        // Skip status words that shouldn't be displayed
        const skipWords = ['operation', 'processing', 'thinking', 'working'];

        // Clean up all ** markers from content first
        content = content.replace(/\*\*/g, '').trim();

        // Remove "operation" prefix if present
        if (content.toLowerCase().startsWith('operation')) {
          content = content.slice(9).trim();
        }

        const contentLower = content.toLowerCase();
        // Only process if content is meaningful (don't return - continue to tool handling)
        if (content && !skipWords.includes(contentLower)) {
          // Handle newlines - each line becomes a new reasoning line
          if (content.includes('\n')) {
            const parts = content.split('\n');
            for (let i = 0; i < parts.length; i++) {
              const part = parts[i].trim();
              if (!part || skipWords.includes(part.toLowerCase())) continue;

              if (i === 0 && newLines.length > 0) {
                // Append to last line with smart spacing
                const lastLine = newLines[newLines.length - 1];
                newLines[newLines.length - 1] = smartJoin(lastLine, part);
              } else {
                newLines.push(part);
              }
            }
          } else {
            // Simple append with smart word joining (avoid "Respond ing")
            if (newLines.length === 0) {
              newLines.push(content);
            } else {
              const lastLine = newLines[newLines.length - 1];
              newLines[newLines.length - 1] = smartJoin(lastLine, content);
            }
          }
          reasoningLinesRef.current = newLines.slice(-15);
          syncStateFromRefs();
        }
      }

      // Handle tool calls / function calls - add to reasoning display
      // Note: Approvals are now handled via streamMessageType (azcli, kubectl, approval, psql)
      for (const content of contents) {
        const contentType = content['$type'] || content.type;
        if (contentType === 'functionCall' || contentType === 'tool_call') {
          const toolName = content.name;
          const contentProps = (content as Record<string, unknown>).additionalProperties as Record<string, unknown> | undefined;
          const userDescription = contentProps?.userDescription as string | undefined;

          if (toolName) {
            setCurrentTask(userDescription || `Running ${toolName}...`);
            const reasoningDisplay = userDescription || toolName;
            reasoningLinesRef.current = [...reasoningLinesRef.current, reasoningDisplay].slice(-4);
            syncStateFromRefs();
          }
        }
        // Handle tool results
        if (contentType === 'functionResult' || contentType === 'tool_result') {
          const resultText = content.text || content.Text || (content as Record<string, unknown>).result;
          if (typeof resultText === 'string' && resultText) {
            const resultLines = resultText.split('\n').slice(0, 3);
            reasoningLinesRef.current = [...reasoningLinesRef.current, ...resultLines].slice(-4);
            syncStateFromRefs();
          }
        }
      }

      // Process text content based on role - but skip if it's reasoning content
      if (textContent && !isReasoningContent) {
        const roleLower = (role || '').toString().toLowerCase();
        if (roleLower === 'assistant' || roleLower === 'agent' || roleLower === 'bot') {
          // Get message ID to detect new messages
          const messageId = (additionalProps.messageId || additionalProps.MessageId || '') as string;

          // Add newline if this is a new message (different ID) and we have existing content
          const current = streamingTextRef.current;
          if (current && messageId && lastStreamingMessageIdRef.current && messageId !== lastStreamingMessageIdRef.current) {
            streamingTextRef.current += '\n' + textContent;
          } else {
            streamingTextRef.current += textContent;
          }

          // Update last message ID
          if (messageId) {
            lastStreamingMessageIdRef.current = messageId;
          }
          lastTokenTimeRef.current = Date.now();
          syncStateFromRefs();
          setCurrentTask('Responding...');
        } else if (roleLower === 'user' || roleLower === 'human') {
          // Skip user messages (we already added them locally)
        } else if (roleLower === 'tool' || roleLower === 'function') {
          // Tool results
          storeActions.addMessage({
            role: 'system',
            content: `🔧 ${textContent}`,
          });
        } else if (roleLower === 'system') {
          // System messages (errors, cancellations, etc.)
          storeActions.addMessage({
            role: 'system',
            content: textContent,
          });
        } else {
          // Other message types - treat as assistant (unless reasoning type)
          if (!isReasoningType) {
            streamingTextRef.current += textContent;
            lastTokenTimeRef.current = Date.now();
            syncStateFromRefs();
          }
        }
      }

      // Handle message completion based on finishReason
      if (finishReason === 'stop') {
        // Clear any pending throttled updates
        cleanupDebounce();

        // Finalize the streaming message with reasoning
        const finalText = streamingTextRef.current;
        const finalReasoning = reasoningLinesRef.current;
        if (finalText) {
          storeActions.addMessage({
            role: 'assistant',
            content: finalText,
            reasoningLines: finalReasoning.length > 0 ? [...finalReasoning] : undefined,
          });
        }

        // Clear refs and state
        streamingTextRef.current = '';
        reasoningLinesRef.current = [];
        processedContentRef.current.clear();
        setStreamingState({ text: '', reasoningLines: [], lastTokenTime: Date.now() });
        setDetailsExpanded(false);

        storeActions.setProcessing(false);
        storeActions.setLoopStatus('idle');
        setCurrentTask(undefined);
      }
    };

    // Handle thread completion (uses refs to avoid stale closures)
    // ThreadUpdate receives a StreamingMessage with thread status information
    const handleThreadUpdate = (update: unknown) => {

      // Parse the update - it's a StreamingMessage with additionalProperties containing threadId
      const msg = update as StreamingMessage;
      const msgAny = update as Record<string, unknown>;

      // Extract threadId from additionalProperties
      const additionalProps = msg.additionalProperties || msg.AdditionalProperties as Record<string, unknown> || {};
      const threadId = (additionalProps.threadId || additionalProps.ThreadId || msgAny.threadId) as string;

      if (threadId && threadId !== currentThreadIdRef.current) return;

      // Check for completion signals
      const finishReason = msg.finishReason || msg.FinishReason;
      const isCancelled = additionalProps.isCancelled || additionalProps.IsCancelled;

      // Determine if thread is complete
      const isComplete = finishReason === 'stop' || isCancelled;

      if (isComplete || isCancelled) {
        // Clear any pending throttled updates
        cleanupDebounce();

        // Finalize the streaming message with reasoning
        const finalText = streamingTextRef.current;
        const finalReasoning = reasoningLinesRef.current;
        if (finalText) {
          storeActions.addMessage({
            role: 'assistant',
            content: finalText,
            reasoningLines: finalReasoning.length > 0 ? [...finalReasoning] : undefined,
          });
        }

        // Clear refs and state
        streamingTextRef.current = '';
        reasoningLinesRef.current = [];
        processedContentRef.current.clear();
        setStreamingState({ text: '', reasoningLines: [], lastTokenTime: Date.now() });
        setDetailsExpanded(false);

        storeActions.setProcessing(false);
        storeActions.setLoopStatus('idle');
        setCurrentTask(undefined);

        // Extract text content for error messages
        const contents = msg.contents || msg.Contents || [];
        let errorText = '';
        for (const content of contents) {
          const text = (content as Record<string, unknown>).text || (content as Record<string, unknown>).Text;
          if (typeof text === 'string') {
            errorText = text;
            break;
          }
        }

        // Handle cancellation
        if (isCancelled) {
          storeActions.addMessage({
            role: 'system',
            content: 'Operation cancelled.',
          });
        } else if (errorText && (errorText.toLowerCase().includes('error') || errorText.toLowerCase().includes('fail'))) {
          storeActions.addMessage({
            role: 'system',
            content: errorText,
          });
        }
      }
    };

    // Handle errors (SPEC-012: Enhanced error display)
    const handleError = ({ type, message }: { type: string; message: string }) => {
      // Format the error for enhanced display
      const formatted = formatErrorDetailed(new Error(`${type}: ${message}`));

      // Add context-aware actions based on error type
      formatted.actions = [];
      if (formatted.category === 'connection') {
        formatted.actions.push({
          key: 'R',
          label: 'Retry',
          handler: () => streamingService.retryNow(),
        });
        formatted.actions.push({
          key: 'I',
          label: 'Init',
          command: '/init',
        });
      }
      if (formatted.category === 'auth') {
        formatted.actions.push({
          key: 'A',
          label: 'Authenticate',
          command: '/auth',
        });
      }

      // Set the current error for display
      setCurrentError(formatted);

      storeActions.setProcessing(false);
      storeActions.setLoopStatus('idle');
      setCurrentTask(undefined);
    };

    // Also handle task updates for tool execution status
    const handleTaskUpdate = (update: unknown) => {
      // Task updates usually indicate tool execution progress
      const msg = update as Record<string, unknown>;
      const contents = (msg.contents || msg.Contents || []) as Array<Record<string, unknown>>;

      for (const content of contents) {
        const text = content.text || content.Text;
        if (typeof text === 'string') {
          // Parse task JSON if possible
          try {
            const taskData = JSON.parse(text);
            if (taskData.name || taskData.toolName) {
              setCurrentTask(`Running ${taskData.name || taskData.toolName}...`);
            }
          } catch {
            // Not JSON, use raw text
            setCurrentTask(text.substring(0, 50) + (text.length > 50 ? '...' : ''));
          }
        }
      }
    };

    // SPEC-011: Handle enhanced connection state updates
    const handleEnhancedConnectionState = (state: EnhancedConnectionState) => {
      setEnhancedConnectionState(state);

      // If disconnected/reconnecting during streaming, save partial response
      if (state.status !== 'connected' && streamingTextRef.current) {
        setInterruptedResponse(streamingTextRef.current);
      }

      // Update the simple connection status for header
      if (state.status === 'connected') {
        storeActions.setConnectionStatus('connected');
      } else if (state.status === 'connecting') {
        storeActions.setConnectionStatus('connecting');
      } else {
        storeActions.setConnectionStatus('disconnected');
      }
    };

    // Subscribe to events
    streamingService.on(StreamingService.EVENTS.MESSAGE_UPDATE, handleMessageUpdate);
    streamingService.on(StreamingService.EVENTS.THREAD_UPDATE, handleThreadUpdate);
    streamingService.on(StreamingService.EVENTS.TASK_UPDATE, handleTaskUpdate);
    streamingService.on(StreamingService.EVENTS.ERROR, handleError);
    streamingService.on(StreamingService.EVENTS.ENHANCED_CONNECTION_STATE, handleEnhancedConnectionState);

    // Connect
    const connect = async () => {
      try {
        storeActions.setConnectionStatus('connecting');
        await streamingService.connect();
        await services.api.connect();
        storeActions.setConnectionStatus('connected');
        // Add success system message
        addSystemMessage(sysMsg.info(`Connected to ${serverUrl}`));

        // Fetch App Insights App ID from ARM (for trace queries)
        try {
          const appInsightsAppId = await services.api.getAppInsightsAppIdFromArm();
          if (appInsightsAppId) {
            setServerAppInsightsAppId(appInsightsAppId);
          }
        } catch (metadataError) {
          // Non-fatal - trace queries may not work but other features will
          console.warn('Could not fetch App Insights App ID from ARM:', metadataError);
        }
      } catch (error) {
        storeActions.setConnectionStatus('disconnected');
        // Add error system message
        const errorMsg = error instanceof Error ? error.message : 'Unknown error';
        addSystemMessage(sysMsg.warning(`Failed to connect: ${errorMsg}`, 'Connection'));
      }
    };
    connect();

    // Cleanup
    return () => {
      streamingService.off(StreamingService.EVENTS.MESSAGE_UPDATE, handleMessageUpdate);
      streamingService.off(StreamingService.EVENTS.THREAD_UPDATE, handleThreadUpdate);
      streamingService.off(StreamingService.EVENTS.TASK_UPDATE, handleTaskUpdate);
      streamingService.off(StreamingService.EVENTS.ERROR, handleError);
      streamingService.off(StreamingService.EVENTS.ENHANCED_CONNECTION_STATE, handleEnhancedConnectionState);
      streamingService.disconnect();
    };
  // Only recreate streaming service when server config changes
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [services.config, services.api, addSystemMessage]);

  // Process initial prompt
  useEffect(() => {
    if (initialPrompt && currentSession) {
      handleSendMessage(initialPrompt);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialPrompt, currentSession?.id]);

  // Helper to clear all streaming state
  const clearStreamingState = useCallback(() => {
    cleanupDebounce();
    streamingTextRef.current = '';
    reasoningLinesRef.current = [];
    lastStreamingMessageIdRef.current = null;
    setStreamingState({ text: '', reasoningLines: [], lastTokenTime: Date.now() });
  }, [cleanupDebounce]);

  // Global keyboard shortcuts - disabled during approval dialogs
  useInput((input, key) => {
    // Escape to cancel current operation
    if (key.escape) {
      if (isProcessing && currentThreadId) {
        const streamingService = streamingServiceRef.current;
        if (streamingService) {
          streamingService.cancelThread(currentThreadId);
        }
        storeActions.setProcessing(false);
        storeActions.setLoopStatus('idle');
        setCurrentTask(undefined);
        clearStreamingState();
        storeActions.addMessage({
          role: 'system',
          content: 'Operation cancelled.',
        });
      }
    }

    // Ctrl+C to exit
    if (key.ctrl && input === 'c') {
      if (isProcessing) {
        // If processing, first press cancels, second press exits
        const streamingService = streamingServiceRef.current;
        if (streamingService && currentThreadId) {
          streamingService.cancelThread(currentThreadId);
        }
        storeActions.setProcessing(false);
        storeActions.setLoopStatus('idle');
        setCurrentTask(undefined);
        clearStreamingState();
      } else {
        exit();
      }
    }

    // Clear on Ctrl+L
    if (key.ctrl && input === 'l') {
      storeActions.clearSession();
      setCurrentThreadId(null);
      clearStreamingState();
      setSystemMessageList([]);
      addSystemMessage(sysMsg.divider('New Session'));
    }

    // Ctrl+O to expand/collapse reasoning and execution output
    if (key.ctrl && input === 'o') {
      setDetailsExpanded(prev => !prev);
    }
  }, { isActive: !pendingApproval && !editorConfig && !wizardConfig });

  // Handle message submission
  const handleSendMessage = useCallback(
    async (text: string) => {
      if (!text.trim() || isProcessing) return;

      // Check for slash commands
      if (text.startsWith('/')) {
        await handleSlashCommand(text);
        return;
      }

      // Add to history
      storeActions.addToHistory(text);
      setHistoryIndex(-1);
      setInputValue('');
      setLastUserMessage(text); // Track for resend functionality (SPEC-011)

      // Add user message
      storeActions.addMessage({ role: 'user', content: text });

      // Clear previous streaming state when sending new message
      clearStreamingState();
      setDetailsExpanded(false);
      // Keep all executions - they persist as static cards in the chat

      // Process with streaming service
      storeActions.setProcessing(true);
      storeActions.setLoopStatus('thinking');
      setCurrentTask('Connecting to agent...');

      const streamingService = streamingServiceRef.current;
      if (!streamingService) {
        storeActions.addMessage({
          role: 'system',
          content: 'Not connected to server. Run /init to configure.',
        });
        storeActions.setProcessing(false);
        storeActions.setLoopStatus('idle');
        setCurrentTask(undefined);
        return;
      }

      try {
        // Get user info
        const userId = process.env.USER || process.env.USERNAME || 'cli-user';
        const displayName = userId;

        // Get agent name from session if available
        const agentName = currentSession?.agentName || undefined;

        if (currentThreadId) {
          // Continue existing thread
          setCurrentTask('Sending message...');
          // Ensure ref is in sync before sending
          currentThreadIdRef.current = currentThreadId;
          await streamingService.sendMessage(currentThreadId, {
            text,
            userId,
            displayName,
            agent: agentName,
          });
        } else {
          // Create new thread
          setCurrentTask('Creating conversation...');
          const threadId = await streamingService.createThread({
            message: text,
            userId,
            displayName,
            agentName,
          });
          // Update both state and ref immediately (ref needed for event handlers)
          currentThreadIdRef.current = threadId;
          setCurrentThreadId(threadId);
        }

        setCurrentTask('Waiting for response...');
        // Message updates will come through event handlers
      } catch (error) {
        const errorMessage = error instanceof Error ? error.message : String(error);
        storeActions.addMessage({
          role: 'system',
          content: `Failed to send message: ${errorMessage}`,
        });
        storeActions.setProcessing(false);
        storeActions.setLoopStatus('idle');
        setCurrentTask(undefined);
      }
    },
    [isProcessing, currentThreadId, currentSession?.agentName]
  );

  // Handle slash commands using command registry
  const handleSlashCommand = async (command: string) => {
    setInputValue('');

    // Build command context
    const ctx: Omit<CommandContext, 'args' | 'rawInput'> = {
      services,
      onOutput: (content: string) => {
        storeActions.addMessage({ role: 'system', content });
      },
      onClear: () => {
        storeActions.clearSession();
      },
      onExit: () => {
        exit();
      },
      onStateChange: (state) => {
        // Update session state (e.g., agent name)
        if (state && typeof state === 'object' && 'agentName' in state) {
          storeActions.setAgentName(state.agentName as string | undefined);
        }
      },
    };

    const result = await executeSlashCommand(command, ctx);

    if (result.shouldExit) {
      exit();
      return;
    }

    // Check if command wants to open editor
    if (result.editor) {
      setEditorConfig(result.editor);
      return;
    }

    // Check if command wants to start wizard
    if (result.wizard) {
      setWizardConfig(result.wizard);
      return;
    }

    // Check if command wants to open trace view
    if (result.traceView) {
      setTraceViewConfig(result.traceView);
      return;
    }

    if (!result.success && result.message) {
      storeActions.addMessage({
        role: 'system',
        content: `Error: ${result.message}`,
      });
    } else if (result.message && !result.silent) {
      storeActions.addMessage({
        role: 'system',
        content: result.message,
      });
    }
  };

  // Handle editor save
  const handleEditorSave = async (content: string) => {
    if (!editorConfig) return;

    try {
      await fs.writeFile(editorConfig.filePath, content, 'utf-8');
      storeActions.addMessage({
        role: 'system',
        content: `✓ Saved: ${editorConfig.filename}`,
      });
    } catch (error) {
      storeActions.addMessage({
        role: 'system',
        content: `Failed to save: ${error instanceof Error ? error.message : String(error)}`,
      });
    }
  };

  // Handle editor quit
  const handleEditorQuit = () => {
    setEditorConfig(null);
  };

  // Handle editor save and quit
  const handleEditorSaveAndQuit = async (content: string) => {
    await handleEditorSave(content);
    setEditorConfig(null);
  };

  // Handle wizard step selection
  const handleWizardSelect = async (value: string) => {
    if (!wizardConfig) return;

    const currentStep = wizardConfig.steps[wizardConfig.currentStep];
    const newData = { ...wizardConfig.data, [currentStep.id]: value };

    // Check if this is the last step
    if (wizardConfig.currentStep >= wizardConfig.steps.length - 1) {
      // Complete the wizard - wait for result BEFORE clearing wizard state
      try {
        const result = await wizardConfig.onComplete(newData);

        // Handle the result - check for nested wizard, editor, or message
        if (result.wizard) {
          // Chain to another wizard (replaces current wizard)
          setWizardConfig(result.wizard);
        } else if (result.editor) {
          // Clear wizard and open editor
          setWizardConfig(null);
          setEditorConfig(result.editor);
          // Also show message if present
          if (result.message && !result.silent) {
            storeActions.addMessage({
              role: 'system',
              content: result.message,
            });
          }
        } else {
          // Clear wizard and show message
          setWizardConfig(null);
          if (result.message) {
            storeActions.addMessage({
              role: 'system',
              content: result.success ? result.message : `Error: ${result.message}`,
            });
          }
        }
      } catch (error) {
        setWizardConfig(null);
        storeActions.addMessage({
          role: 'system',
          content: `Wizard error: ${error instanceof Error ? error.message : String(error)}`,
        });
      }
    } else {
      // Move to next step
      setWizardConfig({
        ...wizardConfig,
        currentStep: wizardConfig.currentStep + 1,
        data: newData,
      });
    }
  };

  // Handle wizard cancel
  const handleWizardCancel = () => {
    setWizardConfig(null);
    storeActions.addMessage({
      role: 'system',
      content: 'Wizard cancelled.',
    });
  };

  // Handle wizard back
  const handleWizardBack = () => {
    if (!wizardConfig || wizardConfig.currentStep === 0) {
      handleWizardCancel();
      return;
    }
    setWizardConfig({
      ...wizardConfig,
      currentStep: wizardConfig.currentStep - 1,
    });
  };

  // Helper to get the correct API base path for executions
  const getExecutionBasePath = (executionType: string | undefined): 'azCliExecution' | 'kubectlExecution' | 'psqlExecution' | null => {
    switch (executionType) {
      case 'azcli': return 'azCliExecution';
      case 'kubectl': return 'kubectlExecution';
      case 'psql': return 'psqlExecution';
      default: return null;
    }
  };

  // Command approval handlers
  const handleApproveCommand = useCallback(async () => {
    if (!pendingApproval) return;

    // Capture values before clearing state
    const { id, command, threadId, executionType } = pendingApproval;
    const basePath = getExecutionBasePath(executionType);

    // Add to reasoning that command was approved
    reasoningLinesRef.current = [...reasoningLinesRef.current, `✓ Approved: ${command.split(' ').slice(0, 3).join(' ')}...`].slice(-4);
    syncStateFromRefs();

    // Clear pending approval and continue
    setPendingApproval(null);
    storeActions.setLoopStatus('tool_execution');
    setCurrentTask(`Running command...`);

    // Send approval to server using correct endpoint
    const streamingService = streamingServiceRef.current;
    if (streamingService && threadId) {
      try {
        if (basePath) {
          // Use execution action endpoint for azcli/kubectl/psql
          await streamingService.postExecutionAction(basePath, threadId, id, 'run');
        } else {
          // Use approval endpoint for regular approvals
          await streamingService.approveToolCall(threadId, id, true);
        }
      } catch (error) {
        const errorMsg = error instanceof Error ? error.message : String(error);
        storeActions.addMessage({
          role: 'system',
          content: `Failed to send approval: ${errorMsg}`,
        });
      }
    }
  }, [pendingApproval]);

  const handleDenyCommand = useCallback(async () => {
    if (!pendingApproval) return;

    // Capture values before clearing state
    const { id, command, threadId, executionType } = pendingApproval;
    const basePath = getExecutionBasePath(executionType);

    // Add to reasoning that command was denied
    reasoningLinesRef.current = [...reasoningLinesRef.current, `✗ Denied: ${command.split(' ').slice(0, 3).join(' ')}...`].slice(-4);
    syncStateFromRefs();

    // Clear pending approval
    setPendingApproval(null);
    storeActions.setLoopStatus('idle');
    setCurrentTask(undefined);

    // Send denial to server using correct endpoint
    const streamingService = streamingServiceRef.current;
    if (streamingService && threadId) {
      try {
        if (basePath) {
          // Use execution action endpoint for azcli/kubectl/psql
          await streamingService.postExecutionAction(basePath, threadId, id, 'cancel');
        } else {
          // Use approval endpoint for regular approvals
          await streamingService.approveToolCall(threadId, id, false);
        }
      } catch (error) {
        const errorMsg = error instanceof Error ? error.message : String(error);
        storeActions.addMessage({
          role: 'system',
          content: `Failed to send denial: ${errorMsg}`,
        });
      }
    }

    storeActions.addMessage({
      role: 'system',
      content: 'Command denied by user.',
    });
  }, [pendingApproval]);

  // History navigation
  const handleHistoryPrev = useCallback(() => {
    if (inputHistory.length === 0) return;
    const newIndex = Math.min(historyIndex + 1, inputHistory.length - 1);
    setHistoryIndex(newIndex);
    setInputValue(inputHistory[inputHistory.length - 1 - newIndex] || '');
  }, [historyIndex, inputHistory]);

  const handleHistoryNext = useCallback(() => {
    if (historyIndex <= 0) {
      setHistoryIndex(-1);
      setInputValue('');
      return;
    }
    const newIndex = historyIndex - 1;
    setHistoryIndex(newIndex);
    setInputValue(inputHistory[inputHistory.length - 1 - newIndex] || '');
  }, [historyIndex, inputHistory]);

  // SPEC-011: Connection status handlers
  const handleConnectionRetry = useCallback(() => {
    const streamingService = streamingServiceRef.current;
    if (streamingService) {
      streamingService.retryNow();
    }
  }, []);

  const handleConnectionCancel = useCallback(() => {
    const streamingService = streamingServiceRef.current;
    if (streamingService) {
      streamingService.cancelReconnect();
    }
    setInterruptedResponse(null);
  }, []);

  // SPEC-012: Error action handler
  const handleErrorAction = useCallback((key: string) => {
    if (!currentError) return;

    const action = currentError.actions?.find(a => a.key === key);
    if (!action) return;

    // Execute action handler or command
    if (action.handler) {
      action.handler();
    } else if (action.command) {
      // Execute as slash command
      handleSlashCommand(action.command);
    }
    setCurrentError(null);
  }, [currentError]);

  const handleErrorDismiss = useCallback(() => {
    setCurrentError(null);
  }, []);

  // Memoize executions array to prevent unnecessary re-renders of ChatView
  const executionsArray = useMemo(() =>
    Array.from(activeExecutions.values()),
    [activeExecutions]
  );

  // Memoize check for active executions (avoid IIFE in render)
  const hasActiveExecution = useMemo(() =>
    executionsArray.some(e => e.status === 'Running' || e.status === 'Pending' || e.status === 'PendingAuthorization'),
    [executionsArray]
  );

  // Memoize server URL and App Insights App ID to avoid calling config.get() in render
  const serverUrl = useMemo(() =>
    services.config.get().server?.url,
    [services.config]
  );

  // Use server-fetched App Insights App ID (from ARM resource)
  // This is fetched on connect via getAgentMetadata()
  const appInsightsAppId = serverAppInsightsAppId;

  const handleConnectionAuth = useCallback(() => {
    // Trigger auth command
    handleSlashCommand('/auth');
  }, []);

  // SPEC-011: Streaming interruption handlers
  const handleStreamingContinue = useCallback(() => {
    // Try to continue from where we left off
    setInterruptedResponse(null);
    if (interruptedResponse) {
      // Re-add the partial response
      streamingTextRef.current = interruptedResponse;
      syncStateFromRefs();
    }
  }, [interruptedResponse, syncStateFromRefs]);

  const handleStreamingResend = useCallback(() => {
    // Resend the last message
    setInterruptedResponse(null);
    clearStreamingState();
    if (lastUserMessage) {
      handleSendMessage(lastUserMessage);
    }
  }, [lastUserMessage, clearStreamingState, handleSendMessage]);

  const handleStreamingCancel = useCallback(() => {
    setInterruptedResponse(null);
    clearStreamingState();
    storeActions.setProcessing(false);
    storeActions.setLoopStatus('idle');
    setCurrentTask(undefined);
    storeActions.addMessage({
      role: 'system',
      content: 'Response cancelled due to connection interruption.',
    });
  }, [clearStreamingState]);

  // Header is ready (static) once connection is established
  // IMPORTANT: This must be defined BEFORE any early returns to avoid hooks order issues
  const headerReady = connectionStatus === 'connected';

  // Header element to be passed to ChatView
  // IMPORTANT: useMemo hooks must be called BEFORE any conditional early returns
  const headerElement = useMemo(() => (
    <Header
      connectionStatus={connectionStatus}
      serverUrl={serverUrl}
      remoteAgentName={currentSession?.agentName}
    />
  ), [connectionStatus, serverUrl, currentSession?.agentName]);

  // Show VimEditor when in editor mode
  if (editorConfig) {
    return (
      <Box flexDirection="column">
        <VimEditor
          initialContent={editorConfig.content}
          filename={editorConfig.filename}
          fileType={editorConfig.fileType || 'yaml'}
          readOnly={editorConfig.readOnly}
          onSave={handleEditorSave}
          onQuit={handleEditorQuit}
          onSaveAndQuit={handleEditorSaveAndQuit}
        />
      </Box>
    );
  }

  // Show wizard when in wizard mode
  if (wizardConfig) {
    const currentStep = wizardConfig.steps[wizardConfig.currentStep];
    return (
      <Box flexDirection="column">
        <Box flexDirection="column" paddingX={1}>
          <Box marginTop={1}>
            <Text bold color="cyan">{wizardConfig.title}</Text>
          </Box>
          <WizardPrompt
            step={currentStep}
            stepNumber={wizardConfig.currentStep + 1}
            totalSteps={wizardConfig.steps.length}
            onSelect={handleWizardSelect}
            onCancel={handleWizardCancel}
            onBack={wizardConfig.currentStep > 0 ? handleWizardBack : undefined}
          />
        </Box>
      </Box>
    );
  }

  // Show trace view when in trace mode
  if (traceViewConfig) {
    return (
      <TraceView
        spans={traceSpans}
        onClose={() => setTraceViewConfig(null)}
        threadId={currentThreadId || undefined}
        agentName={currentSession?.agentName}
        serverUrl={serverUrl}
        appInsightsAppId={appInsightsAppId}
      />
    );
  }

  return (
    <Box flexDirection="column">
      {/* Chat area with header - natural flow */}
      <Box flexDirection="column" paddingX={1}>
        <ChatView
          messages={messages}
          systemMessages={systemMessageList}
          executions={executionsArray}
          isProcessing={isProcessing}
          currentTask={currentTask}
          streamingText={streamingState.text}
          reasoningLines={streamingState.reasoningLines}
          reasoningExpanded={detailsExpanded}
          lastTokenTime={streamingState.lastTokenTime}
          executionOutputExpanded={detailsExpanded}
          header={headerElement}
          headerReady={headerReady}
          staticKey={staticKey}
        />
      </Box>

      {/* SPEC-008: Progress steps for long operations like /init */}
      {progressState.steps.length > 0 && (
        <Box paddingX={1}>
          <ProgressSteps state={progressState} />
        </Box>
      )}

      {/* SPEC-011: Connection status when disconnected/reconnecting */}
      {enhancedConnectionState && enhancedConnectionState.status !== 'connected' && (
        <Box paddingX={1}>
          <ConnectionStatus
            state={enhancedConnectionState}
            onRetry={handleConnectionRetry}
            onCancel={handleConnectionCancel}
            onAuth={handleConnectionAuth}
          />
        </Box>
      )}

      {/* SPEC-011: Streaming interrupted banner */}
      {interruptedResponse && enhancedConnectionState?.status === 'connected' && (
        <Box paddingX={1}>
          <StreamingInterrupted
            partialResponse={interruptedResponse}
            onContinue={handleStreamingContinue}
            onResend={handleStreamingResend}
            onCancel={handleStreamingCancel}
          />
        </Box>
      )}

      {/* SPEC-012: Error banner for formatted error display */}
      {currentError && (
        <Box paddingX={1}>
          <ErrorBanner
            error={currentError}
            onAction={handleErrorAction}
            onDismiss={handleErrorDismiss}
          />
        </Box>
      )}

      {/* Inline command approval prompt */}
      {pendingApproval && (
        <Box paddingX={1}>
          <InlineApproval
            command={pendingApproval.command}
            onApprove={handleApproveCommand}
            onDeny={handleDenyCommand}
            onSendMessage={(message) => {
              // Clear the approval and send the message as user input
              setPendingApproval(null);
              handleSendMessage(message);
            }}
          />
        </Box>
      )}

      {/* Input area - follows content naturally (hidden during approval/processing) */}
      {!isProcessing && !pendingApproval && !hasActiveExecution && (
        <Box marginTop={1}>
          <TextInput
            value={inputValue}
            onChange={setInputValue}
            onSubmit={handleSendMessage}
            placeholder=""
            disabled={false}
            onHistoryPrev={handleHistoryPrev}
            onHistoryNext={handleHistoryNext}
          />
        </Box>
      )}

      {/* Status line when processing with cancel hint - compact, no margin */}
      {isProcessing && (
        <Box paddingX={1}>
          <Text color="yellow">⏳</Text>
          <Text color="gray"> {currentTask || 'Processing...'}</Text>
          <Text color="gray" dimColor>  Esc to cancel</Text>
        </Box>
      )}
    </Box>
  );
};

export default App;

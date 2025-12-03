import { ExtendedTool, ToolParameter } from '../../../../Contracts/ExtendedAgentGraph';

// ─────────────────────────────────────────────────────────────────────────────
// State Types
// ─────────────────────────────────────────────────────────────────────────────

export type Phase = 'prompt' | 'code';
export type TestState = 'idle' | 'running' | 'success' | 'error';

export interface TestResult {
    success: boolean;
    result?: any;
    errorMessage?: string;
    stderr?: string;
    stdout?: string;
    executionTimeMs?: number;
    errorType?: string;
}

export interface PythonToolState {
    phase: Phase;
    prompt: string;
    isGenerating: boolean;
    generateError: string | null;
    testState: TestState;
    testResult: TestResult | null;
    paramValues: Record<string, string>;
    iterationContext: string | null;
}

// ─────────────────────────────────────────────────────────────────────────────
// Component Props
// ─────────────────────────────────────────────────────────────────────────────

export interface PythonToolCreatorProps {
    tool: Partial<ExtendedTool>;
    onChange: (tool: Partial<ExtendedTool>) => void;
    onTestStatusChange: (
        status: 'idle' | 'running' | 'success' | 'error',
        options?: { error?: string; fingerprint?: string | null }
    ) => void;
    fingerprint?: string | null;
}

export interface PromptViewProps {
    prompt: string;
    onPromptChange: (value: string) => void;
    isGenerating: boolean;
    generateError: string | null;
    onGenerate: () => void;
    onSwitchToCode: () => void;
    hasExistingCode: boolean;
    iterationContext: string | null;
}

export interface CodeViewProps {
    tool: Partial<ExtendedTool>;
    onChange: (tool: Partial<ExtendedTool>) => void;
    onSwitchToPrompt: () => void;
}

export interface TerminalProps {
    parameters: ToolParameter[];
    paramValues: Record<string, string>;
    onParamChange: (name: string, value: string) => void;
    testState: TestState;
    testResult: TestResult | null;
    canTest: boolean;
    onRunTest: () => void;
    onQuickFix: () => void;
    onRefine: (suggestion: string) => void;
}

// ─────────────────────────────────────────────────────────────────────────────
// Constants
// ─────────────────────────────────────────────────────────────────────────────

export const EXAMPLE_PROMPTS = [
    { label: 'Health Check', prompt: 'Check if a URL is reachable and return response time and status code' },
    { label: 'JSON Parser', prompt: 'Parse a JSON string and extract specific fields by path' },
    { label: 'Date Calculator', prompt: 'Calculate the number of days between two dates' },
    { label: 'Text Analyzer', prompt: 'Count words, characters, and sentences in a text' },
];

export const REFINEMENT_SUGGESTIONS = [
    { label: 'Add error handling', prompt: 'Add comprehensive error handling for edge cases' },
    { label: 'Optimize', prompt: 'Improve performance and efficiency' },
    { label: 'More output', prompt: 'Return more detailed information in the output' },
    { label: 'Add logging', prompt: 'Add helpful debug logging' },
];

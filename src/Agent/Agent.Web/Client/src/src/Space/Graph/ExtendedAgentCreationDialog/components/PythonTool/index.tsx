import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { EnvironmentContext } from '../../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../../../../Common/Helpers/headers';
import { sanitizeEntityName } from '../../utils/nameValidation';
import { CodeView } from './CodeView';
import { PromptView } from './PromptView';
import { usePythonToolStyles } from './styles';
import { Terminal } from './Terminal';
import { Phase, PythonToolCreatorProps, TestResult, TestState } from './types';

/**
 * PythonToolCreator - Main orchestrator component
 *
 * Two main layouts:
 *
 * 1. PROMPT PHASE (initial or when regenerating):
 * ┌─────────────────────────────────────────────────────────────────────────┐
 * │                         PromptView                                      │
 * │                    (full width, centered)                               │
 * └─────────────────────────────────────────────────────────────────────────┘
 *
 * 2. CODE PHASE (after generation):
 * ┌────────────────────────────────────────┬────────────────────────────────┐
 * │               CodeView                 │           Terminal             │
 * │           (flex: 1, ~60%)              │         (400px fixed)          │
 * │                                        │                                │
 * │  ┌──────────────────────────────────┐  │  ┌──────────────────────────┐  │
 * │  │ [name] [timeout] [AI Assist]     │  │  │ ● Test Playground [Run]  │  │
 * │  ├──────────────────────────────────┤  │  ├──────────────────────────┤  │
 * │  │ [description                   ] │  │  │ $ Parameters:            │  │
 * │  ├──────────────────────────────────┤  │  │   url (str) *required    │  │
 * │  │  1 │ def main(url: str):         │  │  │   [________________]     │  │
 * │  │  2 │     import requests         │  │  │                          │  │
 * │  │  3 │     ...                     │  │  │ $ python main(url="...")  │  │
 * │  │  4 │                             │  │  │ ┌────────────────────┐   │  │
 * │  │  5 │                             │  │  │ │ ✓ Test Passed      │   │  │
 * │  │  6 │                             │  │  │ │ {"status": 200}    │   │  │
 * │  │    │                             │  │  │ └────────────────────┘   │  │
 * │  └──────────────────────────────────┘  │  └──────────────────────────┘  │
 * └────────────────────────────────────────┴────────────────────────────────┘
 */
export const PythonToolCreator: FC<PythonToolCreatorProps> = ({ tool, onChange, onTestStatusChange, fingerprint }) => {
    const styles = usePythonToolStyles();
    const environmentContext = useContext(EnvironmentContext);
    const sreAgentEndpoint = environmentContext?.sreAgentEndpoint || '';

    // ─────────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────────
    const [phase, setPhase] = useState<Phase>(() => (tool.functionCode ? 'code' : 'prompt'));
    const [prompt, setPrompt] = useState('');
    const [isGenerating, setIsGenerating] = useState(false);
    const [generateError, setGenerateError] = useState<string | null>(null);
    const [testState, setTestState] = useState<TestState>('idle');
    const [testResult, setTestResult] = useState<TestResult | null>(null);
    const [paramValues, setParamValues] = useState<Record<string, string>>({});
    const [iterationContext, setIterationContext] = useState<string | null>(null);

    // ─────────────────────────────────────────────────────────────────────
    // Computed (defined before handlers that use it)
    // ─────────────────────────────────────────────────────────────────────
    const canTest = useMemo(() => {
        if (!tool.functionCode?.includes('def main')) return false;
        const requiredParams = (tool.parameters || []).filter(p => p.required !== false && p.name);
        return requiredParams.every(p => paramValues[p.name!]?.trim());
    }, [tool.functionCode, tool.parameters, paramValues]);

    // ─────────────────────────────────────────────────────────────────────
    // Handlers (defined before effects that use them)
    // ─────────────────────────────────────────────────────────────────────

    const handleGenerate = useCallback(async () => {
        if (!prompt.trim()) return;

        setIsGenerating(true);
        setGenerateError(null);
        setTestResult(null);
        setTestState('idle');

        try {
            // Build context-aware prompt for iteration
            let fullPrompt = prompt;

            // When iterating on existing code, provide clear context to the AI
            if (tool.functionCode) {
                if (iterationContext) {
                    // Error fix scenario - provide error context and existing code
                    fullPrompt = `You are iterating on an existing Python function. Here is the CURRENT CODE that needs to be fixed:\n\n\`\`\`python\n${tool.functionCode}\n\`\`\`\n\nPREVIOUS ERROR:\n${iterationContext}\n\nUSER REQUEST:\n${prompt}\n\nPlease fix the code while preserving its core functionality. Only make the minimum changes needed to address the issue.`;
                } else {
                    // Refinement scenario - user wants to improve existing code
                    fullPrompt = `You are iterating on an existing Python function. Here is the CURRENT CODE to improve:\n\n\`\`\`python\n${tool.functionCode}\n\`\`\`\n\nUSER REQUEST:\n${prompt}\n\nPlease improve the code as requested while preserving its core functionality and structure. Make targeted changes to address the user's request.`;
                }
            }

            const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/generate-python-tool`, {
                method: 'POST',
                headers: { ...getAgentHeaders(), 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    intent: fullPrompt,
                    suggestedName: tool.name || undefined,
                    timeoutSeconds: tool.timeoutSeconds || 120,
                    existingCode: tool.functionCode || undefined,
                }),
            });

            if (!response.ok) {
                const errorText = await response.text();
                let errorMessage = `HTTP ${response.status}`;
                try {
                    const errorJson = JSON.parse(errorText);
                    errorMessage = errorJson.message || errorJson.Message || errorMessage;
                } catch {
                    errorMessage = errorText || errorMessage;
                }
                throw new Error(errorMessage);
            }

            const result = await response.json();

            if (!result.success) {
                throw new Error(result.errorMessage || 'Generation failed');
            }

            const functionCode = result.function_code || result.functionCode || '';
            const timeoutSeconds = result.timeout_seconds || result.timeoutSeconds || 120;

            if (!functionCode) {
                throw new Error('Generated function code is empty');
            }

            onChange({
                ...tool,
                name: tool.name || sanitizeEntityName(result.name),
                description: result.description,
                functionCode,
                parameters: result.parameters || [],
                timeoutSeconds,
            });

            setPhase('code');
            setIterationContext(null);
            setPrompt('');
            onTestStatusChange('idle');
        } catch (error) {
            setGenerateError(error instanceof Error ? error.message : 'Failed to generate');
        } finally {
            setIsGenerating(false);
        }
    }, [prompt, sreAgentEndpoint, tool, onChange, onTestStatusChange, iterationContext]);

    const handleTest = useCallback(async () => {
        if (!canTest) return;

        setTestState('running');
        setTestResult(null);
        onTestStatusChange('running');

        try {
            const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/tools/python/test`, {
                method: 'POST',
                headers: { ...getAgentHeaders(), 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    functionCode: tool.functionCode,
                    timeoutSeconds: tool.timeoutSeconds || 120,
                    parameters: paramValues,
                    parameterDefinitions: tool.parameters ?? [],
                }),
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: Test endpoint failed`);
            }

            const results = await response.json();
            setTestResult(results);

            if (results.success) {
                setTestState('success');
                setIterationContext(null);
                onTestStatusChange('success', { fingerprint });
            } else {
                // Build the best error message for AI context
                // Priority: stderr (Python traceback) > meaningful errorMessage > stdout > fallback
                const stderr = results.stderr?.trim();
                const errorMessage = results.errorMessage?.trim();
                const stdout = results.stdout?.trim();
                const isGenericError =
                    errorMessage === 'Python execution failed.' ||
                    errorMessage === 'Python execution failed' ||
                    errorMessage === 'Test failed';

                let errorContext: string;
                if (stderr) {
                    errorContext = stderr;
                } else if (errorMessage && !isGenericError) {
                    errorContext = errorMessage;
                } else if (stdout) {
                    errorContext = stdout;
                } else {
                    errorContext = errorMessage || 'Unknown error occurred';
                }

                setTestState('error');
                setIterationContext(errorContext);
                onTestStatusChange('error', { error: results.errorType || 'Error' });
            }
        } catch (error) {
            const errorMsg = error instanceof Error ? error.message : 'Test failed';
            setTestResult({ success: false, errorMessage: errorMsg });
            setTestState('error');
            setIterationContext(errorMsg);
            onTestStatusChange('error', { error: 'System Error' });
        }
    }, [canTest, tool, paramValues, sreAgentEndpoint, onTestStatusChange, fingerprint]);

    const handleQuickFix = useCallback(() => {
        if (iterationContext) {
            setPrompt(`Fix the error: ${iterationContext.substring(0, 200)}`);
            setPhase('prompt');
        }
    }, [iterationContext]);

    const handleRefine = useCallback((suggestion: string) => {
        setPrompt(suggestion);
        setPhase('prompt');
    }, []);

    const handleParamChange = useCallback((name: string, value: string) => {
        setParamValues(prev => ({ ...prev, [name]: value }));
    }, []);

    // ─────────────────────────────────────────────────────────────────────
    // Effects (after handlers are defined)
    // ─────────────────────────────────────────────────────────────────────

    // Sync param values when parameters change
    useEffect(() => {
        setParamValues(prev => {
            const next: Record<string, string> = {};
            (tool.parameters || []).forEach(param => {
                if (param.name) {
                    next[param.name] = prev[param.name] || '';
                }
            });
            return next;
        });
    }, [tool.parameters]);

    // Keyboard shortcuts
    useEffect(() => {
        const handleKeyDown = (e: KeyboardEvent) => {
            if ((e.metaKey || e.ctrlKey) && e.key === 'Enter') {
                e.preventDefault();
                if (phase === 'prompt' && prompt.trim() && !isGenerating) {
                    handleGenerate();
                } else if (phase === 'code' && canTest && testState !== 'running') {
                    handleTest();
                }
            }
        };
        window.addEventListener('keydown', handleKeyDown);
        return () => window.removeEventListener('keydown', handleKeyDown);
    }, [phase, prompt, isGenerating, testState, canTest, handleGenerate, handleTest]);

    // ─────────────────────────────────────────────────────────────────────
    // Render
    // ─────────────────────────────────────────────────────────────────────

    // Prompt phase - full width
    if (phase === 'prompt') {
        return (
            <PromptView
                prompt={prompt}
                onPromptChange={setPrompt}
                isGenerating={isGenerating}
                generateError={generateError}
                onGenerate={handleGenerate}
                onSwitchToCode={() => setPhase('code')}
                hasExistingCode={!!tool.functionCode}
                iterationContext={iterationContext}
            />
        );
    }

    // Code phase - split view
    return (
        <div className={styles.splitContainer}>
            <div className={styles.leftPanel}>
                <CodeView tool={tool} onChange={onChange} onSwitchToPrompt={() => setPhase('prompt')} />
            </div>
            <div className={styles.rightPanel}>
                <Terminal
                    parameters={tool.parameters || []}
                    paramValues={paramValues}
                    onParamChange={handleParamChange}
                    testState={testState}
                    testResult={testResult}
                    canTest={canTest}
                    onRunTest={handleTest}
                    onQuickFix={handleQuickFix}
                    onRefine={handleRefine}
                />
            </div>
        </div>
    );
};

// Re-export for backwards compatibility
export { PythonToolCreator as default };

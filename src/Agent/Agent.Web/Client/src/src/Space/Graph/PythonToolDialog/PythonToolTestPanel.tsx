import { Button, Field, Input, Label, MessageBar, MessageBarBody, Tab, TabList, Text, Textarea } from '@fluentui/react-components';
import { Play16Regular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../../Common/Helpers/headers';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { sanitizeEntityName } from '../ExtendedAgentCreationDialog/utils/nameValidation';
import { usePythonToolDialogStyles } from './PythonToolDialog.Styles';
import {
    PythonToolFormProps,
    TabValue,
    TestResult,
    extractParametersFromCode,
    getPythonToolFingerprint,
    hasValidMainFunction,
} from './PythonToolUtilities';

interface PythonToolTestPanelProps {
    hasSuccessRunTest: boolean;
    setHasSuccessRunTest: (hasSuccess: boolean) => void;
    lastTestedFingerprint: string | null;
    setLastTestedFingerprint: (fingerprint: string | null) => void;
    isGenerating: boolean;
    setIsGenerating: (isGenerating: boolean) => void;
}

export const PythonToolTestPanel: FC<PythonToolTestPanelProps> = ({
    hasSuccessRunTest,
    setHasSuccessRunTest,
    lastTestedFingerprint,
    setLastTestedFingerprint,
    isGenerating,
    setIsGenerating,
}) => {
    const intl = useIntl();
    const styles = usePythonToolDialogStyles();
    const { values, setFieldValue, setValues, dirty, isValid } = useFormikContext<PythonToolFormProps>();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    // Local state
    const [selectedTab, setSelectedTab] = useState<TabValue>('assistant');
    const [prompt, setPrompt] = useState('');
    const [generateError, setGenerateError] = useState<string | null>(null);
    const [isTesting, setIsTesting] = useState(false);
    const [testResult, setTestResult] = useState<TestResult | null>(null);
    const [testError, setTestError] = useState<string | null>(null);
    const [paramValues, setParamValues] = useState<Record<string, string>>({});

    // Current fingerprint for detecting changes
    const currentFingerprint = useMemo(
        () => getPythonToolFingerprint(values.functionCode, values.parameters, values.timeoutSeconds),
        [values.functionCode, values.parameters, values.timeoutSeconds]
    );

    // Check if code changed after successful test
    const codeChangedAfterTest = useMemo(() => {
        if (!hasSuccessRunTest) return false;
        return currentFingerprint !== lastTestedFingerprint;
    }, [hasSuccessRunTest, currentFingerprint, lastTestedFingerprint]);

    // Reset test status when code changes
    useEffect(() => {
        if (codeChangedAfterTest) {
            setHasSuccessRunTest(false);
            setTestResult(null);
            setTestError(null);
        }
    }, [codeChangedAfterTest, setHasSuccessRunTest]);

    // Auto-extract parameters from function code when it changes
    useEffect(() => {
        if (!values.functionCode || !hasValidMainFunction(values.functionCode)) {
            return;
        }

        const extractedParams = extractParametersFromCode(values.functionCode);
        const currentParams = values.parameters || [];

        // Check if parameters changed (names, types, or required status)
        const extractedSignatures = new Set(extractedParams.map(p => `${p.name}:${p.type}:${p.required}`));
        const currentSignatures = new Set(currentParams.map(p => `${p.name}:${p.type}:${p.required}`));

        const signaturesMatch =
            extractedSignatures.size === currentSignatures.size && [...extractedSignatures].every(s => currentSignatures.has(s));

        if (!signaturesMatch) {
            // Merge: preserve existing descriptions if param still exists
            const merged = extractedParams.map(extracted => {
                const existing = currentParams.find(p => p.name === extracted.name);
                return existing ? { ...extracted, description: existing.description || extracted.description } : extracted;
            });
            setFieldValue('parameters', merged);
        }
    }, [values.functionCode, values.parameters, setFieldValue]);

    // Sync param values with current parameters
    useEffect(() => {
        setParamValues(prev => {
            const next: Record<string, string> = {};
            (values.parameters || []).forEach(param => {
                const name = param.name?.trim();
                if (!name) return;
                if (prev[name] !== undefined) {
                    next[name] = prev[name];
                }
            });
            return next;
        });
    }, [values.parameters]);

    // Computed values for testing
    const requiredParams = useMemo(
        () => (values.parameters || []).filter(param => param.required !== false && !!param.name?.trim()),
        [values.parameters]
    );

    const missingParamNames = useMemo(
        () =>
            requiredParams
                .filter(param => !(param.name && paramValues[param.name]?.trim()))
                .map(param => param.name?.trim())
                .filter((name): name is string => !!name),
        [paramValues, requiredParams]
    );

    const canTest = useMemo(() => {
        if (!hasValidMainFunction(values.functionCode)) return false;
        return missingParamNames.length === 0;
    }, [values.functionCode, missingParamNames.length]);

    const testerStatus = useMemo(() => {
        if (isTesting) {
            return {
                intent: 'info' as const,
                message: intl.formatMessage(SreAgentResources.pythonToolCreatorTestRunning),
            };
        }

        if (testResult?.success === false || testError) {
            const errorMessage = testError || testResult?.errorMessage || intl.formatMessage(SreAgentResources.error);
            return {
                intent: 'error' as const,
                message: intl.formatMessage(SreAgentResources.pythonToolCreatorTestError, { message: errorMessage }),
            };
        }

        if (testResult?.success === true && !codeChangedAfterTest) {
            return {
                intent: 'success' as const,
                message: intl.formatMessage(SreAgentResources.pythonToolCreatorTestSuccess),
            };
        }

        return {
            intent: 'warning' as const,
            message: intl.formatMessage(SreAgentResources.pythonToolCreatorTestPending),
        };
    }, [isTesting, testResult, testError, intl, codeChangedAfterTest]);

    // Handlers
    const handleGenerate = useCallback(
        async (promptOverride?: string) => {
            const promptToUse = promptOverride ?? prompt;
            if (!promptToUse.trim()) return;

            setIsGenerating(true);
            setGenerateError(null);

            try {
                const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/generate-python-tool`, {
                    method: 'POST',
                    headers: { ...getAgentHeaders(), 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        intent: promptToUse,
                        suggestedName: values.name || undefined,
                        timeoutSeconds: values.timeoutSeconds || 120,
                        existingCode: values.functionCode || undefined,
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

                setValues(
                    {
                        ...values,
                        name: !values.name && result.name ? sanitizeEntityName(result.name) : values.name,
                        description: result.description || values.description,
                        functionCode,
                        parameters: result.parameters || [],
                        timeoutSeconds,
                    },
                    true
                );

                setPrompt('');
                setTestResult(null);
                setTestError(null);
                setHasSuccessRunTest(false);

                // Switch to test playground tab after successful generation
                setSelectedTab('test');
            } catch (error) {
                setGenerateError(error instanceof Error ? error.message : 'Failed to generate');
            } finally {
                setIsGenerating(false);
            }
        },
        [prompt, sreAgentEndpoint, values, setValues, setIsGenerating, setHasSuccessRunTest]
    );

    const handleTest = useCallback(async () => {
        if (!canTest) return;

        setIsTesting(true);
        setTestError(null);
        setTestResult(null);

        try {
            const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/tools/python/test`, {
                method: 'POST',
                headers: { ...getAgentHeaders(), 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    functionCode: values.functionCode,
                    timeoutSeconds: values.timeoutSeconds || 120,
                    parameters: paramValues,
                    parameterDefinitions: values.parameters ?? [],
                }),
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: Test endpoint failed`);
            }

            const results = await response.json();
            setTestResult(results);

            if (results.success) {
                setHasSuccessRunTest(true);
                setLastTestedFingerprint(currentFingerprint);
            } else {
                const errorMessage = results.errorMessage || results.stderr || intl.formatMessage(SreAgentResources.error);
                setTestError(errorMessage);
                setHasSuccessRunTest(false);
            }
        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : intl.formatMessage(SreAgentResources.error);
            setTestError(errorMessage);
            setHasSuccessRunTest(false);
        } finally {
            setIsTesting(false);
        }
    }, [canTest, values, paramValues, sreAgentEndpoint, intl, setHasSuccessRunTest, setLastTestedFingerprint, currentFingerprint]);

    const handleFixWithAI = useCallback(() => {
        const errorMessage = testError || testResult?.errorMessage || testResult?.stderr || 'Unknown error';
        const fixPrompt = `Fix the following error in my Python code:
            Error: ${errorMessage}

            Current code:
            \`\`\`python
            ${values.functionCode || ''}
            \`\`\`
            `;

        setPrompt(fixPrompt);
        setSelectedTab('assistant');
        // Auto-start generation
        handleGenerate(fixPrompt);
    }, [testError, testResult, values.functionCode, handleGenerate]);

    return (
        <div className={styles.toolFormRight}>
            <TabList selectedValue={selectedTab} onTabSelect={(_, data) => setSelectedTab(data.value as TabValue)}>
                <Tab value="assistant">{intl.formatMessage(SreAgentResources.pythonToolCreatorAssistantTab)}</Tab>
                <Tab value="test">{intl.formatMessage(SreAgentResources.pythonToolCreatorTestPlaygroundTab)}</Tab>
            </TabList>

            {/* Assistant Tab */}
            {selectedTab === 'assistant' && (
                <div className={styles.tabContent}>
                    <div className={styles.promptArea}>
                        <Label>{intl.formatMessage(SreAgentResources.pythonToolCreatorPromptLabel)}</Label>
                        <Textarea
                            value={prompt}
                            onChange={(_, data) => setPrompt(data.value)}
                            placeholder={intl.formatMessage(SreAgentResources.pythonToolBuilderIntentPlaceholder)}
                            className={styles.promptTextarea}
                            resize="vertical"
                            disabled={isGenerating}
                        />
                        <Button appearance="primary" disabled={!prompt.trim() || isGenerating} onClick={() => handleGenerate()}>
                            {isGenerating
                                ? intl.formatMessage(SreAgentResources.pythonToolCreatorGeneratingButton)
                                : intl.formatMessage(SreAgentResources.pythonToolCreatorGenerateButton)}
                        </Button>
                    </div>

                    {generateError && (
                        <MessageBar intent="error">
                            <MessageBarBody>
                                {intl.formatMessage(SreAgentResources.pythonToolCreatorGenerateError, { message: generateError })}
                            </MessageBarBody>
                        </MessageBar>
                    )}
                </div>
            )}

            {/* Test Playground Tab */}
            {selectedTab === 'test' && (
                <div className={styles.tabContent}>
                    <MessageBar intent="warning">
                        <MessageBarBody>{intl.formatMessage(SreAgentResources.pythonToolCreatorTestPlaygroundWarning)}</MessageBarBody>
                    </MessageBar>

                    <div className={styles.testPanelHeader}>
                        <Text size={300} weight="semibold">
                            {intl.formatMessage(SreAgentResources.pythonToolCreatorTestPlaygroundTab)}
                        </Text>
                        <Button
                            appearance="primary"
                            icon={<Play16Regular />}
                            onClick={handleTest}
                            disabled={!dirty || !isValid || !canTest || isTesting}
                        >
                            {isTesting
                                ? intl.formatMessage(SreAgentResources.pythonToolBuilderTestRunning)
                                : intl.formatMessage(SreAgentResources.pythonToolCreatorTestButton)}
                        </Button>
                    </div>

                    {/* Test Results Section - only show after a test has been run */}
                    {(testResult || testError || isTesting) && (
                        <div className={styles.executionResultsSection}>
                            <Text size={300} weight="semibold">
                                {intl.formatMessage(SreAgentResources.pythonToolCreatorResultsLabel)}
                            </Text>

                            {/* Error state with Fix with AI button */}
                            {testerStatus.intent === 'error' && (
                                <div className={styles.errorContainer}>
                                    <MessageBar className={styles.testPanelMessageBar} intent={testerStatus.intent} role="alert">
                                        <MessageBarBody>{testerStatus.message}</MessageBarBody>
                                    </MessageBar>
                                    <Button appearance="primary" onClick={handleFixWithAI} size="small">
                                        {intl.formatMessage(SreAgentResources.pythonToolCreatorFixWithAI)}
                                    </Button>
                                </div>
                            )}

                            {/* Running status message */}
                            {testerStatus.intent === 'info' && (
                                <MessageBar className={styles.testPanelMessageBar} intent={testerStatus.intent} role="status">
                                    <MessageBarBody>{testerStatus.message}</MessageBarBody>
                                </MessageBar>
                            )}

                            {/* Success results */}
                            {testResult?.success && !codeChangedAfterTest && (
                                <>
                                    <MessageBar intent="success" className={styles.testPanelMessageBar}>
                                        <MessageBarBody>
                                            {intl.formatMessage(SreAgentResources.pythonToolCreatorTestSuccess)}
                                        </MessageBarBody>
                                    </MessageBar>
                                    {testResult.executionTimeMs && (
                                        <Text size={200}>
                                            {intl.formatMessage(SreAgentResources.pythonToolCreatorExecutionTime, {
                                                milliseconds: testResult.executionTimeMs,
                                            })}
                                        </Text>
                                    )}
                                    <div className={styles.resultsContent}>{JSON.stringify(testResult.result, null, 2)}</div>
                                </>
                            )}
                        </div>
                    )}

                    {/* Parameters */}
                    {values.parameters && values.parameters.length > 0 && (
                        <div className={styles.parameterSection}>
                            {/* Missing Params Warning - at top of parameter section */}
                            {missingParamNames.length > 0 && (
                                <MessageBar intent="warning">
                                    <MessageBarBody>
                                        {intl.formatMessage(SreAgentResources.pythonToolCreatorTestMissingParams, {
                                            parameters: missingParamNames.join(', '),
                                        })}
                                    </MessageBarBody>
                                </MessageBar>
                            )}
                            <Label>{intl.formatMessage(SreAgentResources.pythonToolCreatorParameterValuesLabel)}</Label>
                            <div className={styles.parameterList}>
                                {values.parameters.map((param, index) => (
                                    <Field key={index} label={param.name} size="small">
                                        <Input
                                            size="small"
                                            value={paramValues[param.name ?? ''] || ''}
                                            onChange={(_, data) =>
                                                setParamValues(prev => ({
                                                    ...prev,
                                                    [param.name ?? '']: data.value,
                                                }))
                                            }
                                            placeholder={intl.formatMessage(SreAgentResources.pythonToolCreatorParameterPlaceholder, {
                                                type: param.type || 'string',
                                            })}
                                        />
                                        {missingParamNames.includes(param.name ?? '') && (
                                            <div className={styles.paramWarning}>
                                                {intl.formatMessage(SreAgentResources.pythonToolCreatorParamFieldWarning)}
                                            </div>
                                        )}
                                    </Field>
                                ))}
                            </div>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
};

import { Button, Field, Input, Label, MessageBar, MessageBarBody, Tab, TabList, Text } from '@fluentui/react-components';
import { Play16Regular } from '@fluentui/react-icons';
import MonacoEditor from '@monaco-editor/react';
import { useFormikContext } from 'formik';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../../Common/Clients/ExtendedAgentClient';
import InputFormik from '../../../Common/Components/Input/InputFormik';
import TextareaFormik from '../../../Common/Components/Textarea/TextareaFormik';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { sanitizeEntityName } from '../ExtendedAgentCreationDialog/utils/nameValidation';
import { usePythonToolDialogStyles } from './PythonToolDialog.Styles';
import {
    PythonToolFormProps,
    RightPanelTabValue,
    TestResult,
    extractParametersFromCode,
    getDefaultCode,
    getPythonToolFingerprint,
    hasValidMainFunction,
} from './PythonToolUtilities';

interface PythonToolCodeAndTestPanelProps {
    hasSuccessRunTest: boolean;
    setHasSuccessRunTest: (hasSuccess: boolean) => void;
    lastTestedFingerprint: string | null;
    setLastTestedFingerprint: (fingerprint: string | null) => void;
    isGenerating: boolean;
    selectedTab: RightPanelTabValue;
    setSelectedTab: (tab: RightPanelTabValue) => void;
    onFixWithAI: (errorMessage: string) => void;
}

export const PythonToolCodeAndTestPanel: FC<PythonToolCodeAndTestPanelProps> = ({
    hasSuccessRunTest,
    setHasSuccessRunTest,
    lastTestedFingerprint,
    setLastTestedFingerprint,
    isGenerating,
    selectedTab,
    setSelectedTab,
    onFixWithAI,
}) => {
    const intl = useIntl();
    const styles = usePythonToolDialogStyles();
    const { values, setFieldValue, errors, touched, isValid } = useFormikContext<PythonToolFormProps>();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [isTesting, setIsTesting] = useState(false);
    const [testResult, setTestResult] = useState<TestResult | null>(null);
    const [testError, setTestError] = useState<string | null>(null);
    const [paramValues, setParamValues] = useState<Record<string, string>>({});

    const currentFingerprint = useMemo(
        () => getPythonToolFingerprint(values.functionCode, values.parameters, values.timeoutSeconds),
        [values.functionCode, values.parameters, values.timeoutSeconds]
    );

    const codeChangedAfterTest = useMemo(() => {
        if (!hasSuccessRunTest) return false;
        return currentFingerprint !== lastTestedFingerprint;
    }, [hasSuccessRunTest, currentFingerprint, lastTestedFingerprint]);

    useEffect(() => {
        if (codeChangedAfterTest) {
            setHasSuccessRunTest(false);
            setTestResult(null);
            setTestError(null);
        }
    }, [codeChangedAfterTest, setHasSuccessRunTest]);

    useEffect(() => {
        if (!values.functionCode || !hasValidMainFunction(values.functionCode)) {
            return;
        }

        const extractedParams = extractParametersFromCode(values.functionCode);
        const currentParams = values.parameters || [];

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

    const handleNameChange = useCallback(
        (_: React.ChangeEvent<HTMLInputElement>, data: { value: string }) => {
            setFieldValue('name', sanitizeEntityName(data.value));
        },
        [setFieldValue]
    );

    const handleTimeoutChange = useCallback(
        (_: React.ChangeEvent<HTMLInputElement>, data: { value: string }) => {
            const parsed = parseInt(data.value) || 120;
            setFieldValue('timeoutSeconds', parsed);
        },
        [setFieldValue]
    );

    const handleCodeChange = useCallback(
        (value: string | undefined) => {
            setFieldValue('functionCode', value || '');
        },
        [setFieldValue]
    );

    const handleTest = useCallback(async () => {
        if (!canTest) return;

        setIsTesting(true);
        setTestError(null);
        setTestResult(null);

        const client = ExtendedAgentClient.getInstance(sreAgentEndpoint);
        const response = await client.testPythonTool({
            functionCode: values.functionCode,
            timeoutSeconds: values.timeoutSeconds || 120,
            parameters: paramValues,
            parameterDefinitions: values.parameters ?? [],
        });

        if (!response.isSuccessful) {
            const errorMessage = typeof response.error === 'string' ? response.error : intl.formatMessage(SreAgentResources.error);
            setTestError(errorMessage);
            setHasSuccessRunTest(false);
            setIsTesting(false);
            return;
        }

        const results = response.content;
        setTestResult(results ?? null);

        if (results?.success) {
            setHasSuccessRunTest(true);
            setLastTestedFingerprint(currentFingerprint);
        } else {
            const errorMessage = results?.errorMessage || results?.stderr || intl.formatMessage(SreAgentResources.error);
            setTestError(errorMessage);
            setHasSuccessRunTest(false);
        }

        setIsTesting(false);
    }, [canTest, values, paramValues, sreAgentEndpoint, intl, setHasSuccessRunTest, setLastTestedFingerprint, currentFingerprint]);

    const handleFixWithAI = useCallback(() => {
        const errorMessage = testError || testResult?.errorMessage || testResult?.stderr || 'Unknown error';
        onFixWithAI(errorMessage);
    }, [testError, testResult, onFixWithAI]);

    return (
        <div className={styles.codeAndTestPanel}>
            <TabList selectedValue={selectedTab} onTabSelect={(_, data) => setSelectedTab(data.value as RightPanelTabValue)}>
                <Tab value="code">{intl.formatMessage(SreAgentResources.pythonToolCreatorCodeTab)}</Tab>
                <Tab value="test">{intl.formatMessage(SreAgentResources.pythonToolCreatorTestPlaygroundTab)}</Tab>
            </TabList>

            {/* Code Tab */}
            {selectedTab === 'code' && (
                <div className={styles.codeTabContent}>
                    {/* Name and Timeout Row */}
                    <div className={styles.headerRow}>
                        <InputFormik
                            name="name"
                            label={intl.formatMessage(SreAgentResources.pythonToolBuilderNameLabel)}
                            placeholder={intl.formatMessage(SreAgentResources.pythonToolCreatorToolNamePlaceholder)}
                            orientation="vertical"
                            required
                            onChange={handleNameChange}
                            className={styles.nameField}
                            disabled={isGenerating}
                        />
                        <Field
                            label={intl.formatMessage(SreAgentResources.pythonToolBuilderTimeoutLabel)}
                            validationState={touched.timeoutSeconds && errors.timeoutSeconds ? 'error' : undefined}
                            validationMessage={touched.timeoutSeconds ? errors.timeoutSeconds : undefined}
                        >
                            <Input
                                type="number"
                                value={values.timeoutSeconds?.toString() || '120'}
                                onChange={handleTimeoutChange}
                                min={5}
                                max={900}
                                disabled={isGenerating}
                            />
                        </Field>
                    </div>

                    <TextareaFormik
                        name="description"
                        label={intl.formatMessage(SreAgentResources.pythonToolBuilderDescriptionLabel)}
                        placeholder={intl.formatMessage(SreAgentResources.pythonToolCreatorDescriptionPlaceholder)}
                        orientation="vertical"
                        required
                        className={styles.descriptionField}
                        resize="vertical"
                        disabled={isGenerating}
                    />

                    {/* Monaco Editor */}
                    <div className={styles.editorContainer}>
                        <MonacoEditor
                            height="100%"
                            language="python"
                            theme="vs-light"
                            value={values.functionCode || getDefaultCode()}
                            onChange={handleCodeChange}
                            options={{
                                minimap: { enabled: false },
                                lineNumbers: 'on',
                                scrollBeyondLastLine: false,
                                fontSize: 13,
                                fontFamily: 'Consolas, Monaco, "Courier New", monospace',
                                tabSize: 4,
                                wordWrap: 'on',
                                automaticLayout: true,
                                readOnly: isGenerating,
                                padding: { top: 12, bottom: 12 },
                            }}
                        />
                    </div>
                </div>
            )}

            {/* Test Playground Tab */}
            {selectedTab === 'test' && (
                <div className={styles.tabContent}>
                    {!hasSuccessRunTest && (
                        <MessageBar intent="warning">
                            <MessageBarBody>{intl.formatMessage(SreAgentResources.pythonToolCreatorTestPlaygroundWarning)}</MessageBarBody>
                        </MessageBar>
                    )}

                    <div className={styles.testPanelHeader}>
                        <Text size={300} weight="semibold">
                            {intl.formatMessage(SreAgentResources.pythonToolCreatorTestPlaygroundTab)}
                        </Text>
                        <Button
                            appearance="primary"
                            icon={<Play16Regular />}
                            onClick={handleTest}
                            disabled={!isValid || !canTest || isTesting}
                        >
                            {isTesting
                                ? intl.formatMessage(SreAgentResources.pythonToolBuilderTestRunning)
                                : intl.formatMessage(SreAgentResources.pythonToolCreatorTestButton)}
                        </Button>
                    </div>

                    {(testResult || testError || isTesting) && (
                        <div className={styles.executionResultsSection}>
                            <Text size={300} weight="semibold">
                                {intl.formatMessage(SreAgentResources.pythonToolCreatorResultsLabel)}
                            </Text>

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

                            {testerStatus.intent === 'info' && (
                                <MessageBar className={styles.testPanelMessageBar} intent={testerStatus.intent} role="status">
                                    <MessageBarBody>{testerStatus.message}</MessageBarBody>
                                </MessageBar>
                            )}

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

                    {values.parameters && values.parameters.length > 0 && (
                        <div className={styles.parameterSection}>
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

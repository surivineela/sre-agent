import { Body1, Button, Caption1, Field, Input, Label, makeStyles, Spinner, tokens } from '@fluentui/react-components';
import { PlayCircle20Regular } from '@fluentui/react-icons';
import { MessageBar, MessageBarBody } from '@fluentui/react-message-bar';
import MonacoEditor from '@monaco-editor/react';
import { FC, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { PlaygroundResources } from '../../Strings/SREAgentResources';
import { SystemTool } from '../Contracts/ExtendedAgentGraph';

interface SystemToolTesterPanelProps {
    tool: SystemTool;
}

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        overflow: 'hidden',
        gap: tokens.spacingVerticalL,
    },
    infoSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        padding: tokens.spacingVerticalL,
    },
    toolInfo: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusLarge,
    },
    infoRow: {
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
    },
    parameterSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        padding: tokens.spacingVerticalL,
        backgroundColor: tokens.colorNeutralBackground1,
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusLarge,
    },
    parametersForm: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    formField: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    testSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        padding: tokens.spacingVerticalL,
        height: '100%',
    },
    buttonRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        alignItems: 'center',
    },
    resultContent: {
        flex: 1,
        minHeight: '400px',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium,
        overflow: 'hidden',
        backgroundColor: tokens.colorNeutralBackground1,
    },
    loadingContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: tokens.spacingVerticalXXL,
        gap: tokens.spacingHorizontalM,
        flexDirection: 'column',
    },
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        padding: tokens.spacingVerticalXXXL,
        color: tokens.colorNeutralForeground3,
        textAlign: 'center',
        gap: tokens.spacingVerticalS,
    },
});

export const SystemToolTesterPanel: FC<SystemToolTesterPanelProps> = ({ tool }) => {
    const styles = useStyles();
    const intl = useIntl();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const [paramValues, setParamValues] = useState<Record<string, string>>({});
    const [isExecuting, setIsExecuting] = useState(false);
    const [result, setResult] = useState<any>(null);
    const [error, setError] = useState<string | null>(null);
    const requiredParameters = useMemo(() => tool.parameters || [], [tool.parameters]);

    // Initialize parameters with default values
    useEffect(() => {
        if (tool.parameters && tool.parameters.length > 0) {
            const initial: Record<string, string> = {};
            tool.parameters.forEach(param => {
                // Auto-generate ThreadId if it's a parameter
                if (param.toLowerCase() === 'threadid') {
                    initial[param] = 'test-thread-' + Date.now();
                } else {
                    initial[param] = '';
                }
            });
            setParamValues(initial);
        }
    }, [tool.parameters]);

    const handleExecute = async () => {
        setIsExecuting(true);
        setError(null);
        setResult(null);

        try {
            // Build parameters object from form values
            const parsedParams: any = {};
            Object.entries(paramValues).forEach(([key, value]) => {
                if (value.trim()) {
                    parsedParams[key] = value;
                }
            });

            // Ensure ThreadId is set if required (case-insensitive check)
            const threadIdParam = tool.parameters?.find(p => p.toLowerCase() === 'threadid');
            if (threadIdParam && !parsedParams[threadIdParam]) {
                parsedParams[threadIdParam] = 'test-thread-' + Date.now();
            }

            // Call backend API to execute system tool
            const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/systemTool/execute`, {
                method: 'POST',
                headers: {
                    ...getAgentHeaders(),
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    toolName: tool.name,
                    pluginName: tool.pluginName,
                    parameters: parsedParams,
                }),
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`Failed to execute tool: ${response.status} - ${errorText}`);
            }

            const data = await response.json();
            setResult(data);
        } catch (err) {
            const errorMessage = err instanceof Error ? err.message : String(err);
            setError(errorMessage);
        } finally {
            setIsExecuting(false);
        }
    };

    const resultJson = result ? JSON.stringify(result, null, 2) : '';

    // This component is used in both Configuration and Test tabs
    // We'll render different content based on context, but for now show both sections
    const renderTest = () => (
        <div className={styles.testSection}>
            {/* Execute Button */}
            <div className={styles.buttonRow}>
                <Button appearance="primary" icon={<PlayCircle20Regular />} onClick={handleExecute} disabled={isExecuting}>
                    {isExecuting
                        ? intl.formatMessage(PlaygroundResources.systemToolTesterExecuting)
                        : intl.formatMessage(PlaygroundResources.systemToolTesterExecute)}
                </Button>
                {isExecuting && <Spinner size="tiny" />}
            </div>

            {/* Error Display */}
            {error && (
                <MessageBar intent="error">
                    <MessageBarBody>{error}</MessageBarBody>
                </MessageBar>
            )}

            {/* Result Display */}
            {isExecuting && (
                <div className={styles.loadingContainer}>
                    <Spinner size="large" />
                    <Body1>{intl.formatMessage(PlaygroundResources.systemToolTesterExecutingStatus, { name: tool.name })}</Body1>
                </div>
            )}

            {!isExecuting && result && (
                <div className={styles.resultContent}>
                    <MonacoEditor
                        height="100%"
                        language="json"
                        theme="vs-light"
                        value={resultJson}
                        options={{
                            readOnly: true,
                            minimap: { enabled: false },
                            lineNumbers: 'on',
                            scrollBeyondLastLine: false,
                            automaticLayout: true,
                            fontSize: 13,
                            wordWrap: 'on',
                        }}
                    />
                </div>
            )}

            {!isExecuting && !result && !error && (
                <div className={styles.emptyState}>
                    <PlayCircle20Regular style={{ fontSize: '48px' }} />
                    <Body1>{intl.formatMessage(PlaygroundResources.systemToolTesterEmptyState)}</Body1>
                </div>
            )}
        </div>
    );

    return (
        <div className={styles.container}>
            {requiredParameters.length > 0 ? (
                <div className={styles.parameterSection}>
                    <Label size="large" weight="semibold">
                        {intl.formatMessage(PlaygroundResources.systemToolTesterParametersHeading)}
                    </Label>
                    <div className={styles.parametersForm}>
                        {requiredParameters.map(param => {
                            const isThreadId = param.toLowerCase() === 'threadid';
                            return (
                                <Field
                                    key={param}
                                    label={param}
                                    required
                                    hint={isThreadId ? intl.formatMessage(PlaygroundResources.systemToolTesterThreadHint) : undefined}
                                >
                                    <Input
                                        value={paramValues[param] || ''}
                                        onChange={(_e, data) => {
                                            setParamValues(prev => ({
                                                ...prev,
                                                [param]: data.value,
                                            }));
                                        }}
                                        placeholder={
                                            isThreadId
                                                ? intl.formatMessage(PlaygroundResources.systemToolTesterThreadPlaceholder)
                                                : intl.formatMessage(PlaygroundResources.systemToolTesterParameterPlaceholder, {
                                                      name: param,
                                                  })
                                        }
                                    />
                                </Field>
                            );
                        })}
                    </div>
                </div>
            ) : (
                <Body1>{intl.formatMessage(PlaygroundResources.systemToolTesterNoParameters)}</Body1>
            )}
            {renderTest()}
        </div>
    );
};

export const SystemToolConfigurationPanel: FC<{ tool: SystemTool }> = ({ tool }) => {
    const styles = useStyles();
    const intl = useIntl();

    return (
        <div className={styles.infoSection}>
            <div className={styles.toolInfo}>
                <div className={styles.infoRow}>
                    <Caption1 style={{ fontWeight: tokens.fontWeightSemibold }}>
                        {intl.formatMessage(PlaygroundResources.systemToolTesterToolNameLabel)}
                    </Caption1>
                    <Body1>{tool.name}</Body1>
                </div>
                {tool.description && (
                    <div className={styles.infoRow}>
                        <Caption1 style={{ fontWeight: tokens.fontWeightSemibold }}>
                            {intl.formatMessage(PlaygroundResources.systemToolTesterDescriptionLabel)}
                        </Caption1>
                        <Body1 style={{ wordBreak: 'break-word' }}>{tool.description}</Body1>
                    </div>
                )}
                <div className={styles.infoRow}>
                    <Caption1 style={{ fontWeight: tokens.fontWeightSemibold }}>
                        {intl.formatMessage(PlaygroundResources.systemToolTesterPluginLabel)}
                    </Caption1>
                    <Body1>{tool.pluginName}</Body1>
                </div>
                <div className={styles.infoRow}>
                    <Caption1 style={{ fontWeight: tokens.fontWeightSemibold }}>
                        {intl.formatMessage(PlaygroundResources.systemToolTesterCategoryLabel)}
                    </Caption1>
                    <Body1>{tool.category}</Body1>
                </div>
            </div>
        </div>
    );
};

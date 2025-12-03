import { Button, Spinner, Tooltip } from '@fluentui/react-components';
import { ArrowSync16Regular, Checkmark16Filled, Code20Regular, Dismiss16Filled, Play16Filled } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../../Strings/SREAgentResources';
import { terminalColors, useTerminalStyles } from './styles';
import { REFINEMENT_SUGGESTIONS, TerminalProps } from './types';

export const Terminal: FC<TerminalProps> = ({
    parameters,
    paramValues,
    onParamChange,
    testState,
    testResult,
    canTest,
    onRunTest,
    onQuickFix,
    onRefine,
}) => {
    const styles = useTerminalStyles();
    const intl = useIntl();
    const missingParams = parameters.filter(p => p.required !== false && p.name && !paramValues[p.name]?.trim()).map(p => p.name!);

    return (
        <div className={styles.container}>
            {/* Header */}
            <div className={styles.header}>
                <div className={styles.headerTitle}>
                    <div className={styles.headerDot} />
                    <span style={{ fontWeight: 600, fontSize: '13px' }}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.pythonToolTestPlayground)}
                    </span>
                    {testState === 'success' && (
                        <span style={{ color: terminalColors.success, fontSize: '11px' }}>
                            ● {intl.formatMessage(ExtendedAgentsGraphResources.pythonToolReady)}
                        </span>
                    )}
                </div>
                <Tooltip content="Ctrl/Cmd + Enter" relationship="label">
                    <Button
                        appearance="primary"
                        size="small"
                        icon={testState === 'running' ? <Spinner size="tiny" appearance="inverted" /> : <Play16Filled />}
                        onClick={onRunTest}
                        disabled={!canTest || testState === 'running'}
                        style={{
                            backgroundColor: testState === 'running' ? terminalColors.accent : canTest ? terminalColors.accent : undefined,
                            color: testState === 'running' ? 'white' : undefined,
                            opacity: testState === 'running' ? 0.85 : !canTest ? 0.5 : 1,
                        }}
                    >
                        {testState === 'running'
                            ? intl.formatMessage(ExtendedAgentsGraphResources.pythonToolRunning)
                            : intl.formatMessage(ExtendedAgentsGraphResources.pythonToolRun)}
                    </Button>
                </Tooltip>
            </div>

            {/* Content */}
            <div className={styles.content}>
                {/* Parameters Section */}
                {parameters.length > 0 && (
                    <div className={styles.section}>
                        <div className={styles.sectionLabel}>
                            <Code20Regular style={{ fontSize: 14 }} />
                            <span>Parameters:</span>
                        </div>
                        {parameters.map(param => (
                            <div key={param.name} className={styles.paramRow}>
                                <div className={styles.paramLabel}>
                                    <span className={styles.paramName}>{param.name}</span>
                                    {param.type && <span className={styles.paramType}>({param.type})</span>}
                                    {param.required !== false ? (
                                        <span className={styles.paramRequired}>
                                            {intl.formatMessage(ExtendedAgentsGraphResources.pythonToolRequired)}
                                        </span>
                                    ) : (
                                        <span className={styles.paramOptional}>
                                            {intl.formatMessage(ExtendedAgentsGraphResources.pythonToolOptional)}
                                        </span>
                                    )}
                                </div>
                                <input
                                    className={styles.input}
                                    value={paramValues[param.name!] || ''}
                                    onChange={e => onParamChange(param.name!, e.target.value)}
                                    placeholder={param.description || `Enter ${param.type || 'value'}`}
                                    onKeyDown={e => {
                                        if ((e.metaKey || e.ctrlKey) && e.key === 'Enter' && canTest) {
                                            onRunTest();
                                        }
                                    }}
                                />
                            </div>
                        ))}
                        {missingParams.length > 0 && (
                            <div className={styles.missingWarning}>⚠ Missing required: {missingParams.join(', ')}</div>
                        )}
                    </div>
                )}

                {/* No parameters message */}
                {parameters.length === 0 && (
                    <div className={styles.section}>
                        <div className={styles.sectionLabel}>
                            <Code20Regular style={{ fontSize: 14 }} />
                            <span>Parameters:</span>
                        </div>
                        <div style={{ color: terminalColors.textMuted, fontStyle: 'italic', fontSize: '12px' }}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.pythonToolNoParametersRequired)}
                        </div>
                    </div>
                )}

                {/* Test Execution */}
                {(testResult || testState === 'running') && (
                    <div className={styles.section}>
                        {/* Command line */}
                        <div className={styles.commandLine}>
                            $ python main(
                            {Object.entries(paramValues)
                                .filter(([_, v]) => v)
                                .map(([k, v]) => `${k}="${v}"`)
                                .join(', ')}
                            )
                        </div>

                        {/* Running state */}
                        {testState === 'running' && (
                            <div style={{ color: terminalColors.warning, display: 'flex', alignItems: 'center', gap: '8px' }}>
                                <Spinner size="tiny" />
                                Executing...
                            </div>
                        )}

                        {/* Success Result */}
                        {testState === 'success' && testResult && (
                            <div className={`${styles.resultCard} ${styles.successCard}`}>
                                <div className={styles.resultHeader}>
                                    <Checkmark16Filled style={{ color: terminalColors.success }} />
                                    <span style={{ color: terminalColors.success, fontWeight: 600 }}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.pythonToolTestPassed)}
                                    </span>
                                    {testResult.executionTimeMs && (
                                        <span style={{ color: terminalColors.textMuted, fontSize: '11px', marginLeft: 'auto' }}>
                                            {testResult.executionTimeMs}ms
                                        </span>
                                    )}
                                </div>
                                <pre className={styles.resultPre}>{JSON.stringify(testResult.result, null, 2)}</pre>

                                {/* Refinement suggestions */}
                                <div className={styles.refinements}>
                                    <div style={{ fontSize: '11px', color: terminalColors.textMuted, marginBottom: '6px' }}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.pythonToolWantToImprove)}
                                    </div>
                                    <div className={styles.refinementChips}>
                                        {REFINEMENT_SUGGESTIONS.map(s => (
                                            <span key={s.label} className={styles.chip} onClick={() => onRefine(s.prompt)}>
                                                {s.label}
                                            </span>
                                        ))}
                                    </div>
                                </div>
                            </div>
                        )}

                        {/* Error Result */}
                        {testState === 'error' && testResult && (
                            <div className={`${styles.resultCard} ${styles.errorCard}`}>
                                <div className={styles.resultHeader}>
                                    <Dismiss16Filled style={{ color: terminalColors.error }} />
                                    <span style={{ color: terminalColors.error, fontWeight: 600 }}>
                                        {testResult.errorType || 'Test Failed'}
                                    </span>
                                </div>
                                {/* Show error details - prioritize stderr (actual Python traceback),
                                    but also show errorMessage if it has useful content */}
                                <pre className={styles.resultPre} style={{ color: terminalColors.error }}>
                                    {(() => {
                                        const stderr = testResult.stderr?.trim();
                                        const errorMsg = testResult.errorMessage?.trim();
                                        const isGenericError =
                                            errorMsg === 'Python execution failed.' ||
                                            errorMsg === 'Python execution failed' ||
                                            errorMsg === 'Test failed';

                                        // If we have stderr, show it
                                        if (stderr) {
                                            return stderr;
                                        }
                                        // If errorMessage is not generic, show it
                                        if (errorMsg && !isGenericError) {
                                            return errorMsg;
                                        }
                                        // If stdout has error info, show it
                                        if (testResult.stdout?.trim()) {
                                            return testResult.stdout.trim();
                                        }
                                        // Fallback
                                        return errorMsg || 'An error occurred during execution. Check the Python code for issues.';
                                    })()}
                                </pre>

                                {/* One-click fix button */}
                                <Button
                                    appearance="primary"
                                    size="small"
                                    icon={<ArrowSync16Regular />}
                                    onClick={onQuickFix}
                                    className={styles.fixButton}
                                >
                                    Fix with AI
                                </Button>
                            </div>
                        )}
                    </div>
                )}

                {/* Empty state */}
                {testState === 'idle' && !testResult && (
                    <div className={styles.emptyState}>
                        <div className={styles.emptyIcon}>💡</div>
                        <div style={{ marginBottom: '8px' }}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.pythonToolFillInParameters)}
                        </div>
                        <div style={{ fontSize: '11px' }}>
                            Tip: Press <kbd className={styles.kbd}>⌘↵</kbd> or{' '}
                            <kbd className={styles.kbd}>{intl.formatMessage(ExtendedAgentsGraphResources.pythonToolCtrlEnter)}</kbd>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
};

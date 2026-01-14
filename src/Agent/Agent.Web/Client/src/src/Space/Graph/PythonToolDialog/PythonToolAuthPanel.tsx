import { Button, Field, Input, Label, MessageBar, MessageBarBody, Switch, Text, Tooltip } from '@fluentui/react-components';
import { Add16Regular, Dismiss16Regular, Info16Regular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { usePythonToolDialogStyles } from './PythonToolDialog.Styles';
import {
    AUTH_SCOPE_PRESETS,
    DEFAULT_AUTH_SCOPE,
    PythonToolFormProps,
    createAuthScopeConfig,
    getAuthSnippetCode,
    getScopePresetInfo,
    hasAuthSnippet,
    insertAuthSnippet,
} from './PythonToolUtilities';

interface PythonToolAuthPanelProps {
    isGenerating: boolean;
}

export const PythonToolAuthPanel: FC<PythonToolAuthPanelProps> = ({ isGenerating }) => {
    const intl = useIntl();
    const styles = usePythonToolDialogStyles();
    const { values, setFieldValue } = useFormikContext<PythonToolFormProps>();
    const [customScopeInput, setCustomScopeInput] = useState('');

    const handleAuthToggle = useCallback(
        (_: React.ChangeEvent<HTMLInputElement>, data: { checked: boolean }) => {
            setFieldValue('authEnabled', data.checked);

            // Auto-insert snippet when enabling auth and scopes exist
            if (data.checked && values.authScopes.length > 0 && !hasAuthSnippet(values.functionCode)) {
                setFieldValue('functionCode', insertAuthSnippet(values.functionCode, values.authScopes));
            }

            // Add default scope if enabling auth with no scopes
            if (data.checked && values.authScopes.length === 0) {
                setFieldValue('authScopes', [DEFAULT_AUTH_SCOPE]);
                if (!hasAuthSnippet(values.functionCode)) {
                    setFieldValue('functionCode', insertAuthSnippet(values.functionCode, [DEFAULT_AUTH_SCOPE]));
                }
            }
        },
        [setFieldValue, values.authScopes, values.functionCode]
    );

    const addScope = useCallback(
        (scopeUrl: string) => {
            if (values.authScopes.some(s => s.scope === scopeUrl)) return;

            const newScopes = [...values.authScopes, createAuthScopeConfig(scopeUrl)];
            setFieldValue('authScopes', newScopes);

            // Auto-insert snippet if auth is enabled and code doesn't have it yet
            if (values.authEnabled && !hasAuthSnippet(values.functionCode)) {
                setFieldValue('functionCode', insertAuthSnippet(values.functionCode, newScopes));
            }
        },
        [setFieldValue, values.authScopes, values.authEnabled, values.functionCode]
    );

    const handleAddCustomScope = useCallback(() => {
        const trimmedScope = customScopeInput.trim();
        if (!trimmedScope) return;

        addScope(trimmedScope);
        setCustomScopeInput('');
    }, [customScopeInput, addScope]);

    const handleRemoveScope = useCallback(
        (scopeToRemove: string) => {
            setFieldValue(
                'authScopes',
                values.authScopes.filter(s => s.scope !== scopeToRemove)
            );
        },
        [setFieldValue, values.authScopes]
    );

    const handleVariableNameChange = useCallback(
        (scopeUrl: string, newVariableName: string) => {
            setFieldValue(
                'authScopes',
                values.authScopes.map(s => (s.scope === scopeUrl ? { ...s, variableName: newVariableName } : s))
            );
        },
        [setFieldValue, values.authScopes]
    );

    const handleKeyDown = useCallback(
        (e: React.KeyboardEvent<HTMLInputElement>) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                handleAddCustomScope();
            }
        },
        [handleAddCustomScope]
    );

    const snippetPreview = getAuthSnippetCode(values.authScopes);

    return (
        <div className={styles.authPanel}>
            {/* Header Section */}
            <div className={styles.authSection}>
                <div className={styles.authHeaderRow}>
                    <Text size={400} weight="semibold">
                        {intl.formatMessage(SreAgentResources.pythonToolAuthTitle)}
                    </Text>
                    <Tooltip content={intl.formatMessage(SreAgentResources.pythonToolAuthToggleDescription)} relationship="description">
                        <Info16Regular className={styles.infoIcon} />
                    </Tooltip>
                </div>
            </div>

            {/* Enable Toggle */}
            <div className={styles.authSection}>
                <Field>
                    <div className={styles.authToggleRow}>
                        <Switch
                            checked={values.authEnabled}
                            onChange={handleAuthToggle}
                            disabled={isGenerating}
                            label={intl.formatMessage(SreAgentResources.pythonToolAuthToggleLabel)}
                        />
                    </div>
                </Field>
                <Text size={200} className={styles.authDescription}>
                    {intl.formatMessage(SreAgentResources.pythonToolAuthToggleDescription)}
                </Text>
            </div>

            {/* Scopes Section - Only show when auth is enabled */}
            {values.authEnabled && (
                <>
                    <div className={styles.authDivider} />

                    <div className={styles.authSection}>
                        <Label weight="semibold">{intl.formatMessage(SreAgentResources.pythonToolAuthScopesTitle)}</Label>
                        <Text size={200} className={styles.authDescription}>
                            {intl.formatMessage(SreAgentResources.pythonToolAuthScopesDescription)}
                        </Text>

                        {/* Quick Add Presets */}
                        <div className={styles.authPresetRow}>
                            <Text size={200}>{intl.formatMessage(SreAgentResources.pythonToolAuthQuickAddLabel)}</Text>
                            <div className={styles.authPresetButtons}>
                                {AUTH_SCOPE_PRESETS.map(preset => (
                                    <Tooltip key={preset.id} content={preset.description} relationship="description">
                                        <Button
                                            size="small"
                                            appearance="secondary"
                                            icon={<Add16Regular />}
                                            onClick={() => addScope(preset.scope)}
                                            disabled={values.authScopes.some(s => s.scope === preset.scope) || isGenerating}
                                        >
                                            {preset.label}
                                        </Button>
                                    </Tooltip>
                                ))}
                            </div>
                        </div>

                        {/* Configured Scopes List */}
                        {values.authScopes.length > 0 && (
                            <div className={styles.authScopesList}>
                                <Label size="small">{intl.formatMessage(SreAgentResources.pythonToolAuthConfiguredScopes)}</Label>
                                {values.authScopes.map((scopeConfig, index) => {
                                    const presetInfo = getScopePresetInfo(scopeConfig.scope);
                                    const displayName =
                                        presetInfo?.label || intl.formatMessage(SreAgentResources.pythonToolAuthCustomScope);
                                    return (
                                        <div key={scopeConfig.scope} className={styles.authScopeItem}>
                                            <div className={styles.authScopeText}>
                                                <div className={styles.authScopeHeader}>
                                                    <Text className={styles.authScopeName}>{displayName}</Text>
                                                    {index === 0 && (
                                                        <span className={styles.authPrimaryBadge}>
                                                            {intl.formatMessage(SreAgentResources.pythonToolAuthPrimaryBadge)}
                                                        </span>
                                                    )}
                                                </div>
                                                <Text className={styles.authScopeUrl}>{scopeConfig.scope}</Text>
                                                <div className={styles.authVariableRow}>
                                                    <Label size="small">
                                                        {intl.formatMessage(SreAgentResources.pythonToolAuthVariableName)}
                                                    </Label>
                                                    <Input
                                                        size="small"
                                                        value={scopeConfig.variableName}
                                                        onChange={(_, data) => handleVariableNameChange(scopeConfig.scope, data.value)}
                                                        disabled={isGenerating}
                                                        className={styles.authVariableInput}
                                                    />
                                                </div>
                                            </div>
                                            <Button
                                                size="small"
                                                appearance="subtle"
                                                icon={<Dismiss16Regular />}
                                                onClick={() => handleRemoveScope(scopeConfig.scope)}
                                                disabled={isGenerating}
                                                aria-label={intl.formatMessage(SreAgentResources.pythonToolAuthRemoveScope)}
                                            />
                                        </div>
                                    );
                                })}
                            </div>
                        )}

                        {/* Custom Scope Input */}
                        <div className={styles.authCustomScopeRow}>
                            <Input
                                size="small"
                                placeholder={intl.formatMessage(SreAgentResources.pythonToolAuthCustomScopePlaceholder)}
                                value={customScopeInput}
                                onChange={(_, data) => setCustomScopeInput(data.value)}
                                onKeyDown={handleKeyDown}
                                disabled={isGenerating}
                                className={styles.authCustomScopeInput}
                            />
                            <Button
                                size="small"
                                appearance="secondary"
                                icon={<Add16Regular />}
                                onClick={handleAddCustomScope}
                                disabled={!customScopeInput.trim() || isGenerating}
                            >
                                {intl.formatMessage(SreAgentResources.pythonToolAuthAddCustomScope)}
                            </Button>
                        </div>
                    </div>

                    <div className={styles.authDivider} />

                    {/* Code Snippet Preview */}
                    <div className={styles.authSection}>
                        <Label weight="semibold">{intl.formatMessage(SreAgentResources.pythonToolAuthSnippetTitle)}</Label>
                        <div className={styles.authSnippetPreview}>
                            <pre className={styles.authSnippetCode}>{snippetPreview}</pre>
                        </div>
                        <MessageBar intent="info" className={styles.authSnippetInfo}>
                            <MessageBarBody>{intl.formatMessage(SreAgentResources.pythonToolAuthSnippetInfo)}</MessageBarBody>
                        </MessageBar>
                    </div>
                </>
            )}

            {/* Validation Warning */}
            {values.authEnabled && values.authScopes.length === 0 && (
                <MessageBar intent="warning" className={styles.authWarning}>
                    <MessageBarBody>{intl.formatMessage(SreAgentResources.pythonToolAuthScopeRequired)}</MessageBarBody>
                </MessageBar>
            )}
        </div>
    );
};

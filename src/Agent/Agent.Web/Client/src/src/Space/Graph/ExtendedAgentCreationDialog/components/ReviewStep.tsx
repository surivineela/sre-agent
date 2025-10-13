import { Button, MessageBar, MessageBarBody, Text } from '@fluentui/react-components';
import { Copy24Regular, Edit24Regular } from '@fluentui/react-icons';
import Editor from '@monaco-editor/react';
import yaml from 'js-yaml';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { IntlShape } from 'react-intl';
import { ThemeMode } from '../../../../Common/AzPortalProxy/Models/ITheme';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { buildAgentConfigurationYaml, buildMetaAgentYaml, buildToolListYaml } from '../../ExtendedAgentYamlUtils';
import { useCreationDialogStyles } from '../styles';
import { CreationState } from '../types';

// Helper function to sanitize data for YAML
const sanitizeForYaml = (value: any): any => {
    if (value === null || value === undefined) {
        return undefined;
    }

    if (typeof value === 'string') {
        const trimmed = value.trim();
        return trimmed === '' ? undefined : trimmed;
    }

    if (typeof value === 'number' || typeof value === 'boolean') {
        return value;
    }

    if (Array.isArray(value)) {
        const filtered = value.map(sanitizeForYaml).filter(item => item !== undefined);
        return filtered.length === 0 ? undefined : filtered;
    }

    if (typeof value === 'object') {
        const sanitized: Record<string, any> = {};
        let hasValues = false;

        for (const [key, val] of Object.entries(value)) {
            const sanitizedValue = sanitizeForYaml(val);
            if (sanitizedValue !== undefined) {
                sanitized[key] = sanitizedValue;
                hasValues = true;
            }
        }

        return hasValues ? sanitized : undefined;
    }

    return value === undefined ? undefined : value;
};

interface ReviewStepProps {
    state: CreationState;
    intl: IntlShape;
    relationshipSummary?: string;
    relationshipSummaryHeading?: string;
    onEditYaml?: (yaml: string) => void;
    onYamlEditingChange?: (isEditing: boolean) => void;
}

export const ReviewStep: FC<ReviewStepProps> = ({
    state,
    intl,
    relationshipSummary,
    relationshipSummaryHeading,
    onEditYaml,
    onYamlEditingChange,
}) => {
    const styles = useCreationDialogStyles();
    const { theme } = useContext(EnvironmentContext);
    const isDarkTheme = theme?.mode === ThemeMode.Dark || theme?.name === 'dark';
    const editorTheme = isDarkTheme ? 'vs-dark' : 'vs-light';

    const previewData = useMemo(() => {
        const createDeploymentManifest = (kind: string, spec: any) => ({
            api_version: 'azuresre.ai/v1',
            kind,
            spec: sanitizeForYaml(spec) || {},
        });

        switch (state.entityType) {
            case 'agent':
                // Use the specialized agent YAML generator to match the meta agent format
                return buildAgentConfigurationYaml(state.agent || {});
            case 'tool':
                // Use the specialized tool YAML generator for flat structure
                return buildToolListYaml(state.tool || {});
            case 'connector':
                return createDeploymentManifest('ConnectorList', state.connector);
            case 'trigger':
                return createDeploymentManifest('TriggerConfiguration', state.trigger);
            default:
                return undefined;
        }
    }, [state]);

    const metaAgentYaml = buildMetaAgentYaml();
    const metaAgentOverrideEnabled = Boolean((state.agent as any)?.metaAgentOverride);

    const reviewTitle = useMemo(() => {
        switch (state.entityType) {
            case 'agent':
                return intl.formatMessage(ExtendedAgentsGraphResources.reviewYourAgent);
            case 'tool':
                return intl.formatMessage(ExtendedAgentsGraphResources.reviewYourTool);
            case 'connector':
                return intl.formatMessage(ExtendedAgentsGraphResources.reviewYourConnector);
            case 'trigger':
                return intl.formatMessage(ExtendedAgentsGraphResources.reviewYourTrigger);
            default:
                return intl.formatMessage(ExtendedAgentsGraphResources.reviewYourAgent);
        }
    }, [intl, state.entityType]);

    const infoMessage = useMemo(() => {
        if (state.entityType === 'agent') {
            return intl.formatMessage(ExtendedAgentsGraphResources.extendedAgentDeletionReminder);
        }
        return null;
    }, [intl, state.entityType]);

    const previewUnavailableMessage = useMemo(() => intl.formatMessage(ExtendedAgentsGraphResources.reviewYamlPreviewUnavailable), [intl]);

    const previewYaml = useMemo(() => {
        if (!previewData) {
            return previewUnavailableMessage;
        }

        let baseYaml = '';

        try {
            // If previewData is already a string (from buildToolListYaml), use it directly
            if (typeof previewData === 'string') {
                baseYaml = previewData;
            } else {
                const sanitized = sanitizeForYaml(previewData);

                if (
                    sanitized === undefined ||
                    sanitized === null ||
                    (typeof sanitized === 'object' &&
                        !Array.isArray(sanitized) &&
                        Object.keys(sanitized as Record<string, unknown>).length === 0)
                ) {
                    return previewUnavailableMessage;
                }

                baseYaml = yaml.dump(sanitized, { noRefs: true, lineWidth: 120 });
            }

            // If this is an agent and meta agent override is enabled, append the meta agent YAML
            if (state.entityType === 'agent' && metaAgentOverrideEnabled) {
                return baseYaml + '\n' + metaAgentYaml;
            }

            return baseYaml;
        } catch (error) {
            console.error('Failed to generate YAML preview', error);
            return previewUnavailableMessage;
        }
    }, [previewData, previewUnavailableMessage, state.entityType, metaAgentOverrideEnabled, metaAgentYaml]);

    const splitYamlDocuments = useCallback((yamlContent: string): string[] => {
        const trimmed = yamlContent?.trim();
        if (!trimmed) {
            return [];
        }

        const segments = trimmed.split(/^---\s*$/gm);
        const documents: string[] = [];

        let index = 0;
        for (const segment of segments) {
            const content = segment.trim();
            if (!content) {
                continue;
            }

            const needsSeparatorPrefix = index > 0 || trimmed.startsWith('---');
            documents.push(`${needsSeparatorPrefix ? '---\n' : ''}${content}`);
            index += 1;
        }

        return documents;
    }, []);

    const previewDocuments = useMemo(() => {
        if (previewYaml === previewUnavailableMessage) {
            return [];
        }

        return splitYamlDocuments(previewYaml);
    }, [previewUnavailableMessage, previewYaml, splitYamlDocuments]);

    const canCopy = useMemo(() => {
        if (previewYaml === previewUnavailableMessage) {
            return false;
        }
        return typeof navigator !== 'undefined' && !!navigator.clipboard;
    }, [previewUnavailableMessage, previewYaml]);

    const [copySuccess, setCopySuccess] = useState(false);
    const [isEditing, setIsEditing] = useState(false);
    const [editedYaml, setEditedYaml] = useState('');

    const isPreviewAvailable = previewYaml !== previewUnavailableMessage;
    const editorHeight = useMemo(() => {
        if (!isEditing) {
            return 360;
        }

        const lineCount = editedYaml?.split('\n').length ?? 1;
        const rowHeight = 20;
        return Math.min(Math.max(lineCount * rowHeight + 120, 240), 600);
    }, [editedYaml, isEditing]);

    useEffect(() => {
        if (!copySuccess) {
            return;
        }
        const timeout = setTimeout(() => setCopySuccess(false), 2000);
        return () => clearTimeout(timeout);
    }, [copySuccess]);

    useEffect(() => {
        if (!onYamlEditingChange) {
            return;
        }

        onYamlEditingChange(isEditing);

        return () => {
            onYamlEditingChange(false);
        };
    }, [isEditing, onYamlEditingChange]);

    const handleCopy = useCallback(async () => {
        if (!canCopy) {
            return;
        }

        try {
            await navigator.clipboard.writeText(isEditing ? editedYaml : previewYaml);
            setCopySuccess(true);
        } catch (error) {
            console.error('Failed to copy YAML preview', error);
        }
    }, [canCopy, editedYaml, isEditing, previewYaml]);

    const handleEditYaml = useCallback(() => {
        if (!isPreviewAvailable) {
            return;
        }

        setIsEditing(true);
        setEditedYaml(previewYaml);
    }, [isPreviewAvailable, previewYaml]);

    const handleSaveYaml = useCallback(() => {
        if (onEditYaml && editedYaml) {
            onEditYaml(editedYaml);
        }
        setIsEditing(false);
    }, [onEditYaml, editedYaml]);

    const handleCancelEdit = useCallback(() => {
        setIsEditing(false);
        setEditedYaml('');
    }, []);

    return (
        <div className={styles.formSection}>
            {relationshipSummary && (
                <div className={styles.relationshipSummaryContainer}>
                    <Text className={styles.relationshipSummaryTitle}>
                        {relationshipSummaryHeading || intl.formatMessage(ExtendedAgentsGraphResources.relationshipSummaryTitle)}
                    </Text>
                    <div className={styles.relationshipSummaryChip}>{relationshipSummary}</div>
                </div>
            )}

            {infoMessage && (
                <MessageBar intent="info" role="status">
                    <MessageBarBody>{infoMessage}</MessageBarBody>
                </MessageBar>
            )}

            <div className={styles.previewSection}>
                <div className={styles.previewHeader}>
                    <div className={styles.previewTitle}>{reviewTitle}</div>
                    <div style={{ display: 'flex', gap: '8px' }}>
                        {isEditing ? (
                            <>
                                <Button appearance="primary" size="small" onClick={handleSaveYaml}>
                                    {intl.formatMessage(SreAgentResources.save)}
                                </Button>
                                <Button appearance="subtle" size="small" onClick={handleCancelEdit}>
                                    {intl.formatMessage(SreAgentResources.cancel)}
                                </Button>
                            </>
                        ) : (
                            <>
                                <Button
                                    appearance="subtle"
                                    size="small"
                                    icon={<Edit24Regular />}
                                    onClick={handleEditYaml}
                                    disabled={previewYaml === previewUnavailableMessage}
                                    aria-label={intl.formatMessage(ExtendedAgentsGraphResources.yamlOpenButton)}
                                >
                                    {intl.formatMessage(ExtendedAgentsGraphResources.yamlOpenButton)}
                                </Button>
                                <Button
                                    appearance="subtle"
                                    size="small"
                                    icon={<Copy24Regular />}
                                    className={styles.previewCopyButton}
                                    onClick={handleCopy}
                                    disabled={!canCopy}
                                    aria-label={intl.formatMessage(SreAgentResources.copyToClipboard)}
                                >
                                    {copySuccess
                                        ? intl.formatMessage(SreAgentResources.copied)
                                        : intl.formatMessage(SreAgentResources.copy)}
                                </Button>
                            </>
                        )}
                    </div>
                </div>
                {isPreviewAvailable ? (
                    isEditing ? (
                        <div className={styles.previewEditorContainer} style={{ height: `${editorHeight}px` }}>
                            <Editor
                                className={styles.previewEditor}
                                value={editedYaml}
                                defaultLanguage="yaml"
                                language="yaml"
                                theme={editorTheme}
                                onChange={value => {
                                    setEditedYaml(value ?? '');
                                }}
                                options={{
                                    readOnly: false,
                                    minimap: { enabled: false },
                                    wordWrap: 'on',
                                    lineNumbers: 'on',
                                    glyphMargin: false,
                                    folding: false,
                                    scrollBeyondLastLine: false,
                                    renderLineHighlight: 'none',
                                    automaticLayout: true,
                                    fontSize: 14,
                                }}
                                height={`${editorHeight}px`}
                            />
                        </div>
                    ) : previewDocuments.length > 1 ? (
                        previewDocuments.map((doc, index) => (
                            <div key={index} style={{ marginBottom: index < previewDocuments.length - 1 ? '12px' : 0 }}>
                                <Text style={{ fontWeight: 600 }}>{`Document ${index + 1}`}</Text>
                                <pre className={styles.previewContent}>{doc}</pre>
                            </div>
                        ))
                    ) : (
                        <pre className={styles.previewContent}>{previewDocuments[0] ?? previewYaml}</pre>
                    )
                ) : (
                    <pre className={styles.previewContent}>{previewUnavailableMessage}</pre>
                )}
            </div>
        </div>
    );
};

import { useTheme } from '@fluentui/react';
import type { DialogOpenChangeData, DialogOpenChangeEvent } from '@fluentui/react-components';
import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    makeStyles,
    MessageBar,
    MessageBarBody,
    MessageBarTitle,
    Text,
    tokens,
} from '@fluentui/react-components';
import MonacoEditor, { type OnMount } from '@monaco-editor/react';
import yaml from 'js-yaml';
import type { SyntheticEvent } from 'react';
import { memo, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedConnector, ExtendedTool } from '../Contracts/ExtendedAgentGraph';
import { convertExtendedEntityToYaml, ExtendedEntityType } from './ExtendedAgentYamlUtils';

type ExtendedEntity = ExtendedAgent | ExtendedTool | ExtendedConnector;

type ExtendedEntityYamlEditorProps = {
    entity?: ExtendedEntity;
    entityType: ExtendedEntityType;
    sreAgentEndpoint: string;
    isOpen: boolean;
    onClose: () => void;
    onApplied?: () => Promise<void> | void;
};

const expectedKindByType: Record<ExtendedEntityType, string> = {
    agent: 'AgentConfiguration',
    tool: 'Tool',
    connector: 'ConnectorList',
    trigger: 'TriggerConfiguration',
};

const collectionKeyByType: Partial<Record<ExtendedEntityType, 'tools' | 'connectors'>> = {
    tool: 'tools',
    connector: 'connectors',
};

const useStyles = makeStyles({
    dialogSurface: {
        maxWidth: '90vw',
        maxHeight: '90vh',
        width: '800px',
        height: '700px',
    },
    dialogBody: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
    },
    dialogContent: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        flex: 1,
        overflow: 'hidden',
    },
    editorContainer: {
        flex: 1,
        minHeight: '400px',
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'hidden',
        backgroundColor: tokens.colorNeutralBackground1,
        display: 'flex',
    },
    statusRow: {
        display: 'flex',
        justifyContent: 'flex-start',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    statusText: {
        color: tokens.colorNeutralForeground3,
    },
});

const getEntityLabel = (entityType: ExtendedEntityType, intl: ReturnType<typeof useIntl>) => {
    switch (entityType) {
        case 'agent':
            return intl.formatMessage(ExtendedAgentsGraphResources.agentLowercase);
        case 'tool':
            return intl.formatMessage(ExtendedAgentsGraphResources.toolLowercase);
        case 'connector':
        default:
            return intl.formatMessage(ExtendedAgentsGraphResources.connector);
    }
};

const getCollectionLabel = (entityType: ExtendedEntityType, intl: ReturnType<typeof useIntl>) => {
    switch (entityType) {
        case 'tool':
            return intl.formatMessage(ExtendedAgentsGraphResources.toolsCollectionName);
        case 'connector':
            return intl.formatMessage(ExtendedAgentsGraphResources.connectorsCollectionName);
        default:
            return '';
    }
};

export const ExtendedEntityYamlEditor = memo(
    ({ entity, entityType, sreAgentEndpoint, isOpen, onClose, onApplied }: ExtendedEntityYamlEditorProps) => {
        const styles = useStyles();
        const theme = useTheme();
        const intl = useIntl();

        const [yamlValue, setYamlValue] = useState('');
        const [isDirty, setIsDirty] = useState(false);
        const [isSaving, setIsSaving] = useState(false);
        const [errorMessage, setErrorMessage] = useState<string>();
        const [successMessage, setSuccessMessage] = useState<string>();

        const expectedKind = expectedKindByType[entityType];
        const collectionKey = collectionKeyByType[entityType];
        const entityLabel = useMemo(() => getEntityLabel(entityType, intl), [entityType, intl]);
        const collectionLabel = useMemo(
            () => (collectionKey ? getCollectionLabel(entityType, intl) : ''),
            [collectionKey, entityType, intl]
        );

        const entityName = (entity as { name?: string } | undefined)?.name;

        const handleEditorDidMount = useCallback<OnMount>(editorInstance => {
            editorInstance.layout();
        }, []);

        const initialYaml = useMemo(() => {
            if (!entity) {
                return '';
            }

            try {
                return convertExtendedEntityToYaml(
                    entity as Partial<ExtendedAgent> | Partial<ExtendedTool> | Partial<ExtendedConnector>,
                    entityType
                );
            } catch (error) {
                console.error('Failed to build YAML for entity.', error);
                return '';
            }
        }, [entity, entityType]);

        useEffect(() => {
            if (!isOpen) {
                return;
            }

            setYamlValue(initialYaml);
            setIsDirty(false);
            setErrorMessage(undefined);
            setSuccessMessage(undefined);
        }, [initialYaml, isOpen]);

        const handleEditorChange = useCallback(
            (value?: string) => {
                const newValue = value ?? '';
                setYamlValue(newValue);
                setIsDirty(newValue !== initialYaml);
                setErrorMessage(undefined);
                setSuccessMessage(undefined);
            },
            [initialYaml]
        );

        const resetState = useCallback(() => {
            setYamlValue(initialYaml);
            setIsDirty(false);
            setErrorMessage(undefined);
            setSuccessMessage(undefined);
        }, [initialYaml]);

        const requestClose = useCallback(
            (event?: SyntheticEvent | Event) => {
                if (isDirty) {
                    const shouldClose = window.confirm(intl.formatMessage(ExtendedAgentsGraphResources.yamlUnsavedChanges));
                    if (!shouldClose) {
                        if (event && 'preventDefault' in event) {
                            event.preventDefault();
                        }
                        return;
                    }
                }

                resetState();
                onClose();
            },
            [intl, isDirty, onClose, resetState]
        );

        const handleSave = useCallback(async () => {
            if (!entity) {
                return;
            }

            try {
                setIsSaving(true);
                setErrorMessage(undefined);
                setSuccessMessage(undefined);

                let parsed: unknown;
                try {
                    parsed = yaml.load(yamlValue || '');
                } catch (parseError) {
                    const message = parseError instanceof Error ? parseError.message : String(parseError);
                    setErrorMessage(intl.formatMessage(ExtendedAgentsGraphResources.yamlValidationInvalid, { message }));
                    return;
                }

                if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
                    setErrorMessage(
                        intl.formatMessage(ExtendedAgentsGraphResources.yamlValidationInvalid, {
                            message: 'Root must be a mapping.',
                        })
                    );
                    return;
                }

                const document = parsed as Record<string, unknown>;
                const kind = typeof document.kind === 'string' ? document.kind : undefined;

                if (!kind || kind !== expectedKind) {
                    setErrorMessage(
                        intl.formatMessage(ExtendedAgentsGraphResources.yamlValidationKindMissing, {
                            expectedKind,
                        })
                    );
                    return;
                }

                if (!document.spec || typeof document.spec !== 'object') {
                    setErrorMessage(intl.formatMessage(ExtendedAgentsGraphResources.yamlValidationSpecMissing));
                    return;
                }

                const spec = document.spec as Record<string, unknown>;

                if (entityType === 'agent') {
                    const name = spec.name;
                    if (!name || typeof name !== 'string' || name.trim().length === 0) {
                        setErrorMessage(
                            intl.formatMessage(ExtendedAgentsGraphResources.yamlValidationNameMissing, {
                                entityLabel,
                            })
                        );
                        return;
                    }
                } else if (collectionKey) {
                    const collection = spec[collectionKey] as unknown;

                    if (!Array.isArray(collection) || collection.length === 0) {
                        setErrorMessage(
                            intl.formatMessage(ExtendedAgentsGraphResources.yamlValidationCollectionMissing, {
                                collectionName: collectionLabel,
                            })
                        );
                        return;
                    }

                    const primaryEntry = collection[0] as Record<string, unknown> | undefined;

                    if (!primaryEntry || typeof primaryEntry !== 'object') {
                        setErrorMessage(
                            intl.formatMessage(ExtendedAgentsGraphResources.yamlValidationCollectionMissing, {
                                collectionName: collectionLabel,
                            })
                        );
                        return;
                    }

                    const entryName = primaryEntry.name;
                    if (!entryName || typeof entryName !== 'string' || entryName.trim().length === 0) {
                        setErrorMessage(
                            intl.formatMessage(ExtendedAgentsGraphResources.yamlValidationCollectionNameMissing, {
                                entityLabel,
                            })
                        );
                        return;
                    }

                    const entryType = primaryEntry.type;
                    if (!entryType || typeof entryType !== 'string' || entryType.trim().length === 0) {
                        setErrorMessage(
                            intl.formatMessage(ExtendedAgentsGraphResources.yamlValidationCollectionTypeMissing, {
                                entityLabel,
                            })
                        );
                        return;
                    }
                }

                const agentHeaders = getAgentHeaders();
                const { 'Content-Type': _, ...headersWithoutContentType } = agentHeaders;

                const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/apply`, {
                    method: 'PUT',
                    headers: {
                        ...headersWithoutContentType,
                        'Content-Type': 'application/x-yaml',
                    },
                    body: yamlValue,
                });

                if (!response.ok) {
                    let message = response.statusText;
                    try {
                        const text = await response.text();
                        if (text) {
                            const maybeJson = JSON.parse(text);
                            message = maybeJson?.message ?? text;
                        }
                    } catch {
                        // Ignore failures when parsing non-JSON responses.
                    }

                    setErrorMessage(intl.formatMessage(ExtendedAgentsGraphResources.yamlSaveError, { message }));
                    return;
                }

                setSuccessMessage(intl.formatMessage(ExtendedAgentsGraphResources.yamlSaveSuccess));
                setIsDirty(false);

                if (onApplied) {
                    await onApplied();
                }
            } catch (error) {
                const message = error instanceof Error ? error.message : String(error);
                setErrorMessage(intl.formatMessage(ExtendedAgentsGraphResources.yamlSaveError, { message }));
            } finally {
                setIsSaving(false);
            }
        }, [collectionKey, collectionLabel, entity, entityLabel, entityType, expectedKind, intl, onApplied, sreAgentEndpoint, yamlValue]);

        const handleDialogOpenChange = useCallback(
            (event: DialogOpenChangeEvent, data: DialogOpenChangeData) => {
                if (data.open) {
                    return;
                }

                requestClose(event);
            },
            [requestClose]
        );

        if (!isOpen) {
            return null;
        }

        return (
            <Dialog open={isOpen} onOpenChange={handleDialogOpenChange} modalType="modal">
                <DialogSurface className={styles.dialogSurface}>
                    <DialogBody className={styles.dialogBody}>
                        <DialogTitle>
                            {intl.formatMessage(ExtendedAgentsGraphResources.yamlDialogTitle)}
                            {entityName ? ` · ${entityName}` : ''}
                        </DialogTitle>
                        <DialogContent className={styles.dialogContent}>
                            {!entity && (
                                <Text className={styles.statusText}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.yamlEmptyState, { entityLabel })}
                                </Text>
                            )}
                            {entity && (
                                <div className={styles.content}>
                                    <Text className={styles.statusText}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.yamlEditorDescription, { entityLabel })}
                                    </Text>
                                    {errorMessage && (
                                        <MessageBar intent="error">
                                            <MessageBarBody>
                                                <MessageBarTitle>
                                                    {intl.formatMessage(ExtendedAgentsGraphResources.yamlErrorTitle)}
                                                </MessageBarTitle>
                                                {errorMessage}
                                            </MessageBarBody>
                                        </MessageBar>
                                    )}
                                    {successMessage && (
                                        <MessageBar intent="success">
                                            <MessageBarBody>{successMessage}</MessageBarBody>
                                        </MessageBar>
                                    )}
                                    <div className={styles.editorContainer}>
                                        <MonacoEditor
                                            value={yamlValue}
                                            onChange={handleEditorChange}
                                            language="yaml"
                                            theme={theme.isInverted ? 'vs-dark' : 'vs'}
                                            onMount={handleEditorDidMount}
                                            height="100%"
                                            width="100%"
                                            options={{
                                                automaticLayout: true,
                                                fontSize: 14,
                                                minimap: { enabled: false },
                                                scrollBeyondLastLine: false,
                                                wordWrap: 'on',
                                                formatOnPaste: true,
                                                formatOnType: true,
                                                tabSize: 2,
                                            }}
                                        />
                                    </div>
                                    <div className={styles.statusRow}>
                                        {isDirty && (
                                            <Text className={styles.statusText}>
                                                {intl.formatMessage(ExtendedAgentsGraphResources.yamlUnsavedChanges)}
                                            </Text>
                                        )}
                                        {successMessage && !isDirty && <Text className={styles.statusText}>{successMessage}</Text>}
                                    </div>
                                </div>
                            )}
                        </DialogContent>
                        <DialogActions>
                            <Button appearance="secondary" onClick={resetState} disabled={!entity || !isDirty || isSaving}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.yamlResetButton)}
                            </Button>
                            <Button appearance="primary" onClick={handleSave} disabled={!entity || isSaving}>
                                {isSaving
                                    ? intl.formatMessage(ExtendedAgentsGraphResources.yamlSavingLabel)
                                    : intl.formatMessage(ExtendedAgentsGraphResources.yamlSaveButton)}
                            </Button>
                            <Button appearance="outline" onClick={requestClose} disabled={isSaving}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.yamlCloseButton)}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        );
    }
);

ExtendedEntityYamlEditor.displayName = 'ExtendedEntityYamlEditor';

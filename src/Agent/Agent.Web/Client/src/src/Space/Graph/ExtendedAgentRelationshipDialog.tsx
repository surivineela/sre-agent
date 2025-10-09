import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Dropdown,
    Field,
    MessageBar,
    MessageBarBody,
    Option,
    Text,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import { FC, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedTool } from '../Contracts/ExtendedAgentGraph';

type OperationResult = {
    success: boolean;
    message: string;
};

const useRelationshipDialogStyles = makeStyles({
    surface: {
        maxWidth: '720px',
        width: '90vw',
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    row: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalM,
        alignItems: 'flex-end',
    },
    field: {
        flex: 1,
        minWidth: '220px',
    },
    formGrid: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        padding: tokens.spacingHorizontalM,
    },
    formHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    actionsRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
    },
    messageStack: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
});

interface ExtendedAgentRelationshipDialogProps {
    open: boolean;
    agent?: ExtendedAgent;
    onOpenChange: (open: boolean) => void;
    existingAgents: ExtendedAgent[];
    existingTools: ExtendedTool[];
    onAddHandoff: (handoffAgentName: string) => Promise<OperationResult>;
    onAddTool: (toolName: string) => Promise<OperationResult>;
    onLaunchCreateEntity?: (type: 'agent' | 'tool', sourceAgentName: string) => void;
    initialAction?: 'handoff' | 'tool';
}

export const ExtendedAgentRelationshipDialog: FC<ExtendedAgentRelationshipDialogProps> = ({
    open,
    agent,
    onOpenChange,
    existingAgents,
    existingTools,
    onAddHandoff,
    onAddTool,
    onLaunchCreateEntity,
    initialAction,
}) => {
    const styles = useRelationshipDialogStyles();
    const intl = useIntl();

    const [selectedHandoff, setSelectedHandoff] = useState<string>();
    const [selectedTool, setSelectedTool] = useState<string>();
    const [status, setStatus] = useState<{ intent: 'success' | 'error' | 'info'; message: string }>();
    const [busy, setBusy] = useState({ handoff: false, tool: false });

    const showHandoffSection = !initialAction || initialAction === 'handoff';
    const showToolSection = !initialAction || initialAction === 'tool';
    const showCreationSection = !initialAction;
    const hasExistingSection = showHandoffSection || showToolSection;

    useEffect(() => {
        if (!open) {
            return;
        }

        setSelectedHandoff(undefined);
        setSelectedTool(undefined);
        setStatus(undefined);
        setBusy({ handoff: false, tool: false });
    }, [open, agent?.name]);

    useEffect(() => {
        if (!open || !initialAction || status) {
            return;
        }

        const message =
            initialAction === 'handoff'
                ? intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickActionAddHandoffInfo)
                : intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickActionAddToolInfo);
        setStatus({ intent: 'info', message });
    }, [open, initialAction, intl, status]);

    const availableHandoffs = useMemo(() => {
        if (!agent) {
            return [] as string[];
        }

        const current = new Set(agent.handoffs ?? []);
        return existingAgents
            .map(existing => existing.name)
            .filter((name): name is string => !!name && name !== agent.name && !current.has(name));
    }, [agent, existingAgents]);

    const availableTools = useMemo(() => {
        if (!agent) {
            return [] as string[];
        }

        const current = new Set(agent.tools ?? []);
        return existingTools.map(tool => tool.name).filter((name): name is string => !!name && !current.has(name));
    }, [agent, existingTools]);

    const notify = useCallback((intent: 'success' | 'error' | 'info', message: string) => {
        setStatus({ intent, message });
    }, []);

    const handleAddHandoff = useCallback(async () => {
        if (!selectedHandoff) {
            return;
        }

        setBusy(prev => ({ ...prev, handoff: true }));
        try {
            const result = await onAddHandoff(selectedHandoff);
            notify(result.success ? 'success' : 'error', result.message);
            if (result.success) {
                setSelectedHandoff(undefined);
            }
        } catch (error) {
            const message = error instanceof Error ? error.message : String(error);
            notify('error', message);
        } finally {
            setBusy(prev => ({ ...prev, handoff: false }));
        }
    }, [notify, onAddHandoff, selectedHandoff]);

    const handleAddTool = useCallback(async () => {
        if (!selectedTool) {
            return;
        }

        setBusy(prev => ({ ...prev, tool: true }));
        try {
            const result = await onAddTool(selectedTool);
            notify(result.success ? 'success' : 'error', result.message);
            if (result.success) {
                setSelectedTool(undefined);
            }
        } catch (error) {
            const message = error instanceof Error ? error.message : String(error);
            notify('error', message);
        } finally {
            setBusy(prev => ({ ...prev, tool: false }));
        }
    }, [notify, onAddTool, selectedTool]);

    return (
        <Dialog
            open={open}
            onOpenChange={(_, data) => {
                onOpenChange(data.open);
            }}
        >
            <DialogSurface className={styles.surface}>
                <DialogBody>
                    <DialogTitle>
                        {agent
                            ? intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickDialogTitle, {
                                  name: agent.name,
                              })
                            : intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickDialogTitleFallback)}
                    </DialogTitle>
                    <DialogContent className={styles.content}>
                        <div className={styles.messageStack}>
                            <Text>{intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickDialogDescription)}</Text>
                            <MessageBar intent="info">
                                <MessageBarBody>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickDelayNotice)}
                                </MessageBarBody>
                            </MessageBar>
                            {status && (
                                <MessageBar intent={status.intent}>
                                    <MessageBarBody>{status.message}</MessageBarBody>
                                </MessageBar>
                            )}
                        </div>

                        {!agent ? (
                            <MessageBar intent="warning">
                                <MessageBarBody>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickNoAgentSelected)}
                                </MessageBarBody>
                            </MessageBar>
                        ) : (
                            <>
                                {hasExistingSection && (
                                    <div className={styles.section}>
                                        <Text weight="semibold">
                                            {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickExistingTitle)}
                                        </Text>
                                        {showHandoffSection && (
                                            <div className={styles.row}>
                                                <Field
                                                    label={intl.formatMessage(ExtendedAgentsGraphResources.relationshipAddHandoffLabel)}
                                                    className={styles.field}
                                                >
                                                    <Dropdown
                                                        placeholder={intl.formatMessage(
                                                            ExtendedAgentsGraphResources.relationshipSelectAgent
                                                        )}
                                                        selectedOptions={selectedHandoff ? [selectedHandoff] : []}
                                                        onOptionSelect={(_, data) => setSelectedHandoff(data.optionValue as string)}
                                                    >
                                                        {availableHandoffs.map(name => (
                                                            <Option key={name} value={name}>
                                                                {name}
                                                            </Option>
                                                        ))}
                                                    </Dropdown>
                                                </Field>
                                                <Button
                                                    appearance="primary"
                                                    onClick={handleAddHandoff}
                                                    disabled={!selectedHandoff || busy.handoff}
                                                >
                                                    {intl.formatMessage(ExtendedAgentsGraphResources.relationshipAddButton)}
                                                </Button>
                                            </div>
                                        )}
                                        {showToolSection && (
                                            <div className={styles.row}>
                                                <Field
                                                    label={intl.formatMessage(ExtendedAgentsGraphResources.relationshipAddToolLabel)}
                                                    className={styles.field}
                                                >
                                                    <Dropdown
                                                        placeholder={intl.formatMessage(
                                                            ExtendedAgentsGraphResources.relationshipSelectTool
                                                        )}
                                                        selectedOptions={selectedTool ? [selectedTool] : []}
                                                        onOptionSelect={(_, data) => setSelectedTool(data.optionValue as string)}
                                                    >
                                                        {availableTools.map(name => (
                                                            <Option key={name} value={name}>
                                                                {name}
                                                            </Option>
                                                        ))}
                                                    </Dropdown>
                                                </Field>
                                                <Button appearance="primary" onClick={handleAddTool} disabled={!selectedTool || busy.tool}>
                                                    {intl.formatMessage(ExtendedAgentsGraphResources.relationshipAddButton)}
                                                </Button>
                                            </div>
                                        )}
                                    </div>
                                )}

                                {showCreationSection && onLaunchCreateEntity && agent && (
                                    <div className={styles.section}>
                                        <Text weight="semibold">
                                            {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickCreateTitle)}
                                        </Text>
                                        <Text>
                                            {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickCreateAgentReminder, {
                                                agentName: agent.name,
                                            })}
                                        </Text>
                                        <Text>
                                            {intl.formatMessage(ExtendedAgentsGraphResources.relationshipContextToolSubtext, {
                                                agentName: agent.name,
                                            })}
                                        </Text>
                                        <div className={styles.actionsRow}>
                                            <Button appearance="secondary" onClick={() => onLaunchCreateEntity('agent', agent.name)}>
                                                {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickCreateAgentHeader)}
                                            </Button>
                                            <Button appearance="secondary" onClick={() => onLaunchCreateEntity('tool', agent.name)}>
                                                {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickCreateToolHeader)}
                                            </Button>
                                        </div>
                                    </div>
                                )}
                            </>
                        )}
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="secondary" onClick={() => onOpenChange(false)}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.yamlCloseButton)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

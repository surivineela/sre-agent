import {
    CommandBar,
    ICommandBarItemProps,
    Icon,
    MessageBar,
    MessageBarType,
    Panel,
    PanelType,
    Separator,
    Stack,
    Text,
    TextField,
} from '@fluentui/react';
import { tokens } from '@fluentui/react-components';
import { FC, useCallback, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { ScheduledTasksResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ScheduledTask, UpdateScheduledTaskRequest } from '../Contracts/ScheduledTasks';

export interface ScheduledTaskDetailsPanelProps {
    isOpen: boolean;
    task: ScheduledTask;
    onDismiss: () => void;
    onTaskUpdated: () => void;
    updateTask: (id: string, updates: UpdateScheduledTaskRequest) => Promise<boolean>;
    deleteTask: (id: string) => Promise<boolean>;
    pauseTask: (id: string) => Promise<boolean>;
    resumeTask: (id: string) => Promise<boolean>;
}

const ScheduledTaskDetailsPanel: FC<ScheduledTaskDetailsPanelProps> = ({
    isOpen,
    task,
    onDismiss,
    onTaskUpdated,
    updateTask,
    deleteTask,
    pauseTask,
    resumeTask,
}) => {
    const intl = useIntl();
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [isEditing, setIsEditing] = useState(false);

    // Editable field state
    const [name, setName] = useState(task.name);
    const [description, setDescription] = useState(task.description);
    const [cronExpression, setCronExpression] = useState(task.cronExpression);
    const [agentPrompt, setAgentPrompt] = useState(task.agentPrompt);

    // Keep form state in sync when different task is selected / panel reopened
    useEffect(() => {
        setName(task.name);
        setDescription(task.description);
        setCronExpression(task.cronExpression);
        setAgentPrompt(task.agentPrompt);
        setIsEditing(false);
        setError(null);
    }, [task]);

    const handlePause = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const success = await pauseTask(task.id);
            if (success) {
                onTaskUpdated();
            } else {
                setError(intl.formatMessage(ScheduledTasksResources.failedToPauseTask));
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : intl.formatMessage(ScheduledTasksResources.failedToPauseTask));
        } finally {
            setLoading(false);
        }
    }, [task.id, pauseTask, onTaskUpdated, intl]);

    const handleResume = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const success = await resumeTask(task.id);
            if (success) {
                onTaskUpdated();
            } else {
                setError(intl.formatMessage(ScheduledTasksResources.failedToResumeTask));
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : intl.formatMessage(ScheduledTasksResources.failedToResumeTask));
        } finally {
            setLoading(false);
        }
    }, [task.id, resumeTask, onTaskUpdated, intl]);

    const handleDelete = useCallback(async () => {
        if (!window.confirm(intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskConfirmation))) {
            return;
        }

        setLoading(true);
        setError(null);
        try {
            const success = await deleteTask(task.id);
            if (success) {
                onTaskUpdated();
            } else {
                setError(intl.formatMessage(ScheduledTasksResources.failedToDeleteTask));
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : intl.formatMessage(ScheduledTasksResources.failedToDeleteTask));
        } finally {
            setLoading(false);
        }
    }, [task.id, deleteTask, onTaskUpdated, intl]);

    const getStatusIcon = (status: string) => {
        switch (status) {
            case 'Active':
                return { iconName: 'PlayResume', color: '#107c10' };
            case 'Paused':
                return { iconName: 'Pause', color: '#ff8c00' };
            case 'Completed':
                return { iconName: 'Completed', color: '#0078d4' };
            case 'Failed':
                return { iconName: 'ErrorBadge', color: '#d13438' };
            default:
                return { iconName: 'Unknown', color: '#666' };
        }
    };

    const statusIcon = getStatusIcon(task.status);

    const handleEnterEdit = () => {
        setIsEditing(true);
        setError(null);
    };

    const handleCancelEdit = () => {
        // revert to original
        setName(task.name);
        setDescription(task.description);
        setCronExpression(task.cronExpression);
        setAgentPrompt(task.agentPrompt);
        setIsEditing(false);
        setError(null);
    };

    const handleSaveEdit = useCallback(async () => {
        // Basic validation
        if (!name.trim()) {
            setError(intl.formatMessage(ScheduledTasksResources.nameRequired));
            return;
        }
        if (!cronExpression.trim()) {
            setError(intl.formatMessage(ScheduledTasksResources.cronExpressionRequired));
            return;
        }
        if (!agentPrompt.trim()) {
            setError(intl.formatMessage(ScheduledTasksResources.agentPromptRequired));
            return;
        }
        setLoading(true);
        setError(null);
        try {
            const success = await updateTask(task.id, {
                name: name.trim(),
                description: description.trim(),
                cronExpression: cronExpression.trim(),
                agentPrompt: agentPrompt.trim(),
            });
            if (success) {
                setIsEditing(false);
                onTaskUpdated();
            } else {
                setError(intl.formatMessage(ScheduledTasksResources.failedToUpdateTask));
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : intl.formatMessage(ScheduledTasksResources.failedToUpdateTask));
        } finally {
            setLoading(false);
        }
    }, [name, description, cronExpression, agentPrompt, updateTask, task.id, onTaskUpdated, intl]);

    const commandBarItems: ICommandBarItemProps[] = [];

    if (!isEditing) {
        // Edit action
        commandBarItems.push({
            key: 'edit',
            text: intl.formatMessage(ScheduledTasksResources.editAction),
            iconProps: { iconName: 'Edit' },
            onClick: handleEnterEdit,
            disabled: loading,
        });

        if (task.status === 'Active') {
            commandBarItems.push({
                key: 'pause',
                text: intl.formatMessage(ScheduledTasksResources.pause),
                iconProps: { iconName: 'Pause' },
                onClick: () => {
                    handlePause();
                },
                disabled: loading,
            });
        } else if (task.status === 'Paused') {
            commandBarItems.push({
                key: 'resume',
                text: intl.formatMessage(ScheduledTasksResources.resume),
                iconProps: { iconName: 'Play' },
                onClick: () => {
                    handleResume();
                },
                disabled: loading,
            });
        }

        commandBarItems.push({
            key: 'delete',
            text: intl.formatMessage(ScheduledTasksResources.deleteScheduledTask),
            iconProps: { iconName: 'Delete' },
            onClick: () => {
                handleDelete();
            },
            disabled: loading,
        });
    } else {
        // Save / Cancel actions when editing
        commandBarItems.push({
            key: 'save',
            text: intl.formatMessage(ScheduledTasksResources.saveAction),
            iconProps: { iconName: 'Save' },
            onClick: () => {
                void handleSaveEdit();
            },
            disabled: loading,
        });
        commandBarItems.push({
            key: 'cancel',
            text: intl.formatMessage(ScheduledTasksResources.cancelAction),
            iconProps: { iconName: 'Cancel' },
            onClick: handleCancelEdit,
            disabled: loading,
        });
    }

    const formatDateTime = (dateString?: string) => {
        if (!dateString) return intl.formatMessage(ScheduledTasksResources.notAvailable);
        const date = new Date(dateString);
        return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
    };

    return (
        <Panel
            isOpen={isOpen}
            onDismiss={onDismiss}
            type={PanelType.medium}
            headerText={isEditing ? intl.formatMessage(ScheduledTasksResources.editingScheduledTaskHeader, { name: task.name }) : task.name}
            closeButtonAriaLabel={intl.formatMessage(SreAgentResources.close)}
            isLightDismiss={true}
            onOuterClick={onDismiss}
        >
            <Stack tokens={{ childrenGap: 16 }}>
                {error && <MessageBar messageBarType={MessageBarType.error}>{error}</MessageBar>}

                <CommandBar items={commandBarItems} />

                {!isEditing && (
                    <Stack tokens={{ childrenGap: 12 }}>
                        <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 8 }}>
                            <Icon iconName={statusIcon.iconName} style={{ color: statusIcon.color, fontSize: 16 }} />
                            <Text variant="mediumPlus" style={{ color: statusIcon.color, fontWeight: 600 }}>
                                {task.status}
                            </Text>
                        </Stack>

                        <Text variant="medium" styles={{ root: { color: '#666' } }}>
                            {task.description}
                        </Text>
                    </Stack>
                )}

                {isEditing && (
                    <Stack tokens={{ childrenGap: 12 }}>
                        <TextField
                            label={intl.formatMessage(ScheduledTasksResources.name)}
                            required
                            value={name}
                            onChange={(_, v) => setName(v || '')}
                            disabled={loading}
                        />
                        <TextField
                            label={intl.formatMessage(ScheduledTasksResources.description)}
                            multiline
                            autoAdjustHeight
                            value={description}
                            onChange={(_, v) => setDescription(v || '')}
                            disabled={loading}
                        />
                        {}
                    </Stack>
                )}

                <Separator />

                <Stack tokens={{ childrenGap: 16 }}>
                    <Stack tokens={{ childrenGap: 8 }}>
                        <Text variant="medium" styles={{ root: { fontWeight: 600 } }}>
                            {intl.formatMessage(ScheduledTasksResources.scheduleSection)}
                        </Text>
                        {!isEditing && (
                            <Text
                                variant="medium"
                                styles={{
                                    root: {
                                        fontFamily: 'Monaco, monospace',
                                        background: tokens.colorNeutralBackground3,
                                        padding: 4,
                                        borderRadius: 4,
                                    },
                                }}
                            >
                                {task.cronExpression || intl.formatMessage(ScheduledTasksResources.dashPlaceholder)}
                            </Text>
                        )}
                        {isEditing && (
                            <TextField
                                label={undefined}
                                value={cronExpression}
                                onChange={(_, v) => setCronExpression(v || '')}
                                disabled={loading}
                                description={intl.formatMessage(ScheduledTasksResources.cronExampleEveryFiveMinutes)}
                            />
                        )}
                    </Stack>

                    <Stack tokens={{ childrenGap: 8 }}>
                        <Text variant="medium" styles={{ root: { fontWeight: 600 } }}>
                            {intl.formatMessage(ScheduledTasksResources.agentPromptSection)}
                        </Text>
                        {!isEditing && (
                            <div
                                style={{
                                    border: `1px solid ${tokens.colorNeutralStroke1}`,
                                    background: tokens.colorNeutralBackground3,
                                    padding: 8,
                                    maxHeight: 260,
                                    overflowY: 'auto',
                                    borderRadius: 4,
                                    fontFamily: 'Monaco, monospace',
                                    fontSize: 13,
                                    lineHeight: 1.4,
                                    whiteSpace: 'pre-wrap',
                                }}
                            >
                                {task.agentPrompt && task.agentPrompt.trim().length > 0 ? (
                                    task.agentPrompt
                                ) : (
                                    <em style={{ color: '#605e5c' }}>{intl.formatMessage(ScheduledTasksResources.noPromptProvided)}</em>
                                )}
                            </div>
                        )}
                        {isEditing && (
                            <TextField
                                label={undefined}
                                required
                                multiline
                                autoAdjustHeight
                                value={agentPrompt}
                                onChange={(_, v) => setAgentPrompt(v || '')}
                                disabled={loading}
                                styles={{ fieldGroup: { fontFamily: 'Monaco, monospace' } }}
                            />
                        )}
                    </Stack>

                    <Stack tokens={{ childrenGap: 8 }}>
                        <Text variant="smallPlus" styles={{ root: { fontWeight: 600 } }}>
                            {intl.formatMessage(ScheduledTasksResources.executionDetailsSection)}
                        </Text>
                        <Stack tokens={{ childrenGap: 4 }}>
                            <Text variant="small">
                                <strong>{intl.formatMessage(ScheduledTasksResources.executionCount)}:</strong> {task.executionCount}
                                {task.maxExecutions && ` / ${task.maxExecutions}`}
                            </Text>
                            <Text variant="small">
                                <strong>{intl.formatMessage(ScheduledTasksResources.lastExecution)}:</strong>{' '}
                                {formatDateTime(task.lastExecutionTime)}
                            </Text>
                            <Text variant="small">
                                <strong>{intl.formatMessage(ScheduledTasksResources.nextExecution)}:</strong>{' '}
                                {formatDateTime(task.nextExecutionTime)}
                            </Text>
                        </Stack>
                    </Stack>

                    <Stack tokens={{ childrenGap: 8 }}>
                        <Text variant="smallPlus" styles={{ root: { fontWeight: 600 } }}>
                            {intl.formatMessage(ScheduledTasksResources.taskDetailsSection)}
                        </Text>
                        <Stack tokens={{ childrenGap: 4 }}>
                            <Text variant="small">
                                <strong>{intl.formatMessage(ScheduledTasksResources.createdBy)}:</strong> {task.createdBy}
                            </Text>
                            <Text variant="small">
                                <strong>{intl.formatMessage(ScheduledTasksResources.createdAt)}:</strong> {formatDateTime(task.createdAt)}
                            </Text>
                            {task.threadId && (
                                <Text variant="small">
                                    <strong>{intl.formatMessage(ScheduledTasksResources.threadId)}:</strong> {task.threadId}
                                </Text>
                            )}
                            {task.startTime && (
                                <Text variant="small">
                                    <strong>{intl.formatMessage(ScheduledTasksResources.startTime)}:</strong>{' '}
                                    {formatDateTime(task.startTime)}
                                </Text>
                            )}
                            {task.endTime && (
                                <Text variant="small">
                                    <strong>{intl.formatMessage(ScheduledTasksResources.endTime)}:</strong> {formatDateTime(task.endTime)}
                                </Text>
                            )}
                        </Stack>
                    </Stack>
                </Stack>
            </Stack>
        </Panel>
    );
};

export default ScheduledTaskDetailsPanel;

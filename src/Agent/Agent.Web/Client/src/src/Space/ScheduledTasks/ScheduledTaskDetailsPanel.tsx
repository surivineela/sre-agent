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
import { FC, useCallback, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { ScheduledTasksResources } from '../../Strings/SREAgentResources';
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
                setError('Failed to pause task');
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to pause task');
        } finally {
            setLoading(false);
        }
    }, [task.id, pauseTask, onTaskUpdated]);

    const handleResume = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const success = await resumeTask(task.id);
            if (success) {
                onTaskUpdated();
            } else {
                setError('Failed to resume task');
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to resume task');
        } finally {
            setLoading(false);
        }
    }, [task.id, resumeTask, onTaskUpdated]);

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
                setError('Failed to delete task');
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to delete task');
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
            setError('Name is required');
            return;
        }
        if (!cronExpression.trim()) {
            setError('Cron expression is required');
            return;
        }
        if (!agentPrompt.trim()) {
            setError('Agent prompt is required');
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
                setError('Failed to update task');
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to update task');
        } finally {
            setLoading(false);
        }
    }, [name, description, cronExpression, agentPrompt, updateTask, task.id, onTaskUpdated]);

    const commandBarItems: ICommandBarItemProps[] = [];

    if (!isEditing) {
        // Edit action
        commandBarItems.push({
            key: 'edit',
            text: 'Edit',
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
            text: 'Save',
            iconProps: { iconName: 'Save' },
            onClick: () => {
                void handleSaveEdit();
            },
            disabled: loading,
        });
        commandBarItems.push({
            key: 'cancel',
            text: 'Cancel',
            iconProps: { iconName: 'Cancel' },
            onClick: handleCancelEdit,
            disabled: loading,
        });
    }

    const formatDateTime = (dateString?: string) => {
        if (!dateString) return 'N/A';
        const date = new Date(dateString);
        return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
    };

    return (
        <Panel
            isOpen={isOpen}
            onDismiss={onDismiss}
            type={PanelType.medium}
            headerText={isEditing ? `Editing: ${task.name}` : task.name}
            closeButtonAriaLabel="Close"
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
                        <TextField label="Name" required value={name} onChange={(_, v) => setName(v || '')} disabled={loading} />
                        <TextField
                            label="Description"
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
                            Schedule
                        </Text>
                        {!isEditing && (
                            <Text
                                variant="medium"
                                styles={{ root: { fontFamily: 'Monaco, monospace', background: '#faf9f8', padding: 4, borderRadius: 4 } }}
                            >
                                {task.cronExpression || '—'}
                            </Text>
                        )}
                        {isEditing && (
                            <TextField
                                label={undefined}
                                value={cronExpression}
                                onChange={(_, v) => setCronExpression(v || '')}
                                disabled={loading}
                                description="e.g. */5 * * * *"
                            />
                        )}
                    </Stack>

                    <Stack tokens={{ childrenGap: 8 }}>
                        <Text variant="medium" styles={{ root: { fontWeight: 600 } }}>
                            Agent Prompt
                        </Text>
                        {!isEditing && (
                            <div
                                style={{
                                    border: '1px solid #edebe9',
                                    background: '#faf9f8',
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
                                    <em style={{ color: '#605e5c' }}>No prompt provided.</em>
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
                            Execution Details
                        </Text>
                        <Stack tokens={{ childrenGap: 4 }}>
                            <Text variant="small">
                                <strong>Executions:</strong> {task.executionCount}
                                {task.maxExecutions && ` / ${task.maxExecutions}`}
                            </Text>
                            <Text variant="small">
                                <strong>Last execution:</strong> {formatDateTime(task.lastExecutionTime)}
                            </Text>
                            <Text variant="small">
                                <strong>Next execution:</strong> {formatDateTime(task.nextExecutionTime)}
                            </Text>
                        </Stack>
                    </Stack>

                    <Stack tokens={{ childrenGap: 8 }}>
                        <Text variant="smallPlus" styles={{ root: { fontWeight: 600 } }}>
                            Task Details
                        </Text>
                        <Stack tokens={{ childrenGap: 4 }}>
                            <Text variant="small">
                                <strong>Created by:</strong> {task.createdBy}
                            </Text>
                            <Text variant="small">
                                <strong>Created:</strong> {formatDateTime(task.createdAt)}
                            </Text>
                            {task.threadId && (
                                <Text variant="small">
                                    <strong>Thread ID:</strong> {task.threadId}
                                </Text>
                            )}
                            {task.startTime && (
                                <Text variant="small">
                                    <strong>Start time:</strong> {formatDateTime(task.startTime)}
                                </Text>
                            )}
                            {task.endTime && (
                                <Text variant="small">
                                    <strong>End time:</strong> {formatDateTime(task.endTime)}
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

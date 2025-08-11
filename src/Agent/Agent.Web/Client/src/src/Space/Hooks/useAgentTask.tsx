import cloneDeep from 'lodash/cloneDeep';
import { useContext, useEffect, useMemo, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AgentTaskClient } from '../../Common/Clients/AgentTaskClient';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { AgentTask, AgentTaskMetaData, InvestigationTreeState, TaskProgressUpdate } from '../../Common/Contracts/DataPlane/AgentTask';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { Guid } from '../../Common/Helpers/Guid';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { IAgentTaskProps } from '../Contracts/Activities';
import { StreamingContext } from '../Contracts/Context';
import { useAgentTaskStreamHandler } from './useAgentTaskStreamHandler';

interface PendingTask {
    agentTask: AgentTask | null;
    taskProgressUpdate: TaskProgressUpdate | null;
}

interface TreeStateValue {
    taskId: string;
    treeState: InvestigationTreeState | null;
    changeIdentifier: string;
}

interface TreeStatesMapValue extends TreeStateValue {
    pendingUpdate: PendingTask[] | null;
    isTreeStateInitialized: boolean;
}

const getDefaultTreeStateValue = (): TreeStateValue => ({
    taskId: '',
    treeState: null,
    changeIdentifier: '',
});

export const useAgentTask = (props: IAgentTaskProps) => {
    const { threadId, userDefinedThreadId, task } = props;

    const { updateTreeStateFromTaskProgress, updateTreeStateFromTaskUpdate } = useAgentTaskStreamHandler();

    const [taskDropdownOptions, setTaskDropdownOptions] = useState<AgentTaskMetaData[]>([]);
    const [selectedTaskId, setSelectedTaskId] = useState<string>('');
    const [isLoadingTaskDropdown, setIsLoadingTaskDropdown] = useState<boolean>(false);
    const [treeStates, setTreeStates] = useState<Map<string, TreeStatesMapValue>>(new Map());
    const [isLoadingTreeState, setIsLoadingTreeState] = useState<boolean>(false);
    const [currentTreeStateValue, setCurrentTreeStateValue] = useState<TreeStateValue>(getDefaultTreeStateValue());

    const threadIdRef = useRef<string | null>(threadId || userDefinedThreadId || null);
    const treeStatesRef = useRef<Map<string, TreeStatesMapValue>>(treeStates);

    const { subscribeTaskUpdateEvent } = useContext(StreamingContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);
    const agentTaskClient = AgentTaskClient.getInstance(sreAgentEndpoint);

    threadIdRef.current = threadId || userDefinedThreadId || null;
    treeStatesRef.current = treeStates;

    const updateTaskDropdownOption = (...tasks: AgentTaskMetaData[]) => {
        setTaskDropdownOptions(prev => {
            const tasksToBeUpdated: AgentTaskMetaData[] = [];
            const tasksToBeAdded: AgentTaskMetaData[] = [];

            for (const task of tasks) {
                const existingTask = prev.find(option => option.id === task.id);
                if (existingTask) {
                    // ToDo: use isStatusAllowed and the timestamp to determine if the task should be updated
                    if (existingTask.status !== task.status || existingTask.title !== task.title) {
                        tasksToBeUpdated.push(task);
                    }
                } else {
                    tasksToBeAdded.push(task);
                }
            }

            if (tasksToBeUpdated.length === 0 && tasksToBeAdded.length === 0) {
                return prev;
            }

            const newTasks = [...prev];
            for (const task of tasksToBeUpdated) {
                const index = newTasks.findIndex(option => option.id === task.id);
                if (index !== -1) {
                    newTasks[index] = task;
                }
            }

            return cloneDeep([...newTasks, ...tasksToBeAdded]);
        });
    };

    const processTask = (pendingTask: PendingTask) => {
        setTreeStates(prev => {
            const { agentTask, taskProgressUpdate } = pendingTask;
            const id = agentTask?.id || taskProgressUpdate?.taskId || '';
            if (id) {
                const currentTreeStateValue: TreeStatesMapValue = prev.get(id) || {
                    ...getDefaultTreeStateValue(),
                    isTreeStateInitialized: false,
                    taskId: id,
                    pendingUpdate: null,
                };

                if (agentTask) {
                    if (currentTreeStateValue.isTreeStateInitialized) {
                        currentTreeStateValue.treeState = updateTreeStateFromTaskUpdate(agentTask, currentTreeStateValue.treeState);
                    } else {
                        currentTreeStateValue.pendingUpdate = [
                            ...(currentTreeStateValue.pendingUpdate || []),
                            { agentTask, taskProgressUpdate: null },
                        ];
                    }
                } else if (taskProgressUpdate) {
                    if (currentTreeStateValue.isTreeStateInitialized) {
                        currentTreeStateValue.treeState = updateTreeStateFromTaskProgress(
                            taskProgressUpdate,
                            currentTreeStateValue.treeState
                        );
                    } else {
                        currentTreeStateValue.pendingUpdate = [
                            ...(currentTreeStateValue.pendingUpdate || []),
                            { agentTask: null, taskProgressUpdate },
                        ];
                    }
                }

                // Update the changeIdentifier to indicate the treeStateValue for this task has changed
                currentTreeStateValue.changeIdentifier = Guid.newGuid();

                prev.set(id, currentTreeStateValue);

                return new Map(prev);
            }

            return prev;
        });
    };

    const initializeTreeStateForSelectedTask = async (taskId: string, agentTask: AgentTask | null) => {
        setTreeStates(prev => {
            const currentTreeState = prev.get(taskId) || {
                ...getDefaultTreeStateValue(),
                taskId,
                isTreeStateInitialized: true,
                pendingUpdate: null,
            };

            // If the tree state is already initialized, return the previous state
            if (currentTreeState.isTreeStateInitialized) {
                return prev;
            }

            // Create a investigation tree state based on agentTask input.
            let newTreeState = updateTreeStateFromTaskUpdate(agentTask, null);
            // Update this new investigation tree state with current pending updates
            const pendingUpdates = currentTreeState.pendingUpdate || [];
            for (const pendingUpdate of pendingUpdates) {
                if (pendingUpdate.agentTask) {
                    newTreeState = updateTreeStateFromTaskUpdate(pendingUpdate.agentTask, newTreeState);
                } else if (pendingUpdate.taskProgressUpdate) {
                    newTreeState = updateTreeStateFromTaskProgress(pendingUpdate.taskProgressUpdate, newTreeState);
                }
            }

            // Set the tree state of this entry to the newly created tree state, set the pendingUpdates to null and set isTreeStateInitialized to true.
            // Moving forward, all the incoming update will be applied to this tree state.
            currentTreeState.treeState = newTreeState;
            currentTreeState.pendingUpdate = null;
            currentTreeState.changeIdentifier = Guid.newGuid();
            currentTreeState.isTreeStateInitialized = true;

            prev.set(taskId, currentTreeState);

            return cloneDeep(new Map(prev));
        });
    };

    const taskDropdownValue = useMemo(() => {
        if (selectedTaskId) {
            const option = taskDropdownOptions.find(option => option.id === selectedTaskId);
            return option ? option.title || '' : '';
        }
        return '';
    }, [selectedTaskId, taskDropdownOptions]);

    useEffect(() => {
        let isSubscribed = true;

        if (threadId) {
            const setAgentTasks = async () => {
                setIsLoadingTaskDropdown(true);
                const response = await threadClient.getThread(threadId);
                const tasks = response.content?.agentTasks || [];
                if (isSubscribed) {
                    if (tasks.length > 0) {
                        updateTaskDropdownOption(...tasks);
                        setSelectedTaskId(prev => {
                            if (prev) {
                                return prev;
                            }

                            return tasks.length > 0 ? tasks[tasks.length - 1].id : prev;
                        });
                    }
                    setIsLoadingTaskDropdown(false);
                }
            };

            setAgentTasks();
        }

        return () => {
            isSubscribed = false;
        };
    }, [threadId]);

    useEffect(() => {
        if (task) {
            setSelectedTaskId(task.id);
            updateTaskDropdownOption(task);
        }
    }, [task]);

    const isSelectedTreeStateInitialized = (selectedTaskId?: string) => {
        if (selectedTaskId) {
            const treeStateValue = treeStatesRef.current.get(selectedTaskId);
            return treeStateValue && treeStateValue.isTreeStateInitialized;
        }

        return false;
    };

    useEffect(() => {
        let isSubscribed = true;

        // Reset the tree state loading state when the selected task changes
        setIsLoadingTreeState(false);

        if (selectedTaskId && !isSelectedTreeStateInitialized(selectedTaskId) && threadIdRef.current) {
            const fetchAgentTask = async (threadId: string) => {
                setIsLoadingTreeState(true);

                const response = await agentTaskClient.getAgentTask(threadId, selectedTaskId);
                if (isSubscribed) {
                    if (response.isSuccessful) {
                        initializeTreeStateForSelectedTask(selectedTaskId, response.content || null);
                    } else {
                        // ToDo: handle get agent task error, such as displaying an error message with an refresh button
                    }
                    setIsLoadingTreeState(false);
                }
            };

            fetchAgentTask(threadIdRef.current);
        }

        return () => {
            isSubscribed = false;
        };
    }, [selectedTaskId]);

    useEffect(() => {
        if (selectedTaskId) {
            const treeStateValue = treeStates.get(selectedTaskId);
            if (!treeStateValue) {
                // Reset the current tree state value to default if no tree state is found for the selected task
                setCurrentTreeStateValue(prev => {
                    if (prev.taskId === selectedTaskId && prev.treeState === null && prev.changeIdentifier === '') {
                        return prev;
                    }

                    return {
                        taskId: selectedTaskId,
                        treeState: null,
                        changeIdentifier: '',
                    };
                });
            } else if (
                currentTreeStateValue.treeState === null ||
                currentTreeStateValue.taskId !== selectedTaskId ||
                currentTreeStateValue.changeIdentifier !== treeStateValue.changeIdentifier
            ) {
                // If the current tree state is null, or the task id for the current tree state is different from the selected task id, or the identifier has changed which indicates the
                // current tree state has been updated, then update the current tree state to the tree state value for the selected task id.
                setCurrentTreeStateValue({
                    taskId: selectedTaskId,
                    treeState: treeStateValue.treeState,
                    changeIdentifier: treeStateValue.changeIdentifier,
                });
            }
        }
    }, [selectedTaskId, treeStates, currentTreeStateValue]);

    useEffect(() => {
        let isSubscribed = true;

        const unsubscribe = subscribeTaskUpdateEvent((message?: StreamingMessage) => {
            const threadId = message?.additionalProperties?.threadId;
            const streamMessageType = message?.additionalProperties?.streamMessageType;
            const isTaskUpdate = streamMessageType && equals(streamMessageType, 'taskupdate', AntUxStringComparison.IgnoreCase);
            const isTaskProgress = streamMessageType && equals(streamMessageType, 'taskprogress', AntUxStringComparison.IgnoreCase);
            const isTaskStream = isTaskUpdate || isTaskProgress;
            const content = message?.contents?.[0]?.text;
            if (isSubscribed && threadId && threadId === threadIdRef.current && isTaskStream && content) {
                if (isTaskUpdate) {
                    try {
                        const task = JSON.parse(content) as AgentTask;
                        updateTaskDropdownOption(task);
                        processTask({ agentTask: task, taskProgressUpdate: null });
                    } catch {
                        // ToDo: log error
                    }
                } else {
                    try {
                        const progress = JSON.parse(content) as TaskProgressUpdate;
                        processTask({ agentTask: null, taskProgressUpdate: progress });
                    } catch {
                        // ToDo: log error
                    }
                }
            }
        });

        return () => {
            unsubscribe();
            isSubscribed = false;
        };
    }, [subscribeTaskUpdateEvent]);

    return {
        taskDropdownOptions,
        isLoadingTaskDropdown,
        setSelectedTaskId,
        selectedTaskId,
        taskDropdownValue,

        currentTreeStateValue,
        isLoadingTreeState,
    };
};

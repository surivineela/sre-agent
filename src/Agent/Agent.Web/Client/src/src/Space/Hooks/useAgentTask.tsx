import cloneDeep from 'lodash/cloneDeep';
import { Dispatch, SetStateAction, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AgentTaskClient } from '../../Common/Clients/AgentTaskClient';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { AgentTask, AgentTaskMetaData } from '../../Common/Contracts/DataPlane/AgentTask';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { Guid } from '../../Common/Helpers/Guid';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { TreeStateValue } from '../Contracts/Activities';
import { StreamingContext } from '../Contracts/Context';
import { useAgentTaskStreamHandler } from './useAgentTaskStreamHandler';

export const useAgentTask = (
    threadId: string | undefined,
    userDefinedThreadId: string,
    isLoadingInitialChatHistory: boolean,
    canOpenAgentTaskPanel: boolean,
    onOpenAgentTaskPanel?: () => void,
    onCloseAgentTaskPanel?: () => void,
    setMenuCollapsed?: Dispatch<SetStateAction<boolean>>,
    closeMemorySidePanel?: () => void
) => {
    const { updateTreeState } = useAgentTaskStreamHandler();

    const [isAgentTaskCollapsed, setIsAgentTaskCollapsed] = useState<boolean>(true);
    const [task, setTask] = useState<AgentTaskMetaData | null>(null);
    const [taskDropdownOptions, setTaskDropdownOptions] = useState<AgentTaskMetaData[]>([]);
    const [selectedTaskId, setSelectedTaskId] = useState<string>('');
    const [isLoadingTaskDropdown, setIsLoadingTaskDropdown] = useState<boolean>(false);
    const [treeStates, setTreeStates] = useState<Map<string, TreeStateValue>>(new Map());
    const [isLoadingTreeState, setIsLoadingTreeState] = useState(false);
    const [currentTreeStateValue, setCurrentTreeStateValue] = useState<TreeStateValue | null>(null);

    const threadIdRef = useRef<string | null>(threadId || userDefinedThreadId || null);
    const treeStatesRef = useRef<Map<string, TreeStateValue>>(treeStates);

    const { subscribeTaskUpdateEvent } = useContext(StreamingContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);
    const agentTaskClient = AgentTaskClient.getInstance(sreAgentEndpoint);
    const isMounted = useRef(true);

    threadIdRef.current = threadId || userDefinedThreadId || null;
    treeStatesRef.current = treeStates;

    const openAgentTask = useCallback(
        (task: AgentTaskMetaData) => {
            if (isAgentTaskCollapsed) {
                setIsAgentTaskCollapsed(false);
                setMenuCollapsed?.(true);
            }
            setTask(task);
            // Close memory side panel when opening agent task
            closeMemorySidePanel?.();
            onOpenAgentTaskPanel?.();
        },
        [setMenuCollapsed, isAgentTaskCollapsed, closeMemorySidePanel, onOpenAgentTaskPanel]
    );

    const closeAgentTask = useCallback(() => {
        setIsAgentTaskCollapsed(true);
        onCloseAgentTaskPanel?.();
    }, [onCloseAgentTaskPanel]);

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

    const toggleNode = useCallback(
        (nodeId: string) => {
            setTreeStates(prev => {
                const treeStateValue = selectedTaskId ? prev.get(selectedTaskId) : null;

                if (!treeStateValue) {
                    // Node not found for toggle
                    return prev;
                }

                const { treeState, ...rest } = treeStateValue;

                if (treeState) {
                    const nodes = new Map(treeState.nodes);
                    const node = nodes.get(nodeId);

                    if (node) {
                        const updatedNode = {
                            ...node,
                            expanded: !node.expanded,
                        };

                        nodes.set(nodeId, updatedNode);

                        // Create a new treeState object
                        const newTreeState = {
                            ...treeState,
                            nodes,
                        };

                        // Create a new Map for prev and set the updated treeState
                        const newTreeStates = new Map(prev);
                        newTreeStates.set(selectedTaskId, {
                            ...rest,
                            treeState: newTreeState,
                            changeIdentifier: Guid.newGuid(),
                        });

                        return cloneDeep(newTreeStates);
                    }
                }

                return prev;
            });
        },
        [selectedTaskId]
    );

    const getNodeStatus = useCallback(
        (nodeId: string) => {
            return currentTreeStateValue?.treeState?.nodes.get(nodeId)?.status || null;
        },
        [currentTreeStateValue]
    );

    const processTask = (task: AgentTask) => {
        setTreeStates(prev => {
            if (task) {
                const taskId = task.id;
                const currentTreeStateValue: TreeStateValue | null = prev.get(taskId) || null;
                const updatedTreeState = updateTreeState(task, currentTreeStateValue?.treeState || null);
                if (updatedTreeState === currentTreeStateValue?.treeState) {
                    return prev;
                }

                if (updatedTreeState) {
                    prev.set(taskId, {
                        taskId: taskId,
                        treeState: updatedTreeState,
                        changeIdentifier: Guid.newGuid(),
                    });
                } else {
                    prev.delete(taskId);
                }

                return cloneDeep(prev);
            }
            return prev;
        });
    };

    const taskDropdownValue = useMemo(() => {
        if (selectedTaskId) {
            return taskDropdownOptions.find(option => option.id === selectedTaskId);
        }
    }, [selectedTaskId, taskDropdownOptions]);

    useEffect(() => {
        if (threadId) {
            const setAgentTasks = async () => {
                setIsLoadingTaskDropdown(true);
                const response = await threadClient.getThread(threadId);
                const tasks = response.content?.agentTasks || [];
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
            };

            setAgentTasks();
        }
    }, [threadId]);

    useEffect(() => {
        if (task) {
            setSelectedTaskId(task.id);
        }
    }, [task]);

    useEffect(() => {
        let isSubscribed = true;

        if (selectedTaskId && !treeStatesRef.current.get(selectedTaskId) && threadIdRef.current) {
            const initializeTreeStateForSelectedTask = async (threadId: string) => {
                setIsLoadingTreeState(true);
                const response = await agentTaskClient.getAgentTask(threadId, selectedTaskId);

                if (isSubscribed) {
                    if (response.isSuccessful) {
                        if (response.content) {
                            processTask(response.content);
                        }
                    } else {
                        // ToDo: handle get agent task error, such as displaying an error message with an refresh button
                    }
                    setIsLoadingTreeState(false);
                }
            };

            initializeTreeStateForSelectedTask(threadIdRef.current);
        }

        return () => {
            isSubscribed = false;
            // Reset loading status
            setIsLoadingTreeState(false);
        };
    }, [selectedTaskId]);

    useEffect(() => {
        if (selectedTaskId) {
            const treeStateValue = treeStates.get(selectedTaskId) || null;
            setCurrentTreeStateValue(prev => {
                const isPrevNull = prev === null;
                const isCurrentNull = treeStateValue === null;
                const isSameTaskId = prev?.taskId === treeStateValue?.taskId;
                const isCurrentTaskChanged = treeStateValue?.changeIdentifier !== prev?.changeIdentifier;
                if (isPrevNull || isCurrentNull || !isSameTaskId || isCurrentTaskChanged) {
                    return treeStateValue;
                } else {
                    return prev;
                }
            });
        }
    }, [selectedTaskId, treeStates]);

    useEffect(() => {
        let isSubscribed = true;

        const unsubscribe = subscribeTaskUpdateEvent((message?: StreamingMessage) => {
            const threadId = message?.additionalProperties?.threadId;
            const streamMessageType = message?.additionalProperties?.streamMessageType;
            const isTaskUpdate = streamMessageType && equals(streamMessageType, 'taskupdate', AntUxStringComparison.IgnoreCase);
            const content = message?.contents?.[0]?.text;
            if (isSubscribed && threadId && threadId === threadIdRef.current && isTaskUpdate && content) {
                try {
                    const task = JSON.parse(content) as AgentTask;
                    updateTaskDropdownOption(task);
                    setSelectedTaskId(prev => {
                        if (prev) return prev;
                        return task.id;
                    });
                    processTask(task);
                } catch {
                    // ToDo: log error
                }
            }
        });

        return () => {
            unsubscribe();
            isSubscribed = false;
        };
    }, [subscribeTaskUpdateEvent]);

    const hasExistingTasks = useMemo(() => taskDropdownOptions.length > 0, [taskDropdownOptions]);

    useEffect(() => {
        isMounted.current = true;

        return () => {
            isMounted.current = false;
        };
    }, []);
    useEffect(() => {
        // If the component is unmounted, do not call collapseResizables as it is on activities level which can accidentally open the thread menu when the thread is already navigted away.
        if (!isLoadingInitialChatHistory && hasExistingTasks && isMounted.current && canOpenAgentTaskPanel) {
            setIsAgentTaskCollapsed(false);
            setMenuCollapsed?.(true);
        } else {
            setIsAgentTaskCollapsed(true);
            setMenuCollapsed?.(false);
        }
    }, [isLoadingInitialChatHistory, hasExistingTasks, setMenuCollapsed, canOpenAgentTaskPanel]);

    return {
        taskDropdownOptions,
        isLoadingTaskDropdown,
        setSelectedTaskId,
        selectedTaskId,
        taskDropdownValue,
        isAgentTaskCollapsed,
        setIsAgentTaskCollapsed,
        openAgentTask,
        closeAgentTask,

        currentTreeStateValue,
        isLoadingTreeState,
        toggleNode,
        getNodeStatus,
        hasExistingTasks,
    };
};

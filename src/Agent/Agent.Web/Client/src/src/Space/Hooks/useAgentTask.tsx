import cloneDeep from 'lodash/cloneDeep';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AgentTaskClient } from '../../Common/Clients/AgentTaskClient';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { AgentTask, AgentTaskMetaData } from '../../Common/Contracts/DataPlane/AgentTask';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { Guid } from '../../Common/Helpers/Guid';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { IAgentTaskProps, TreeStateValue } from '../Contracts/Activities';
import { StreamingContext } from '../Contracts/Context';
import { useAgentTaskStreamHandler } from './useAgentTaskStreamHandler';

export const useAgentTask = (props: IAgentTaskProps) => {
    const { threadId, userDefinedThreadId, task } = props;

    const { updateTreeState } = useAgentTaskStreamHandler();

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
                    const node = treeState.nodes.get(nodeId);

                    if (node) {
                        const updatedNode = {
                            ...node,
                            expanded: !node.expanded,
                        };

                        treeState.nodes.set(nodeId, updatedNode);

                        prev.set(selectedTaskId, {
                            ...rest,
                            treeState,
                            changeIdentifier: Guid.newGuid(),
                        });

                        return cloneDeep(new Map(prev));
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
                if (
                    prev === null ||
                    treeStateValue === null ||
                    prev.taskId !== treeStateValue.taskId ||
                    prev.changeIdentifier !== treeStateValue.changeIdentifier
                )
                    return treeStateValue;
                return prev;
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

    return {
        taskDropdownOptions,
        isLoadingTaskDropdown,
        setSelectedTaskId,
        selectedTaskId,
        taskDropdownValue,

        currentTreeStateValue,
        isLoadingTreeState,
        toggleNode,
        getNodeStatus,
    };
};

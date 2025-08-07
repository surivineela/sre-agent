import {
    DrawerHeader,
    DrawerHeaderNavigation,
    Dropdown,
    makeStyles,
    Option,
    Skeleton,
    SkeletonItem,
    Spinner,
    tokens,
    Toolbar,
    ToolbarButton,
    ToolbarGroup,
} from '@fluentui/react-components';
import { CheckmarkCircleColor, Dismiss24Regular, DismissCircleFilled, ErrorCircleColor } from '@fluentui/react-icons';
import cloneDeep from 'lodash/cloneDeep';
import { memo, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AgentTaskClient } from '../../Common/Clients/AgentTaskClient';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import {
    AgentTask as AgentTaskData,
    AgentTaskMetaData,
    AgentTaskStatus,
    TaskProgressUpdate,
} from '../../Common/Contracts/DataPlane/AgentTask';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import Fade from '../Components/Fade';
import { StreamingContext } from '../Contracts/Context';

interface IAgentTaskProps {
    threadId?: string;
    userDefinedThreadId: string;
    taskId?: string;
    collapsed?: boolean;
    setCollapsed: (collapsed: boolean) => void;
}

const useAgentTaskStyles = makeStyles({
    root: {
        backgroundColor: tokens.colorNeutralBackground1,
        height: '100%',
    },
    header: {
        width: '100%',
        display: 'flex',
        justifyContent: 'space-between',
        flexWrap: 'wrap',
    },
    dropdownItem: {
        display: 'flex',
        justifyItems: 'flex-start',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    dropdownItemText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        minWidth: '0',
        flex: '1 1 auto',
    },
    loader: {
        width: '50%',
    },
    loaderItem: {
        height: '100%',
        width: '100%',
    },
});

const AgentTask = (props: IAgentTaskProps) => {
    const { threadId, userDefinedThreadId, taskId, collapsed, setCollapsed } = props;
    const [taskDropdownOptions, setTaskDropdownOptions] = useState<AgentTaskMetaData[]>([]);
    const [selectedTaskId, setSelectedTaskId] = useState<string>('');
    const [isLoading, setIsLoading] = useState<boolean>(false);
    const threadIdRef = useRef<string | null>(threadId || userDefinedThreadId || null);

    const { subscribeTaskUpdateEvent } = useContext(StreamingContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);
    const agentTaskClient = AgentTaskClient.getInstance(sreAgentEndpoint);

    const { root, header, dropdownItem, dropdownItemText, loader, loaderItem } = useAgentTaskStyles();

    const getAgentDropdownItemIcon = (task: AgentTaskMetaData | null) => {
        const status = task?.status?.toLowerCase();
        const styleProps = {
            fontSize: tokens.fontSizeBase600,
            style: { flex: '0 0  auto' },
        };
        switch (status) {
            case AgentTaskStatus.InProgress:
                return <Spinner size="tiny" />;
            case AgentTaskStatus.Complete:
                return <CheckmarkCircleColor {...styleProps} />;
            case AgentTaskStatus.Failed:
                return <ErrorCircleColor {...styleProps} />;
            case AgentTaskStatus.Canceled:
                return <DismissCircleFilled {...styleProps} />;
            default:
                return null;
        }
    };

    const updateTaskDropdownOption = (...tasks: AgentTaskMetaData[]) => {
        setTaskDropdownOptions(prev => {
            const tasksToBeUpdated: AgentTaskMetaData[] = [];
            const tasksToBeAdded: AgentTaskMetaData[] = [];

            for (const task of tasks) {
                const existingTask = prev.find(option => option.id === task.id);
                if (existingTask) {
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

    useEffect(() => {
        threadIdRef.current = threadId || userDefinedThreadId || null;
    }, [threadId, userDefinedThreadId]);

    useEffect(() => {
        let isSubscribed = true;

        if (threadId) {
            const setAgentTasks = async () => {
                setIsLoading(true);
                const response = await threadClient.getThread(threadId);
                const tasks = response.content?.agentTasks || [];
                if (isSubscribed) {
                    setIsLoading(false);
                    if (tasks.length > 0) {
                        updateTaskDropdownOption(...tasks);
                    }
                }
            };

            setAgentTasks();
        }

        return () => {
            isSubscribed = false;
        };
    }, [threadId]);

    useEffect(() => {
        let isSubscribed = true;

        if (threadIdRef.current && taskId && !taskDropdownOptions.find(option => option.id === taskId)) {
            const addAgentTask = async (threadId: string) => {
                const response = await agentTaskClient.getAgentTask(threadId, taskId);
                const task = response.content;

                if (isSubscribed && task) {
                    updateTaskDropdownOption(task);

                    //ToDo: add tasks' other properties
                }
            };

            addAgentTask(threadIdRef.current);
        }

        return () => {
            isSubscribed = false;
        };
    }, [taskId, taskDropdownOptions]);

    useEffect(() => {
        const id = taskId || (taskDropdownOptions.length > 0 ? taskDropdownOptions[taskDropdownOptions.length - 1].id : '');
        setSelectedTaskId(prev => {
            if (prev !== id) {
                return id;
            }
            return prev;
        });
    }, [taskDropdownOptions, taskId]);

    useEffect(() => {
        let isSubscribed = true;

        const unsubscribe = subscribeTaskUpdateEvent((message?: StreamingMessage) => {
            const threadId = message?.additionalProperties?.threadId;
            const streamMessageType = message?.additionalProperties?.streamMessageType;
            const content = message?.contents?.[0]?.text;
            if (isSubscribed && threadId && threadId === threadIdRef.current && streamMessageType && content) {
                if (streamMessageType === 'taskupdate') {
                    try {
                        const task = JSON.parse(content) as AgentTaskData;
                        updateTaskDropdownOption(task);

                        //ToDo: add tasks' other properties
                    } catch {
                        // ToDo: log error
                    }
                } else if (streamMessageType === 'taskprogress') {
                    try {
                        const progress = JSON.parse(content) as TaskProgressUpdate;

                        // ToDo: update task progress
                        console.log(progress);
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

    const TaskDropdownItem = ({ taskId, taskDropdownOptions }: { taskId: string | null; taskDropdownOptions: AgentTaskMetaData[] }) => {
        const task = taskDropdownOptions.find(option => option.id === taskId) || null;

        return (
            <>
                {getAgentDropdownItemIcon(task)}
                <div className={dropdownItemText}>{task?.title || task?.id}</div>
            </>
        );
    };

    const taskDropdownValue = useMemo(() => {
        if (selectedTaskId) {
            const option = taskDropdownOptions.find(option => option.id === selectedTaskId);
            console.log(option);
            return option ? option.title || '' : '';
        }
        return '';
    }, [selectedTaskId, taskDropdownOptions]);

    return (
        <Fade visible={!collapsed} appear={true} unmountOnExit={true}>
            <div className={root}>
                <DrawerHeader>
                    <DrawerHeaderNavigation>
                        <Toolbar>
                            <ToolbarGroup className={header}>
                                {isLoading ? (
                                    <Skeleton className={loader}>
                                        <SkeletonItem size={20} className={loaderItem} />
                                    </Skeleton>
                                ) : (
                                    <Dropdown
                                        selectedOptions={selectedTaskId ? [selectedTaskId] : []}
                                        value={taskDropdownValue}
                                        onOptionSelect={(_, data) => {
                                            if (data.selectedOptions.length > 0) {
                                                const option = data.selectedOptions[0];
                                                console.log('Selected option:', option);
                                                setSelectedTaskId(option);
                                            }
                                        }}
                                    >
                                        {taskDropdownOptions.map(task => (
                                            <Option className={dropdownItem} key={task.id} text={task.title || task.id} value={task.id}>
                                                <TaskDropdownItem taskId={task.id} taskDropdownOptions={taskDropdownOptions} />
                                            </Option>
                                        ))}
                                    </Dropdown>
                                )}
                                <ToolbarButton
                                    aria-label="Close panel"
                                    appearance="subtle"
                                    icon={<Dismiss24Regular />}
                                    onClick={() => setCollapsed(true)}
                                />
                            </ToolbarGroup>
                        </Toolbar>
                    </DrawerHeaderNavigation>
                </DrawerHeader>
            </div>
        </Fade>
    );
};

export default memo(AgentTask);

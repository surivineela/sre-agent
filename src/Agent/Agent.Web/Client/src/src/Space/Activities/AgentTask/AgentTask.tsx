import {
    DrawerHeader,
    DrawerHeaderNavigation,
    Dropdown,
    InlineDrawer,
    makeStyles,
    Option,
    Skeleton,
    SkeletonItem,
    Spinner,
    tokens,
    Toolbar,
    ToolbarButton,
    ToolbarGroup,
    useRestoreFocusSource,
} from '@fluentui/react-components';
import { CheckmarkCircleColor, Dismiss24Regular, DismissCircleFilled, ErrorCircleColor } from '@fluentui/react-icons';
import { ReactFlowProvider } from '@xyflow/react';
import { Dispatch, memo, SetStateAction, useCallback, useEffect, useRef, useState } from 'react';
import { AgentTaskMetaData, AgentTaskStatus } from '../../../Common/Contracts/DataPlane/AgentTask';
import { AgentTaskGraphHandle, TreeStateValue } from '../../Contracts/Activities';
import { AgentTaskContext } from '../../Contracts/Context';
import AgentTaskGraph from './AgentTaskGraph';

interface IAgentTaskProps {
    taskDropdownOptions: AgentTaskMetaData[];
    isLoadingTaskDropdown: boolean;
    setSelectedTaskId: Dispatch<SetStateAction<string>>;
    selectedTaskId: string;
    taskDropdownValue: string;
    currentTreeStateValue: TreeStateValue | null;
    isLoadingTreeState: boolean;
    shouldFitView: boolean;
    toggleNode: (nodeId: string) => void;
    getNodeStatus: (nodeId: string) => string | null;
    collapsed?: boolean;
    setCollapsed: (collapsed: boolean) => void;
}

const useAgentTaskStyles = makeStyles({
    root: {
        backgroundColor: tokens.colorNeutralBackground1,
        height: 'calc(100vh - 100px)',
        flex: '1 0 auto',
        borderTopRightRadius: tokens.borderRadiusXLarge,
        borderBottomRightRadius: tokens.borderRadiusXLarge,
        position: 'relative',
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
    resizer: {
        width: '2px',
        height: '100%',
        position: 'absolute',
        top: 0,
        left: 0,
        bottom: 0,
        cursor: 'col-resize',
        border: 'none',
        minWidth: '0px',

        '&:before': {
            width: '2px',
            content: '""',
            position: 'absolute',
            borderLeft: `1px solid ${tokens.colorNeutralBackground5}`,
            height: '100%',
        },
        ':hover': {
            cursor: 'col-resize',
        },
        ':hover:active': {
            cursor: 'col-resize',
            userSelect: 'none',
        },
    },
});

const AgentTask = (props: IAgentTaskProps) => {
    const {
        taskDropdownOptions,
        isLoadingTaskDropdown,
        setSelectedTaskId,
        selectedTaskId,
        taskDropdownValue,
        currentTreeStateValue,
        isLoadingTreeState,
        shouldFitView,
        toggleNode,
        getNodeStatus,
        collapsed,
        setCollapsed,
    } = props;

    const { root, header, dropdownItem, dropdownItemText, loader, loaderItem, resizer } = useAgentTaskStyles();

    const restoreFocusSourceAttributes = useRestoreFocusSource();

    const agentTaskGraphRef = useRef<AgentTaskGraphHandle | null>(null);
    const animationFrame = useRef<number>(0);
    const sidebarRef = useRef<HTMLDivElement>(null);
    const [sideBarWidth, setSidebarWidth] = useState<number | null>(null);
    const [isResizing, setIsResizing] = useState(false);

    const startResizing = useCallback(() => setIsResizing(true), []);
    const stopResizing = useCallback(() => setIsResizing(false), []);

    const resize = useCallback(
        ({ clientX }: { clientX: number }) => {
            animationFrame.current = requestAnimationFrame(() => {
                if (isResizing && sidebarRef.current) {
                    const newSidebarWidth = sidebarRef.current.getBoundingClientRect().right - clientX;
                    setSidebarWidth(newSidebarWidth);
                    agentTaskGraphRef.current?.centerGraph();
                }
            });
        },
        [isResizing]
    );

    useEffect(() => {
        window.addEventListener('mousemove', resize);
        window.addEventListener('mouseup', stopResizing);

        return () => {
            cancelAnimationFrame(animationFrame.current);
            window.removeEventListener('mousemove', resize);
            window.removeEventListener('mouseup', stopResizing);
        };
    }, [resize, stopResizing]);

    const TaskDropdownItem = ({ taskId, taskDropdownOptions }: { taskId: string | null; taskDropdownOptions: AgentTaskMetaData[] }) => {
        const task = taskDropdownOptions.find(option => option.id === taskId) || null;

        const getAgentDropdownItemIcon = (task: AgentTaskMetaData | null) => {
            const status = task?.status?.toLowerCase();
            const styleProps = {
                fontSize: tokens.fontSizeBase600,
                style: { flex: '0 0 auto' },
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

        return (
            <>
                {getAgentDropdownItemIcon(task)}
                <div className={dropdownItemText}>{task?.title || task?.id}</div>
            </>
        );
    };

    return (
        <AgentTaskContext.Provider value={{ toggleNode, getNodeStatus }}>
            <ReactFlowProvider>
                <InlineDrawer
                    {...restoreFocusSourceAttributes}
                    position="end"
                    open={!collapsed}
                    ref={sidebarRef}
                    className={root}
                    style={{ width: sideBarWidth === null ? '80%' : `${sideBarWidth}px` }}
                >
                    <DrawerHeader>
                        <DrawerHeaderNavigation>
                            <Toolbar>
                                <ToolbarGroup className={header}>
                                    {isLoadingTaskDropdown ? (
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
                    <AgentTaskGraph
                        treeStateValue={currentTreeStateValue}
                        isLoading={isLoadingTreeState}
                        shouldFitView={shouldFitView}
                        ref={agentTaskGraphRef}
                    />
                    <div
                        className={resizer}
                        onMouseDown={startResizing}
                        aria-label="Resize drawer"
                        role="separator"
                        aria-orientation="vertical"
                    />
                </InlineDrawer>
            </ReactFlowProvider>
        </AgentTaskContext.Provider>
    );
};

export default memo(AgentTask);

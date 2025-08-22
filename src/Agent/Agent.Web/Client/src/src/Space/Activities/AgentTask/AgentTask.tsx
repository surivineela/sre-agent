import {
    InlineDrawer,
    Menu,
    MenuButton,
    MenuCheckedValueChangeData,
    MenuCheckedValueChangeEvent,
    MenuItemRadio,
    MenuList,
    MenuPopover,
    MenuTrigger,
    Subtitle2,
    Text,
    tokens,
    Toolbar,
    ToolbarButton,
    useRestoreFocusSource,
} from '@fluentui/react-components';
import {
    ArrowCounterclockwise24Regular,
    ArrowCounterclockwiseFilled,
    CheckmarkCircleRegular,
    Dismiss24Regular,
    DismissCircleFilled,
    SubtractCircleRegular,
} from '@fluentui/react-icons';
import { mergeStyleSets } from '@fluentui/react/lib/Styling';
import { ReactFlowProvider } from '@xyflow/react';
import { Dispatch, memo, SetStateAction, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AgentTaskMetaData, AgentTaskStatus } from '../../../Common/Contracts/DataPlane/AgentTask';
import NodeStatusPill from '../../Components/AgentTask/NodeStatusPill';
import { AgentTaskGraphHandle, TreeStateValue } from '../../Contracts/Activities';
import { AgentTaskContext } from '../../Contracts/Context';
import { AgentTaskStyleProps } from '../../Styles/Activities.styles';
import AgentTaskGraph from './AgentTaskGraph';

interface IAgentTaskProps {
    taskDropdownOptions: AgentTaskMetaData[];
    isLoadingTaskDropdown: boolean;
    setSelectedTaskId: Dispatch<SetStateAction<string>>;
    selectedTaskId: string;
    taskDropdownValue?: AgentTaskMetaData;
    currentTreeStateValue: TreeStateValue | null;
    isLoadingTreeState: boolean;
    shouldFitView: boolean;
    toggleNode: (nodeId: string) => void;
    getNodeStatus: (nodeId: string) => string | null;
    collapsed?: boolean;
    setCollapsed: (collapsed: boolean) => void;
    stylesProps?: AgentTaskStyleProps;
}

const useAgentTaskStyles = (overrides?: AgentTaskStyleProps) =>
    mergeStyleSets({
        root: {
            backgroundColor: tokens.colorNeutralBackground1,
            height: 'calc(100vh - 100px)',
            flex: '1 0 auto',
            borderTopRightRadius: tokens.borderRadiusXLarge,
            borderBottomRightRadius: tokens.borderRadiusXLarge,
            position: 'relative',
            ...overrides?.root,
        },
        header: {
            width: '100%',
            maxWidth: '100%',
            padding: `${tokens.spacingVerticalXXL} ${tokens.spacingHorizontalXXL} ${tokens.spacingVerticalS}`,
            gap: tokens.spacingHorizontalS,
            alignSelf: 'stretch',
            display: 'flex',
            justifyContent: 'space-between',
            boxSizing: 'border-box',
            position: 'relative',
            zIndex: 2,
            ...overrides?.header,
        },
        titleContainer: {
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'flex-start',
            gap: tokens.spacingHorizontalS,
            flex: '1 1 auto',
            ...overrides?.titleContainer,
        },
        titleText: { textOverflow: 'ellipsis', overflow: 'hidden', ...overrides?.titleText },
        titleStatus: {
            display: 'flex',
            alignItems: 'center',
            gap: tokens.spacingHorizontalXS,
            minWidth: '50px',
            flex: '0 0 auto',
            ...overrides?.titleStatus,
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
            ...overrides?.resizer,
        },
    });

const AgentTask = (props: IAgentTaskProps) => {
    const {
        taskDropdownOptions,
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
        stylesProps,
    } = props;

    const styles = useAgentTaskStyles(stylesProps);

    const restoreFocusSourceAttributes = useRestoreFocusSource();

    const agentTaskGraphRef = useRef<AgentTaskGraphHandle | null>(null);
    const animationFrame = useRef<number>(0);
    const sidebarRef = useRef<HTMLDivElement>(null);
    const [sideBarWidth, setSidebarWidth] = useState<number | null>(null);
    const [isResizing, setIsResizing] = useState(false);

    const selectedTaskMenuId: Record<string, string[]> = useMemo(() => {
        return { taskId: selectedTaskId ? [selectedTaskId] : [] };
    }, [selectedTaskId]);

    const onSelectedTaskChange = useCallback((_: MenuCheckedValueChangeEvent, data: MenuCheckedValueChangeData) => {
        setSelectedTaskId(data.checkedItems[0] || '');
    }, []);

    const getMenuItemIcon = useCallback((task: AgentTaskMetaData) => {
        switch (task.status.toLowerCase()) {
            case AgentTaskStatus.Complete.toLowerCase():
                return <CheckmarkCircleRegular style={{ color: tokens.colorPaletteGreenBackground3 }} />;
            case AgentTaskStatus.InProgress.toLowerCase():
                return <ArrowCounterclockwiseFilled style={{ color: tokens.colorBrandForegroundLinkHover }} />;
            case AgentTaskStatus.Failed.toLowerCase():
                return <DismissCircleFilled style={{ color: tokens.colorStatusDangerForeground3 }} />;
            default:
                return <SubtractCircleRegular />;
        }
    }, []);

    const startResizing = useCallback(() => setIsResizing(true), []);
    const stopResizing = useCallback(() => setIsResizing(false), []);

    const resize = useCallback(
        ({ clientX }: { clientX: number }) => {
            animationFrame.current = requestAnimationFrame(() => {
                if (isResizing && sidebarRef.current) {
                    const newSidebarWidth = sidebarRef.current.getBoundingClientRect().right - clientX;
                    setSidebarWidth(Math.max(newSidebarWidth, 1000));
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

    return (
        <AgentTaskContext.Provider value={{ toggleNode, getNodeStatus }}>
            <ReactFlowProvider>
                <InlineDrawer
                    {...restoreFocusSourceAttributes}
                    position="end"
                    open={!collapsed}
                    ref={sidebarRef}
                    className={styles.root}
                    style={{ width: sideBarWidth === null ? '80%' : `${sideBarWidth}px` }}
                >
                    <div className={styles.header}>
                        <div className={styles.titleContainer}>
                            <Subtitle2 wrap={false} block={true} className={styles.titleText}>
                                {taskDropdownValue?.title}
                            </Subtitle2>
                            <div className={styles.titleStatus}>
                                <NodeStatusPill status={taskDropdownValue?.status} showIcon={true} />
                            </div>
                        </div>
                        <Toolbar>
                            <Menu checkedValues={selectedTaskMenuId} onCheckedValueChange={onSelectedTaskChange}>
                                <MenuTrigger disableButtonEnhancement>
                                    <MenuButton aria-label="select task" appearance="subtle">
                                        <ArrowCounterclockwise24Regular />
                                    </MenuButton>
                                </MenuTrigger>
                                <MenuPopover>
                                    <MenuList>
                                        {taskDropdownOptions.map(task => {
                                            return (
                                                <MenuItemRadio
                                                    key={task.id}
                                                    name={'taskId'}
                                                    value={task.id}
                                                    icon={getMenuItemIcon(task)}
                                                    style={{ alignItems: 'center', gap: tokens.spacingHorizontalS }}
                                                >
                                                    <Text>{task.title || task.id}</Text>
                                                </MenuItemRadio>
                                            );
                                        })}
                                    </MenuList>
                                </MenuPopover>
                            </Menu>

                            <ToolbarButton
                                aria-label="Close panel"
                                appearance="subtle"
                                icon={<Dismiss24Regular />}
                                onClick={() => setCollapsed(true)}
                            />
                        </Toolbar>
                    </div>
                    <AgentTaskGraph
                        treeStateValue={currentTreeStateValue}
                        isLoading={isLoadingTreeState}
                        shouldFitView={shouldFitView}
                        ref={agentTaskGraphRef}
                    />
                    <div
                        className={styles.resizer}
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

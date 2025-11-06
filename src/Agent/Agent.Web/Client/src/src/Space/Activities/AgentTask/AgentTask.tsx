import {
    makeStyles,
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
} from '@fluentui/react-components';
import {
    ArrowCounterclockwise24Regular,
    ArrowCounterclockwiseFilled,
    CheckmarkCircleRegular,
    Dismiss24Regular,
    DismissCircleFilled,
    SubtractCircleRegular,
} from '@fluentui/react-icons';
import { ReactFlowProvider } from '@xyflow/react';
import { Dispatch, forwardRef, memo, SetStateAction, useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { AgentTaskMetaData, AgentTaskStatus } from '../../../Common/Contracts/DataPlane/AgentTask';
import { GenericErrorResources } from '../../../Strings/SREAgentResources';
import NodeStatusPill from '../../Components/AgentTask/NodeStatusPill';
import { AgentTaskGraphHandle, TreeStateValue } from '../../Contracts/Activities';
import { AgentTaskContext } from '../../Contracts/Context';
import AgentTaskGraph from './AgentTaskGraph';

interface IAgentTaskProps {
    taskDropdownOptions: AgentTaskMetaData[];
    isLoadingTaskDropdown: boolean;
    setSelectedTaskId: Dispatch<SetStateAction<string>>;
    selectedTaskId: string;
    taskDropdownValue?: AgentTaskMetaData;
    currentTreeStateValue: TreeStateValue | null;
    isLoadingTreeState: boolean;
    toggleNode: (nodeId: string) => void;
    getNodeStatus: (nodeId: string) => string | null;
    closeAgentTask: () => void;
}

const useAgentTaskStyles = makeStyles({
    header: {
        width: '100%',
        maxWidth: '100%',
        padding: `${tokens.spacingVerticalXL} ${tokens.spacingHorizontalXXL} ${tokens.spacingVerticalS}`,
        gap: tokens.spacingHorizontalS,
        alignSelf: 'stretch',
        display: 'flex',
        justifyContent: 'space-between',
        boxSizing: 'border-box',
        position: 'relative',
        zIndex: 2,
    },
    titleContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'flex-start',
        gap: tokens.spacingHorizontalS,
        flex: '1 1 auto',
    },
    titleText: {
        textOverflow: 'ellipsis',
        overflow: 'hidden',
        flex: '0 0 auto',
    },
    titleStatus: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        minWidth: '50px',
        flex: '0 0 auto',
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

const AgentTask = forwardRef<AgentTaskGraphHandle, IAgentTaskProps>((props, agentTaskGraphRef) => {
    const {
        taskDropdownOptions,
        setSelectedTaskId,
        selectedTaskId,
        taskDropdownValue,
        currentTreeStateValue,
        isLoadingTreeState,
        toggleNode,
        getNodeStatus,
        closeAgentTask,
    } = props;

    const styles = useAgentTaskStyles();

    const intl = useIntl();

    const selectedTaskMenuId: Record<string, string[]> = useMemo(() => {
        return { taskId: selectedTaskId ? [selectedTaskId] : [] };
    }, [selectedTaskId]);

    const onSelectedTaskChange = useCallback(
        (_: MenuCheckedValueChangeEvent, data: MenuCheckedValueChangeData) => {
            setSelectedTaskId(data.checkedItems[0] || '');
        },
        [setSelectedTaskId]
    );

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

    return (
        <AgentTaskContext.Provider value={{ toggleNode, getNodeStatus }}>
            <ReactFlowProvider>
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
                        <Menu
                            positioning={{ autoSize: true }}
                            checkedValues={selectedTaskMenuId}
                            onCheckedValueChange={onSelectedTaskChange}
                        >
                            <MenuTrigger disableButtonEnhancement>
                                <MenuButton
                                    aria-label={intl.formatMessage(GenericErrorResources.selectTask)}
                                    disabled={taskDropdownOptions.length === 0}
                                    appearance="subtle"
                                >
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
                            aria-label={intl.formatMessage(GenericErrorResources.closePanel)}
                            appearance="subtle"
                            icon={<Dismiss24Regular />}
                            onClick={() => closeAgentTask()}
                        />
                    </Toolbar>
                </div>
                <AgentTaskGraph treeStateValue={currentTreeStateValue} isLoading={isLoadingTreeState} ref={agentTaskGraphRef} />
            </ReactFlowProvider>
        </AgentTaskContext.Provider>
    );
});

export default memo(AgentTask);

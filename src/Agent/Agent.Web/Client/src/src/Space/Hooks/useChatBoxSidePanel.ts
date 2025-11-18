import { Dispatch, ForwardedRef, SetStateAction, useCallback, useEffect, useImperativeHandle, useRef, useState } from 'react';
import { AgentTaskMetaData } from '../../Common/Contracts/DataPlane/AgentTask';
import { TodoPlan } from '../../Common/Contracts/DataPlane/TodoPlan';
import { ChatBoxHandleRef, ChatBoxSidePanelData, ChatBoxSidePanelType } from '../Contracts/Activities';
import { useAgentTask } from './useAgentTask';
import { useMemorySearchResultDrawer } from './useMemorySearchResultDrawer';
import { useTodoPlanDrawer } from './useTodoPlanDrawer';

export const useChatBoxSidePanel = (
    threadId: string | undefined,
    userDefinedThreadId: string,
    initialSidePanelData: ChatBoxSidePanelData | undefined | null,
    isLoadingInitialChatHistory: boolean,
    canOpenSidePanel: boolean,
    setMenuCollapsed: Dispatch<SetStateAction<boolean>> | undefined,
    onOpenSidePanel: ((panelType: ChatBoxSidePanelType, sidePanelData: ChatBoxSidePanelData) => void) | undefined,
    onCloseSidePanel: ((panelType: ChatBoxSidePanelType) => void) | undefined,
    setHasToDoPlans: ((value: boolean) => void) | undefined,
    ref: ForwardedRef<ChatBoxHandleRef>
) => {
    //!important: Do not set the side panel's open/close state based on selectedSidePanelType, as the type change is also used on
    // resetting the side panel styles. Using selectedSidePanelType to control open/close will cause animation issues when closing the panel.
    const [selectedSidePanelType, setSelectedSidePanelType] = useState<ChatBoxSidePanelType | null>(null);
    const [isSidePanelOpen, setIsSidePanelOpen] = useState<boolean>(false);
    const [sidePanelWidth, setSidePanelWidth] = useState<number | null>(null);
    const [exisitingLatestAgentTask, setExistingLatestAgentTask] = useState<AgentTaskMetaData | null>(null);
    const [existingLatestToDoPlan, setExistingLatestToDoPlan] = useState<TodoPlan | null>(null);

    const isSidePanelOpenRef = useRef<boolean>(isSidePanelOpen);
    isSidePanelOpenRef.current = isSidePanelOpen;

    const openSidePanel = useCallback(
        (panelType: ChatBoxSidePanelType, sidePanelData: ChatBoxSidePanelData) => {
            if (!isSidePanelOpenRef.current) {
                setMenuCollapsed?.(true);
            }

            setSidePanelWidth(null);
            setSelectedSidePanelType(panelType);
            setIsSidePanelOpen(true);
            onOpenSidePanel?.(panelType, sidePanelData);
        },
        [setMenuCollapsed, onOpenSidePanel]
    );

    const closeSidePanel = useCallback(
        (panelType: ChatBoxSidePanelType) => {
            setSidePanelWidth(null);
            setIsSidePanelOpen(false);
            onCloseSidePanel?.(panelType);
        },
        [onCloseSidePanel]
    );

    const initSidePanel = useCallback(
        (initialSidePanelData: ChatBoxSidePanelData | undefined | null) => {
            const shouldOpenSidePanel =
                !isLoadingInitialChatHistory && initialSidePanelData && (initialSidePanelData.agentTask || initialSidePanelData.todoInfo);

            setSelectedSidePanelType(
                shouldOpenSidePanel
                    ? initialSidePanelData!.agentTask
                        ? ChatBoxSidePanelType.AgentTask
                        : ChatBoxSidePanelType.ToDoPlan
                    : null
            );
            setIsSidePanelOpen(!!shouldOpenSidePanel);
            setMenuCollapsed?.(!!shouldOpenSidePanel);
        },
        [setMenuCollapsed, isLoadingInitialChatHistory]
    );

    const { setTask, ...agentTaskProps } = useAgentTask(
        threadId,
        userDefinedThreadId,
        openSidePanel,
        closeSidePanel,
        setExistingLatestAgentTask
    );

    const { setToDoInfo, ...todoPlanProps } = useTodoPlanDrawer(
        threadId,
        userDefinedThreadId,
        setHasToDoPlans,
        openSidePanel,
        closeSidePanel,
        setExistingLatestToDoPlan
    );

    const memorySearchResultProps = useMemorySearchResultDrawer(initialSidePanelData, openSidePanel, closeSidePanel, initSidePanel);

    useImperativeHandle(ref, () => ({
        openTodoPlanFromOutside: () => {
            if (todoPlanProps.todoInfo) {
                todoPlanProps.openTodoPlan(todoPlanProps.todoInfo);
            } else {
                const todoPlans = todoPlanProps.todoPlans;
                if (todoPlans.length > 0) {
                    todoPlanProps.openTodoPlan(todoPlans[todoPlans.length - 1]);
                }
            }
        },
        closeTodoPlanFromOutside: () => todoPlanProps.closeTodoPlan(),
    }));

    useEffect(() => {
        if (!canOpenSidePanel) {
            initSidePanel(null);
            return;
        }

        const sidePanelData: ChatBoxSidePanelData = { ...initialSidePanelData };

        if (!sidePanelData.agentTask && !sidePanelData.todoInfo) {
            if (exisitingLatestAgentTask) {
                sidePanelData.agentTask = { ...exisitingLatestAgentTask };
            } else if (existingLatestToDoPlan) {
                sidePanelData.todoInfo = { ...existingLatestToDoPlan };
            }
        }
        initSidePanel(sidePanelData);

        if (sidePanelData?.agentTask) {
            setTask(sidePanelData.agentTask);
        } else if (sidePanelData?.todoInfo) {
            setToDoInfo(sidePanelData.todoInfo);
        }
    }, [initSidePanel, initialSidePanelData, setTask, setToDoInfo, exisitingLatestAgentTask, existingLatestToDoPlan, canOpenSidePanel]);

    return {
        sidePanelProps: {
            selectedSidePanelType,
            isSidePanelOpen,
            sidePanelWidth,
            setSidePanelWidth,
        },
        agentTaskProps,
        todoPlanProps,
        memorySearchResultProps,
    };
};

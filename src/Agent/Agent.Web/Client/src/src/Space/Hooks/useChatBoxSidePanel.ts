import { ForwardedRef, useCallback, useEffect, useImperativeHandle, useRef, useState } from 'react';
import { ChatBoxHandleRef, ChatBoxSidePanelData, ChatBoxSidePanelType } from '../Contracts/Activities';
import { useAgentTask } from './useAgentTask';
import { useKnowledgeGraphSearchResultDrawer } from './useKnowledgeGraphSearchResultDrawer';
import { useMemorySearchResultDrawer } from './useMemorySearchResultDrawer';
import { useTodoPlanDrawer } from './useTodoPlanDrawer';

export const useChatBoxSidePanel = (
    threadId: string | undefined | null,
    threadIdUsedForCreatingNewThread: string,
    initialSidePanelData: ChatBoxSidePanelData | undefined | null,
    isLoadingInitialChatHistory: boolean,
    canOpenSidePanel: boolean,
    expandOrCollapseNavBar: ((state: boolean) => void) | undefined,
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

    const isSidePanelOpenRef = useRef<boolean>(isSidePanelOpen);
    isSidePanelOpenRef.current = isSidePanelOpen;

    const openSidePanel = useCallback(
        (panelType: ChatBoxSidePanelType, sidePanelData: ChatBoxSidePanelData) => {
            if (!isSidePanelOpenRef.current) {
                expandOrCollapseNavBar?.(false);
            }

            setSidePanelWidth(null);
            setSelectedSidePanelType(panelType);
            setIsSidePanelOpen(true);
            onOpenSidePanel?.(panelType, sidePanelData);
        },
        [expandOrCollapseNavBar, onOpenSidePanel]
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
                !isLoadingInitialChatHistory &&
                initialSidePanelData &&
                (initialSidePanelData.agentTask ||
                    initialSidePanelData.todoInfo ||
                    initialSidePanelData.memorySearchResult ||
                    initialSidePanelData.knowledgeGraphSearchResult);

            let panelType: ChatBoxSidePanelType | null = null;
            if (shouldOpenSidePanel && initialSidePanelData) {
                if (initialSidePanelData.agentTask) {
                    panelType = ChatBoxSidePanelType.AgentTask;
                } else if (initialSidePanelData.todoInfo) {
                    panelType = ChatBoxSidePanelType.ToDoPlan;
                } else if (initialSidePanelData.memorySearchResult) {
                    panelType = ChatBoxSidePanelType.MemorySearchResult;
                } else if (initialSidePanelData.knowledgeGraphSearchResult) {
                    panelType = ChatBoxSidePanelType.KnowledgeGraphSearchResult;
                }
            }

            setSelectedSidePanelType(panelType);
            setIsSidePanelOpen(!!shouldOpenSidePanel);
            if (shouldOpenSidePanel) {
                expandOrCollapseNavBar?.(false);
            }

        },
        [expandOrCollapseNavBar, isLoadingInitialChatHistory]
    );

    const { setTask, ...agentTaskProps } = useAgentTask(
        threadId,
        threadIdUsedForCreatingNewThread,
        openSidePanel,
        closeSidePanel,
    );

    const { setToDoInfo, ...todoPlanProps } = useTodoPlanDrawer(
        threadId,
        threadIdUsedForCreatingNewThread,
        setHasToDoPlans,
        openSidePanel,
        closeSidePanel
    );

    const { setMemorySearchResult, ...memorySearchResultProps } = useMemorySearchResultDrawer(initialSidePanelData, openSidePanel, closeSidePanel, initSidePanel);

    const { setKnowledgeGraphSearchResult, ...knowledgeGraphSearchResultProps } = useKnowledgeGraphSearchResultDrawer(
        initialSidePanelData,
        openSidePanel,
        closeSidePanel,
        initSidePanel
    );

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
        initSidePanel(initialSidePanelData);

        if (initialSidePanelData?.agentTask) {
            setTask(initialSidePanelData.agentTask);
        } else if (initialSidePanelData?.todoInfo) {
            setToDoInfo(initialSidePanelData.todoInfo);
        } else if (initialSidePanelData?.memorySearchResult) {
            setMemorySearchResult(initialSidePanelData.memorySearchResult);
        } else if (initialSidePanelData?.knowledgeGraphSearchResult) {
            setKnowledgeGraphSearchResult(initialSidePanelData.knowledgeGraphSearchResult);
        }
    }, [initSidePanel, initialSidePanelData, setTask, setToDoInfo, setMemorySearchResult, setKnowledgeGraphSearchResult, canOpenSidePanel]);

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
        knowledgeGraphSearchResultProps,
    };
};

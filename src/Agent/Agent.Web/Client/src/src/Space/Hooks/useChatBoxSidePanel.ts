import { Dispatch, SetStateAction, useCallback, useRef, useState } from 'react';
import { ChatBoxSidePanelData, ChatBoxSidePanelType } from '../Contracts/Activities';

export const useChatBoxSidePanel = (
    isLoadingInitialChatHistory: boolean,
    setMenuCollapsed: Dispatch<SetStateAction<boolean>> | undefined,
    onOpenSidePanel: ((panelType: ChatBoxSidePanelType, sidePanelData: ChatBoxSidePanelData) => void) | undefined,
    onCloseSidePanel: ((panelType: ChatBoxSidePanelType) => void) | undefined
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

    return {
        selectedSidePanelType,
        isSidePanelOpen,
        sidePanelWidth,
        setSidePanelWidth,
        openSidePanel,
        closeSidePanel,
        initSidePanel,
    };
};

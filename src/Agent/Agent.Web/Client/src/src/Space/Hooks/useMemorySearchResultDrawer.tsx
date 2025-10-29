import { useCallback, useEffect, useState } from 'react';
import { MemorySearchResult } from '../../Common/Contracts/DataPlane/Message';
import { ChatBoxSidePanelData, ChatBoxSidePanelType } from '../Contracts/Activities';

export const useMemorySearchResultDrawer = (
    initialSidePanelData: ChatBoxSidePanelData | undefined | null,
    openSidePanel: (panelType: ChatBoxSidePanelType, sidePanelData: ChatBoxSidePanelData) => void,
    closeSidePanel: (panelType: ChatBoxSidePanelType) => void,
    initSidePanel: (initialSidePanelData: ChatBoxSidePanelData | undefined | null) => void
) => {
    const [memorySearchResult, setMemorySearchResult] = useState<MemorySearchResult | null>(null);

    const openMemorySearchResult = useCallback(
        (result: MemorySearchResult) => {
            openSidePanel(ChatBoxSidePanelType.MemorySearchResult, { memorySearchResult: result });
            setMemorySearchResult(result);
        },
        [openSidePanel]
    );

    const closeMemorySearchResult = useCallback(() => {
        closeSidePanel(ChatBoxSidePanelType.MemorySearchResult);
    }, [closeSidePanel]);

    useEffect(() => {
        initSidePanel(initialSidePanelData);

        if (initialSidePanelData?.memorySearchResult) {
            setMemorySearchResult(initialSidePanelData.memorySearchResult);
        }
    }, [initSidePanel, initialSidePanelData]);

    return {
        memorySearchResult,
        openMemorySearchResult,
        closeMemorySearchResult,
    };
};

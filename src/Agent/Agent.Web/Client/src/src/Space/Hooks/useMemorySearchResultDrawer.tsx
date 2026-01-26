import { useCallback, useEffect, useState } from 'react';
import { MemorySearchResult } from '../../Common/Contracts/DataPlane/Message';
import { ChatBoxSidePanelData, ChatBoxSidePanelType } from '../Contracts/Activities';
import { MemorySearchFocusOptions } from '../Contracts/Context';

export const useMemorySearchResultDrawer = (
    initialSidePanelData: ChatBoxSidePanelData | undefined | null,
    openSidePanel: (panelType: ChatBoxSidePanelType, sidePanelData: ChatBoxSidePanelData) => void,
    closeSidePanel: (panelType: ChatBoxSidePanelType) => void,
    initSidePanel: (initialSidePanelData: ChatBoxSidePanelData | undefined | null) => void
) => {
    const [memorySearchResult, setMemorySearchResult] = useState<MemorySearchResult | null>(null);
    const [focusOptions, setFocusOptions] = useState<MemorySearchFocusOptions | undefined>(undefined);

    const openMemorySearchResult = useCallback(
        (result: MemorySearchResult, focus?: MemorySearchFocusOptions) => {
            openSidePanel(ChatBoxSidePanelType.MemorySearchResult, { memorySearchResult: result });
            setMemorySearchResult(result);
            setFocusOptions(focus);
        },
        [openSidePanel]
    );

    const clearFocusOptions = useCallback(() => {
        setFocusOptions(undefined);
    }, []);

    const closeMemorySearchResult = useCallback(() => {
        closeSidePanel(ChatBoxSidePanelType.MemorySearchResult);
        setFocusOptions(undefined);
    }, [closeSidePanel]);

    useEffect(() => {
        initSidePanel(initialSidePanelData);

        if (initialSidePanelData?.memorySearchResult) {
            setMemorySearchResult(initialSidePanelData.memorySearchResult);
        }
    }, [initSidePanel, initialSidePanelData]);

    return {
        memorySearchResult,
        focusOptions,
        clearFocusOptions,
        openMemorySearchResult,
        closeMemorySearchResult,
    };
};

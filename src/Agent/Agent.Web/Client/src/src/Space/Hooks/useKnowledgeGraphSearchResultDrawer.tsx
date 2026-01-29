import { useCallback, useEffect, useState } from 'react';
import { KnowledgeGraphSearchResult } from '../../Common/Contracts/DataPlane/Message';
import { ChatBoxSidePanelData, ChatBoxSidePanelType } from '../Contracts/Activities';

export const useKnowledgeGraphSearchResultDrawer = (
    initialSidePanelData: ChatBoxSidePanelData | undefined | null,
    openSidePanel: (panelType: ChatBoxSidePanelType, sidePanelData: ChatBoxSidePanelData) => void,
    closeSidePanel: (panelType: ChatBoxSidePanelType) => void,
    initSidePanel: (initialSidePanelData: ChatBoxSidePanelData | undefined | null) => void
) => {
    const [knowledgeGraphSearchResult, setKnowledgeGraphSearchResult] = useState<KnowledgeGraphSearchResult | null>(null);

    const openKnowledgeGraphSearchResult = useCallback(
        (result: KnowledgeGraphSearchResult) => {
            openSidePanel(ChatBoxSidePanelType.KnowledgeGraphSearchResult, { knowledgeGraphSearchResult: result });
            setKnowledgeGraphSearchResult(result);
        },
        [openSidePanel]
    );

    const closeKnowledgeGraphSearchResult = useCallback(() => {
        closeSidePanel(ChatBoxSidePanelType.KnowledgeGraphSearchResult);
    }, [closeSidePanel]);

    useEffect(() => {
        initSidePanel(initialSidePanelData);

        if (initialSidePanelData?.knowledgeGraphSearchResult) {
            setKnowledgeGraphSearchResult(initialSidePanelData.knowledgeGraphSearchResult);
        }
    }, [initSidePanel, initialSidePanelData]);

    return {
        knowledgeGraphSearchResult,
        openKnowledgeGraphSearchResult,
        closeKnowledgeGraphSearchResult,
        setKnowledgeGraphSearchResult
    };
};

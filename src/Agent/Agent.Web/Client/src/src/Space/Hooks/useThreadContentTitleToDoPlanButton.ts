import { RefObject, useCallback, useState } from 'react';
import { ChatBoxHandleRef, ChatBoxSidePanelType } from '../Contracts/Activities';

export const useThreadContentTitleToDoPlanButton = (chatboxHandleRef: RefObject<ChatBoxHandleRef>) => {
    const [hasToDoPlans, setHasToDoPlans] = useState(false);
    const [isToDoPlanOpen, setIsToDoPlanOpen] = useState(false);

    const openToDoPlan = useCallback(() => {
        chatboxHandleRef.current?.openTodoPlanFromOutside();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const closeToDoPlan = useCallback(() => {
        chatboxHandleRef.current?.closeTodoPlanFromOutside();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const onOpenSidePanel = useCallback((panelType: ChatBoxSidePanelType) => {
        setIsToDoPlanOpen(panelType === ChatBoxSidePanelType.ToDoPlan);
    }, []);

    const onCloseSidePanel = useCallback((_panelType: ChatBoxSidePanelType) => {
        setIsToDoPlanOpen(false);
    }, []);

    return {
        hasToDoPlans,
        isToDoPlanOpen,
        openToDoPlan,
        closeToDoPlan,
        onOpenSidePanel,
        onCloseSidePanel,
        setHasToDoPlans,
    };
};

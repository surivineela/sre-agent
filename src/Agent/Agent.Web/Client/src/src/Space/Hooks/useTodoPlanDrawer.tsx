import { Dispatch, SetStateAction, useCallback, useEffect, useState } from 'react';
import { TodoInfo } from '../../Common/Contracts/DataPlane/TodoPlan';
import { ChatBoxSidePanelData, ChatBoxSidePanelType } from '../Contracts/Activities';
import { useTodoPlans } from './useTodoPlans';

export const useTodoPlanDrawer = (
    currentThreadId: string | undefined,
    userDefinedThreadId: string | undefined,
    initialSidePanelData: ChatBoxSidePanelData | undefined | null,
    setHasToDoPlan: Dispatch<SetStateAction<boolean>> | undefined,
    openSidePanel: (panelType: ChatBoxSidePanelType, sidePanelData: ChatBoxSidePanelData) => void,
    closeSidePanel: (panelType: ChatBoxSidePanelType) => void,
    initSidePanel: (initialSidePanelData: ChatBoxSidePanelData | undefined | null) => void
) => {
    const threadId = currentThreadId || userDefinedThreadId || null;

    const [todoInfo, setToDoInfo] = useState<TodoInfo | null>(null);

    const { todoPlans, isLoading, error } = useTodoPlans(threadId);

    const openTodoPlan = useCallback(
        (todoInfo: TodoInfo) => {
            openSidePanel(ChatBoxSidePanelType.ToDoPlan, { todoInfo: todoInfo });
            setToDoInfo(todoInfo);
        },
        [openSidePanel]
    );

    const closeTodoPlan = useCallback(() => {
        closeSidePanel(ChatBoxSidePanelType.ToDoPlan);
    }, [closeSidePanel]);

    useEffect(() => {
        if (setHasToDoPlan) {
            setHasToDoPlan(todoPlans.length > 0);
        }
    }, [setHasToDoPlan, todoPlans.length]);

    useEffect(() => {
        initSidePanel(initialSidePanelData);

        if (initialSidePanelData?.todoInfo) {
            setToDoInfo(initialSidePanelData.todoInfo);
        }
    }, [initSidePanel, initialSidePanelData]);

    return {
        // Data
        todoPlans,
        isLoading,
        error,

        // Plan selection
        todoInfo,

        // Open/close drawer logic
        openTodoPlan,
        closeTodoPlan,
    };
};

import { useCallback, useEffect, useState } from 'react';
import { TodoInfo, TodoPlan } from '../../Common/Contracts/DataPlane/TodoPlan';
import { ChatBoxSidePanelData, ChatBoxSidePanelType } from '../Contracts/Activities';
import { useTodoPlans } from './useTodoPlans';

export const useTodoPlanDrawer = (
    threadId: string | undefined | null,
    threadIdUsedForCreatingNewThread: string,
    setHasToDoPlans: ((val: boolean) => void) | undefined,
    openSidePanel: (panelType: ChatBoxSidePanelType, sidePanelData: ChatBoxSidePanelData) => void,
    closeSidePanel: (panelType: ChatBoxSidePanelType) => void,
    setExistingLatestToDoPlan: (plan: TodoPlan | null) => void
) => {
    const [todoInfo, setToDoInfo] = useState<TodoInfo | null>(null);

    const { todoPlans, isLoading, error } = useTodoPlans(threadId || threadIdUsedForCreatingNewThread || null, setExistingLatestToDoPlan);

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
        if (setHasToDoPlans) {
            setHasToDoPlans(todoPlans.length > 0);
        }
    }, [setHasToDoPlans, todoPlans.length]);

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

        setToDoInfo,
    };
};

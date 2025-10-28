import { Dispatch, SetStateAction, useCallback, useEffect, useRef, useState } from 'react';
import { useTodoPlans } from './useTodoPlans';

export const useTodoPlanDrawer = (
    threadId: string | null,
    setMenuCollapsed: Dispatch<SetStateAction<boolean>>,
    isLoadingInitialChatHistory: boolean
) => {
    const [isTodoPlanDrawerCollapsed, setIsTodoPlanDrawerCollapsed] = useState<boolean>(true);
    const [selectedPlanId, setSelectedPlanId] = useState<string | undefined>(undefined);
    const hasAutoOpenedRef = useRef(false);

    const { todoPlans, isLoading, error } = useTodoPlans(threadId);

    const hasExistingPlans = todoPlans.length > 0;

    useEffect(() => {
        setIsTodoPlanDrawerCollapsed(true);
        setSelectedPlanId(undefined);
        hasAutoOpenedRef.current = false;
    }, [threadId]);

    // Auto-show when new todos are streamed
    useEffect(() => {
        const currentCount = todoPlans.length;

        if (!hasAutoOpenedRef.current && currentCount > 0 && isTodoPlanDrawerCollapsed && !isLoadingInitialChatHistory) {
            setIsTodoPlanDrawerCollapsed(false);
            setMenuCollapsed(true);
            hasAutoOpenedRef.current = true;
        }
    }, [todoPlans.length, isTodoPlanDrawerCollapsed, setMenuCollapsed, isLoadingInitialChatHistory]);

    const openTodoPlanDrawer = useCallback(
        (planId?: string) => {
            if (isTodoPlanDrawerCollapsed) {
                setIsTodoPlanDrawerCollapsed(false);
                setMenuCollapsed(true);
            }

            if (planId) {
                setSelectedPlanId(planId);
            }
        },
        [isTodoPlanDrawerCollapsed, setMenuCollapsed]
    );

    const shouldShowDrawer = todoPlans.length > 0;

    return {
        // Data
        todoPlans,
        isLoading,
        error,

        // Plan selection
        selectedPlanId,
        setSelectedPlanId,

        // Drawer state
        isTodoPlanDrawerCollapsed,
        setIsTodoPlanDrawerCollapsed,
        openTodoPlanDrawer,
        shouldShowDrawer,

        // Utilities
        hasExistingPlans,
    };
};

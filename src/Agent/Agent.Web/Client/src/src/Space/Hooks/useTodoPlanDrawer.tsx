import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTodoPlans } from './useTodoPlans';

export const useTodoPlanDrawer = (
    threadId: string | null,
    collapseResizables: (() => void) | undefined,
    isLoadingInitialChatHistory: boolean
) => {
    const [isTodoPlanDrawerCollapsed, setIsTodoPlanDrawerCollapsed] = useState<boolean>(true);
    const [selectedPlanId, setSelectedPlanId] = useState<string | undefined>(undefined);
    const userManuallyHidRef = useRef(false);

    const { todoPlans, isLoading, error, refetch } = useTodoPlans(threadId);

    // Compute recency: consider a thread "recent" if the most recent plan update is within the last hour
    const mostRecentPlanTime = useMemo(() => {
        if (!todoPlans?.length) return undefined as number | undefined;
        const toMs = (iso?: string) => (iso ? new Date(iso).getTime() : undefined);
        let maxTs: number | undefined = undefined;
        for (const p of todoPlans) {
            const ts = Math.max(...[toMs(p.lastUpdated), toMs(p.createdAt)].filter((v): v is number => typeof v === 'number'));
            if (!isNaN(ts)) {
                maxTs = typeof maxTs === 'number' ? Math.max(maxTs, ts) : ts;
            }
        }
        return maxTs;
    }, [todoPlans]);

    const isRecent = useMemo(() => {
        if (!mostRecentPlanTime) return false;
        const ONE_HOUR_MS = 60 * 60 * 1000;
        return Date.now() - mostRecentPlanTime <= ONE_HOUR_MS;
    }, [mostRecentPlanTime]);

    useEffect(() => {
        userManuallyHidRef.current = false;
        setSelectedPlanId(undefined);
    }, [threadId]);

    const openTodoPlanDrawer = useCallback(
        (planId?: string) => {
            if (isTodoPlanDrawerCollapsed) {
                setIsTodoPlanDrawerCollapsed(false);
                collapseResizables?.();
            }
            // If specific plan ID provided (from TodoPlanChatMessage click), select it
            if (planId) {
                setSelectedPlanId(planId);
            }
        },
        [collapseResizables, isTodoPlanDrawerCollapsed]
    );

    // Always mount the drawer when there are todo plans so a button can open it on demand
    const shouldShowDrawer = todoPlans.length > 0;

    // Auto-show/hide logic that respects user intent
    useEffect(() => {
        const currentTodoCount = todoPlans.length;

        // Auto-show only if there are todos, the drawer is collapsed, user hasn't manually hidden, AND content is recent (<= 1h)
        if (currentTodoCount > 0 && isTodoPlanDrawerCollapsed && !userManuallyHidRef.current && isRecent) {
            setIsTodoPlanDrawerCollapsed(false);
            collapseResizables?.();
        } else if (currentTodoCount === 0 && !isTodoPlanDrawerCollapsed) {
            // Auto-hide when no todos (and reset manual hide flag for next time)
            setIsTodoPlanDrawerCollapsed(true);
            userManuallyHidRef.current = false;
        }

        // Update the previous count
    }, [todoPlans.length, isTodoPlanDrawerCollapsed, collapseResizables, isRecent]);

    // Track when user manually toggles visibility
    useEffect(() => {
        if (!isTodoPlanDrawerCollapsed && userManuallyHidRef.current) {
            // User manually showed via some action - reset hide flag
            userManuallyHidRef.current = false;
        } else if (isTodoPlanDrawerCollapsed && todoPlans.length > 0) {
            // User manually hid when todos exist - set hide flag
            userManuallyHidRef.current = true;
        }
    }, [isTodoPlanDrawerCollapsed, todoPlans.length]);

    // Handle manual close action
    const handleCloseDrawer = useCallback(() => {
        userManuallyHidRef.current = true;
        setIsTodoPlanDrawerCollapsed(true);
    }, []);

    const hasExistingPlans = useMemo(() => todoPlans.length > 0, [todoPlans]);

    // Auto-expand on initial load only if the plans are recent (<= 1h)
    useEffect(() => {
        if (!isLoadingInitialChatHistory && hasExistingPlans && isRecent && !userManuallyHidRef.current) {
            setIsTodoPlanDrawerCollapsed(false);
            collapseResizables?.();
        }
    }, [isLoadingInitialChatHistory, hasExistingPlans, collapseResizables, isRecent]);

    return {
        // Data
        todoPlans,
        isLoading,
        error,
        refetch,

        // Plan selection
        selectedPlanId,
        setSelectedPlanId,

        // Drawer state
        isTodoPlanDrawerCollapsed,
        setIsTodoPlanDrawerCollapsed: handleCloseDrawer,
        openTodoPlanDrawer,
        shouldShowDrawer,

        // Utilities
        hasExistingPlans,
    };
};

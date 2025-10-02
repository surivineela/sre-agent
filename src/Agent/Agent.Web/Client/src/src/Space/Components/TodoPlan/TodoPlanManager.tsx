import { memo, useEffect } from 'react';
import { useTodoPlanDrawer } from '../../Hooks/useTodoPlanDrawer';
import TodoPlanDrawer from './TodoPlanDrawer';

interface TodoPlanManagerProps {
    drawerState: ReturnType<typeof useTodoPlanDrawer>;
}

const TodoPlanManager = ({ drawerState }: TodoPlanManagerProps) => {
    const {
        todoPlans,
        isLoading,
        selectedPlanId,
        setSelectedPlanId,
        isTodoPlanDrawerCollapsed,
        setIsTodoPlanDrawerCollapsed,
        shouldShowDrawer,
        openTodoPlanDrawer,
    } = drawerState;

    // Bridge for legacy header button (toggle & state broadcast)
    // Keeps new single-hook architecture while not breaking existing button wiring.
    // Can be removed once header consumes a proper context instead of DOM events.
    useEffect(() => {
        const onToggle = () => {
            if (isTodoPlanDrawerCollapsed) {
                openTodoPlanDrawer();
            } else {
                setIsTodoPlanDrawerCollapsed();
            }
        };
        window.addEventListener('toggle-todo-plan', onToggle);
        return () => window.removeEventListener('toggle-todo-plan', onToggle);
    }, [isTodoPlanDrawerCollapsed, openTodoPlanDrawer, setIsTodoPlanDrawerCollapsed]);

    useEffect(() => {
        window.dispatchEvent(new CustomEvent('todo-plan-state', { detail: { open: !isTodoPlanDrawerCollapsed } }));
    }, [isTodoPlanDrawerCollapsed]);

    if (!shouldShowDrawer) return null;

    return (
        <TodoPlanDrawer
            todoPlans={todoPlans}
            isLoading={isLoading}
            selectedPlanId={selectedPlanId}
            setSelectedPlanId={setSelectedPlanId}
            collapsed={isTodoPlanDrawerCollapsed}
            setCollapsed={setIsTodoPlanDrawerCollapsed}
        />
    );
};

export default memo(TodoPlanManager);

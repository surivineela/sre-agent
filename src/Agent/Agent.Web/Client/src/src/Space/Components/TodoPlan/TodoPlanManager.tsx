import { memo } from 'react';
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
    } = drawerState;

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

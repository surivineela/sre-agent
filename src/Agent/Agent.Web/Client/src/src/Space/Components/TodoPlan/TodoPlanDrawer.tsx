import { Button, Caption1, DrawerHeader, InlineDrawer, Subtitle2, Text, useRestoreFocusSource } from '@fluentui/react-components';
import { ChatWarningRegular, Dismiss24Regular, TaskListLtrRegular } from '@fluentui/react-icons';
import { memo, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { TodoItemStatus, TodoPlan } from '../../../Common/Contracts/DataPlane/TodoPlan';
import { AntUxStringComparison, equals } from '../../../Common/Helpers/Strings';
import { SreAgentResources, ToDoPlanResources } from '../../../Strings/SREAgentResources';
import { useTodoPlanDrawerStyles } from '../../Styles/TodoPlan.styles';
import TodoPlanContentFixed from './TodoPlanContentFixed';

interface ITodoPlanDrawerProps {
    todoPlans: TodoPlan[];
    isLoading: boolean;
    selectedPlanId?: string;
    setSelectedPlanId: (planId: string | undefined) => void;
    collapsed?: boolean;
    setCollapsed: (collapsed: boolean) => void;
}

const TodoPlanDrawer = (props: ITodoPlanDrawerProps) => {
    const { todoPlans, selectedPlanId, collapsed, setCollapsed } = props;

    const intl = useIntl();
    const styles = useTodoPlanDrawerStyles();
    const restoreFocusSourceAttributes = useRestoreFocusSource();
    const sidebarRef = useRef<HTMLDivElement>(null);
    const animationFrame = useRef<number>(0);
    const [sideBarWidth, setSidebarWidth] = useState<number | null>(null);
    const [isResizing, setIsResizing] = useState(false);

    // Show latest plan by default, but allow selection of specific plans via TodoPlanChatMessage clicks
    const selectedPlan = useMemo(() => {
        if (!todoPlans.length) return undefined;

        // If a specific plan is selected AND it exists in current plans, show it
        if (selectedPlanId) {
            const specificPlan = todoPlans.find(plan => plan.id === selectedPlanId);
            if (specificPlan) return specificPlan;
        }

        return todoPlans[0];
    }, [todoPlans, selectedPlanId]);

    const startResizing = useCallback(() => setIsResizing(true), []);
    const stopResizing = useCallback(() => setIsResizing(false), []);

    const resize = useCallback(
        ({ clientX }: { clientX: number }) => {
            animationFrame.current = requestAnimationFrame(() => {
                if (isResizing && sidebarRef.current) {
                    const newSidebarWidth = sidebarRef.current.getBoundingClientRect().right - clientX;
                    setSidebarWidth(newSidebarWidth);
                }
            });
        },
        [isResizing]
    );

    useEffect(() => {
        window.addEventListener('mousemove', resize);
        window.addEventListener('mouseup', stopResizing);

        return () => {
            cancelAnimationFrame(animationFrame.current);
            window.removeEventListener('mousemove', resize);
            window.removeEventListener('mouseup', stopResizing);
        };
    }, [resize, stopResizing]);

    return (
        <InlineDrawer
            {...restoreFocusSourceAttributes}
            position="end"
            open={!collapsed}
            ref={sidebarRef}
            className={styles.root}
            style={{
                minWidth: '480px',
                width: sideBarWidth === null ? '520px' : `${sideBarWidth}px`,
            }}
        >
            <DrawerHeader>
                <div className={styles.header}>
                    <div className={styles.headerIconContainer}>
                        <TaskListLtrRegular style={{ height: '100%' }} fontSize={'32px'} />
                    </div>
                    <div className={styles.headerTextContainer}>
                        <Subtitle2 block={true} wrap={false} className={styles.headerText}>
                            {selectedPlan?.title || intl.formatMessage(ToDoPlanResources.todoPlanText)}
                        </Subtitle2>
                        <Caption1 block={true} wrap={false} className={styles.headerText}>
                            {(() => {
                                const total = selectedPlan?.items?.length ?? 0;
                                const comp =
                                    selectedPlan?.items?.filter(i =>
                                        equals(i.status, TodoItemStatus.Completed, AntUxStringComparison.IgnoreCase)
                                    ).length ?? 0;
                                return intl.formatMessage(ToDoPlanResources.todoPlanProgress, { completed: comp, total });
                            })()}
                        </Caption1>
                    </div>
                    <div className={styles.headerButton}>
                        <Button
                            aria-label={intl.formatMessage(SreAgentResources.closePanel)}
                            appearance="subtle"
                            icon={<Dismiss24Regular />}
                            onClick={() => setCollapsed(true)}
                            style={{ width: '100%' }}
                        />
                    </div>
                </div>
            </DrawerHeader>

            <div className={styles.content}>
                {selectedPlan ? (
                    <TodoPlanContentFixed plan={selectedPlan} />
                ) : (
                    <div className={styles.emptyStateContainer}>
                        <ChatWarningRegular className={styles.emptyStateIcon} />
                        <Text>{intl.formatMessage(ToDoPlanResources.noTodoPlanAvailable)}</Text>
                    </div>
                )}
            </div>

            <div
                className={styles.resizer}
                onMouseDown={startResizing}
                aria-label={intl.formatMessage(SreAgentResources.resizeDrawer)}
                role="separator"
                aria-orientation="vertical"
            />
        </InlineDrawer>
    );
};

export default memo(TodoPlanDrawer);

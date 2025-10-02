import { InlineDrawer, Toolbar, ToolbarButton, useRestoreFocusSource } from '@fluentui/react-components';
import { Dismiss24Regular, List24Regular } from '@fluentui/react-icons';
import { memo, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { TodoPlan } from '../../../Common/Contracts/DataPlane/TodoPlan';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
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
            <div className={styles.header}>
                <div className={styles.titleContainer}>
                    <h3 className={styles.titleText}>{selectedPlan?.title || 'Todo Plan'}</h3>
                    <div className={styles.titleProgressRow}>
                        <div className={styles.titleProgressBar}>
                            <div
                                className={styles.titleProgressFill as any}
                                style={{
                                    width: `${(() => {
                                        const total = selectedPlan?.items?.length ?? 0;
                                        const comp = selectedPlan?.items?.filter(i => i.status === 'Completed').length ?? 0;
                                        return total > 0 ? (comp / total) * 100 : 0;
                                    })()}%`,
                                }}
                            />
                        </div>
                        <span className={styles.titleCount}>
                            {(() => {
                                const total = selectedPlan?.items?.length ?? 0;
                                const comp = selectedPlan?.items?.filter(i => i.status === 'Completed').length ?? 0;
                                return `${comp}/${total}`;
                            })()}
                        </span>
                    </div>
                </div>
                <Toolbar className={styles.headerToolbar}>
                    <ToolbarButton
                        aria-label={intl.formatMessage(SreAgentResources.closePanel)}
                        appearance="subtle"
                        icon={<Dismiss24Regular />}
                        onClick={() => setCollapsed(true)}
                    />
                </Toolbar>
            </div>

            <div className={styles.content}>
                {selectedPlan ? (
                    <TodoPlanContentFixed plan={selectedPlan} />
                ) : (
                    <div className={styles.emptyStateContainer}>
                        <List24Regular className={styles.emptyStateIcon} />
                        <span className={styles.emptyStateText}>{intl.formatMessage(SreAgentResources.noTodoPlanAvailable)}</span>
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

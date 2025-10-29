import { Caption1, Subtitle2 } from '@fluentui-copilot/react-copilot';
import { Button, DrawerHeader, Text } from '@fluentui/react-components';
import { ChatWarningRegular, Dismiss24Regular, TaskListLtrRegular } from '@fluentui/react-icons';
import { memo, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { TodoInfo, TodoItemStatus, TodoPlan as TodoPlanObject } from '../../../Common/Contracts/DataPlane/TodoPlan';
import { AntUxStringComparison, equals } from '../../../Common/Helpers/Strings';
import { SreAgentResources, ToDoPlanResources } from '../../../Strings/SREAgentResources';
import { useTodoPlanDrawerStyles } from '../../Styles/TodoPlan.styles';
import TodoPlanContent from './TodoPlanContent';

interface ITodoPlanProps {
    todoPlans: TodoPlanObject[];
    isLoading: boolean;
    error: string | null;
    todoInfo: TodoInfo | null;
    closeTodoPlan: () => void;
}

const TodoPlan = (props: ITodoPlanProps) => {
    const { todoPlans, todoInfo, closeTodoPlan } = props;

    const intl = useIntl();
    const styles = useTodoPlanDrawerStyles();

    // Show latest plan by default, but allow selection of specific plans via TodoPlanChatMessage clicks
    const selectedPlan = useMemo(() => {
        if (!todoPlans.length) return undefined;

        // If a specific plan is selected AND it exists in current plans, show it
        if (todoInfo?.id) {
            const specificPlan = todoPlans.find(plan => plan.id === todoInfo.id);
            if (specificPlan) return specificPlan;
        }

        return todoPlans[0];
    }, [todoPlans, todoInfo]);

    return (
        <>
            <DrawerHeader>
                <div className={styles.header}>
                    <div className={styles.headerIconContainer}>
                        <TaskListLtrRegular style={{ height: '100%' }} fontSize={'32px'} />
                    </div>
                    <div className={styles.headerTextContainer}>
                        <Subtitle2 block={true} className={styles.headerText}>
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
                            onClick={() => closeTodoPlan()}
                            style={{ width: '100%' }}
                        />
                    </div>
                </div>
            </DrawerHeader>

            <div className={styles.content}>
                {selectedPlan ? (
                    <TodoPlanContent plan={selectedPlan} />
                ) : (
                    <div className={styles.emptyStateContainer}>
                        <ChatWarningRegular className={styles.emptyStateIcon} />
                        <Text>{intl.formatMessage(ToDoPlanResources.noTodoPlanAvailable)}</Text>
                    </div>
                )}
            </div>
        </>
    );
};

export default memo(TodoPlan);

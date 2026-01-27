import { Caption2, Text, tokens } from '@fluentui/react-components';
import { CheckmarkRegular, CircleFilled, CircleRegular, DismissRegular } from '@fluentui/react-icons';
import { memo, useMemo } from 'react';
import { TodoItem, TodoItemStatus, TodoPlan } from '../../../Common/Contracts/DataPlane/TodoPlan';
import { getSafeDateTime } from '../../../Common/Helpers/Date';
import { useTodoPlanContentStyles } from '../../Styles/TodoPlan.styles';

interface TodoPlanContentProps {
    plan: TodoPlan;
}

const TodoPlanContent = ({ plan }: TodoPlanContentProps) => {
    const styles = useTodoPlanContentStyles();

    return (
        <div className={styles.container}>
            {plan.items.map((item: TodoItem, index) => (
                <div key={index} className={styles.taskItem}>
                    <StatusIcon status={item.status} />
                    <Status item={item} />
                </div>
            ))}
        </div>
    );
};

const StatusIcon = memo(({ status }: { status: TodoItemStatus }) => {
    const styles = useTodoPlanContentStyles();

    switch (status) {
        case TodoItemStatus.Completed:
            return <CheckmarkRegular className={styles.taskItemIcon} style={{ color: tokens.colorPaletteGreenForeground1 }} />;
        case TodoItemStatus.InProgress:
            return <CircleFilled className={styles.taskItemIcon} style={{ color: tokens.colorNeutralForeground3 }} />;
        case TodoItemStatus.Failed:
            return <DismissRegular className={styles.taskItemIcon} style={{ color: tokens.colorPaletteRedForeground1 }} />;
        default:
            return <CircleRegular className={styles.taskItemIcon} style={{ opacity: 0.5 }} />;
    }
});

const Status = memo(({ item }: { item: TodoItem }) => {
    const styles = useTodoPlanContentStyles();

    const timestampString = useMemo(() => {
        const timestamp = item.startedAt || item.completedAt;

        if (!timestamp) return '';

        const date = getSafeDateTime(timestamp);

        return date.toLocaleString();
    }, [item.startedAt, item.completedAt]);

    return (
        <Text
            block={true}
            className={styles.taskItemContent}
            style={{
                width: '100%',
                opacity: item.status === TodoItemStatus.Pending ? 0.5 : 1,
            }}
        >
            {item.content} {timestampString && <Caption2 as={'span'}>{timestampString}</Caption2>}
        </Text>
    );
});

export default memo(TodoPlanContent);

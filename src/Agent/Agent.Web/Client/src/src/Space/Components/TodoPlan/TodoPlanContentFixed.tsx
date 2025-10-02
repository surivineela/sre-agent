import { mergeClasses } from '@fluentui/react-components';
import { CheckmarkFilled } from '@fluentui/react-icons';
import { memo, useMemo } from 'react';
import { TodoItem, TodoItemStatus, TodoPlan } from '../../../Common/Contracts/DataPlane/TodoPlan';
import { todoPlanAnimations, useTodoPlanContentStyles } from '../../Styles/TodoPlan.styles';

interface TodoPlanContentFixedProps {
    plan: TodoPlan;
}

const TodoPlanContentFixed = ({ plan }: TodoPlanContentFixedProps) => {
    const styles = useTodoPlanContentStyles();

    const getTaskDotClass = (status: TodoItemStatus) => {
        switch (status) {
            case TodoItemStatus.Completed:
                return mergeClasses(styles.taskDot, styles.taskDotCompleted);
            case TodoItemStatus.InProgress:
                return mergeClasses(styles.taskDot, styles.taskDotInProgress);
            case TodoItemStatus.Failed:
                return mergeClasses(styles.taskDot, styles.taskDotFailed);
            case TodoItemStatus.Pending:
            default:
                return mergeClasses(styles.taskDot, styles.taskDotPending);
        }
    };

    const formatTimestamp = (timestamp?: string) => {
        if (!timestamp) return null;
        const date = new Date(timestamp);
        const now = new Date();
        const diffMs = now.getTime() - date.getTime();
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMins / 60);

        if (diffMins < 1) return 'now';
        if (diffMins < 60) return `${diffMins}m`;
        if (diffHours < 24) return `${diffHours}h`;

        const isToday = date.toDateString() === now.toDateString();
        if (isToday) {
            return date.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit', hour12: true });
        }
        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    };

    const sortedItems = useMemo(() => {
        return [...plan.items].sort((a, b) => a.order - b.order);
    }, [plan.items]);

    return (
        <>
            <style>{todoPlanAnimations}</style>

            <div className={styles.container}>
                <div className={styles.timeline}>
                    <div className={styles.timelineLine} />

                    {sortedItems.map((item: TodoItem, index) => (
                        <div
                            key={index}
                            className={mergeClasses(styles.taskItem, index === sortedItems.length - 1 ? styles.taskItemLast : undefined)}
                        >
                            <div className={getTaskDotClass(item.status)}>
                                {item.status === TodoItemStatus.Completed && <CheckmarkFilled className={styles.completedIcon} />}
                                {item.status === TodoItemStatus.Pending && <div className={styles.innerDotPending} />}
                                {item.status === TodoItemStatus.Failed && <div className={styles.innerDotFailed} />}
                            </div>

                            <div className={styles.taskContent}>
                                <p
                                    className={mergeClasses(
                                        styles.taskText,
                                        item.status === TodoItemStatus.Completed ? styles.taskTextCompleted : undefined
                                    )}
                                >
                                    {item.content}
                                    {item.status === TodoItemStatus.Completed && item.completedAt && (
                                        <span className={mergeClasses(styles.taskMeta, styles.taskMetaInline)}>
                                            · {formatTimestamp(item.completedAt)}
                                        </span>
                                    )}
                                </p>

                                {item.status === TodoItemStatus.InProgress && item.startedAt && (
                                    <div className={styles.taskMeta}>Started {formatTimestamp(item.startedAt)}</div>
                                )}
                            </div>
                        </div>
                    ))}
                </div>
            </div>
        </>
    );
};

export default memo(TodoPlanContentFixed);

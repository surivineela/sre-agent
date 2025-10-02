import { tokens } from '@fluentui/react-components';
import { CheckmarkFilled, CircleFilled, ClockFilled, DismissFilled } from '@fluentui/react-icons';
import { memo, useMemo } from 'react';
import { TodoItemStatus } from '../../../Common/Contracts/DataPlane/TodoPlan';
import { useTodoItemStatusPillStyles } from '../../Styles/TodoPlan.styles';

interface TodoItemStatusPillProps {
    status: TodoItemStatus;
    showIcon?: boolean;
}

const TodoItemStatusPill = ({ status, showIcon = true }: TodoItemStatusPillProps) => {
    const styles = useTodoItemStatusPillStyles();

    const statusProps = useMemo(() => {
        switch (status) {
            case TodoItemStatus.Pending:
                return {
                    icon: CircleFilled,
                    text: 'Pending',
                    backgroundColor: tokens.colorNeutralBackground2,
                    textColor: tokens.colorNeutralForeground3,
                    iconColor: tokens.colorNeutralForeground3,
                };
            case TodoItemStatus.InProgress:
                return {
                    icon: ClockFilled,
                    text: 'Active',
                    backgroundColor: tokens.colorBrandBackground,
                    textColor: tokens.colorBrandForeground2,
                    iconColor: tokens.colorBrandForeground2,
                };
            case TodoItemStatus.Completed:
                return {
                    icon: CheckmarkFilled,
                    text: 'Done',
                    backgroundColor: tokens.colorNeutralBackground2,
                    textColor: tokens.colorNeutralForeground3,
                    iconColor: tokens.colorNeutralForeground3,
                };
            case TodoItemStatus.Failed:
                return {
                    icon: DismissFilled,
                    text: 'Failed',
                    backgroundColor: tokens.colorNeutralBackground2,
                    textColor: tokens.colorNeutralForeground3,
                    iconColor: tokens.colorNeutralForeground3,
                };
            default:
                return {
                    icon: CircleFilled,
                    text: 'Unknown',
                    backgroundColor: tokens.colorNeutralBackground2,
                    textColor: tokens.colorNeutralForeground3,
                    iconColor: tokens.colorNeutralForeground3,
                };
        }
    }, [status]);

    return (
        <div
            className={styles.statusContainer}
            style={{
                backgroundColor: statusProps.backgroundColor,
                color: statusProps.textColor,
            }}
        >
            {showIcon && <statusProps.icon className={styles.icon} style={{ color: statusProps.iconColor }} />}
            <span>{statusProps.text}</span>
        </div>
    );
};

export default memo(TodoItemStatusPill);

import { Card, Text } from '@fluentui/react-components';
import { FC } from 'react';
import { useScheduledTasksStyles } from '../ScheduledTasks.styles';

interface ScheduledTaskCardProps {
    title: string;
    count: number;
}

export const ScheduledTaskCard: FC<ScheduledTaskCardProps> = ({ title, count }) => {
    const styles = useScheduledTasksStyles();

    return (
        <Card className={styles.card}>
            <div className={styles.cardContent}>
                <Text size={400}>{title}</Text>
                <Text size={700} weight="semibold">
                    {count}
                </Text>
            </div>
        </Card>
    );
};

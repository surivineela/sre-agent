import { Body1, Subtitle1 } from '@fluentui/react-components';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ScheduledTasksResources } from '../../../Strings/SREAgentResources';
import { useScheduledTasks } from '../../Hooks/useScheduledTasks';
import { useScheduledTasksStyles } from './ScheduledTasks.styles';
import { ScheduledTasksDataGrid } from './ScheduledTasksDataGrid';

export const ScheduledTasks: FC = () => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();
    const { scheduledTasks, loading: isScheduledTasksLoading } = useScheduledTasks();

    return (
        <div className={styles.root}>
            <div className={styles.content}>
                <div className={styles.padding}>
                    <div className={styles.title}>
                        <Subtitle1 as="h3" style={{ margin: 0 }}>
                            {intl.formatMessage(ScheduledTasksResources.tasks)}
                        </Subtitle1>
                        <Body1>{intl.formatMessage(ScheduledTasksResources.scheduledTasksDescription)}</Body1>
                    </div>
                    <ScheduledTasksDataGrid scheduledTasks={scheduledTasks} isScheduledTasksLoading={isScheduledTasksLoading} />
                </div>
            </div>
        </div>
    );
};

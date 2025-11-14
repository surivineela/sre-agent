import { Caption1, Caption1Stronger, MenuItem } from '@fluentui/react-components';
import { PlayRegular } from '@fluentui/react-icons';
import * as React from 'react';
import { useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessageOrStringify } from '../../Common/Clients/ArmClient';
import { ScheduledTasksClient } from '../../Common/Clients/ScheduledTasksClient';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { ScheduledTasksResources } from '../../Strings/SREAgentResources';
import { ScheduledTask } from '../Contracts/ScheduledTasks';
import { EntityIcon } from '../Graph/EntityIcon';
import ScheduledTaskStatusBadge from '../ScheduledTasks/V2/Common/ScheduledTaskStatusBadge';
import { getHumanReadableCronExpression, GroupMessageKey } from '../ScheduledTasks/V2/ScheduledTasksUtilities';
import ScheduledTaskChatMessage from './ScheduledTaskChatMessage';

interface ScheduledTaskCreationChatMessageProps {
    task: ScheduledTask;
}

const ScheduledTaskCreationChatMessage: React.FC<ScheduledTaskCreationChatMessageProps> = ({ task }) => {
    const intl = useIntl();

    const azPortalContext = useContext(AzPortalContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const scheduledTasksClient = useMemo(
        () => ScheduledTasksClient.getInstance(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
        [azPortalContext, sreAgentEndpoint]
    );

    const [isRunningSchedulledTask, setIsRunningScheduledTask] = React.useState(false);

    const onRunTasks = useCallback(async () => {
        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(ScheduledTasksResources.runTasksTitle),
            intl.formatMessage(ScheduledTasksResources.runTasksInProgress)
        );

        setIsRunningScheduledTask(true);
        const response = await scheduledTasksClient.runScheduledTask(task.id);
        if (response.isSuccessful) {
            azPortalContext.stopNotification(notificationId, true, intl.formatMessage(ScheduledTasksResources.tasksRanSuccessfully));
        } else {
            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(ScheduledTasksResources.failedToRunTask, {
                    errorMessage: getErrorMessageOrStringify(response.error),
                })
            );
        }

        setIsRunningScheduledTask(false);
    }, [azPortalContext, intl, scheduledTasksClient, task.id]);

    return (
        <ScheduledTaskChatMessage
            name={task.name}
            media={<EntityIcon type={'scheduledTask'} shorthandStyle={{ wrapperSize: 40, iconSize: 28, borderRadius: 8 }} />}
            description={task.description}
            secondaryText={intl.formatMessage(ScheduledTasksResources.taskCreatedSuccessfully)}
            footer={{
                status: <ScheduledTaskStatusBadge status={task.status} />,
                timestamp: (
                    <Caption1>
                        {intl.formatMessage(ScheduledTasksResources.createdTimestampBadgeText)}{' '}
                        <Caption1Stronger>{getSafeDateTime(task.createdAt).toLocaleString()}</Caption1Stronger>
                    </Caption1>
                ),
                schedule: (
                    <Caption1>
                        {intl.formatMessage(ScheduledTasksResources.scheduleBadgeText)}{' '}
                        <Caption1Stronger>{getHumanReadableCronExpression(task.cronExpression, intl)}</Caption1Stronger>
                    </Caption1>
                ),
                messageGrouping: task.threadId ? GroupMessageKey.SameThread : GroupMessageKey.NewThread,
            }}
            actions={{
                menuItems: (
                    <>
                        <MenuItem icon={<PlayRegular />} onClick={onRunTasks} disabled={isRunningSchedulledTask}>
                            {intl.formatMessage(ScheduledTasksResources.runTaskNow)}
                        </MenuItem>
                    </>
                ),
            }}
        />
    );
};

export default React.memo(ScheduledTaskCreationChatMessage);

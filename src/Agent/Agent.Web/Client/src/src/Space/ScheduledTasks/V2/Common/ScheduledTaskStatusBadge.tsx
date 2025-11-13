import { Badge } from '@fluentui/react-components';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { ScheduledTasksResources } from '../../../../Strings/SREAgentResources';
import { ScheduledTaskStatus } from '../../../Contracts/ScheduledTasks';

const ScheduledTaskStatusBadge = ({ status }: { status: ScheduledTaskStatus }) => {
    const intl = useIntl();

    switch (status) {
        case ScheduledTaskStatus.Active:
            return (
                <Badge appearance="tint" color="success">
                    {intl.formatMessage(ScheduledTasksResources.on)}
                </Badge>
            );
        case ScheduledTaskStatus.Paused:
            return (
                <Badge appearance="tint" color="severe">
                    {intl.formatMessage(ScheduledTasksResources.off)}
                </Badge>
            );
        case ScheduledTaskStatus.Completed:
            return (
                <Badge appearance="tint" color="informative">
                    {intl.formatMessage(ScheduledTasksResources.ended)}
                </Badge>
            );
        default:
            return (
                <Badge appearance="tint" color="informative">
                    {status}
                </Badge>
            );
    }
};

export default memo(ScheduledTaskStatusBadge);

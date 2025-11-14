import { Badge, BadgeProps, Caption1, Caption1Stronger } from '@fluentui/react-components';
import {
    ArrowClockwiseRegular,
    CheckmarkCircleRegular,
    CircleRegular,
    ClockRegular,
    DismissCircleRegular,
    SubtractCircleRegular,
} from '@fluentui/react-icons';
import * as React from 'react';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { ScheduledTasksResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ScheduledTask, ScheduledTaskStatus } from '../Contracts/ScheduledTasks';
import { EntityIcon } from '../Graph/EntityIcon';
import ScheduledTaskChatMessage from './ScheduledTaskChatMessage';

export interface ScheduledTaskExecutionChatMessageProps {
    task: ScheduledTask;
    executionTime?: Date;
}

const ScheduledTaskExecutionChatMessage: React.FC<ScheduledTaskExecutionChatMessageProps> = ({ task, executionTime }) => {
    const intl = useIntl();

    return (
        <div>
            <ScheduledTaskChatMessage
                name={task.name}
                media={<EntityIcon type={'scheduledTaskRun'} shorthandStyle={{ wrapperSize: 40, iconSize: 28, borderRadius: 8 }} />}
                description={task.description}
                secondaryText={intl.formatMessage(SreAgentResources.scheduledTaskExecutionTitle)}
                footer={{
                    status: <Status status={task.status} />,
                    timestamp: executionTime ? (
                        <Caption1>
                            {intl.formatMessage(ScheduledTasksResources.startTimestampBadgeText)}{' '}
                            <Caption1Stronger>{getSafeDateTime(executionTime).toLocaleString()}</Caption1Stronger>
                        </Caption1>
                    ) : undefined,
                }}
            />
        </div>
    );
};

const Status = memo(({ status }: { status: string | ScheduledTaskStatus }) => {
    const intl = useIntl();

    const getStatusTextAndIcon = (status?: string | ScheduledTaskStatus): { text?: string; icon: JSX.Element | null } => {
        const s = (status || '').toLowerCase();

        switch (s) {
            case ScheduledTaskStatus.Active.toLowerCase():
                return { text: intl.formatMessage(SreAgentResources.active), icon: <ClockRegular /> };
            case ScheduledTaskStatus.Paused.toLowerCase():
                return { text: intl.formatMessage(SreAgentResources.paused), icon: <CircleRegular /> };
            case ScheduledTaskStatus.Completed.toLowerCase():
            case 'complete':
            case 'success':
                return { text: intl.formatMessage(SreAgentResources.completed), icon: <CheckmarkCircleRegular /> };
            case ScheduledTaskStatus.Failed.toLowerCase():
            case 'fail':
            case 'error':
                return { text: intl.formatMessage(SreAgentResources.failed), icon: <DismissCircleRegular /> };
            case 'running':
            case 'in-progress':
                return { text: intl.formatMessage(SreAgentResources.running), icon: <ArrowClockwiseRegular /> };
            case 'cancel':
                return { text: intl.formatMessage(SreAgentResources.canceled), icon: <SubtractCircleRegular /> };
            case 'schedule':
                return { text: intl.formatMessage(SreAgentResources.scheduled), icon: <ClockRegular /> };
            default:
                return { text: status, icon: null };
        }
    };

    const getStatusBadgeColor = (status?: string | ScheduledTaskStatus): BadgeProps['color'] => {
        const s = (status || '').toLowerCase();
        switch (s) {
            case ScheduledTaskStatus.Active.toLowerCase():
            case 'running':
            case 'in-progress':
            case 'schedule':
                return 'brand';
            case ScheduledTaskStatus.Paused.toLowerCase():
            case 'cancel':
                return 'informative';
            case ScheduledTaskStatus.Completed.toLowerCase():
            case 'complete':
            case 'success':
                return 'success';
            case ScheduledTaskStatus.Failed.toLowerCase():
            case 'fail':
            case 'error':
                return 'danger';
            default:
                return 'important';
        }
    };
    const { text, icon } = getStatusTextAndIcon(status);
    const color = getStatusBadgeColor(status);

    return (
        <Badge appearance={icon ? 'filled' : 'outline'} color={color} icon={icon} size={'large'}>
            {text ? text : intl.formatMessage(SreAgentResources.unknownStatus)}
        </Badge>
    );
});

export default memo(ScheduledTaskExecutionChatMessage);

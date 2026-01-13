import { Spinner, tokens } from '@fluentui/react-components';
import { CheckmarkCircle32Filled, DismissCircle32Filled, ErrorCircleFilled, SearchSparkleColor } from '@fluentui/react-icons';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { AgentTaskMetaData, AgentTaskStatus } from '../../Common/Contracts/DataPlane/AgentTask';
import { AgentTaskResources } from '../../Strings/SREAgentResources';
import { ChatBoxSidePanelContext } from '../Contracts/Context';
import ApprovalMessage, { IApprovalMessageProps } from './ApprovalMessage';
import { SpecialMessageCard } from './Chat/SpecialMessageCard';

interface IAgentTaskChatMessageProps extends IApprovalMessageProps {
    agentTask: AgentTaskMetaData;
}

const AgentTaskChatMessage = ({ agentTask, ...rest }: IAgentTaskChatMessageProps) => {
    const { openAgentTask } = useContext(ChatBoxSidePanelContext);

    const intl = useIntl();

    const StatusIcon = () => {
        switch (agentTask.status?.toLowerCase()) {
            case AgentTaskStatus.Complete.toLowerCase():
                return <CheckmarkCircle32Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />;
            case AgentTaskStatus.InProgress.toLowerCase():
                return <Spinner size={'medium'} />;
            case AgentTaskStatus.Failed.toLowerCase():
                return <ErrorCircleFilled fontSize={32} style={{ color: tokens.colorPaletteRedForeground1 }} />;
            case AgentTaskStatus.Cancelled.toLowerCase():
                return <DismissCircle32Filled />;
            default:
                return <SearchSparkleColor fontSize={32} />;
        }
    };

    return (
        <SpecialMessageCard
            icon={<StatusIcon />}
            primaryText={agentTask.title ?? ''}
            secondaryText={intl.formatMessage(AgentTaskResources.deepInvestigation)}
            onClick={() => {
                openAgentTask(agentTask);
            }}
        >
            {rest.approval && <ApprovalMessage {...rest} />}
        </SpecialMessageCard>
    );
};

export default memo(AgentTaskChatMessage);

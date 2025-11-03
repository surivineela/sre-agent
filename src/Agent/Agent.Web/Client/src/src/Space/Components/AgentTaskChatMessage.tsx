import { EntityCard, EntityTitle } from '@fluentui-copilot/react-copilot';
import { Link, makeStyles, Spinner, Subtitle2, Text, tokens } from '@fluentui/react-components';
import { CheckmarkCircle32Filled, DismissCircle32Filled, ErrorCircleFilled, SearchSparkleColor } from '@fluentui/react-icons';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { AgentTaskMetaData, AgentTaskStatus } from '../../Common/Contracts/DataPlane/AgentTask';
import { AgentTaskResources } from '../../Strings/SREAgentResources';
import { ChatBoxSidePanelContext } from '../Contracts/Context';
import ApprovalMessage, { IApprovalMessageProps } from './ApprovalMessage';

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        padding: '5px 0px',
    },
    text: {
        paddingLeft: '3px',
    },
});

interface IAgentTaskChatMessageProps extends IApprovalMessageProps {
    agentTask: AgentTaskMetaData;
}

const AgentTaskChatMessage = ({ agentTask, ...rest }: IAgentTaskChatMessageProps) => {
    const { openAgentTask } = useContext(ChatBoxSidePanelContext);

    const styles = useStyles();
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
                return <SearchSparkleColor />;
        }
    };

    return (
        <div className={styles.root}>
            <Text className={styles.text}>{intl.formatMessage(AgentTaskResources.chatMessageIntro)}</Text>
            <EntityCard
                orientation="horizontal"
                role="group"
                style={{ maxWidth: 'unset' }}
                entityTitle={
                    <EntityTitle
                        media={<StatusIcon />}
                        primaryText={
                            <Link
                                appearance="subtle"
                                onClick={e => {
                                    e.stopPropagation();
                                    openAgentTask(agentTask);
                                }}
                            >
                                <Subtitle2 wrap={true} block={true}>
                                    {agentTask.title}
                                </Subtitle2>
                            </Link>
                        }
                        secondaryText={intl.formatMessage(AgentTaskResources.deepInvestigation)}
                    />
                }
            />
            {rest.approval && <ApprovalMessage {...rest} />}
        </div>
    );
};

export default memo(AgentTaskChatMessage);

import { EntityCard, EntityTitle } from '@fluentui-copilot/react-copilot';
import { Link, makeStyles, Subtitle2, Text } from '@fluentui/react-components';
import { SearchSparkleColor } from '@fluentui/react-icons';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { AgentTaskMetaData } from '../../Common/Contracts/DataPlane/AgentTask';
import { AgentTaskResources } from '../../Strings/SREAgentResources';
import { ChatBoxContext } from '../Contracts/Context';

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

const AgentTaskChatMessage = ({ agentTask }: { agentTask: AgentTaskMetaData }) => {
    const { openAgentTask } = useContext(ChatBoxContext);

    const styles = useStyles();
    const intl = useIntl();

    return (
        <div className={styles.root}>
            <Text className={styles.text}>{intl.formatMessage(AgentTaskResources.chatMessageIntro)}</Text>
            <EntityCard
                orientation="horizontal"
                role="group"
                style={{ maxWidth: 'unset' }}
                entityTitle={
                    <EntityTitle
                        media={<SearchSparkleColor fontSize={'32px'} />}
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
        </div>
    );
};

export default memo(AgentTaskChatMessage);

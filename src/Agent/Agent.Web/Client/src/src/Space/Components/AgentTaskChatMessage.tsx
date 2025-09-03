import { Card, CardHeader, Link, makeStyles, Subtitle2, Text, tokens } from '@fluentui/react-components';
import { SearchSparkleColor } from '@fluentui/react-icons';
import { memo, useContext } from 'react';
import { FormattedMessage } from 'react-intl';
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
    card: {
        height: 'fit-content',
        padding: '20px',
        borderRadius: tokens.borderRadiusLarge,
    },
    header: {
        width: '100%',
    },
});

const AgentTaskChatMessage = ({ agentTask }: { agentTask: AgentTaskMetaData }) => {
    const { openAgentTask } = useContext(ChatBoxContext);

    const styles = useStyles();

    return (
        <div className={styles.root}>
            <Text>
                <FormattedMessage {...AgentTaskResources.chatMessageIntro} />
            </Text>
            <Card
                orientation="horizontal"
                className={styles.card}
                onClick={e => {
                    e.stopPropagation();
                }}
            >
                <CardHeader
                    className={styles.header}
                    image={<SearchSparkleColor fontSize={32} />}
                    header={
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
                />
            </Card>
        </div>
    );
};

export default memo(AgentTaskChatMessage);

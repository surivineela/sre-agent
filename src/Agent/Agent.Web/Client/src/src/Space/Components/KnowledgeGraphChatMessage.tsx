import { EntityCard, EntityTitle } from '@fluentui-copilot/react-copilot';
import { Link, makeStyles, Subtitle2, Text } from '@fluentui/react-components';
import { Diagram24Regular } from '@fluentui/react-icons';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { KnowledgeGraphSearchResult } from '../../Common/Contracts/DataPlane/Message';
import { KnowledgeGraphCardResources } from '../../Strings/SREAgentResources';
import { ChatBoxSidePanelContext } from '../Contracts/Context';

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

const KnowledgeGraphChatMessage = ({ knowledgeGraphSearchResult }: { knowledgeGraphSearchResult: KnowledgeGraphSearchResult }) => {
    const { openKnowledgeGraphSearchResult } = useContext(ChatBoxSidePanelContext);
    const intl = useIntl();

    const styles = useStyles();

    // Build subtitle text
    const buildSubtitle = () => {
        const parts = [];

        if (knowledgeGraphSearchResult.totalEntities > 0) {
            parts.push(`${knowledgeGraphSearchResult.totalEntities} entities`);
        }
        if (knowledgeGraphSearchResult.totalRelations > 0) {
            parts.push(`${knowledgeGraphSearchResult.totalRelations} relations`);
        }

        if (parts.length === 0) {
            parts.push('No results found');
        }

        return parts.join(' • ');
    };

    return (
        <div className={styles.root}>
            <Text className={styles.text}>{intl.formatMessage(KnowledgeGraphCardResources.knowledgeGraphSearchResultsIntro)}</Text>
            <EntityCard
                orientation="horizontal"
                role="group"
                style={{ maxWidth: 'unset' }}
                entityTitle={
                    <EntityTitle
                        media={<Diagram24Regular />}
                        primaryText={
                            <Link
                                appearance="subtle"
                                onClick={e => {
                                    e.stopPropagation();
                                    openKnowledgeGraphSearchResult(knowledgeGraphSearchResult);
                                }}
                            >
                                <Subtitle2 wrap={true} block={true}>
                                    {intl.formatMessage(KnowledgeGraphCardResources.viewKnowledgeGraphSearchResults)}
                                </Subtitle2>
                            </Link>
                        }
                        secondaryText={buildSubtitle()}
                    />
                }
            />
        </div>
    );
};

export default memo(KnowledgeGraphChatMessage);

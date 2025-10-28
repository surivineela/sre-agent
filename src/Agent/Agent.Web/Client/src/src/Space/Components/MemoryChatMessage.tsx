import { EntityCard, EntityTitle } from '@fluentui-copilot/react-copilot';
import { Link, makeStyles, Subtitle2, Text } from '@fluentui/react-components';
import { Book24Regular } from '@fluentui/react-icons';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { MemorySearchResult } from '../../Common/Contracts/DataPlane/Message';
import { MemorySearchCardResources } from '../../Strings/SREAgentResources';
import { MemorySidePanelContext } from '../Contracts/Context';

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

const MemoryChatMessage = ({ memorySearchResult }: { memorySearchResult: MemorySearchResult }) => {
    const { openMemorySidePanel } = useContext(MemorySidePanelContext);
    const intl = useIntl();

    const styles = useStyles();

    // Build subtitle text
    const buildSubtitle = () => {
        const parts = [];
        parts.push(`${memorySearchResult.totalResults} relevant items found`);

        if (memorySearchResult.documents && memorySearchResult.documents.length > 0) {
            parts.push(`${memorySearchResult.documents.length} documents`);
        }
        if (memorySearchResult.sameResourceTrajectories && memorySearchResult.sameResourceTrajectories.length > 0) {
            parts.push(`${memorySearchResult.sameResourceTrajectories.length} incidents on same resource`);
        }
        if (memorySearchResult.similarSymptomsTrajectories && memorySearchResult.similarSymptomsTrajectories.length > 0) {
            parts.push(`${memorySearchResult.similarSymptomsTrajectories.length} incidents with similar symptoms`);
        }
        if (memorySearchResult.userMemories && memorySearchResult.userMemories.length > 0) {
            parts.push(`${memorySearchResult.userMemories.length} memories`);
        }

        return parts.join(' • ');
    };

    return (
        <div className={styles.root}>
            <Text className={styles.text}>{intl.formatMessage(MemorySearchCardResources.memorySearchResultsIntro)}</Text>
            <EntityCard
                orientation="horizontal"
                role="group"
                style={{ maxWidth: 'unset' }}
                entityTitle={
                    <EntityTitle
                        media={<Book24Regular />}
                        primaryText={
                            <Link
                                appearance="subtle"
                                onClick={e => {
                                    e.stopPropagation();
                                    openMemorySidePanel(memorySearchResult);
                                }}
                            >
                                <Subtitle2 wrap={true} block={true}>
                                    {intl.formatMessage(MemorySearchCardResources.viewMemorySearchResults)}
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

export default memo(MemoryChatMessage);

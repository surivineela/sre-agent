import { Book24Regular } from '@fluentui/react-icons';
import { memo, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { MemorySearchResult } from '../../Common/Contracts/DataPlane/Message';
import { MemorySearchCardResources } from '../../Strings/SREAgentResources';
import { ChatBoxSidePanelContext } from '../Contracts/Context';
import { SpecialMessageCard } from './Chat/SpecialMessageCard';
import CitationList from './Citations/CitationList';

const MemoryChatMessage = ({ memorySearchResult }: { memorySearchResult: MemorySearchResult }) => {
    const { openMemorySearchResult } = useContext(ChatBoxSidePanelContext);
    const intl = useIntl();

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

    // Combine all trajectories for citation list
    const allTrajectories = useMemo(
        () => [...(memorySearchResult.sameResourceTrajectories || []), ...(memorySearchResult.similarSymptomsTrajectories || [])],
        [memorySearchResult.sameResourceTrajectories, memorySearchResult.similarSymptomsTrajectories]
    );

    return (
        <SpecialMessageCard
            icon={<Book24Regular />}
            primaryText={intl.formatMessage(MemorySearchCardResources.viewMemorySearchResults)}
            secondaryText={buildSubtitle()}
            onClick={() => {
                openMemorySearchResult(memorySearchResult);
            }}
        >
            <CitationList
                documents={memorySearchResult.documents || []}
                trajectories={allTrajectories}
                userMemories={memorySearchResult.userMemories || []}
                memorySearchResult={memorySearchResult}
            />
        </SpecialMessageCard>
    );
};

export default memo(MemoryChatMessage);

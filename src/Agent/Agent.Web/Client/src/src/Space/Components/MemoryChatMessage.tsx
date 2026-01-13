import { Book24Regular } from '@fluentui/react-icons';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { MemorySearchResult } from '../../Common/Contracts/DataPlane/Message';
import { MemorySearchCardResources } from '../../Strings/SREAgentResources';
import { ChatBoxSidePanelContext } from '../Contracts/Context';
import { SpecialMessageCard } from './Chat/SpecialMessageCard';

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

    return (
        <SpecialMessageCard
            icon={<Book24Regular />}
            primaryText={intl.formatMessage(MemorySearchCardResources.viewMemorySearchResults)}
            secondaryText={buildSubtitle()}
            onClick={() => {
                openMemorySearchResult(memorySearchResult);
            }}
        />
    );
};

export default memo(MemoryChatMessage);

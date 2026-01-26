import { Button, Caption1Strong, makeStyles, tokens } from '@fluentui/react-components';
import { ChevronDown16Regular, ChevronUp16Regular } from '@fluentui/react-icons';
import { memo, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { DocumentResult, MemorySearchResult, TrajectoryResult } from '../../../Common/Contracts/DataPlane/Message';
import { dedupeById } from '../../../Common/Helpers/Array';
import { MemorySearchCardResources } from '../../../Strings/SREAgentResources';
import { ChatBoxSidePanelContext, MemorySearchFocusOptions } from '../../Contracts/Context';
import DocumentCitation from './DocumentCitation';
import MemoryCitation from './MemoryCitation';
import TrajectoryInsightCitation from './TrajectoryInsightCitation';

const COLLAPSED_ITEM_COUNT = 3;

interface CitationListProps {
    documents: DocumentResult[];
    trajectories: TrajectoryResult[];
    userMemories: string[];
    memorySearchResult: MemorySearchResult;
}

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        marginTop: tokens.spacingVerticalS,
    },
    list: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    toggleButton: {
        alignSelf: 'flex-start',
    },
});

const CitationList = ({ documents, trajectories, userMemories, memorySearchResult }: CitationListProps) => {
    const styles = useStyles();
    const intl = useIntl();
    const { openMemorySearchResult } = useContext(ChatBoxSidePanelContext);

    const dedupedDocs = useMemo(() => dedupeById(documents || []), [documents]);
    const dedupedTrajectories = useMemo(() => dedupeById(trajectories || []), [trajectories]);
    const dedupedMemories = useMemo(() => [...new Set(userMemories || [])], [userMemories]);

    const totalCount = dedupedDocs.length + dedupedTrajectories.length + dedupedMemories.length;
    const hasAnySources = totalCount > 0;
    const shouldCollapse = totalCount > COLLAPSED_ITEM_COUNT;

    const [isExpanded, setIsExpanded] = useState(!shouldCollapse);

    const openPanelWithFocus = useCallback(
        (focusOptions: MemorySearchFocusOptions) => {
            openMemorySearchResult(memorySearchResult, focusOptions);
        },
        [openMemorySearchResult, memorySearchResult]
    );

    const handleDocumentClick = useCallback(
        (doc: DocumentResult) => {
            // Documents with URLs open external link directly
            if (doc.url) {
                window.open(doc.url, '_blank', 'noopener,noreferrer');
            } else {
                // Documents without URLs open panel with focus
                openPanelWithFocus({ itemId: doc.id, itemType: 'document' });
            }
        },
        [openPanelWithFocus]
    );

    const handleTrajectoryClick = useCallback(
        (trajectory: TrajectoryResult) => {
            openPanelWithFocus({ itemId: trajectory.id, itemType: 'trajectory' });
        },
        [openPanelWithFocus]
    );

    const handleMemoryClick = useCallback(
        (index: number) => {
            openPanelWithFocus({ itemIndex: index, itemType: 'memory' });
        },
        [openPanelWithFocus]
    );

    // Build combined list of citation elements for slicing when collapsed
    const allCitations = useMemo(() => {
        const items: { key: string; element: React.ReactNode }[] = [];

        dedupedDocs.forEach(doc => {
            items.push({
                key: `doc-${doc.id}`,
                element: <DocumentCitation key={doc.id} title={doc.title} url={doc.url} onClick={() => handleDocumentClick(doc)} />,
            });
        });

        dedupedTrajectories.forEach(trajectory => {
            items.push({
                key: `traj-${trajectory.id}`,
                element: (
                    <TrajectoryInsightCitation
                        key={trajectory.id}
                        title={trajectory.title}
                        onClick={() => handleTrajectoryClick(trajectory)}
                    />
                ),
            });
        });

        dedupedMemories.forEach((memory, index) => {
            items.push({
                key: `mem-${index}`,
                element: <MemoryCitation key={index} text={memory} onClick={() => handleMemoryClick(index)} />,
            });
        });

        return items;
    }, [dedupedDocs, dedupedTrajectories, dedupedMemories, handleDocumentClick, handleTrajectoryClick, handleMemoryClick]);

    const visibleCitations = isExpanded ? allCitations : allCitations.slice(0, COLLAPSED_ITEM_COUNT);
    const hiddenCount = totalCount - COLLAPSED_ITEM_COUNT;

    const toggleExpanded = useCallback(() => {
        setIsExpanded(prev => !prev);
    }, []);

    if (!hasAnySources) {
        return null;
    }

    return (
        <div className={styles.container}>
            <Caption1Strong>{intl.formatMessage(MemorySearchCardResources.sourcesHeader)}</Caption1Strong>
            <div className={styles.list} role="list">
                {visibleCitations.map(item => item.element)}
            </div>
            {shouldCollapse && (
                <Button
                    className={styles.toggleButton}
                    appearance="subtle"
                    size="small"
                    icon={isExpanded ? <ChevronUp16Regular /> : <ChevronDown16Regular />}
                    onClick={toggleExpanded}
                    aria-expanded={isExpanded}
                >
                    {isExpanded
                        ? intl.formatMessage(MemorySearchCardResources.showFewer)
                        : intl.formatMessage(MemorySearchCardResources.showMore, { count: hiddenCount })}
                </Button>
            )}
        </div>
    );
};

export default memo(CitationList);

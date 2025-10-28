import { Badge, Button, Caption1, Text, Tooltip, makeStyles, mergeClasses, shorthands, tokens } from '@fluentui/react-components';
import { ChevronDownUp16Regular, ChevronUpDown16Regular, SearchSparkle20Regular } from '@fluentui/react-icons';
import { memo, useState } from 'react';
import { useIntl } from 'react-intl';
import { MemorySearchResult } from '../../Common/Contracts/DataPlane/Message';
import { MemorySearchCardResources, SreAgentResources } from '../../Strings/SREAgentResources';

interface MemorySearchCardProps {
    memoryResult: MemorySearchResult;
    className?: string;
}

// Configuration for preview mode when collapsed
const PREVIEW_CONFIG = {
    maxItemsPerSection: 1, // Show top 1 item per section
    maxLinesPerText: 3, // Truncate text to 3 lines
    maxCharsPerLine: 150, // Approximate characters per line for truncation
};

const useStyles = makeStyles({
    card: {
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke1),
        maxWidth: '100%',
        ...shorthands.padding(tokens.spacingVerticalM, tokens.spacingHorizontalM),
        marginBottom: tokens.spacingVerticalS,
    },
    header: {
        display: 'flex',
        alignItems: 'center',
        marginBottom: tokens.spacingVerticalM,
        gap: tokens.spacingHorizontalS,
    },
    headerIcon: {
        fontSize: '20px',
        color: tokens.colorBrandForeground1,
    },
    headerContent: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    headerTitle: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        fontSize: tokens.fontSizeBase300,
    },
    headerSubtitle: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    sectionTitle: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        fontSize: tokens.fontSizeBase300,
    },
    itemsContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    trajectoryItem: {
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalS),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    trajectoryTitle: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        fontSize: tokens.fontSizeBase300,
    },
    trajectoryDetail: {
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase200,
    },
    memoryItem: {
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalS),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase200,
    },
    badge: {
        marginLeft: 'auto',
    },
    collapsedSummary: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
    },
    expandButton: {
        marginLeft: 'auto',
    },
});

const MemorySearchCard = ({ memoryResult, className }: MemorySearchCardProps) => {
    const styles = useStyles();
    const intl = useIntl();
    const [isCollapsed, setIsCollapsed] = useState(true);

    const hasResults = memoryResult.totalResults > 0;

    // Utility function to truncate text
    const truncateText = (text: string, maxLines: number = PREVIEW_CONFIG.maxLinesPerText): string => {
        if (!text) return '';
        const maxChars = maxLines * PREVIEW_CONFIG.maxCharsPerLine;
        if (text.length <= maxChars) return text;
        return text.substring(0, maxChars) + '...';
    };

    // Generate collapsed summary text
    const getCollapsedSummary = () => {
        const parts: string[] = [];
        if (memoryResult.sameResourceTrajectories.length > 0) {
            parts.push(
                `${memoryResult.sameResourceTrajectories.length} past incident${memoryResult.sameResourceTrajectories.length > 1 ? 's' : ''}`
            );
        }
        if (memoryResult.similarSymptomsTrajectories.length > 0) {
            parts.push(
                `${memoryResult.similarSymptomsTrajectories.length} similar symptom${memoryResult.similarSymptomsTrajectories.length > 1 ? 's' : ''}`
            );
        }
        if (memoryResult.userMemories.length > 0) {
            parts.push(`${memoryResult.userMemories.length} user memor${memoryResult.userMemories.length > 1 ? 'ies' : 'y'}`);
        }
        if (memoryResult.documents.length > 0) {
            parts.push(`${memoryResult.documents.length} document${memoryResult.documents.length > 1 ? 's' : ''}`);
        }
        return parts.join(', ');
    };

    const renderTrajectories = (trajectories: any[], title: string, isPreview: boolean = false) => {
        if (trajectories.length === 0) return null;

        const itemsToShow = isPreview ? trajectories.slice(0, PREVIEW_CONFIG.maxItemsPerSection) : trajectories;
        const hasMore = isPreview && trajectories.length > PREVIEW_CONFIG.maxItemsPerSection;

        return (
            <div className={styles.section}>
                <Text className={styles.sectionTitle}>
                    {title} ({trajectories.length})
                </Text>
                <div className={styles.itemsContainer}>
                    {itemsToShow.map((trajectory, index) => (
                        <div key={index} className={styles.trajectoryItem}>
                            <Text className={styles.trajectoryTitle}>{trajectory.title}</Text>
                            {isPreview ? (
                                <>
                                    <Text className={styles.trajectoryDetail}>
                                        {intl.formatMessage(MemorySearchCardResources.symptomsLabel)}{' '}
                                        {truncateText(trajectory.symptomsObserved, 1)}
                                    </Text>
                                    <Text className={styles.trajectoryDetail}>
                                        {intl.formatMessage(MemorySearchCardResources.rootCauseLabel)}{' '}
                                        {truncateText(trajectory.rootCause, 1)}
                                    </Text>
                                    <Text className={styles.trajectoryDetail} style={{ fontStyle: 'italic' }}>
                                        ...
                                    </Text>
                                </>
                            ) : (
                                <>
                                    <Text className={styles.trajectoryDetail}>
                                        {intl.formatMessage(MemorySearchCardResources.symptomsLabel)} {trajectory.symptomsObserved}
                                    </Text>
                                    <Text className={styles.trajectoryDetail}>
                                        {intl.formatMessage(MemorySearchCardResources.rootCauseLabel)} {trajectory.rootCause}
                                    </Text>
                                    <Text className={styles.trajectoryDetail}>
                                        {intl.formatMessage(MemorySearchCardResources.stepsLabel)} {trajectory.stepsFollowed}
                                    </Text>
                                </>
                            )}
                        </div>
                    ))}
                    {hasMore && (
                        <Caption1 className={styles.collapsedSummary}>
                            +{trajectories.length - PREVIEW_CONFIG.maxItemsPerSection} more...
                        </Caption1>
                    )}
                </div>
            </div>
        );
    };

    const renderMemories = (memories: string[], title: string, isPreview: boolean = false) => {
        if (memories.length === 0) return null;

        const itemsToShow = isPreview ? memories.slice(0, PREVIEW_CONFIG.maxItemsPerSection) : memories;
        const hasMore = isPreview && memories.length > PREVIEW_CONFIG.maxItemsPerSection;

        return (
            <div className={styles.section}>
                <Text className={styles.sectionTitle}>
                    {title} ({memories.length})
                </Text>
                <div className={styles.itemsContainer}>
                    {itemsToShow.map((memory, index) => (
                        <div key={index} className={styles.memoryItem}>
                            <Text className={styles.trajectoryDetail}>{isPreview ? truncateText(memory, 2) : memory}</Text>
                        </div>
                    ))}
                    {hasMore && (
                        <Caption1 className={styles.collapsedSummary}>
                            +{memories.length - PREVIEW_CONFIG.maxItemsPerSection} more...
                        </Caption1>
                    )}
                </div>
            </div>
        );
    };

    return (
        <div className={mergeClasses(styles.card, className)}>
            <div className={styles.header}>
                <SearchSparkle20Regular className={styles.headerIcon} />
                <div className={styles.headerContent}>
                    <Text className={styles.headerTitle}>{intl.formatMessage(MemorySearchCardResources.memorySearchResults)}</Text>
                    <Caption1 className={styles.headerSubtitle}>
                        {hasResults
                            ? intl.formatMessage(MemorySearchCardResources.relevantMemoriesFound, {
                                  numMemories: memoryResult.totalResults,
                              })
                            : intl.formatMessage(MemorySearchCardResources.relevantMemoriesNotFound)}
                    </Caption1>
                    {isCollapsed && hasResults && <Caption1 className={styles.collapsedSummary}>{getCollapsedSummary()}</Caption1>}
                </div>
                {hasResults && (
                    <>
                        <Badge appearance="filled" color="brand" className={styles.badge}>
                            {intl.formatMessage(MemorySearchCardResources.memory)}
                        </Badge>
                        <Tooltip
                            relationship="label"
                            content={
                                isCollapsed ? intl.formatMessage(SreAgentResources.expand) : intl.formatMessage(SreAgentResources.collapse)
                            }
                        >
                            <Button
                                icon={isCollapsed ? <ChevronUpDown16Regular /> : <ChevronDownUp16Regular />}
                                onClick={() => setIsCollapsed(!isCollapsed)}
                                size="small"
                                className={styles.expandButton}
                            />
                        </Tooltip>
                    </>
                )}
            </div>

            {hasResults && (
                <div className={styles.content}>
                    {renderTrajectories(
                        memoryResult.sameResourceTrajectories,
                        intl.formatMessage(MemorySearchCardResources.pastIncidentsOnSameResource),
                        isCollapsed
                    )}
                    {renderTrajectories(
                        memoryResult.similarSymptomsTrajectories,
                        intl.formatMessage(MemorySearchCardResources.similarSymptomIncidents),
                        isCollapsed
                    )}
                    {renderMemories(memoryResult.userMemories, intl.formatMessage(MemorySearchCardResources.userMemories), isCollapsed)}
                    {/* Documents are now shown in the side panel instead */}
                </div>
            )}
        </div>
    );
};

export default memo(MemorySearchCard);
export type { MemorySearchCardProps };

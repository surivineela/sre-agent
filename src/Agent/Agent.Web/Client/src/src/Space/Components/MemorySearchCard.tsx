import { Badge, Caption1, Text, makeStyles, mergeClasses, shorthands, tokens } from '@fluentui/react-components';
import { SearchSparkle20Regular } from '@fluentui/react-icons';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { MemorySearchResult } from '../../Common/Contracts/DataPlane/Message';
import { MemorySearchCardResources } from '../../Strings/SREAgentResources';

interface MemorySearchCardProps {
    memoryResult: MemorySearchResult;
    className?: string;
}

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
        fontSize: tokens.fontSizeBase200,
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
        fontSize: tokens.fontSizeBase200,
    },
    trajectoryDetail: {
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase100,
        lineHeight: tokens.lineHeightBase100,
    },
    memoryItem: {
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalS),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
        fontStyle: 'italic',
        lineHeight: tokens.lineHeightBase200,
    },
    badge: {
        marginLeft: 'auto',
    },
});

const MemorySearchCard = ({ memoryResult, className }: MemorySearchCardProps) => {
    const styles = useStyles();
    const intl = useIntl();

    const hasResults = memoryResult.TotalResults > 0;

    const renderTrajectories = (trajectories: any[], title: string) => {
        if (trajectories.length === 0) return null;

        return (
            <div className={styles.section}>
                <Text className={styles.sectionTitle}>
                    {title} ({trajectories.length})
                </Text>
                <div className={styles.itemsContainer}>
                    {trajectories.map((trajectory, index) => (
                        <div key={index} className={styles.trajectoryItem}>
                            <Text className={styles.trajectoryTitle}>{trajectory.Title}</Text>
                            <Text className={styles.trajectoryDetail}>
                                {intl.formatMessage(MemorySearchCardResources.symptoms, { symptoms: trajectory.SymptomsObserved })}
                            </Text>
                            <Text className={styles.trajectoryDetail}>
                                {intl.formatMessage(MemorySearchCardResources.rootCause, { rootCause: trajectory.RootCause })}
                            </Text>
                            <Text className={styles.trajectoryDetail}>
                                {intl.formatMessage(MemorySearchCardResources.steps, { steps: trajectory.StepsFollowed })}
                            </Text>
                        </div>
                    ))}
                </div>
            </div>
        );
    };

    const renderMemories = (memories: string[], title: string) => {
        if (memories.length === 0) return null;

        return (
            <div className={styles.section}>
                <Text className={styles.sectionTitle}>
                    {title} ({memories.length})
                </Text>
                <div className={styles.itemsContainer}>
                    {memories.map((memory, index) => (
                        <div key={index} className={styles.memoryItem}>
                            <Text>{memory}</Text>
                        </div>
                    ))}
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
                                  numMemories: memoryResult.TotalResults,
                              })
                            : intl.formatMessage(MemorySearchCardResources.relevantMemoriesNotFound)}
                    </Caption1>
                </div>
                {hasResults && (
                    <Badge appearance="filled" color="brand" className={styles.badge}>
                        {intl.formatMessage(MemorySearchCardResources.memory)}
                    </Badge>
                )}
            </div>

            {hasResults && (
                <div className={styles.content}>
                    {renderTrajectories(
                        memoryResult.SameResourceTrajectories,
                        intl.formatMessage(MemorySearchCardResources.pastIncidentsOnSameResource)
                    )}
                    {renderTrajectories(
                        memoryResult.SimilarSymptomsTrajectories,
                        intl.formatMessage(MemorySearchCardResources.similarSymptomIncidents)
                    )}
                    {renderMemories(memoryResult.UserMemories, intl.formatMessage(MemorySearchCardResources.userMemories))}
                    {renderMemories(memoryResult.Documents, intl.formatMessage(MemorySearchCardResources.relevantDocuments))}
                </div>
            )}
        </div>
    );
};

export default memo(MemorySearchCard);
export type { MemorySearchCardProps };

import { Badge, Button, Text, makeStyles, mergeClasses, shorthands, tokens } from '@fluentui/react-components';
import { ChevronDown20Regular, ChevronUp20Regular, Document20Regular, Open16Regular } from '@fluentui/react-icons';
import { memo, useState } from 'react';
import { useIntl } from 'react-intl';
import { DocumentResult, MemorySearchResult } from '../../Common/Contracts/DataPlane/Message';
import { MemorySearchCardResources } from '../../Strings/SREAgentResources';

interface MemorySearchPanelContentProps {
    memoryResult: MemorySearchResult;
}

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        maxWidth: '100%',
        overflowX: 'hidden',
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
    documentItem: {
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.padding(tokens.spacingVerticalM, tokens.spacingHorizontalM),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        maxWidth: '100%',
        overflowWrap: 'break-word',
    },
    documentItemClickable: {
        cursor: 'pointer',
        transition: 'all 0.2s ease',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground2Hover,
            ...shorthands.borderColor(tokens.colorBrandStroke1),
            boxShadow: tokens.shadow4,
        },
    },
    documentHeader: {
        display: 'flex',
        alignItems: 'flex-start',
        gap: tokens.spacingHorizontalXS,
        justifyContent: 'space-between',
    },
    documentTitleRow: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        flex: 1,
    },
    documentIcon: {
        fontSize: '16px',
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
    },
    documentTitle: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        fontSize: tokens.fontSizeBase300,
        wordBreak: 'break-word',
        overflowWrap: 'break-word',
    },
    documentMetadata: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalXS,
        alignItems: 'center',
    },
    documentBadge: {
        fontSize: tokens.fontSizeBase100,
    },
    documentSummary: {
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase200,
        wordBreak: 'break-word',
        overflowWrap: 'break-word',
    },
    documentContent: {
        backgroundColor: tokens.colorNeutralBackground3,
        ...shorthands.padding(tokens.spacingVerticalM),
        ...shorthands.borderRadius(tokens.borderRadiusSmall),
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        overflowWrap: 'break-word',
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        maxHeight: '400px',
        overflowY: 'auto',
        marginTop: tokens.spacingVerticalS,
    },
    expandButton: {
        alignSelf: 'flex-start',
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
        wordBreak: 'break-word',
        overflowWrap: 'break-word',
    },
    trajectoryDetail: {
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase200,
        wordBreak: 'break-word',
        overflowWrap: 'break-word',
    },
    memoryItem: {
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalS),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase200,
        wordBreak: 'break-word',
        overflowWrap: 'break-word',
    },
});

const MemorySearchPanelContent = ({ memoryResult }: MemorySearchPanelContentProps) => {
    const styles = useStyles();
    const intl = useIntl();
    const [expandedDocuments, setExpandedDocuments] = useState<Set<string>>(new Set());

    const toggleDocumentExpansion = (documentId: string) => {
        const newExpanded = new Set(expandedDocuments);
        if (newExpanded.has(documentId)) {
            newExpanded.delete(documentId);
        } else {
            newExpanded.add(documentId);
        }
        setExpandedDocuments(newExpanded);
    };

    const renderTrajectory = (trajectory: any) => (
        <div key={trajectory.id} className={styles.trajectoryItem}>
            <Text className={styles.trajectoryTitle}>{trajectory.title}</Text>
            {trajectory.symptomsObserved && (
                <Text className={styles.trajectoryDetail}>
                    <strong>{intl.formatMessage(MemorySearchCardResources.symptomsLabel)}</strong> {trajectory.symptomsObserved}
                </Text>
            )}
            {trajectory.rootCause && (
                <Text className={styles.trajectoryDetail}>
                    <strong>{intl.formatMessage(MemorySearchCardResources.rootCauseLabel)}</strong> {trajectory.rootCause}
                </Text>
            )}
            {trajectory.stepsFollowed && (
                <Text className={styles.trajectoryDetail}>
                    <strong>{intl.formatMessage(MemorySearchCardResources.stepsLabel)}</strong> {trajectory.stepsFollowed}
                </Text>
            )}
        </div>
    );

    const renderDocument = (doc: DocumentResult) => {
        const hasPublicUrl = !!doc.url;
        const canViewSource = !!doc.content && !hasPublicUrl;
        const isExpanded = expandedDocuments.has(doc.id);

        const handleDocumentClick = () => {
            if (hasPublicUrl && doc.url) {
                window.open(doc.url, '_blank', 'noopener,noreferrer');
            }
        };

        return (
            <div
                key={doc.id}
                className={mergeClasses(styles.documentItem, hasPublicUrl && styles.documentItemClickable)}
                onClick={hasPublicUrl ? handleDocumentClick : undefined}
            >
                <div className={styles.documentHeader}>
                    <div className={styles.documentTitleRow}>
                        <Document20Regular className={styles.documentIcon} />
                        <Text className={styles.documentTitle}>{doc.title}</Text>
                    </div>
                    {hasPublicUrl && <Open16Regular className={styles.documentIcon} style={{ fontSize: '14px' }} />}
                </div>

                {doc.summary && <Text className={styles.documentSummary}>{doc.summary}</Text>}

                <div className={styles.documentMetadata}>
                    {doc.documentType && (
                        <Badge appearance="tint" size="small" className={styles.documentBadge}>
                            {doc.documentType}
                        </Badge>
                    )}
                </div>

                {canViewSource && (
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={isExpanded ? <ChevronUp20Regular /> : <ChevronDown20Regular />}
                        className={styles.expandButton}
                        onClick={e => {
                            e.stopPropagation();
                            toggleDocumentExpansion(doc.id);
                        }}
                    >
                        {isExpanded ? 'Hide Full Document' : 'View Full Document'}
                    </Button>
                )}

                {isExpanded && doc.content && <div className={styles.documentContent}>{doc.content}</div>}
            </div>
        );
    };

    return (
        <div className={styles.container}>
            {/* Past Incidents on Same Resource */}
            {memoryResult.sameResourceTrajectories && memoryResult.sameResourceTrajectories.length > 0 && (
                <div className={styles.section}>
                    <Text className={styles.sectionTitle}>
                        {intl.formatMessage(MemorySearchCardResources.pastIncidentsOnSameResource)} (
                        {memoryResult.sameResourceTrajectories.length})
                    </Text>
                    <div className={styles.itemsContainer}>{memoryResult.sameResourceTrajectories.map(renderTrajectory)}</div>
                </div>
            )}

            {/* Similar Symptom Incidents */}
            {memoryResult.similarSymptomsTrajectories && memoryResult.similarSymptomsTrajectories.length > 0 && (
                <div className={styles.section}>
                    <Text className={styles.sectionTitle}>
                        {intl.formatMessage(MemorySearchCardResources.similarSymptomIncidents)} (
                        {memoryResult.similarSymptomsTrajectories.length})
                    </Text>
                    <div className={styles.itemsContainer}>{memoryResult.similarSymptomsTrajectories.map(renderTrajectory)}</div>
                </div>
            )}

            {/* User Memories */}
            {memoryResult.userMemories && memoryResult.userMemories.length > 0 && (
                <div className={styles.section}>
                    <Text className={styles.sectionTitle}>
                        {intl.formatMessage(MemorySearchCardResources.userMemories)} ({memoryResult.userMemories.length})
                    </Text>
                    <div className={styles.itemsContainer}>
                        {memoryResult.userMemories.map((memory, index) => (
                            <div key={index} className={styles.memoryItem}>
                                {memory}
                            </div>
                        ))}
                    </div>
                </div>
            )}

            {/* Relevant Documents */}
            {memoryResult.documents && memoryResult.documents.length > 0 && (
                <div className={styles.section}>
                    <Text className={styles.sectionTitle}>
                        {intl.formatMessage(MemorySearchCardResources.relevantDocuments)} ({memoryResult.documents.length})
                    </Text>
                    <div className={styles.itemsContainer}>{memoryResult.documents.map(renderDocument)}</div>
                </div>
            )}
        </div>
    );
};

export default memo(MemorySearchPanelContent);

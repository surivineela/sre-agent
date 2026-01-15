import { Body1, Body1Strong, Caption1, EntityCard, EntityTitle, Subtitle2 } from '@fluentui-copilot/react-copilot';
import { Badge, Button, Caption1Strong, Card, DrawerBody, makeStyles, tokens } from '@fluentui/react-components';
import { ChevronDown20Regular, ChevronUp20Regular, Document20Regular, Open16Regular } from '@fluentui/react-icons';
import { memo, useState } from 'react';
import { useIntl } from 'react-intl';
import { DocumentResult, MemorySearchResult } from '../../Common/Contracts/DataPlane/Message';
import { MemorySearchCardResources } from '../../Strings/SREAgentResources';

interface MemorySearchPanelContentProps {
    memoryResult: MemorySearchResult;
}

const useStyles = makeStyles({
    drawerBody: {
        padding: `${tokens.spacingVerticalXL} ${tokens.spacingHorizontalXXL}`,
    },
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXL,
        maxWidth: '100%',
        overflowX: 'hidden',
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    itemsContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        width: '100%',
    },
    card: {
        width: '100%',
        boxSizing: 'border-box',
        backgroundColor: tokens.colorNeutralBackground2,
    },
    cardClickable: {
        cursor: 'pointer',
    },
    cardContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    documentCardContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        height: '100%',
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
    documentMetadata: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalXS,
        alignItems: 'center',
    },
    documentContent: {
        backgroundColor: tokens.colorNeutralBackground3,
        padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalS}`,
        display: 'flex',
        alignItems: 'center',
    },
    expandButton: {
        alignSelf: 'flex-start',
    },
    trajectoryContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
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
        <Card key={trajectory.id} className={styles.card} size="small">
            <div className={styles.trajectoryContent}>
                <Body1Strong>{trajectory.title}</Body1Strong>
                {trajectory.symptomsObserved && (
                    <Caption1>
                        <Caption1Strong>{intl.formatMessage(MemorySearchCardResources.symptomsLabel)}</Caption1Strong>{' '}
                        {trajectory.symptomsObserved}
                    </Caption1>
                )}
                {trajectory.rootCause && (
                    <Caption1>
                        <Caption1Strong>{intl.formatMessage(MemorySearchCardResources.rootCauseLabel)}</Caption1Strong>{' '}
                        {trajectory.rootCause}
                    </Caption1>
                )}
            </div>
        </Card>
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
            <>
                <EntityCard
                    key={doc.id}
                    role={'group'}
                    className={hasPublicUrl ? styles.cardClickable : undefined}
                    onClick={hasPublicUrl ? handleDocumentClick : undefined}
                    entityTitle={
                        <EntityTitle
                            media={<Document20Regular />}
                            primaryText={doc.title}
                            actions={
                                hasPublicUrl ? (
                                    <Button
                                        appearance="transparent"
                                        aria-label={intl.formatMessage(MemorySearchCardResources.documentAriaLabel)}
                                        icon={<Open16Regular />}
                                    />
                                ) : undefined
                            }
                        />
                    }
                    content={{
                        style: {
                            width: '100%',
                            borderRadius: '0px',
                            maxWidth: 'unset',
                        },
                    }}
                    style={{ maxWidth: 'unset' }}
                >
                    <div className={styles.documentCardContent}>
                        {doc.summary && <Caption1>{doc.summary}</Caption1>}
                        <div className={styles.documentMetadata}>
                            {doc.documentType && (
                                <Badge appearance="tint" size="small">
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
                                {isExpanded
                                    ? intl.formatMessage(MemorySearchCardResources.hideFullDocument)
                                    : intl.formatMessage(MemorySearchCardResources.viewFullDocument)}
                            </Button>
                        )}

                        {isExpanded && doc.content && (
                            <div className={styles.documentContent}>
                                <Caption1>{doc.content}</Caption1>
                            </div>
                        )}
                    </div>
                </EntityCard>
            </>
        );
    };

    return (
        <DrawerBody className={styles.drawerBody}>
            <div className={styles.container}>
                {/* Past Incidents on Same Resource */}
                {memoryResult.sameResourceTrajectories && memoryResult.sameResourceTrajectories.length > 0 && (
                    <div className={styles.section}>
                        <Subtitle2>
                            {intl.formatMessage(MemorySearchCardResources.pastIncidentsOnSameResource)} (
                            {memoryResult.sameResourceTrajectories.length})
                        </Subtitle2>
                        <div className={styles.itemsContainer}>{memoryResult.sameResourceTrajectories.map(renderTrajectory)}</div>
                    </div>
                )}

                {/* Similar Symptom Incidents */}
                {memoryResult.similarSymptomsTrajectories && memoryResult.similarSymptomsTrajectories.length > 0 && (
                    <div className={styles.section}>
                        <Subtitle2>
                            {intl.formatMessage(MemorySearchCardResources.similarSymptomIncidents)} (
                            {memoryResult.similarSymptomsTrajectories.length})
                        </Subtitle2>
                        <div className={styles.itemsContainer}>{memoryResult.similarSymptomsTrajectories.map(renderTrajectory)}</div>
                    </div>
                )}

                {/* User Memories */}
                {memoryResult.userMemories && memoryResult.userMemories.length > 0 && (
                    <div className={styles.section}>
                        <Subtitle2>
                            {intl.formatMessage(MemorySearchCardResources.userMemories)} ({memoryResult.userMemories.length})
                        </Subtitle2>
                        <div className={styles.itemsContainer}>
                            {memoryResult.userMemories.map((memory, index) => (
                                <Card key={index} className={styles.card} size="small">
                                    <Body1>{memory}</Body1>
                                </Card>
                            ))}
                        </div>
                    </div>
                )}

                {/* Relevant Documents */}
                {memoryResult.documents && memoryResult.documents.length > 0 && (
                    <div className={styles.section}>
                        <Subtitle2>
                            {intl.formatMessage(MemorySearchCardResources.relevantDocuments)} ({memoryResult.documents.length})
                        </Subtitle2>
                        <div className={styles.itemsContainer}>{memoryResult.documents.map(renderDocument)}</div>
                    </div>
                )}
            </div>
        </DrawerBody>
    );
};

export default memo(MemorySearchPanelContent);

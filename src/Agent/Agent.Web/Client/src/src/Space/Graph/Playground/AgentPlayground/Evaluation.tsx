import {
    Body1,
    Body1Strong,
    Button,
    Caption1Strong,
    List,
    ListItem,
    mergeClasses,
    MessageBar,
    MessageBarActions,
    MessageBarBody,
    MessageBarTitle,
    Spinner,
    Text,
    tokens,
    Tooltip,
} from '@fluentui/react-components';
import { Info20Regular, SparkleRegular } from '@fluentui/react-icons';
import { FC, useMemo, useState } from 'react';
import { IntlShape, useIntl } from 'react-intl';
import { PlaygroundResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgent } from '../../../Contracts/ExtendedAgentGraph';
import { useAgentPlaygroundStyles } from './AgentPlayground.Styles';
import { QualityResult, QualityStatus, RelativeTimeCutoffs } from './Contracts';
import { useEvaluationStyles } from './Evalutaion.Styles';
import { QuickFixesDialog } from './QuickFixesDialog';

interface EvaluationProps {
    agent: ExtendedAgent;
    qualityResult: QualityResult | null;
    qualityStatus: QualityStatus;
    qualityLastAnalyzed: number | null;
    insightSections: Array<{ key: string; title: string; items: string[] }>;
    insightsError: string | undefined;
    insightsLoading: boolean;
    handleInsightsRefresh: (overrideAgent?: Partial<ExtendedAgent>) => void;
    areSuggestionsStale: boolean;
    areSuggestionsOutdated: boolean;
    onApply: (agent: ExtendedAgent, save: boolean) => void;
    hidden?: boolean;
}

export const Evaluation: FC<EvaluationProps> = ({
    agent,
    qualityResult,
    qualityStatus,
    qualityLastAnalyzed,
    insightSections,
    insightsError,
    insightsLoading,
    handleInsightsRefresh,
    areSuggestionsStale,
    areSuggestionsOutdated,
    onApply,
    hidden,
}) => {
    const intl = useIntl();
    const styles = useEvaluationStyles();
    const agentPlaygroundStyles = useAgentPlaygroundStyles();
    const hasFindings = useMemo(() => !!qualityResult?.findings?.length, [qualityResult]);
    const [quickFixesDialogOpen, setQuickFixesDialogOpen] = useState(false);

    const { showOutdatedMessage, showStaleMessage, showHasFindingsMessage, showHasNoFindingsMessage } = useMemo(() => {
        const showOutdatedMessage = areSuggestionsOutdated;
        const showStaleMessage = !showOutdatedMessage && areSuggestionsStale;
        const showHasFindingsMessage = !showOutdatedMessage && hasFindings;
        const showHasNoFindingsMessage = !showOutdatedMessage && !hasFindings;
        return { showOutdatedMessage, showStaleMessage, showHasFindingsMessage, showHasNoFindingsMessage };
    }, [areSuggestionsOutdated, areSuggestionsStale, hasFindings]);

    const intentMatchSubscoreDetails = useMemo(() => {
        const intentMatchSubscore = qualityResult?.subScores.find(s => s.id === 'intentMatch');
        if (!intentMatchSubscore) {
            return undefined;
        }
        const rawScore = intentMatchSubscore.score || 0;
        // Normalize: if score is > 5, assume it's on 0-100 scale and convert to 1-5
        const intentMatchScore = rawScore > 5 ? Math.max(1, Math.min(5, Math.round(rawScore / 20))) : rawScore;
        return {
            score: intentMatchScore,
            color: getIntentMatchColor(intentMatchScore),
        };
    }, [qualityResult]);

    return (
        <section
            className={mergeClasses(styles.watcherPanel, hidden && styles.hidden)}
            role="complementary"
            aria-label={intl.formatMessage(PlaygroundResources.qualityDrawerTitle)}
        >
            <div className={styles.watcherPanelBody}>
                {qualityStatus === 'running' && (
                    <div className={styles.inProgressWrapper}>
                        <Spinner size="tiny" label={intl.formatMessage(PlaygroundResources.qualityDrawerLoadingTitle)} />
                        <Body1 as="p" className={styles.inProgressText} >
                            {intl.formatMessage(PlaygroundResources.qualityDrawerLoadingSubtitle)}
                        </Body1>
                    </div>
                )}

                {insightsError && qualityStatus !== 'running' && (
                    <MessageBar intent="error" layout="multiline" className={styles.insightsErrorMessageBar}>
                        <MessageBarBody>
                            {insightsError}{' '}
                            <Button appearance="subtle" size="small" onClick={() => handleInsightsRefresh()}>
                                {intl.formatMessage(PlaygroundResources.insightsRefreshButton)}
                            </Button>
                        </MessageBarBody>
                    </MessageBar>
                )}

                {qualityResult && qualityStatus !== 'running' && (
                    <>
                        <div className={styles.messageBarWrapper}   >
                            {showOutdatedMessage && (
                                <MessageBar intent="info" layout="multiline" className={styles.messageBar}>
                                    {intl.formatMessage(PlaygroundResources.evaluationOutdated)}
                                </MessageBar>
                            )}
                            {showStaleMessage && (
                                <MessageBar intent="info" layout="multiline" className={styles.messageBar}>
                                    {intl.formatMessage(PlaygroundResources.evaluationNewChatActivity)}
                                </MessageBar>
                            )}
                            {showHasFindingsMessage && (
                                <MessageBar intent="info" layout="multiline" className={styles.messageBar}>
                                    <MessageBarBody>
                                        <MessageBarTitle>
                                            {intl.formatMessage(PlaygroundResources.qualityDrawerQuickFixesTitle)}
                                        </MessageBarTitle>
                                        {intl.formatMessage(PlaygroundResources.evaluationQuickFixesAvailable, {
                                            count: qualityResult.findings.length,
                                        })}
                                    </MessageBarBody>
                                    <MessageBarActions>
                                        <Button appearance="primary" onClick={() => setQuickFixesDialogOpen(true)}>
                                            {intl.formatMessage(PlaygroundResources.reviewAndApply)}
                                        </Button>
                                    </MessageBarActions>
                                </MessageBar>
                            )}
                            {showHasNoFindingsMessage && (
                                <MessageBar intent="info" layout="multiline" className={styles.messageBar}>
                                    {intl.formatMessage(PlaygroundResources.qualityDrawerNoFindings)}
                                </MessageBar>
                            )}
                        </div>
                        {/* Overall Score Card */}
                        <div className={styles.overallScoreCard}>
                            <Caption1Strong className={styles.overallScoreLabel}>
                                {intl.formatMessage(PlaygroundResources.qualityOverallLabel)}
                            </Caption1Strong>
                            <div className={styles.overallScoreValue}>
                                {qualityResult.overallScore}
                            </div>
                            <div className={styles.overallScoreEvidence}>
                                {qualityResult.evidence}
                            </div>
                        </div>

                        {/* Intent Match Score */}
                        {intentMatchSubscoreDetails && (
                            <div className={styles.intentMatchScore}>
                                <div className={styles.intentMatchScoreLabelWrapper}>
                                    <Caption1Strong className={styles.intentMatchScoreLabel}>
                                        {intl.formatMessage(PlaygroundResources.qualityIntentLabel)}
                                    </Caption1Strong>
                                    <Tooltip
                                        content={
                                            <div className={styles.tooltipContent}>
                                                <div className={styles.tooltipTitle}>
                                                    {intl.formatMessage(PlaygroundResources.qualityIntentTooltip)}
                                                </div>
                                                <div className={styles.tooltipBody}>
                                                    {intl.formatMessage(PlaygroundResources.intentQualityQuestion)}
                                                    <List className={styles.tooltipList}>
                                                        <ListItem>
                                                            <Body1Strong>5:</Body1Strong> {intl.formatMessage(PlaygroundResources.intentQualityFive)}
                                                        </ListItem>
                                                        <ListItem>
                                                            <Body1Strong>4:</Body1Strong> {intl.formatMessage(PlaygroundResources.intentQualityFour)}
                                                        </ListItem>
                                                        <ListItem>
                                                            <Body1Strong>3:</Body1Strong> {intl.formatMessage(PlaygroundResources.intentQualityThree)}
                                                        </ListItem>
                                                        <ListItem>
                                                            <Body1Strong>2:</Body1Strong> {intl.formatMessage(PlaygroundResources.intentQualityTwo)}
                                                        </ListItem>
                                                        <ListItem>
                                                            <Body1Strong>1:</Body1Strong> {intl.formatMessage(PlaygroundResources.intentQualityOne)}
                                                        </ListItem>
                                                    </List>
                                                </div>
                                            </div>
                                        }
                                        relationship="description"
                                    >
                                        <Info20Regular className={styles.infoIcon} />
                                    </Tooltip>
                                </div>
                                <div
                                    className={styles.subscoreValue}
                                    style={{ color: intentMatchSubscoreDetails.color }}
                                >
                                    {Math.round(intentMatchSubscoreDetails.score)}/5
                                </div>
                            </div>
                        )}

                        {/* Subscores */}
                        <div className={styles.watcherScoresRow}>
                            <ul className={styles.watcherSubscoreList}>
                                {qualityResult.subScores
                                    .filter(s => s.id !== 'intentMatch' && s.id !== 'actionability')
                                    .map(sub => {
                                        const getScoreDescription = (id: string) => {
                                            switch (id) {
                                                case 'completeness':
                                                    return intl.formatMessage(PlaygroundResources.completenessScoreDescription);
                                                case 'toolFit':
                                                    return intl.formatMessage(PlaygroundResources.toolFitScoreDescription);
                                                case 'promptClarity':
                                                    return intl.formatMessage(PlaygroundResources.promptClarityScoreDescription);
                                                case 'safety':
                                                    return intl.formatMessage(PlaygroundResources.safetyScoreDescription);
                                                default:
                                                    return intl.formatMessage(PlaygroundResources.defaultScoreDescription);
                                            }
                                        };

                                        return (
                                            <li key={sub.id} className={styles.watcherSubscoreItem}>
                                                <div className={styles.subscoreLabelWrapper}>
                                                    <Caption1Strong className={styles.watcherSubscoreLabel}>{sub.label}</Caption1Strong>
                                                    <Tooltip
                                                        content={<div className={styles.tooltipContentSmall}>{getScoreDescription(sub.id)}</div>}
                                                        relationship="description"
                                                    >
                                                        <Info20Regular className={styles.infoIcon} />
                                                    </Tooltip>
                                                </div>
                                                <Body1 as="p" className={styles.subscoreValue}>
                                                    {sub.score}
                                                </Body1>
                                                <Body1 as="p" className={styles.subscoreEvidence}>
                                                    {sub.evidence}
                                                </Body1>
                                            </li>
                                        );
                                    })}
                            </ul>
                        </div>

                        {!!insightSections.length && (
                            <div className={styles.highlightsSection}>
                                <Caption1Strong>{intl.formatMessage(PlaygroundResources.qualityDrawerHighlightsTitle)}</Caption1Strong>
                                {insightSections.map(section => (
                                    <div key={section.key} className={styles.highlightGroup}>
                                        <Caption1Strong className={styles.highlightGroupTitle}>{section.title}</Caption1Strong>
                                        <ul className={styles.highlightList}>
                                            {section.items.map((item, index) => (
                                                <li key={`${section.key}-${index}`}>
                                                    <Body1 as="p" className={styles.highlightItem}>
                                                        {item}
                                                    </Body1>
                                                </li>
                                            ))}
                                        </ul>
                                    </div>
                                ))}
                            </div>
                        )}
                    </>
                )}

                {!qualityResult && qualityStatus !== 'running' && !insightsError && (
                    <div className={styles.emptyStateWrapper}>
                        <SparkleRegular className={styles.emptyStateIcon} />
                        <Text>{intl.formatMessage(PlaygroundResources.qualityDrawerEmpty)}</Text>
                    </div>
                )}
            </div>
            <div className={agentPlaygroundStyles.buttonsContainer}>
                <div className={styles.footerStatusWrapper}>
                    <div className={styles.footerStatusText}>
                        {qualityStatus === 'running'
                            ? intl.formatMessage(PlaygroundResources.qualityStatusRunning)
                            : qualityStatus === 'analyzed' && qualityLastAnalyzed
                                ? intl.formatMessage(PlaygroundResources.qualityStatusAnalyzedWithTime, {
                                    time: formatRelativeTime(qualityLastAnalyzed, intl),
                                })
                                : qualityStatus === 'analyzed'
                                    ? intl.formatMessage(PlaygroundResources.qualityStatusAnalyzed)
                                    : intl.formatMessage(PlaygroundResources.qualityStatusNotAnalyzed)}
                    </div>
                </div>
                <Button appearance="primary" onClick={() => handleInsightsRefresh()} disabled={insightsLoading}>
                    {intl.formatMessage(PlaygroundResources.evaluate)}
                </Button>
            </div>
            <QuickFixesDialog
                open={quickFixesDialogOpen}
                onClose={() => setQuickFixesDialogOpen(false)}
                onApply={onApply}
                findings={qualityResult?.findings || []}
                agent={agent}
            />
        </section>
    );
};

const getIntentMatchColor = (score: number) => {
    if (score <= 2) return tokens.colorPaletteRedForeground1;
    if (score === 3) return tokens.colorPaletteYellowForeground2;
    return tokens.colorPaletteGreenForeground1;
};

const formatRelativeTime = (timestamp: number, intl: IntlShape): string => {
    const deltaSeconds = Math.max(0, Math.floor((Date.now() - timestamp) / 1000));
    if (deltaSeconds < RelativeTimeCutoffs.oneMinute) {
        return intl.formatMessage(PlaygroundResources.momentsAgo);
    }
    if (deltaSeconds < RelativeTimeCutoffs.oneHour) {
        const minutes = Math.floor(deltaSeconds / RelativeTimeCutoffs.oneMinute);
        return intl.formatMessage(PlaygroundResources.minutesAgo, { minutes });
    }
    if (deltaSeconds < RelativeTimeCutoffs.oneDay) {
        const hours = Math.floor(deltaSeconds / RelativeTimeCutoffs.oneHour);
        return intl.formatMessage(PlaygroundResources.hoursAgo, { hours });
    }
    const days = Math.floor(deltaSeconds / RelativeTimeCutoffs.oneDay);
    return intl.formatMessage(PlaygroundResources.daysAgo, { days });
};

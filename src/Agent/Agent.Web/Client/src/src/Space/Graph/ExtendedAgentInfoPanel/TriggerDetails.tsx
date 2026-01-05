import { Badge, Button, mergeClasses, Text, tokens } from '@fluentui/react-components';
import { ArrowRightRegular } from '@fluentui/react-icons';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { AgentMode } from '../../../Common/Contracts/Azure/SreAgent';
import { getHumanReadableCronExpression } from '../../../Common/Helpers/CronExpression';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ExtendedTrigger } from '../../Contracts/ExtendedAgentGraph';
import { PrimaryNavItemValues, SecondaryNavItemValues } from '../../Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from '../../Hooks/useAgentSiteNavigate';
import { useExtendedAgentInfoStyles } from '../../Styles/ExtendedAgentGraph.styles';

const EMPTY_DISPLAY = '-' as const;

type TriggerDetailsProps = {
    trigger: ExtendedTrigger;
};

export const TriggerDetails = memo(({ trigger }: TriggerDetailsProps) => {
    const styles = useExtendedAgentInfoStyles();
    const intl = useIntl();
    const navigate = useAgentSiteNavigate();

    const handleGoToIncidents = () => {
        navigate({
            primaryNavItemValue: PrimaryNavItemValues.Activities,
            secondaryNavItemValue: SecondaryNavItemValues.IncidentOverview,
        });
    };

    const handleGoToScheduled = () => {
        navigate({
            primaryNavItemValue: PrimaryNavItemValues.Builder,
            secondaryNavItemValue: SecondaryNavItemValues.ScheduledTasks,
        });
    };

    return (
        <>
            <div className={styles.metadataRow}>
                <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.statusLabel)}</Text>
                <div className={styles.badgeRow}>
                    <Badge
                        appearance={trigger.status === 'Active' ? 'tint' : 'outline'}
                        size="medium"
                        color={trigger.status === 'Active' ? 'success' : 'danger'}
                    >
                        {trigger.status === 'Active'
                            ? intl.formatMessage(ExtendedAgentsGraphResources.onLabel)
                            : intl.formatMessage(ExtendedAgentsGraphResources.offLabel)}
                    </Badge>
                </div>
            </div>

            <div className={styles.metadataRow}>
                <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.subagent)}</Text>
                <Text>{trigger?.subAgent || trigger?.data?.agentName}</Text>
            </div>
            {trigger.type === 'incident' && (
                <>
                    <div className={styles.metadataRow}>
                        <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.severityLabel)}</Text>
                        <Text>{trigger.severity ?? EMPTY_DISPLAY}</Text>
                    </div>

                    <div className={styles.metadataRow}>
                        <Text className={styles.metadataKey}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.triggerIncidentTypeLabel)}
                        </Text>
                        <Text>{trigger.incidentType ?? EMPTY_DISPLAY}</Text>
                    </div>

                    <div className={styles.metadataRow}>
                        <Text className={styles.metadataKey}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.incidentImpactedService)}
                        </Text>
                        <Text>{trigger.impactedService ?? EMPTY_DISPLAY}</Text>
                    </div>
                    <div className={styles.metadataRow}>
                        <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.agentAutonomy)}</Text>
                        <Text>
                            {trigger.agentMode === AgentMode.autonomous
                                ? intl.formatMessage(SreAgentResources.autonomousWord)
                                : intl.formatMessage(SreAgentResources.reviewWord)}
                        </Text>
                    </div>
                </>
            )}

            {trigger.type === 'scheduled' && (
                <>
                    <div className={styles.metadataRow}>
                        <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.scheduleTitle)}</Text>
                        <Text>
                            {getHumanReadableCronExpression(
                                trigger.schedule || trigger.cronExpression || intl.formatMessage(SreAgentResources.NA),
                                intl
                            )}
                        </Text>
                    </div>
                    <div className={styles.metadataRow}>
                        <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.agentAutonomy)}</Text>
                        <Text>{intl.formatMessage(SreAgentResources.autonomousWord)}</Text>
                    </div>
                </>
            )}

            <div className={styles.instructionsSection}>
                <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.instructionsTitle)}</Text>
                <Text className={styles.instructions}>
                    {trigger?.data?.description ||
                        trigger?.description ||
                        intl.formatMessage(ExtendedAgentsGraphResources.listViewDescriptionFallback)}
                </Text>
            </div>

            {trigger.type === 'incident' && (
                <div className={styles.subSection}>
                    <Text className={mergeClasses(styles.sectionTitle, styles.marginBottom8)}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.incidents)}
                    </Text>
                    <Text color={tokens.colorNeutralForeground3}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.incidentDescription)}
                    </Text>
                    <Button appearance="outline" icon={<ArrowRightRegular />} className={styles.actionButton} onClick={handleGoToIncidents}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.goToIncidents)}
                    </Button>
                </div>
            )}

            {trigger.type === 'scheduled' && (
                <div className={styles.subSection}>
                    <Text className={mergeClasses(styles.sectionTitle, styles.marginBottom8)}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.runs)}
                    </Text>
                    <Text>{intl.formatMessage(ExtendedAgentsGraphResources.runDescription)}</Text>
                    <Button appearance="outline" icon={<ArrowRightRegular />} className={styles.actionButton} onClick={handleGoToScheduled}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.goToScheduledTasks)}
                    </Button>
                </div>
            )}
        </>
    );
});

TriggerDetails.displayName = 'TriggerDetails';

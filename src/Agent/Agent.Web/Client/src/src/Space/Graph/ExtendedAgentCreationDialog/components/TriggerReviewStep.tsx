import { Badge, Button, Card, CardHeader, Text } from '@fluentui/react-components';
import { Alert24Regular, Clock24Regular, Edit16Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { IntlShape } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { useCreationDialogStyles } from '../styles';
import { TriggerStateController } from '../types';
import { getScheduleDescription } from '../utils/schedule';

interface TriggerReviewStepProps {
    controller: TriggerStateController;
    intl: IntlShape;
    onEdit: () => void;
    onSubmit: () => void;
    isSubmitting: boolean;
}

export const TriggerReviewStep: FC<TriggerReviewStepProps> = ({ controller, intl, onEdit, onSubmit, isSubmitting }) => {
    const styles = useCreationDialogStyles();
    const { trigger } = controller;

    const scheduleDescription =
        trigger.mode === 'scheduled' && trigger.schedule.cronExpression
            ? getScheduleDescription(trigger.schedule.cronExpression)
            : undefined;

    const renderTriggerTypeCard = () => (
        <Card className={styles.reviewCard}>
            <CardHeader
                header={
                    <div className={styles.reviewCardHeader}>
                        <div className={styles.reviewCardIcon}>{trigger.mode === 'incident' ? <Alert24Regular /> : <Clock24Regular />}</div>
                        <div>
                            <Text weight="semibold">
                                {trigger.mode === 'incident'
                                    ? intl.formatMessage(ExtendedAgentsGraphResources.triggerModeIncidentTitle)
                                    : intl.formatMessage(ExtendedAgentsGraphResources.triggerModeScheduledTitle)}
                            </Text>
                            <Text size={200} block className={styles.reviewCardSubtitle}>
                                {trigger.mode === 'incident'
                                    ? intl.formatMessage(ExtendedAgentsGraphResources.triggerModeIncidentDescription)
                                    : intl.formatMessage(ExtendedAgentsGraphResources.triggerModeScheduledDescription)}
                            </Text>
                        </div>
                        <Button appearance="subtle" size="small" icon={<Edit16Regular />} onClick={onEdit}>
                            Edit
                        </Button>
                    </div>
                }
            />
        </Card>
    );

    const renderAgentCard = () => (
        <Card className={styles.reviewCard}>
            <CardHeader
                header={
                    <div className={styles.reviewCardHeader}>
                        <div>
                            <Text weight="semibold">{intl.formatMessage(ExtendedAgentsGraphResources.triggerAgentLabel)}</Text>
                            <Text size={200} block className={styles.reviewCardSubtitle}>
                                {trigger.agentDisplayName || trigger.agentName || 'No agent selected'}
                            </Text>
                        </div>
                        <Button appearance="subtle" size="small" icon={<Edit16Regular />} onClick={onEdit}>
                            Edit
                        </Button>
                    </div>
                }
            />
        </Card>
    );

    const renderStrategyCard = () => (
        <Card className={styles.reviewCard}>
            <CardHeader
                header={
                    <div className={styles.reviewCardHeader}>
                        <div>
                            <Text weight="semibold">{intl.formatMessage(ExtendedAgentsGraphResources.triggerStrategyLabel)}</Text>
                            <Text size={200} block className={styles.reviewCardSubtitle}>
                                {trigger.strategy === 'quick'
                                    ? intl.formatMessage(ExtendedAgentsGraphResources.triggerStrategyQuick)
                                    : intl.formatMessage(ExtendedAgentsGraphResources.triggerStrategyExisting)}
                            </Text>
                            {trigger.strategy === 'existing' && trigger.existingName && (
                                <Text size={200} block className={styles.reviewCardDetail}>
                                    Using: {trigger.existingName}
                                </Text>
                            )}
                        </div>
                        <Button appearance="subtle" size="small" icon={<Edit16Regular />} onClick={onEdit}>
                            Edit
                        </Button>
                    </div>
                }
            />
        </Card>
    );

    const renderDetailsCard = () => {
        if (trigger.strategy === 'existing') {
            return null; // Don't show details for existing triggers
        }

        return (
            <Card className={styles.reviewCard}>
                <CardHeader
                    header={
                        <div className={styles.reviewCardHeader}>
                            <div style={{ flex: 1 }}>
                                <Text weight="semibold">
                                    {trigger.mode === 'incident' ? 'Incident Handler Details' : 'Scheduled Task Details'}
                                </Text>

                                <div className={styles.reviewDetails}>
                                    <div className={styles.reviewDetailRow}>
                                        <Text size={200} className={styles.reviewDetailLabel}>
                                            Name:
                                        </Text>
                                        <Text size={200}>{trigger.name || 'Unnamed'}</Text>
                                    </div>

                                    {trigger.mode === 'incident' && (
                                        <>
                                            {trigger.incidentPriority && (
                                                <div className={styles.reviewDetailRow}>
                                                    <Text size={200} className={styles.reviewDetailLabel}>
                                                        Priority:
                                                    </Text>
                                                    <Badge
                                                        appearance="filled"
                                                        color={
                                                            trigger.incidentPriority === 'Sev0' || trigger.incidentPriority === 'Sev1'
                                                                ? 'danger'
                                                                : trigger.incidentPriority === 'Sev2'
                                                                  ? 'warning'
                                                                  : 'informative'
                                                        }
                                                        size="small"
                                                    >
                                                        {trigger.incidentPriority}
                                                    </Badge>
                                                </div>
                                            )}
                                            {trigger.incidentType && (
                                                <div className={styles.reviewDetailRow}>
                                                    <Text size={200} className={styles.reviewDetailLabel}>
                                                        Type:
                                                    </Text>
                                                    <Badge appearance="filled" color="brand" size="small">
                                                        {trigger.incidentType}
                                                    </Badge>
                                                </div>
                                            )}
                                        </>
                                    )}

                                    {trigger.mode === 'scheduled' && (
                                        <>
                                            {trigger.description && (
                                                <div className={styles.reviewDetailRow}>
                                                    <Text size={200} className={styles.reviewDetailLabel}>
                                                        Description:
                                                    </Text>
                                                    <Text size={200}>{trigger.description}</Text>
                                                </div>
                                            )}
                                            {trigger.schedule.cronExpression && (
                                                <div className={styles.reviewDetailRow}>
                                                    <Text size={200} className={styles.reviewDetailLabel}>
                                                        Schedule:
                                                    </Text>
                                                    <div>
                                                        <Text size={200} block>
                                                            {trigger.schedule.cronExpression}
                                                        </Text>
                                                        {scheduleDescription && (
                                                            <Text size={200} className={styles.reviewDetailHint}>
                                                                {scheduleDescription}
                                                            </Text>
                                                        )}
                                                    </div>
                                                </div>
                                            )}
                                            {trigger.schedule.timezone && (
                                                <div className={styles.reviewDetailRow}>
                                                    <Text size={200} className={styles.reviewDetailLabel}>
                                                        Timezone:
                                                    </Text>
                                                    <Text size={200}>{trigger.schedule.timezone}</Text>
                                                </div>
                                            )}
                                        </>
                                    )}

                                    {trigger.instructions && (
                                        <div className={styles.reviewDetailRow}>
                                            <Text size={200} className={styles.reviewDetailLabel}>
                                                Instructions:
                                            </Text>
                                            <Text size={200} className={styles.reviewInstructions}>
                                                {trigger.instructions.length > 200
                                                    ? `${trigger.instructions.substring(0, 200)}...`
                                                    : trigger.instructions}
                                            </Text>
                                        </div>
                                    )}
                                </div>
                            </div>
                            <Button appearance="subtle" size="small" icon={<Edit16Regular />} onClick={onEdit}>
                                Edit
                            </Button>
                        </div>
                    }
                />
            </Card>
        );
    };

    return (
        <div className={styles.formSection}>
            <Text className={styles.triggerInfoLead}>{intl.formatMessage(ExtendedAgentsGraphResources.triggerReviewLead)}</Text>

            <div className={styles.reviewContainer}>
                {renderTriggerTypeCard()}
                {renderAgentCard()}
                {renderStrategyCard()}
                {renderDetailsCard()}
            </div>

            <div className={styles.reviewActions}>
                <Button appearance="secondary" onClick={onEdit} disabled={isSubmitting}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.triggerReviewBackToEdit)}
                </Button>
                <Button appearance="primary" onClick={onSubmit} disabled={isSubmitting}>
                    {isSubmitting
                        ? trigger.mode === 'incident'
                            ? 'Creating Incident Handler...'
                            : 'Creating Scheduled Task...'
                        : trigger.mode === 'incident'
                          ? 'Create Incident Handler'
                          : 'Create Scheduled Task'}
                </Button>
            </div>
        </div>
    );
};

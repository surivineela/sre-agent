import { makeStyles, Text, tokens } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { useSubscriptions } from '../../../Common/Contexts/SubscriptionsContext';
import { AgentSpaceCreateFormValues } from '../../../Common/Contracts/AgentSpace';
import { parseArmId } from '../../../Common/Utilities/ArmId';
import { PortalResources, RolesAndPermissions } from '../../../Strings/Resources';

interface AgentSpaceReviewProps {
    isDeploying: boolean;
}

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
        padding: '24px',
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
    },
    sectionHeader: {
        fontWeight: 600,
        fontSize: '16px',
    },
    detailsGrid: {
        display: 'grid',
        gridTemplateColumns: '200px 1fr',
        rowGap: tokens.spacingVerticalM,
        columnGap: tokens.spacingHorizontalXL,
    },
    label: {
        color: tokens.colorNeutralForeground2,
    },
    value: {
        color: tokens.colorNeutralForeground1,
    },
});

export const AgentSpaceReview = ({ isDeploying: _isDeploying }: AgentSpaceReviewProps) => {
    const intl = useIntl();
    const styles = useStyles();
    const { values } = useFormikContext<AgentSpaceCreateFormValues>();
    const { getSubscriptionById } = useSubscriptions();

    const subscription = useMemo(() => {
        return getSubscriptionById(values.subscriptionId);
    }, [getSubscriptionById, values.subscriptionId]);

    const resourceGroupName = useMemo(() => {
        return parseArmId(values.resourceGroupId)?.resourceGroup ?? '';
    }, [values.resourceGroupId]);

    const allowedActionsCount = useMemo(() => {
        return values.allowedActions.filter(a => a.actionName && a.extension).length;
    }, [values.allowedActions]);

    return (
        <div className={styles.container}>
            {/* Basics Section */}
            <div className={styles.section}>
                <Text className={styles.sectionHeader}>{intl.formatMessage(PortalResources.basics)}</Text>
                <div className={styles.detailsGrid}>
                    <Text className={styles.label}>{intl.formatMessage(PortalResources.subscription)}</Text>
                    <Text className={styles.value}>{subscription?.displayName ?? values.subscriptionId}</Text>

                    <Text className={styles.label}>{intl.formatMessage(PortalResources.resourceGroup)}</Text>
                    <Text className={styles.value}>
                        {resourceGroupName}
                        {values.isResourceGroupNew && ` (${intl.formatMessage(PortalResources.createNew)})`}
                    </Text>

                    <Text className={styles.label}>{intl.formatMessage(PortalResources.agentSpaceName)}</Text>
                    <Text className={styles.value}>{values.name}</Text>

                    <Text className={styles.label}>{intl.formatMessage(PortalResources.region)}</Text>
                    <Text className={styles.value}>{values.location}</Text>

                    <Text className={styles.label}>{intl.formatMessage(PortalResources.maxAgentCount)}</Text>
                    <Text className={styles.value}>{values.maxAgentCount}</Text>

                    <Text className={styles.label}>{intl.formatMessage(PortalResources.description)}</Text>
                    <Text className={styles.value}>{values.description || intl.formatMessage(PortalResources.noDescription)}</Text>
                </div>
            </div>

            {/* Geneva Action Policies Section */}
            <div className={styles.section}>
                <Text className={styles.sectionHeader}>{intl.formatMessage(PortalResources.genevaActionPolicies)}</Text>
                <div className={styles.detailsGrid}>
                    <Text className={styles.label}>{intl.formatMessage(PortalResources.enableGenevaActions)}</Text>
                    <Text className={styles.value}>
                        {values.enableGenevaActions ? intl.formatMessage(PortalResources.yes) : intl.formatMessage(PortalResources.no)}
                    </Text>

                    {values.enableGenevaActions && (
                        <>
                            <Text className={styles.label}>{intl.formatMessage(RolesAndPermissions.allowedActions)}</Text>
                            <Text className={styles.value}>{allowedActionsCount}</Text>
                        </>
                    )}
                </div>
            </div>
        </div>
    );
};

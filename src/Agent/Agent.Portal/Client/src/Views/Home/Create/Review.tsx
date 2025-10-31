import { makeStyles, Text, tokens } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { useSubscriptions } from '../../../Common/Contexts/SubscriptionsContext';
import { parseArmId } from '../../../Common/Utilities/ArmId';
import { PortalResources } from '../../../Strings/Resources';
import { SreAgentCreateFormProps } from './CreateAgentDialog';
import ManagedResourceGroupsGrid from './ManagedResourceGroupsGrid';

interface ReviewProps {
    isDeploying: boolean;
}

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '20px',
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
    },
    detailsGrid: {
        display: 'grid',
        gridTemplateColumns: '1fr 2fr',
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

export const Review = ({ isDeploying }: ReviewProps) => {
    const intl = useIntl();
    const styles = useStyles();
    const { values } = useFormikContext<SreAgentCreateFormProps>();
    const { getSubscriptionById } = useSubscriptions();

    const subscription = useMemo(() => {
        return getSubscriptionById(values.subscriptionId);
    }, [getSubscriptionById, values.subscriptionId]);

    const resourceGroupName = useMemo(() => {
        return parseArmId(values.resourceGroupId)?.resourceGroup ?? '';
    }, [values.resourceGroupId]);

    return (
        <div className={styles.container}>
            <div className={styles.section}>
                <Text size={500} weight="semibold">
                    {intl.formatMessage(PortalResources.agentDetails)}
                </Text>
                <div className={styles.detailsGrid}>
                    <Text className={styles.label}>{intl.formatMessage(PortalResources.agentName)}</Text>
                    <Text className={styles.value}>{values.name}</Text>

                    <Text className={styles.label}>{intl.formatMessage(PortalResources.region)}</Text>
                    <Text className={styles.value}>{values.location}</Text>

                    <Text className={styles.label}>{intl.formatMessage(PortalResources.subscription)}</Text>
                    <Text className={styles.value}>{subscription?.displayName ?? values.subscriptionId}</Text>

                    <Text className={styles.label}>{intl.formatMessage(PortalResources.resourceGroup)}</Text>
                    <Text className={styles.value}>{resourceGroupName}</Text>
                </div>
            </div>

            {values.managedResourceGroups.length > 0 && <ManagedResourceGroupsGrid isDeploying={isDeploying} />}
        </div>
    );
};

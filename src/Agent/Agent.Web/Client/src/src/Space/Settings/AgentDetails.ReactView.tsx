import { Link, Shimmer } from '@fluentui/react';
import { Label } from '@fluentui/react/lib/Label';
import { FC, useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import { SettingsTabResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useSreAgent } from './Hooks/useSreAgent';
import { useSubscription } from './Hooks/useSubscription';
import { useSettingsStyles } from './Styles/Settings.styles';

const AgentDetails: FC = () => {
    const intl = useIntl();
    const styles = useSettingsStyles();
    const { resourceId } = useContext(EnvironmentContext);
    const az = useContext(AzPortalContext);
    const { agent, agentLoading } = useSreAgent(resourceId);
    const region = useMemo(() => agent?.location, [agent?.location]);

    const {
        resourceGroup,
        subscription: subscriptionGuid,
        resourceName,
    } = useMemo(() => new ArmResourceDescriptor(resourceId), [resourceId]);

    const { subscription, subscriptionLoading } = useSubscription(subscriptionGuid);

    const { identityId, identityName } = useMemo(() => {
        const identityId = Object.keys(agent?.identity?.userAssignedIdentities || {})[0];
        const identityDescriptor = identityId ? new ArmResourceDescriptor(identityId) : undefined;
        const identityName = identityDescriptor?.resourceName || '';
        return { identityId, identityName };
    }, [agent?.identity?.userAssignedIdentities]);

    const openSubscription = useCallback(() => {
        az.openBlade({
            detailBlade: 'ResourceMenuBlade',
            detailBladeInputs: { id: `/subscriptions/${subscriptionGuid}` },
            extension: 'HubsExtension',
        });
    }, [az, subscriptionGuid]);

    const openResourceGroup = useCallback(() => {
        az.openBlade({
            detailBlade: 'ResourceMenuBlade',
            detailBladeInputs: { id: `/subscriptions/${subscriptionGuid}/resourcegroups/${resourceGroup}` },
            extension: 'HubsExtension',
        });
    }, [az, subscriptionGuid]);

    const openManagedIdentity = useCallback(() => {
        az.openBlade({
            detailBlade: 'ResourceMenuBlade',
            detailBladeInputs: { id: identityId },
            extension: 'HubsExtension',
        });
    }, [az, identityId]);

    return (
        <>
            <div style={styles.generalSettingsHeader}>{intl.formatMessage(SettingsTabResources.agentDetails)}</div>
            <div style={styles.gridStyle}>
                <Label>{intl.formatMessage(SreAgentResources.name)}</Label>
                {resourceName}
                <Label>{intl.formatMessage(SreAgentResources.subscription)}</Label>
                <Shimmer isDataLoaded={!subscriptionLoading || !!subscription?.displayName}>
                    {subscription?.displayName ? <Link onClick={openSubscription}>{subscription?.displayName}</Link> : '-'}
                </Shimmer>
                <Label>{intl.formatMessage(SreAgentResources.subscriptionId)}</Label>
                {subscriptionGuid}
                <Label>{intl.formatMessage(SreAgentResources.resourceGroup)}</Label>
                <Link onClick={openResourceGroup}>{resourceGroup}</Link>
                <Label>{intl.formatMessage(SreAgentResources.region)}</Label>
                <Shimmer isDataLoaded={!agentLoading || !!region}>{region ?? '-'}</Shimmer>
                <Label>{intl.formatMessage(SreAgentResources.managedIdentity)}</Label>
                <Shimmer isDataLoaded={!agentLoading || (!!identityId && !!identityName)}>
                    {identityId && identityName ? <Link onClick={openManagedIdentity}>{identityName}</Link> : '-'}
                </Shimmer>
            </div>
        </>
    );
};

export default AgentDetails;

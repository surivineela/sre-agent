import { Shimmer } from '@fluentui/react';
import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    Link,
} from '@fluentui/react-components';
import { Delete16Regular } from '@fluentui/react-icons';
import { Label } from '@fluentui/react/lib/Label';
import { FC, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import SreAgentClient from '../../Common/Clients/SreAgentClient';
import { getAgentAccessLevelDisplayName } from '../../Common/Helpers/AgentMode';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import { SettingsTabResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useSreAgent } from './Hooks/useSreAgent';
import { useSubscription } from './Hooks/useSubscription';
import { useSettingsStyles } from './Styles/Settings.styles';

const Basics: FC = () => {
    const intl = useIntl();
    const styles = useSettingsStyles();
    const { resourceId } = useContext(EnvironmentContext);
    const az = useContext(AzPortalContext);
    const { agent, agentLoading } = useSreAgent(resourceId);
    const region = useMemo(() => agent?.location, [agent?.location]);

    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

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

    const agentAccessLevelValue = useMemo(
        () => getAgentAccessLevelDisplayName(agent?.properties?.actionConfiguration?.accessLevel, intl),
        [agent?.properties?.actionConfiguration?.accessLevel, intl]
    );

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

    const onDeleteAgent = useCallback(async () => {
        setDeleteDialogOpen(false);
        const notificationId = az.startNotification(
            intl.formatMessage(SreAgentResources.deleteAgentNotificationTitle),
            intl.formatMessage(SreAgentResources.deleteAgentNotificationDescription, { name: resourceName })
        );

        az.log({
            action: 'deleteAgent',
            actionModifier: 'started',
            resourceId,
            logLevel: 'info',
            data: {
                resourceId,
            },
        });

        const response = await SreAgentClient.deleteAgent(resourceId);

        if (response.metadata.success) {
            az.stopNotification(
                notificationId,
                true,
                intl.formatMessage(SreAgentResources.deleteAgentNotificationSuccess, { name: resourceName })
            );
            az.log({
                action: 'deleteAgent',
                actionModifier: 'succeeded',
                resourceId,
                logLevel: 'info',
                data: {
                    resourceId,
                },
            });
            az.openBlade({
                extension: 'Microsoft_Azure_PaasServerless',
                detailBlade: 'SreAgentHome.ReactView',
                detailBladeInputs: {},
            });
        } else {
            az.stopNotification(
                notificationId,
                false,
                intl.formatMessage(SreAgentResources.deleteAgentNotificationError, { name: resourceName })
            );
            az.log({
                action: 'deleteAgent',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    resourceId,
                    error: response.metadata.error,
                },
            });
        }
    }, [az, intl, resourceId, resourceName]);

    return (
        <>
            <div style={styles.generalSettingsHeader}>{intl.formatMessage(SettingsTabResources.basics)}</div>
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
                <Label>{intl.formatMessage(SreAgentResources.agentPermissionsLevel)}</Label>
                <Shimmer isDataLoaded={!agentLoading || !!agentAccessLevelValue}>{agentAccessLevelValue}</Shimmer>
            </div>
            <Dialog open={deleteDialogOpen}>
                <DialogTrigger disableButtonEnhancement>
                    <Button icon={<Delete16Regular />} style={styles.deleteButtonStyle} onClick={() => setDeleteDialogOpen(true)}>
                        {intl.formatMessage(SreAgentResources.delete)}
                    </Button>
                </DialogTrigger>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>{intl.formatMessage(SreAgentResources.deleteAgentTitle)}</DialogTitle>
                        <DialogContent>{intl.formatMessage(SreAgentResources.deleteAgentDescription)}</DialogContent>
                        <DialogActions>
                            <Button appearance="primary" onClick={onDeleteAgent}>
                                {intl.formatMessage(SreAgentResources.delete)}
                            </Button>
                            <DialogTrigger disableButtonEnhancement>
                                <Button appearance="secondary" onClick={() => setDeleteDialogOpen(false)}>
                                    {intl.formatMessage(SreAgentResources.cancel)}
                                </Button>
                            </DialogTrigger>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        </>
    );
};

export default Basics;

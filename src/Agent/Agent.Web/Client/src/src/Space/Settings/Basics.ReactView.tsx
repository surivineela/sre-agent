import { Shimmer } from '@fluentui/react';
import {
    Button,
    Card,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    Link,
    Switch,
    Text,
    Tooltip,
} from '@fluentui/react-components';
import { Delete16Regular, Info16Regular, Play16Regular, RecordStop16Regular } from '@fluentui/react-icons';
import { Label } from '@fluentui/react/lib/Label';
import { FC, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import SreAgentClient from '../../Common/Clients/SreAgentClient';
import PermissionedButton from '../../Common/Components/PermissionedButton';
import { UpgradeChannel } from '../../Common/Contracts/Azure/SreAgent';
import { getAgentAccessLevelDisplayName } from '../../Common/Helpers/AgentMode';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import useUserPermissions from '../../Common/Hooks/useUserPermissions';
import { SettingsTabResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { useSubscription } from './Hooks/useSubscription';
import { useDialogStyles, useSettingsStyles } from './Styles/Settings.styles';

const Basics: FC = () => {
    const intl = useIntl();
    const styles = useSettingsStyles();
    const dialogStyles = useDialogStyles();
    const { resourceId } = useContext(EnvironmentContext);
    const { stopAgent, startAgent, agentObj: agent, agentLoading, refresh } = useContext(SreAgentContext);
    const az = useContext(AzPortalContext);
    const { canDeleteAgent } = useUserPermissions();
    const region = useMemo(() => agent?.location, [agent?.location]);

    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
    const [isUpdatingUpgradeChannel, setIsUpdatingUpgradeChannel] = useState(false);

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

    const isAgentStopped = useMemo(
        () => equals(agent?.properties?.powerState || '', 'Stopped', AntUxStringComparison.IgnoreCase),
        [agent?.properties?.powerState]
    );

    const { agentSpaceId, agentSpaceName } = useMemo(() => {
        const agentSpaceId = agent?.properties?.agentSpaceId;
        const agentSpaceDescriptor = agentSpaceId ? new ArmResourceDescriptor(agentSpaceId) : undefined;
        const agentSpaceName = agentSpaceDescriptor?.resourceName || '';
        return { agentSpaceId, agentSpaceName };
    }, [agent?.properties?.agentSpaceId]);

    const isPreviewChannel = useMemo(() => {
        const channel = agent?.properties?.upgradeChannel ?? UpgradeChannel.Stable;
        return channel === UpgradeChannel.Preview;
    }, [agent?.properties?.upgradeChannel]);

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
    }, [az, resourceGroup, subscriptionGuid]);

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
        az.logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: 'confirmDeleteAgent',
            targetFriendlyName: 'Confirm delete agent',
            valueObjectName: resourceId,
            valueObjectFriendlyName: resourceId,
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

    const openAgentSpace = useCallback(() => {
        if (!agentSpaceId) return;
        az.openBlade({
            extension: 'Microsoft_Azure_PaasServerless',
            detailBlade: 'SreAgentSpaceOverview.ReactView',
            detailBladeInputs: { id: agentSpaceId },
        });
    }, [az, agentSpaceId]);

    const onUpgradeChannelToggle = useCallback(async () => {
        if (isUpdatingUpgradeChannel) return;

        setIsUpdatingUpgradeChannel(true);
        const newUpgradeChannel = isPreviewChannel ? UpgradeChannel.Stable : UpgradeChannel.Preview;

        const notificationId = az.startNotification(
            intl.formatMessage(SettingsTabResources.upgradeChannelUpdatingTitle),
            intl.formatMessage(SettingsTabResources.upgradeChannelUpdatingDescription, { channel: newUpgradeChannel })
        );

        try {
            const updatePayload = {
                properties: {
                    upgradeChannel: newUpgradeChannel,
                },
            };

            const response = await SreAgentClient.patchAgent(resourceId, updatePayload);

            if (response.metadata.success) {
                az.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(SettingsTabResources.upgradeChannelUpdateSuccess, { channel: newUpgradeChannel })
                );
                az.log({
                    action: 'updateUpgradeChannel',
                    actionModifier: 'succeeded',
                    resourceId,
                    logLevel: 'info',
                    data: {
                        upgradeChannel: newUpgradeChannel,
                    },
                });
                refresh();
            } else {
                az.stopNotification(notificationId, false, intl.formatMessage(SettingsTabResources.upgradeChannelUpdateFailed));
                az.log({
                    action: 'updateUpgradeChannel',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        error: response.metadata.error,
                    },
                });
            }
        } catch (error) {
            az.stopNotification(notificationId, false, intl.formatMessage(SettingsTabResources.upgradeChannelUpdateFailed));
            az.log({
                action: 'updateUpgradeChannel',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    error: error,
                },
            });
        } finally {
            setIsUpdatingUpgradeChannel(false);
        }
    }, [isUpdatingUpgradeChannel, isPreviewChannel, az, intl, resourceId, refresh]);

    return (
        <>
            <div style={styles.generalSettingsHeader}>{intl.formatMessage(SettingsTabResources.basics)}</div>

            <Card style={styles.basicsCardStyle}>
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
                    <Label>{intl.formatMessage(SreAgentResources.agentEndpoint)}</Label>
                    <Shimmer isDataLoaded={!agentLoading || !!agent?.properties?.agentEndpoint}>
                        {agent?.properties?.agentEndpoint ?? '-'}
                    </Shimmer>
                    <Label>{intl.formatMessage(SreAgentResources.managedIdentity)}</Label>
                    <Shimmer isDataLoaded={!agentLoading || (!!identityId && !!identityName)}>
                        {identityId && identityName ? <Link onClick={openManagedIdentity}>{identityName}</Link> : '-'}
                    </Shimmer>
                    <Label>{intl.formatMessage(SreAgentResources.agentPermissionsLevel)}</Label>
                    <Shimmer isDataLoaded={!agentLoading || !!agentAccessLevelValue}>{agentAccessLevelValue}</Shimmer>
                    <Label>{intl.formatMessage(SreAgentResources.agentSpace)}</Label>
                    <Shimmer isDataLoaded={!agentLoading || !!agentSpaceId}>
                        {agentSpaceName ? <Link onClick={openAgentSpace}>{agentSpaceName}</Link> : '-'}
                    </Shimmer>
                    <Label
                        id="upgrade-channel-switch-label"
                        style={{ display: 'flex', alignItems: 'center', gap: 6, margin: 0, whiteSpace: 'nowrap' }}
                    >
                        {intl.formatMessage(SettingsTabResources.upgradeChannel)}
                        <Tooltip
                            content={
                                isPreviewChannel
                                    ? intl.formatMessage(SettingsTabResources.upgradeChannelStable)
                                    : intl.formatMessage(SettingsTabResources.upgradeChannelPreview)
                            }
                            relationship="description"
                        >
                            <button
                                type="button"
                                aria-label={intl.formatMessage(SettingsTabResources.upgradeChannelCurrentStatus)}
                                style={{
                                    background: 'none',
                                    border: 'none',
                                    padding: 0,
                                    lineHeight: 0,
                                    cursor: 'pointer',
                                    color: '#616161',
                                    display: 'flex',
                                    alignItems: 'center',
                                }}
                            >
                                <Info16Regular />
                            </button>
                        </Tooltip>
                    </Label>
                    <div style={{ display: 'flex', alignItems: 'center', marginLeft: '-5px' }}>
                        <Shimmer isDataLoaded={!agentLoading}>
                            <Switch
                                aria-label={intl.formatMessage(SettingsTabResources.upgradeChannel)}
                                checked={isPreviewChannel}
                                onChange={onUpgradeChannelToggle}
                                disabled={agentLoading || isUpdatingUpgradeChannel}
                            />
                        </Shimmer>
                    </div>
                </div>
            </Card>

            <Card style={styles.basicsCardStyle}>
                <div style={styles.actionSectionStyle}>
                    <div style={styles.actionTextContainerStyle}>
                        <div style={styles.sectionTitleStyle}>
                            {intl.formatMessage(isAgentStopped ? SreAgentResources.startAgent : SreAgentResources.stopAgent)}
                        </div>
                        <Text style={styles.sectionDescriptionStyle}>
                            {intl.formatMessage(
                                isAgentStopped ? SreAgentResources.startAgentDescription : SreAgentResources.stopAgentDescription
                            )}
                        </Text>
                    </div>
                    <Button
                        appearance="outline"
                        icon={isAgentStopped ? <Play16Regular /> : <RecordStop16Regular />}
                        onClick={() => (isAgentStopped ? startAgent() : stopAgent())}
                    >
                        {intl.formatMessage(isAgentStopped ? SreAgentResources.start : SreAgentResources.stop)}
                    </Button>
                </div>
            </Card>

            <Card style={styles.basicsCardStyle}>
                <div style={styles.actionSectionStyle}>
                    <div style={styles.actionTextContainerStyle}>
                        <div style={styles.sectionTitleStyle}>{intl.formatMessage(SreAgentResources.deleteAgentTitle)}</div>
                        <Text style={styles.sectionDescriptionStyle}>{intl.formatMessage(SreAgentResources.deleteAgentDescription)}</Text>
                    </div>
                    <Dialog open={deleteDialogOpen}>
                        <DialogTrigger disableButtonEnhancement>
                            <PermissionedButton
                                icon={<Delete16Regular />}
                                appearance="primary"
                                className={dialogStyles.dangerButton}
                                canPerform={canDeleteAgent}
                                noPermissionTooltip={intl.formatMessage(SreAgentResources.noPermissionDeleteAgent)}
                                onClick={() => {
                                    setDeleteDialogOpen(true);
                                    az.logAmplitudeControlEvent({
                                        targetType: 'button',
                                        targetAction: 'clicked',
                                        targetName: 'deleteAgent',
                                        targetFriendlyName: 'Delete agent (dialog)',
                                        valueObjectName: SpecialControlValue.DoAction,
                                        valueObjectFriendlyName: SpecialControlValue.DoAction,
                                    });
                                }}
                            >
                                {intl.formatMessage(SreAgentResources.delete)}
                            </PermissionedButton>
                        </DialogTrigger>
                        <DialogSurface>
                            <DialogBody>
                                <DialogTitle>{intl.formatMessage(SreAgentResources.deleteAgentTitle)}</DialogTitle>
                                <DialogContent>{intl.formatMessage(SreAgentResources.deleteAgentDescription)}</DialogContent>
                                <DialogActions>
                                    <Button appearance="primary" className={dialogStyles.dangerButton} onClick={onDeleteAgent}>
                                        {intl.formatMessage(SreAgentResources.yes)}
                                    </Button>
                                    <DialogTrigger disableButtonEnhancement>
                                        <Button appearance="secondary" onClick={() => setDeleteDialogOpen(false)}>
                                            {intl.formatMessage(SreAgentResources.no)}
                                        </Button>
                                    </DialogTrigger>
                                </DialogActions>
                            </DialogBody>
                        </DialogSurface>
                    </Dialog>
                </div>
            </Card>
        </>
    );
};

export default Basics;

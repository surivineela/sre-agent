import { Button, Card, InfoLabel, Label, Link, Skeleton, SkeletonItem, Switch, Text } from '@fluentui/react-components';
import { Code16Regular } from '@fluentui/react-icons';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AppInsightsClient } from '../../Common/Clients/AppInsightsClient';
import SreAgentClient from '../../Common/Clients/SreAgentClient';
import CopyButton from '../../Common/Components/CopyButton';
import ViewResourceJsonDialog from '../../Common/Components/ViewResourceJsonDialog';
import { UpgradeChannel } from '../../Common/Contracts/Azure/SreAgent';
import { getAgentAccessLevelDisplayName, getLocalizedAgentMode } from '../../Common/Helpers/AgentMode';
import { getUserFriendlyLocation } from '../../Common/Helpers/LocationHelper';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import useUserPermissions from '../../Common/Hooks/useUserPermissions';
import { AgentModeResources, SettingsTabResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { ApplicationInsightsDialog } from './Components/ApplicationInsightsDialog';
import { DefaultModelPickerCard } from './Components/DefaultModelPickerCard';
import { DeleteAgentCard } from './Components/DeleteAgentCard';
import { StartStopAgentCard } from './Components/StartStopAgentCard';
import { useSubscription } from './Hooks/useSubscription';
import { useLabelStyles, useSettingsStyles } from './Styles/Settings.styles';

const Basics = () => {
    const intl = useIntl();
    const styles = useSettingsStyles();
    const labelStyles = useLabelStyles();
    const { resourceId } = useContext(EnvironmentContext);
    const { stopAgent, startAgent, agentObj: agent, agentLoading, refresh } = useContext(SreAgentContext);
    const az = useContext(AzPortalContext);
    const { canDeleteAgent } = useUserPermissions();

    const showDefaultModelPicker = useConfigSetting(SettingNames.ShowDefaultModelPicker);

    const [showJsonDialog, setShowJsonDialog] = useState(false);
    const [isUpdatingUpgradeChannel, setIsUpdatingUpgradeChannel] = useState(false);

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

    const [isAppInsightsDialogOpen, setIsAppInsightsDialogOpen] = useState(false);
    const [appInsightsResourceId, setAppInsightsResourceId] = useState<string>();
    const [appInsightsLoading, setAppInsightsLoading] = useState(false);

    const appInsightsName = useMemo(() => {
        if (!appInsightsResourceId) return '';
        const appInsightsDescriptor = new ArmResourceDescriptor(appInsightsResourceId);
        return appInsightsDescriptor?.resourceName || '';
    }, [appInsightsResourceId]);

    const fetchAppInsightsId = useCallback(async () => {
        // First check for applicationInsightsResourceId from configuration
        const configResourceId = agent?.properties?.logConfiguration?.applicationInsightsConfiguration?.applicationInsightsResourceId;
        if (configResourceId) {
            setAppInsightsResourceId(configResourceId);
            return;
        }

        // Fall back to hidden tag
        const tagResourceId = agent?.tags?.['hidden-link: /app-insights-resource-id'];
        if (tagResourceId) {
            setAppInsightsResourceId(tagResourceId);
            return;
        }

        // If not found in config or tags, get appId from logConfiguration and query for resource ID
        const appId = agent?.properties?.logConfiguration?.applicationInsightsConfiguration?.appId;
        if (!appId) {
            setAppInsightsResourceId(undefined);
            return;
        }

        setAppInsightsLoading(true);
        const response = await AppInsightsClient.getAppInsightsComponentFromAppId([subscriptionGuid], resourceGroup, appId);
        if (response) {
            setAppInsightsResourceId(response);
        }
        setAppInsightsLoading(false);
    }, [
        agent?.tags,
        agent?.properties?.logConfiguration?.applicationInsightsConfiguration?.applicationInsightsResourceId,
        agent?.properties?.logConfiguration?.applicationInsightsConfiguration?.appId,
        subscriptionGuid,
        resourceGroup,
    ]);

    useEffect(() => {
        if (agent) {
            fetchAppInsightsId();
        }
    }, [agent, fetchAppInsightsId]);

    const agentAccessLevelValue = useMemo(
        () => getAgentAccessLevelDisplayName(agent?.properties?.actionConfiguration?.accessLevel, intl),
        [agent?.properties?.actionConfiguration?.accessLevel, intl]
    );

    const agentActionModeValue = useMemo(
        () => getLocalizedAgentMode(agent?.properties?.actionConfiguration?.mode || '-', intl),
        [agent?.properties?.actionConfiguration?.mode, intl]
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

    const openAppInsightsDialog = useCallback(() => {
        setIsAppInsightsDialogOpen(true);
    }, []);

    const closeAppInsightsDialog = useCallback(() => {
        setIsAppInsightsDialogOpen(false);
    }, []);

    const handleAppInsightsSaved = useCallback(() => {
        refresh();
    }, [refresh]);

    const openAgentSpace = useCallback(() => {
        if (!agentSpaceId) return;
        // TODO: Update to point to SREA Portal
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

        const response = await SreAgentClient.patchAgent(resourceId, {
            properties: {
                upgradeChannel: newUpgradeChannel,
            },
        });

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

        setIsUpdatingUpgradeChannel(false);
    }, [isUpdatingUpgradeChannel, isPreviewChannel, az, intl, resourceId, refresh]);

    return (
        <>
            <div style={{ ...styles.generalSettingsHeader, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                {intl.formatMessage(SettingsTabResources.basics)}
                <Button appearance="subtle" icon={<Code16Regular />} onClick={() => setShowJsonDialog(true)}>
                    {intl.formatMessage(SreAgentResources.viewJson)}
                </Button>
            </div>

            <Card style={styles.basicsCardStyle}>
                <div style={styles.gridStyle}>
                    <Label className={labelStyles.small}>{intl.formatMessage(SreAgentResources.name)}</Label>
                    <Text>{resourceName}</Text>
                    <Label className={labelStyles.small}>{intl.formatMessage(SreAgentResources.subscription)}</Label>
                    <SkeletonField isLoading={subscriptionLoading && !subscription?.displayName}>
                        {subscription?.displayName ? <Link onClick={openSubscription}>{subscription.displayName}</Link> : '-'}
                    </SkeletonField>
                    <Label className={labelStyles.small}>{intl.formatMessage(SreAgentResources.subscriptionId)}</Label>
                    <div style={styles.copyFieldContainer}>
                        <Text>{subscriptionGuid}</Text>
                        <CopyButton textToCopy={subscriptionGuid} buttonAppearance="transparent" />
                    </div>
                    <Label className={labelStyles.small}>{intl.formatMessage(SreAgentResources.resourceGroup)}</Label>
                    <Link onClick={openResourceGroup}>{resourceGroup}</Link>
                    <Label className={labelStyles.small}>{intl.formatMessage(SreAgentResources.region)}</Label>
                    {agentLoading && !region ? (
                        <Skeleton>
                            <SkeletonItem />
                        </Skeleton>
                    ) : (
                        <Text>{region ? getUserFriendlyLocation(region) : '-'}</Text>
                    )}
                    <Label className={labelStyles.small}>{intl.formatMessage(SreAgentResources.agentEndpoint)}</Label>
                    {agentLoading && !agent?.properties?.agentEndpoint ? (
                        <Skeleton>
                            <SkeletonItem />
                        </Skeleton>
                    ) : agent?.properties?.agentEndpoint ? (
                        <div style={styles.copyFieldContainer}>
                            <Text>{agent.properties.agentEndpoint}</Text>
                            <CopyButton textToCopy={agent.properties.agentEndpoint} buttonAppearance="transparent" />
                        </div>
                    ) : (
                        '-'
                    )}
                    <Label className={labelStyles.small}>{intl.formatMessage(SreAgentResources.managedIdentity)}</Label>
                    <SkeletonField isLoading={agentLoading && (!identityId || !identityName)}>
                        {identityId && identityName ? <Link onClick={openManagedIdentity}>{identityName}</Link> : '-'}
                    </SkeletonField>
                    <Label className={labelStyles.small}>{intl.formatMessage(SreAgentResources.applicationInsights)}</Label>
                    <SkeletonField isLoading={agentLoading || appInsightsLoading}>
                        {appInsightsResourceId && appInsightsName ? (
                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                <Text>{appInsightsName}</Text>
                                <Link onClick={openAppInsightsDialog}>{intl.formatMessage(SreAgentResources.edit)}</Link>
                            </div>
                        ) : (
                            <Link onClick={openAppInsightsDialog}>{intl.formatMessage(SreAgentResources.add)}</Link>
                        )}
                    </SkeletonField>
                    <Label className={labelStyles.small}>{intl.formatMessage(SreAgentResources.agentPermissionsLevel)}</Label>
                    {agentLoading && !agentAccessLevelValue ? (
                        <Skeleton>
                            <SkeletonItem />
                        </Skeleton>
                    ) : (
                        <Text>{agentAccessLevelValue}</Text>
                    )}
                    <Label className={labelStyles.small}>{intl.formatMessage(AgentModeResources.agentMode)}</Label>
                    {agentLoading && !agentActionModeValue ? (
                        <Skeleton>
                            <SkeletonItem />
                        </Skeleton>
                    ) : (
                        <Text>{agentActionModeValue}</Text>
                    )}
                    <Label className={labelStyles.small}>{intl.formatMessage(SreAgentResources.agentSpaceLabel)}</Label>
                    <SkeletonField isLoading={agentLoading && !agentSpaceId}>
                        {agentSpaceName ? <Link onClick={openAgentSpace}>{agentSpaceName}</Link> : '-'}
                    </SkeletonField>
                    <InfoLabel
                        info={
                            isPreviewChannel
                                ? intl.formatMessage(SettingsTabResources.upgradeChannelPreview)
                                : intl.formatMessage(SettingsTabResources.upgradeChannelStable)
                        }
                        label={{ className: labelStyles.small, style: { whiteSpace: 'nowrap' } }}
                        style={{ display: 'flex', alignItems: 'center' }}
                    >
                        {intl.formatMessage(SettingsTabResources.upgradeChannel)}
                    </InfoLabel>
                    <div style={{ marginLeft: '-5px' }}>
                        {agentLoading ? (
                            <Skeleton>
                                <SkeletonItem />
                            </Skeleton>
                        ) : (
                            <Switch
                                aria-label={intl.formatMessage(SettingsTabResources.upgradeChannel)}
                                checked={isPreviewChannel}
                                onChange={onUpgradeChannelToggle}
                                disabled={agentLoading || isUpdatingUpgradeChannel}
                            />
                        )}
                    </div>
                </div>
            </Card>

            {showDefaultModelPicker && (
                <DefaultModelPickerCard
                    resourceId={resourceId}
                    region={region ?? ''}
                    currentProvider={agent?.properties.defaultModel?.provider}
                    isAgentLoading={agentLoading}
                    isUpdatingUpgradeChannel={isUpdatingUpgradeChannel}
                />
            )}

            <StartStopAgentCard isAgentStopped={isAgentStopped} onStart={startAgent} onStop={stopAgent} />

            <DeleteAgentCard resourceId={resourceId} resourceName={resourceName} canDeleteAgent={canDeleteAgent} />

            <ApplicationInsightsDialog
                isOpen={isAppInsightsDialogOpen}
                onClose={closeAppInsightsDialog}
                currentAppInsightsId={appInsightsResourceId}
                agentResourceId={resourceId}
                onSave={handleAppInsightsSaved}
            />
            <ViewResourceJsonDialog resourceId={resourceId} isOpen={showJsonDialog} onClose={() => setShowJsonDialog(false)} />
        </>
    );
};

interface SkeletonFieldProps {
    isLoading: boolean;
    children: React.ReactNode;
}

const SkeletonField = ({ isLoading, children }: SkeletonFieldProps) => {
    if (isLoading) {
        return (
            <Skeleton>
                <SkeletonItem />
            </Skeleton>
        );
    }
    return <>{children}</>;
};

export default Basics;

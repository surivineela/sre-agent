import {
    Button,
    Caption1,
    Card,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    Dropdown,
    Field,
    Link,
    MessageBar,
    MessageBarBody,
    Option,
    Skeleton,
    SkeletonItem,
    Switch,
    Text,
    Tooltip,
} from '@fluentui/react-components';
import { Delete16Regular, Info16Regular, Play16Regular, RecordStop16Regular } from '@fluentui/react-icons';
import { Label } from '@fluentui/react/lib/Label';
import { Formik } from 'formik';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AppInsightsClient } from '../../Common/Clients/AppInsightsClient';
import { getErrorMessageOrStringify } from '../../Common/Clients/ArmClient';
import SreAgentClient from '../../Common/Clients/SreAgentClient';
import PermissionedButton from '../../Common/Components/PermissionedButton';
import { SreAgentFwLinks } from '../../Common/Constants/FwLinks';
import { Model, ModelProvider, UpgradeChannel } from '../../Common/Contracts/Azure/SreAgent';
import { getAgentAccessLevelDisplayName, getLocalizedAgentMode } from '../../Common/Helpers/AgentMode';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import useUserPermissions from '../../Common/Hooks/useUserPermissions';
import { AgentModeResources, SettingsTabResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { ApplicationInsightsDialog } from './Components/ApplicationInsightsDialog';
import { useSubscription } from './Hooks/useSubscription';
import { useSupportedModels } from './Hooks/useSupportedModels';
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

    const showDefaultModelPicker = useConfigSetting(SettingNames.ShowDefaultModelPicker);

    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
    const [isUpdatingUpgradeChannel, setIsUpdatingUpgradeChannel] = useState(false);

    const {
        resourceGroup,
        subscription: subscriptionGuid,
        resourceName,
    } = useMemo(() => new ArmResourceDescriptor(resourceId), [resourceId]);

    const { subscription, subscriptionLoading } = useSubscription(subscriptionGuid);

    const {
        supportedProviders,
        isSupportedModelsLoading,
        getSupportedModelsFailure,
        updateDefaultModel,
        isUpdatingDefaultModel,
        refreshSupportedModels,
    } = useSupportedModels(resourceId, region ?? '');

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
        // First check for the hidden tag
        const tagResourceId = agent?.tags?.['hidden-link: /app-insights-resource-id'];
        if (tagResourceId) {
            setAppInsightsResourceId(tagResourceId);
            return;
        }

        // If not found in tags, get appId from logConfiguration and query for resource ID
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
    }, [agent?.tags, agent?.properties?.logConfiguration?.applicationInsightsConfiguration?.appId, subscriptionGuid, resourceGroup]);

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

    const onDeleteAgent = useCallback(async () => {
        setDeleteDialogOpen(false);
        const notificationId = az.startNotification(
            intl.formatMessage(SreAgentResources.deleteAgentNotificationTitle, { count: 1 }),
            intl.formatMessage(SreAgentResources.deleteAgentNotificationInProgress, { count: 1, name: resourceName })
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
                intl.formatMessage(SreAgentResources.deleteAgentNotificationSuccess, { count: 1, name: resourceName })
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
                intl.formatMessage(SreAgentResources.deleteAgentNotificationFailure, {
                    count: 1,
                    name: resourceName,
                    errorMessage: getErrorMessageOrStringify(response.metadata.error),
                })
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

    const subscriptionField = useMemo(() => {
        if (subscriptionLoading && !subscription?.displayName) {
            return (
                <Skeleton>
                    <SkeletonItem />
                </Skeleton>
            );
        }
        return subscription?.displayName ? <Link onClick={openSubscription}>{subscription?.displayName}</Link> : '-';
    }, [subscriptionLoading, subscription, openSubscription]);

    const managedIdentityField = useMemo(() => {
        if (agentLoading && (!identityId || !identityName)) {
            return (
                <Skeleton>
                    <SkeletonItem />
                </Skeleton>
            );
        }
        return identityId && identityName ? <Link onClick={openManagedIdentity}>{identityName}</Link> : '-';
    }, [agentLoading, identityId, identityName, openManagedIdentity]);

    const applicationInsightsField = useMemo(() => {
        if (agentLoading || appInsightsLoading) {
            return (
                <Skeleton>
                    <SkeletonItem />
                </Skeleton>
            );
        }

        if (appInsightsResourceId && appInsightsName) {
            return (
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <Text>{appInsightsName}</Text>
                    <Link onClick={openAppInsightsDialog}>{intl.formatMessage(SreAgentResources.edit)}</Link>
                </div>
            );
        }
        return <Link onClick={openAppInsightsDialog}>{intl.formatMessage(SreAgentResources.add)}</Link>;
    }, [agentLoading, appInsightsLoading, appInsightsName, appInsightsResourceId, intl, openAppInsightsDialog]);

    const agentSpaceField = useMemo(() => {
        if (agentLoading && !agentSpaceId) {
            return (
                <Skeleton>
                    <SkeletonItem />
                </Skeleton>
            );
        }
        return agentSpaceName ? <Link onClick={openAgentSpace}>{agentSpaceName}</Link> : '-';
    }, [agentLoading, agentSpaceId, agentSpaceName, openAgentSpace]);

    return (
        <>
            <div style={styles.generalSettingsHeader}>{intl.formatMessage(SettingsTabResources.basics)}</div>

            <Card style={styles.basicsCardStyle}>
                <div style={styles.gridStyle}>
                    <Label>{intl.formatMessage(SreAgentResources.name)}</Label>
                    {resourceName}
                    <Label>{intl.formatMessage(SreAgentResources.subscription)}</Label>
                    {subscriptionField}
                    <Label>{intl.formatMessage(SreAgentResources.subscriptionId)}</Label>
                    {subscriptionGuid}
                    <Label>{intl.formatMessage(SreAgentResources.resourceGroup)}</Label>
                    <Link onClick={openResourceGroup}>{resourceGroup}</Link>
                    <Label>{intl.formatMessage(SreAgentResources.region)}</Label>
                    {agentLoading && !region ? (
                        <Skeleton>
                            <SkeletonItem />
                        </Skeleton>
                    ) : (
                        (region ?? '-')
                    )}
                    <Label>{intl.formatMessage(SreAgentResources.agentEndpoint)}</Label>
                    {agentLoading && !agent?.properties?.agentEndpoint ? (
                        <Skeleton>
                            <SkeletonItem />
                        </Skeleton>
                    ) : (
                        (agent?.properties?.agentEndpoint ?? '-')
                    )}
                    <Label>{intl.formatMessage(SreAgentResources.managedIdentity)}</Label>
                    {managedIdentityField}
                    <Label>{intl.formatMessage(SreAgentResources.applicationInsights)}</Label>
                    {applicationInsightsField}
                    <Label>{intl.formatMessage(SreAgentResources.agentPermissionsLevel)}</Label>
                    {agentLoading && !agentAccessLevelValue ? (
                        <Skeleton>
                            <SkeletonItem />
                        </Skeleton>
                    ) : (
                        agentAccessLevelValue
                    )}
                    <Label>{intl.formatMessage(AgentModeResources.agentMode)}</Label>
                    {agentLoading && !agentActionModeValue ? (
                        <Skeleton>
                            <SkeletonItem />
                        </Skeleton>
                    ) : (
                        agentActionModeValue
                    )}
                    <Label>{intl.formatMessage(SreAgentResources.agentSpace)}</Label>
                    {agentSpaceField}
                    <Label
                        id="upgrade-channel-switch-label"
                        style={{ display: 'flex', alignItems: 'center', gap: 6, margin: 0, whiteSpace: 'nowrap' }}
                    >
                        {intl.formatMessage(SettingsTabResources.upgradeChannel)}
                        <Tooltip
                            content={
                                isPreviewChannel
                                    ? intl.formatMessage(SettingsTabResources.upgradeChannelPreview)
                                    : intl.formatMessage(SettingsTabResources.upgradeChannelStable)
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
                                disabled={agentLoading || isUpdatingUpgradeChannel || isUpdatingDefaultModel}
                            />
                        )}
                    </div>
                </div>
            </Card>

            {showDefaultModelPicker && (
                <Formik<Model>
                    initialValues={{ provider: agent?.properties.defaultModel?.provider || '' }}
                    enableReinitialize
                    onSubmit={values => updateDefaultModel(values)}
                >
                    {({ dirty, values, setFieldValue, resetForm, submitForm }) => (
                        <Card style={styles.basicsCardStyle}>
                            <div style={styles.sectionTitleStyle}>{intl.formatMessage(SettingsTabResources.modelProviderLabel)}</div>

                            {getSupportedModelsFailure && (
                                <MessageBar intent="error" layout="multiline" style={{ alignItems: 'center' }}>
                                    <MessageBarBody style={styles.failedToLoadMessageBarContentStyle}>
                                        {getSupportedModelsFailure}
                                        <Button appearance="outline" size="small" onClick={() => refreshSupportedModels()}>
                                            {intl.formatMessage(SreAgentResources.refresh)}
                                        </Button>
                                    </MessageBarBody>
                                </MessageBar>
                            )}

                            <Field id="providerField" label={intl.formatMessage(SettingsTabResources.providerLabel)} orientation="vertical">
                                {agentLoading || isSupportedModelsLoading ? (
                                    <Skeleton>
                                        <SkeletonItem style={styles.dropdownSkeletonStyle} />
                                    </Skeleton>
                                ) : (
                                    <Dropdown
                                        id="provider"
                                        style={styles.dropdownStyles}
                                        value={supportedProviders?.find(option => option.key === values.provider)?.text || values.provider}
                                        onOptionSelect={(_event, data) => {
                                            az.logAmplitudeControlEvent({
                                                targetType: 'dropdown',
                                                targetAction: 'changed',
                                                targetName: 'provider',
                                                targetFriendlyName: 'provider',
                                                valueObjectName: data?.optionValue ?? '',
                                                valueObjectFriendlyName: data?.optionText ?? '',
                                            });
                                            setFieldValue('provider', data.optionValue);
                                        }}
                                        disabled={isUpdatingUpgradeChannel || !!getSupportedModelsFailure || isUpdatingDefaultModel}
                                    >
                                        {supportedProviders?.map(option => (
                                            <Option value={option.key} checkIcon={null}>
                                                {option.text}
                                            </Option>
                                        ))}
                                    </Dropdown>
                                )}
                            </Field>

                            {values.provider === ModelProvider.Anthropic && (
                                <MessageBar layout="multiline" style={{ alignItems: 'center' }}>
                                    <MessageBarBody>
                                        <Caption1>{intl.formatMessage(SettingsTabResources.anthropicEuRegionInfoMessage)}</Caption1>{' '}
                                        <Link href={SreAgentFwLinks.sreAgentDataHandling} target="_blank" rel="noopener noreferrer">
                                            <Caption1>{intl.formatMessage(SettingsTabResources.anthropicEuRegionLearnMore)}</Caption1>
                                        </Link>
                                    </MessageBarBody>
                                </MessageBar>
                            )}

                            <div style={styles.commandBarButtonContainerStyle}>
                                <Button
                                    appearance="primary"
                                    onClick={() => submitForm()}
                                    disabled={
                                        !dirty ||
                                        isUpdatingUpgradeChannel ||
                                        isSupportedModelsLoading ||
                                        !!getSupportedModelsFailure ||
                                        isUpdatingDefaultModel
                                    }
                                >
                                    {intl.formatMessage(SreAgentResources.save)}
                                </Button>
                                <Button
                                    appearance="outline"
                                    onClick={() => resetForm()}
                                    disabled={
                                        !dirty ||
                                        isUpdatingUpgradeChannel ||
                                        isSupportedModelsLoading ||
                                        !!getSupportedModelsFailure ||
                                        isUpdatingDefaultModel
                                    }
                                >
                                    {intl.formatMessage(SreAgentResources.cancel)}
                                </Button>
                            </div>
                        </Card>
                    )}
                </Formik>
            )}

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
                    <ApplicationInsightsDialog
                        isOpen={isAppInsightsDialogOpen}
                        onClose={closeAppInsightsDialog}
                        currentAppInsightsId={appInsightsResourceId}
                        agentResourceId={resourceId}
                        onSave={handleAppInsightsSaved}
                    />
                </div>
            </Card>
        </>
    );
};

export default Basics;

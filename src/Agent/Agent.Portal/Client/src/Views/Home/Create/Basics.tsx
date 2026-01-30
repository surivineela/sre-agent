import {
    Caption1,
    Dropdown,
    Field,
    Link,
    MessageBar,
    MessageBarBody,
    Option,
    Skeleton,
    SkeletonItem,
    Text,
} from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PermissionsClient } from '../../../Common/Clients/PermissionsClient';
import { ComboboxWithFilterFormik } from '../../../Common/Components/Formik/ComboboxWithFilterFormik';
import { DropdownFormik } from '../../../Common/Components/Formik/DropdownFormik';
import { InputFormik } from '../../../Common/Components/Formik/InputFormik';
import { RadioGroupFormik } from '../../../Common/Components/Formik/RadioGroupFormik';
import { ResourceGroupDropdown } from '../../../Common/Components/ResourceGroupDropdown';
import { SubscriptionDropdown } from '../../../Common/Components/SubscriptionDropdown';
import { ApiVersions } from '../../../Common/Constants/ApiVersions';
import { LearnMoreLinks } from '../../../Common/Constants/Links';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { SpecialControlValue } from '../../../Common/Contracts/Amplitude';
import { Subscription } from '../../../Common/Contracts/Arm';
import { FieldRestrictionResult } from '../../../Common/Contracts/Permissions';
import { ModelProvider } from '../../../Common/Contracts/SreAgent';
import { useAmplitudeTelemetry } from '../../../Common/Hooks/useAmplitudeTelemetry';
import { SettingNames, useConfigSetting } from '../../../Common/Hooks/useConfigSettings';
import { useSubscriptionAppInsights } from '../../../Common/Hooks/useSubscriptionAppInsights';
import { ArmServiceType } from '../../../Common/Utilities/ArmTemplateBuilder/ArmTemplateTypes';
import { getCanonicalLocation } from '../../../Common/Utilities/Location';
import { PortalResources } from '../../../Strings/Resources';
import { ApplicationInsightsSetup, SreAgentCreateFormProps } from './CreateAgentDialog';
import { useSreAgentLocations } from './useSreAgentLocations';
import { useSreSupportedModels } from './useSreSupportedModels';

const getContentDetailsForPolicyCheck = (scope: string, type: string, name: string, location: string, apiVersion: string) => {
    return {
        resourceDetails: {
            scope,
            apiVersion,
            resourceContent: {
                type,
            },
        },
        pendingFields: [
            {
                field: 'name',
                values: [name],
            },
            {
                field: 'location',
                values: [location],
            },
            {
                field: 'tags',
            },
        ],
    };
};

interface BasicsProps {
    agentSpaceLocation?: string;
    isDeploying: boolean;
}

export const Basics = (props: BasicsProps) => {
    const { isDeploying, agentSpaceLocation } = props;

    const intl = useIntl();
    const { values, setFieldValue, errors } = useFormikContext<SreAgentCreateFormProps>();
    const { logControlEvent } = useAmplitudeTelemetry();
    const { locationsList, locationsLoading, containsNoLocations } = useSreAgentLocations(
        values.subscriptionId,
        TelemetrySource.SreAgentCreate
    );
    const { appInsights, isLoading: appInsightsLoading } = useSubscriptionAppInsights(
        values.appInsightsSubscriptionId,
        TelemetrySource.SreAgentCreate
    );

    const showDefaultModelPicker = useConfigSetting(SettingNames.ShowDefaultModelPicker);

    const { supportedProviders, isSupportedModelsLoading, getSupportedModelsFailure, showAnthropicDisabledMessage } = useSreSupportedModels(
        values.subscriptionId,
        values.location,
        TelemetrySource.SreAgentCreate
    );

    const [policyErrorMessage, setPolicyErrorMessage] = useState<string>('');

    const permissionsClient = useMemo(() => PermissionsClient.getInstance(TelemetrySource.SreAgentCreate), []);

    const isLocationDisabled = useMemo(() => !!agentSpaceLocation, [agentSpaceLocation]);

    const locationDropdownOptions = useMemo(() => {
        const locationOptionsList = (locationsList ?? []).map(location => ({
            key: getCanonicalLocation(location),
            text: location,
            data: getCanonicalLocation(location),
        }));

        return locationOptionsList;
    }, [locationsList]);

    const appInsightsDropdownOptions = useMemo(() => {
        return appInsights.map(ai => ({
            key: ai.id,
            text: ai.name,
            data: ai.id,
        }));
    }, [appInsights]);

    const onSubscriptionChange = useCallback(
        (subscription?: Subscription) => {
            logControlEvent({
                targetType: 'dropdown',
                targetAction: 'changed',
                targetName: 'sreAgentCreateSubscription',
                targetFriendlyName: 'SRE Agent Create - Subscription',
                valueObjectName: subscription?.subscriptionId ?? '',
                valueObjectFriendlyName: subscription?.subscriptionId ?? '',
            });
            setFieldValue('subscriptionId', subscription?.subscriptionId ?? '');
            if (!isDeploying) {
                setFieldValue('managedResourceGroups', []);
                setFieldValue('maxResourceGroupsError', false);
                setFieldValue('managedResourceGroupsPermissionError', false);
                setFieldValue('managedResourceGroupsLockError', false);
            }
        },
        [isDeploying, setFieldValue, logControlEvent]
    );

    const getPolicyErrorMessage = useCallback(async (): Promise<string> => {
        const deploymentContent = getContentDetailsForPolicyCheck(
            values.resourceGroupId,
            ArmServiceType.Deployments,
            `${values.name}-deployment`,
            values.location,
            ApiVersions.armApiVersion20250301
        );
        const userIdentityContent = getContentDetailsForPolicyCheck(
            values.resourceGroupId,
            ArmServiceType.UserIdentity,
            `${values.name}-identity`,
            values.location,
            ApiVersions.identityApiVersion20241130
        );
        const workspaceContent = getContentDetailsForPolicyCheck(
            values.resourceGroupId,
            ArmServiceType.Workspace,
            `${values.name}-workspace`,
            values.location,
            ApiVersions.workspacesApiVersion20250201
        );
        const agentContent = getContentDetailsForPolicyCheck(
            values.resourceGroupId,
            ArmServiceType.Agents,
            values.name,
            values.location,
            ApiVersions.microsoftAppApiVersion20250501Preview
        );

        const policyCheckResponse = await Promise.all([
            permissionsClient.checkPolicies(values.resourceGroupId, deploymentContent),
            permissionsClient.checkPolicies(values.resourceGroupId, userIdentityContent),
            permissionsClient.checkPolicies(values.resourceGroupId, workspaceContent),
            permissionsClient.checkPolicies(values.resourceGroupId, agentContent),
        ]);

        for (const response of policyCheckResponse) {
            const hasDenyPolicies = response?.content?.fieldRestrictions?.some(fieldRestriction => {
                return fieldRestriction.restrictions?.some(restriction => {
                    return restriction.result === FieldRestrictionResult.Deny;
                });
            });
            if (hasDenyPolicies) {
                const denyInfo = response?.content?.fieldRestrictions?.find(fieldRestriction =>
                    fieldRestriction.restrictions?.some(restriction => restriction.result === FieldRestrictionResult.Deny)
                );
                const denyRestriction = denyInfo?.restrictions?.find(restriction => restriction.result === FieldRestrictionResult.Deny);
                return intl.formatMessage(PortalResources.policyErrorFormattedMessage, {
                    field: denyInfo?.field ?? '',
                    id: denyRestriction?.policy.policyAssignmentId ?? '',
                });
            }
        }
        return '';
    }, [intl, permissionsClient, values.location, values.name, values.resourceGroupId]);

    useEffect(() => {
        const fetchPolicyErrorMessage = async () => {
            const message = await getPolicyErrorMessage();
            setPolicyErrorMessage(message);
        };
        if (values.resourceGroupId && values.location && values.subscriptionId) {
            fetchPolicyErrorMessage();
        }
    }, [getPolicyErrorMessage, values.location, values.resourceGroupId, values.subscriptionId]);

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
            {containsNoLocations && (
                <div>
                    <MessageBar intent="error">
                        <MessageBarBody>
                            <Text>{intl.formatMessage(PortalResources.allowlistWarning)}</Text>
                        </MessageBarBody>
                    </MessageBar>
                </div>
            )}

            <SubscriptionDropdown
                onSubscriptionChange={onSubscriptionChange}
                selectedSubscriptionId={values.subscriptionId}
                disabled={isDeploying}
            />

            <ResourceGroupDropdown
                subscriptionId={values.subscriptionId}
                selectedResourceGroupId={values.resourceGroupId}
                onResourceGroupChange={resourceGroup => {
                    logControlEvent({
                        targetType: 'dropdown',
                        targetAction: 'changed',
                        targetName: 'sreAgentCreateResourceGroup',
                        targetFriendlyName: 'SRE Agent Create - Resource group',
                        valueObjectName: SpecialControlValue.CustomerSuppliedData,
                        valueObjectFriendlyName: SpecialControlValue.CustomerSuppliedData,
                    });
                    setFieldValue('resourceGroupId', resourceGroup?.id);
                    setFieldValue('isResourceGroupNew', resourceGroup?.new ?? false);
                }}
                disabled={isDeploying}
                errorMessage={values.subscriptionId && values.resourceGroupId ? policyErrorMessage || errors.resourceGroupId : undefined}
                telemetrySource={TelemetrySource.SreAgentCreate}
                createNew
            />

            <InputFormik
                name="name"
                label={intl.formatMessage(PortalResources.agentName)}
                required
                placeholder={intl.formatMessage(PortalResources.enterName)}
                disabled={isDeploying}
                orientation="vertical"
            />

            <DropdownFormik
                name="location"
                label={intl.formatMessage(PortalResources.region)}
                required
                value={locationDropdownOptions.find(opt => opt.data === values.location)?.text ?? ''}
                selectedOptions={values.location ? [values.location] : []}
                placeholder={intl.formatMessage(PortalResources.selectRegion)}
                disabled={locationsLoading || isDeploying || isLocationDisabled}
                isLoading={locationsLoading}
                orientation="vertical"
                onOptionSelect={(_e, data) => {
                    logControlEvent({
                        targetType: 'dropdown',
                        targetAction: 'changed',
                        targetName: 'sreAgentCreateRegion',
                        targetFriendlyName: 'SRE Agent Create - Region',
                        valueObjectName: data.optionValue ?? '',
                        valueObjectFriendlyName: data.optionValue ?? '',
                    });
                }}
                onBlur={() => {
                    logControlEvent({
                        targetType: 'dropdown',
                        targetAction: 'blurred',
                        targetName: 'sreAgentCreateRegion',
                        targetFriendlyName: 'SRE Agent Create - Region',
                        valueObjectName: SpecialControlValue.DoAction,
                        valueObjectFriendlyName: SpecialControlValue.DoAction,
                    });
                }}
            >
                {locationDropdownOptions.map(option => (
                    <Option key={option.key} value={option.data} text={option.text}>
                        {option.text}
                    </Option>
                ))}
            </DropdownFormik>

            {showAnthropicDisabledMessage && (
                <MessageBar layout="multiline" style={{ alignItems: 'center' }}>
                    <MessageBarBody>
                        <Caption1>{intl.formatMessage(PortalResources.anthropicNotAvailable)}</Caption1>
                    </MessageBarBody>
                </MessageBar>
            )}

            {showDefaultModelPicker && (
                <Field
                    id="defaultModelProvider"
                    label={intl.formatMessage(PortalResources.modelProviderLabel)}
                    orientation="vertical"
                    required
                    validationMessage={getSupportedModelsFailure}
                >
                    {isSupportedModelsLoading && !!values.location && !!values.subscriptionId ? (
                        <Skeleton aria-label={intl.formatMessage(PortalResources.loading)}>
                            <SkeletonItem size={32} />
                        </Skeleton>
                    ) : (
                        <Dropdown
                            name="defaultModelProvider"
                            disabled={isDeploying || isSupportedModelsLoading}
                            value={
                                supportedProviders?.find(option => option.key === values.defaultModelProvider)?.text ||
                                values.defaultModelProvider
                            }
                            placeholder={intl.formatMessage(PortalResources.providerPlaceholder)}
                            onOptionSelect={(_event, data) => {
                                logControlEvent({
                                    targetType: 'dropdown',
                                    targetAction: 'changed',
                                    targetName: 'sreAgentCreateDefaultModelProvider',
                                    targetFriendlyName: 'SRE Agent Create - Default Model Provider',
                                    valueObjectName: data.optionValue ?? '',
                                    valueObjectFriendlyName: data.optionValue ?? '',
                                });
                                setFieldValue('defaultModelProvider', data.optionValue);
                            }}
                            onBlur={() => {
                                logControlEvent({
                                    targetType: 'dropdown',
                                    targetAction: 'blurred',
                                    targetName: 'sreAgentCreateDefaultModelProvider',
                                    targetFriendlyName: 'SRE Agent Create - Default Model Provider',
                                    valueObjectName: SpecialControlValue.DoAction,
                                    valueObjectFriendlyName: SpecialControlValue.DoAction,
                                });
                            }}
                        >
                            {supportedProviders?.map(option => (
                                <Option value={option.key} checkIcon={null} disabled={option.disabled}>
                                    {option.text}
                                </Option>
                            ))}
                        </Dropdown>
                    )}
                </Field>
            )}

            {values.defaultModelProvider === ModelProvider.Anthropic && (
                <MessageBar layout="multiline" style={{ alignItems: 'center' }}>
                    <MessageBarBody>
                        <Caption1>{intl.formatMessage(PortalResources.anthropicEuRegionInfoMessage)}</Caption1>{' '}
                        <Link href={LearnMoreLinks.sreAgentDataHandling} target="_blank" rel="noopener noreferrer">
                            <Caption1>{intl.formatMessage(PortalResources.anthropicEuRegionLearnMore)}</Caption1>
                        </Link>
                    </MessageBarBody>
                </MessageBar>
            )}

            <RadioGroupFormik
                name="createNewAppInsights"
                label={intl.formatMessage(PortalResources.applicationInsights)}
                options={[
                    { key: ApplicationInsightsSetup.New, text: intl.formatMessage(PortalResources.createNew) },
                    { key: ApplicationInsightsSetup.Existing, text: intl.formatMessage(PortalResources.useExisting) },
                ]}
                onChange={() => {
                    setFieldValue('existingAppInsightsId', '');
                    setFieldValue('appInsightsSubscriptionId', '');
                }}
                disabled={isDeploying}
                stackVertical={false}
            />
            {values.createNewAppInsights === ApplicationInsightsSetup.Existing && (
                <>
                    <SubscriptionDropdown
                        label={intl.formatMessage(PortalResources.appInsightsSubscription)}
                        onSubscriptionChange={subscription => {
                            setFieldValue('appInsightsSubscriptionId', subscription?.subscriptionId ?? '');
                            setFieldValue('existingAppInsightsId', '');
                            logControlEvent({
                                targetType: 'dropdown',
                                targetAction: 'changed',
                                targetName: 'sreAgentCreateAppInsightsSubscription',
                                targetFriendlyName: 'SRE Agent Create - App Insights Subscription',
                                valueObjectName: subscription?.subscriptionId ?? '',
                                valueObjectFriendlyName: subscription?.subscriptionId ?? '',
                            });
                        }}
                        selectedSubscriptionId={values.appInsightsSubscriptionId}
                        disabled={isDeploying}
                    />
                    <ComboboxWithFilterFormik
                        label={intl.formatMessage(PortalResources.existingAppInsightsName)}
                        name="existingAppInsightsId"
                        options={appInsightsDropdownOptions.map(opt => ({
                            value: opt.data,
                            text: opt.text,
                        }))}
                        placeholder={intl.formatMessage(PortalResources.existingAppInsightsNamePlaceholder)}
                        disabled={appInsightsLoading || isDeploying || !values.appInsightsSubscriptionId}
                        isLoading={appInsightsLoading}
                        orientation="vertical"
                        noOptionsMessage={intl.formatMessage(PortalResources.noResultsFound)}
                        onOptionSelect={(_e, option) => {
                            logControlEvent({
                                targetType: 'dropdown',
                                targetAction: 'changed',
                                targetName: 'sreAgentCreateAppInsights',
                                targetFriendlyName: 'SRE Agent Create - Application Insights',
                                valueObjectName: option.optionValue ?? '',
                                valueObjectFriendlyName: option.optionValue ?? '',
                            });
                        }}
                    />
                </>
            )}
        </div>
    );
};

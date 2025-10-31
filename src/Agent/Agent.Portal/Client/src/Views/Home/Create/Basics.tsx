import { Dropdown, Field, Input, MessageBar, MessageBarBody, Option, Skeleton, SkeletonItem, Text } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PermissionsClient } from '../../../Common/Clients/PermissionsClient';
import { ResourceGroupDropdown } from '../../../Common/Components/ResourceGroupDropdown';
import { SubscriptionDropdown } from '../../../Common/Components/SubscriptionDropdown';
import { ApiVersions } from '../../../Common/Constants/ApiVersions';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { Subscription } from '../../../Common/Contracts/Arm';
import { FieldRestrictionResult } from '../../../Common/Contracts/Permissions';
import { ArmServiceType } from '../../../Common/Utilities/ArmTemplateBuilder/ArmTemplateTypes';
import { getCanonicalLocation } from '../../../Common/Utilities/Location';
import { PortalResources } from '../../../Strings/Resources';
import { SreAgentCreateFormProps } from './CreateAgentDialog';
import { useSreAgentLocations } from './useSreAgentLocations';

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
    const { locationsList, locationsLoading, containsNoLocations } = useSreAgentLocations(
        values.subscriptionId,
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

    const onSubscriptionChange = useCallback(
        (subscription?: Subscription) => {
            setFieldValue('subscriptionId', subscription?.subscriptionId ?? '');
            if (!isDeploying) {
                setFieldValue('managedResourceGroups', []);
                setFieldValue('maxResourceGroupsError', false);
                setFieldValue('managedResourceGroupsPermissionError', false);
                setFieldValue('managedResourceGroupsLockError', false);
            }
        },
        [isDeploying, setFieldValue]
    );

    const getPolicyErrorMessage = useCallback(async (): Promise<string> => {
        const deploymentContent = getContentDetailsForPolicyCheck(
            values.resourceGroupId,
            ArmServiceType.Deployments,
            `${values.name}-deployment`,
            values.location,
            ApiVersions.armApiVersion20230301
        );
        const userIdentityContent = getContentDetailsForPolicyCheck(
            values.resourceGroupId,
            ArmServiceType.UserIdentity,
            `${values.name}-identity`,
            values.location,
            ApiVersions.userIdentityApiVersion20181130
        );
        const workspaceContent = getContentDetailsForPolicyCheck(
            values.resourceGroupId,
            ArmServiceType.Workspace,
            `${values.name}-workspace`,
            values.location,
            ApiVersions.workspacesApiVersion20200801
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
                    setFieldValue('resourceGroupId', resourceGroup?.id);
                    setFieldValue('isResourceGroupNew', resourceGroup?.new ?? false);
                }}
                disabled={isDeploying}
                errorMessage={values.subscriptionId && values.resourceGroupId ? policyErrorMessage || errors.resourceGroupId : undefined}
                telemetrySource={TelemetrySource.SreAgentCreate}
                createNew
            />

            <Field
                label={intl.formatMessage(PortalResources.agentName)}
                required
                validationMessage={errors.name}
                validationState={errors.name ? 'error' : undefined}
            >
                <Input
                    value={values.name}
                    onChange={(_, data) => setFieldValue('name', data.value)}
                    placeholder={intl.formatMessage(PortalResources.enterName)}
                    disabled={isDeploying}
                />
            </Field>

            <Field
                label={intl.formatMessage(PortalResources.region)}
                required
                validationMessage={errors.location}
                validationState={errors.location ? 'error' : undefined}
            >
                {locationsLoading ? (
                    <Skeleton>
                        <SkeletonItem />
                    </Skeleton>
                ) : (
                    <Dropdown
                        value={locationDropdownOptions.find(opt => opt.data === values.location)?.text ?? ''}
                        selectedOptions={values.location ? [values.location] : []}
                        onOptionSelect={(_e, data) => setFieldValue('location', data.optionValue ?? '')}
                        placeholder={intl.formatMessage(PortalResources.selectRegion)}
                        disabled={locationsLoading || isDeploying || isLocationDisabled}
                    >
                        {locationDropdownOptions.map(option => (
                            <Option key={option.key} value={option.data} text={option.text}>
                                {option.text}
                            </Option>
                        ))}
                    </Dropdown>
                )}
            </Field>

            {/* TODO: Create new / use existing App Insights */}
        </div>
    );
};

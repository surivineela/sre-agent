import { useCallback, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import * as Yup from 'yup';
import { DeploymentClient } from '../../../Common/Clients/DeploymentClient';
import { ResourceClient } from '../../../Common/Clients/ResourceClient';
import { ResourceGroupClient } from '../../../Common/Clients/ResourceGroupClient';
import { DeploymentProvisioningStates, ResourceTypes } from '../../../Common/Constants/Arm';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { useNotifications } from '../../../Common/Contexts/NotificationContext';
import { AgentSpaceAllowedAction, AgentSpaceCreateFormValues } from '../../../Common/Contracts/AgentSpace';
import { LogLevel } from '../../../Common/Contracts/Telemetry';
import { useTelemetry } from '../../../Common/Hooks/useTelemetry';
import { parseArmId } from '../../../Common/Utilities/ArmId';
import { AgentSpaceTemplateResource } from '../../../Common/Utilities/ArmTemplateBuilder/AgentSpace/AgentSpaceTemplateResource';
import { ArmTemplateBuilder } from '../../../Common/Utilities/ArmTemplateBuilder/ArmTemplateBuilder';
import { AgentSpaceParameterName, ArmTemplateParameterName } from '../../../Common/Utilities/ArmTemplateBuilder/ArmTemplateTypes';
import { getArmErrorMessage } from '../../../Common/Utilities/Client';
import { newShortGuid } from '../../../Common/Utilities/Guid';
import { getCanonicalLocation } from '../../../Common/Utilities/Location';
import { PortalResources } from '../../../Strings/Resources';

const ARM_DEPLOYMENT_NAME_LIMIT = 64;

export interface UseAgentSpaceCreateProps {
    onDeploymentStarted?: () => void;
}

export interface UseAgentSpaceCreateReturn {
    initialValues: AgentSpaceCreateFormValues;
    validationSchema: Yup.AnyObjectSchema;
    onSubmit: (values: AgentSpaceCreateFormValues) => Promise<void>;
    isDeploying: boolean;
    deploymentResourceId: string;
    agentSpaceResourceId: string;
}

export const useAgentSpaceCreate = ({ onDeploymentStarted }: UseAgentSpaceCreateProps): UseAgentSpaceCreateReturn => {
    const intl = useIntl();
    const notifications = useNotifications();
    const { logEvent } = useTelemetry(TelemetrySource.AgentSpaceCreate, undefined);

    const createStartTimeRef = useRef<number | undefined>(undefined);

    const [isDeploying, setIsDeploying] = useState(false);
    const [deploymentResourceId, setDeploymentResourceId] = useState('');
    const [agentSpaceResourceId, setAgentSpaceResourceId] = useState('');

    const deploymentClient = useMemo(() => DeploymentClient.getInstance(TelemetrySource.AgentSpaceCreate), []);
    const resourceClient = useMemo(() => ResourceClient.getInstance(TelemetrySource.AgentSpaceCreate), []);
    const resourceGroupClient = useMemo(() => ResourceGroupClient.getInstance(TelemetrySource.AgentSpaceCreate), []);

    const initialValues: AgentSpaceCreateFormValues = useMemo(
        () => ({
            subscriptionId: '',
            resourceGroupId: '',
            isResourceGroupNew: false,
            name: '',
            location: '',
            description: '',
            maxAgentCount: 10,
            enableGenevaActions: false,
            endpoint: '',
            clientId: '',
            extensionName: '',
            allowedActions: [
                {
                    id: newShortGuid(),
                    actionName: '',
                    extension: '',
                    approvalRequired: false,
                },
            ],
        }),
        []
    );

    const validationSchema = useMemo(
        () =>
            Yup.object({
                subscriptionId: Yup.string().required(intl.formatMessage(PortalResources.subscription)),
                resourceGroupId: Yup.string().required(intl.formatMessage(PortalResources.resourceGroup)),
                name: Yup.string()
                    .min(2, intl.formatMessage(PortalResources.agentSpaceNameMinMax))
                    .max(32, intl.formatMessage(PortalResources.agentSpaceNameMinMax))
                    .matches(/^[a-z0-9]([a-z0-9-]*[a-z0-9])?$/, intl.formatMessage(PortalResources.agentSpaceNameValidation))
                    .required(intl.formatMessage(PortalResources.agentSpaceName)),
                location: Yup.string().required(intl.formatMessage(PortalResources.selectRegion)),
                maxAgentCount: Yup.number().min(1).required(),
            }),
        [intl]
    );

    const generateTemplate = useCallback((values: AgentSpaceCreateFormValues) => {
        const builder = new ArmTemplateBuilder();

        // Build Geneva Actions config if enabled
        const genevaActionsConfiguration = values.enableGenevaActions
            ? {
                  acisEndpoint: values.endpoint,
                  clientId: values.clientId,
                  extensionName: values.extensionName,
                  allowedActions: values.allowedActions
                      .filter(a => a.actionName && a.extension)
                      .map(
                          (a): AgentSpaceAllowedAction => ({
                              actionName: a.actionName,
                              extension: a.extension,
                              approvalRequired: a.approvalRequired,
                          })
                      ),
              }
            : undefined;

        const agentSpaceResource = new AgentSpaceTemplateResource(builder, {
            genevaActionsConfiguration,
        });

        builder.addResource(agentSpaceResource);
        return builder.getTemplate();
    }, []);

    const generateParameters = useCallback((values: AgentSpaceCreateFormValues) => {
        const parsedRgId = parseArmId(values.resourceGroupId);
        return {
            [ArmTemplateParameterName.SubscriptionId]: { value: values.subscriptionId },
            [ArmTemplateParameterName.ResourceGroupName]: { value: parsedRgId.resourceGroup },
            [ArmTemplateParameterName.Location]: { value: getCanonicalLocation(values.location) },
            [AgentSpaceParameterName.AgentSpaceName]: { value: values.name },
            [AgentSpaceParameterName.AgentSpaceDescription]: { value: values.description },
            [AgentSpaceParameterName.AgentSpaceMaxCount]: { value: Number(values.maxAgentCount) },
        };
    }, []);

    const onSubmit = useCallback(
        async (values: AgentSpaceCreateFormValues) => {
            setIsDeploying(true);
            createStartTimeRef.current = Date.now();

            const parsedRg = parseArmId(values.resourceGroupId);
            const resourceGroupName = parsedRg.resourceGroup || '';
            const subscriptionId = values.subscriptionId;

            // Generate deployment name
            const maxNameLength = ARM_DEPLOYMENT_NAME_LIMIT - 30;
            const safeName = values.name.length > maxNameLength ? values.name.substring(0, maxNameLength) : values.name;
            const deploymentName = `SRE-AgentSpace-${safeName}-${Date.now()}`;

            const rgDeploymentId = `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroupName}/providers/Microsoft.Resources/deployments/${deploymentName}`;
            const spaceResourceId = `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroupName}/providers/Microsoft.App/agentSpaces/${values.name}`;

            setDeploymentResourceId(rgDeploymentId);
            setAgentSpaceResourceId(spaceResourceId);

            // Show validation notification
            const validationNotificationId = notifications.start(
                intl.formatMessage(PortalResources.creatingAgentSpace),
                intl.formatMessage(PortalResources.creatingAgentSpaceDescription, { name: values.name })
            );

            try {
                // Create resource group if new
                if (values.isResourceGroupNew) {
                    const rgResponse = await resourceGroupClient.createResourceGroup(
                        subscriptionId,
                        resourceGroupName,
                        getCanonicalLocation(values.location),
                        {}
                    );

                    if (!rgResponse.isSuccessful) {
                        throw new Error(`Failed to create resource group: ${getArmErrorMessage(rgResponse.error)}`);
                    }
                }

                // Register Microsoft.App provider
                await resourceClient.registerProvider(subscriptionId, ResourceTypes.AppProvider);

                // Build and deploy ARM template
                const template = generateTemplate(values);
                const parameters = generateParameters(values);

                const deployResponse = await deploymentClient.createNewDeployment(
                    rgDeploymentId,
                    template,
                    parameters,
                    spaceResourceId,
                    true // skipPolling
                );

                if (!deployResponse.isSuccessful) {
                    const errorMessage = getArmErrorMessage(deployResponse.error);
                    notifications.fail(
                        validationNotificationId,
                        intl.formatMessage(PortalResources.creatingAgentSpace),
                        errorMessage
                            ? intl.formatMessage(PortalResources.agentSpaceCreatedErrorDetail, {
                                  name: values.name,
                                  error: errorMessage,
                              })
                            : intl.formatMessage(PortalResources.agentSpaceCreatedError, { name: values.name })
                    );

                    logEvent({
                        action: 'createAgentSpace',
                        actionModifier: 'failed',
                        logLevel: LogLevel.Error,
                        additionalData: { error: errorMessage },
                    });

                    setIsDeploying(false);
                    return;
                }

                // Validation succeeded, trigger step change
                notifications.succeed(
                    validationNotificationId,
                    intl.formatMessage(PortalResources.creatingAgentSpace),
                    intl.formatMessage(PortalResources.creatingAgentSpaceDescription, { name: values.name })
                );

                if (onDeploymentStarted) {
                    onDeploymentStarted();
                }

                // Start deployment polling
                notifications.startWithPolling(intl.formatMessage(PortalResources.creatingAgentSpace), {
                    pollFn: async () => {
                        const deploymentResponse = await deploymentClient.getDeployment(rgDeploymentId);
                        const provisioningState = deploymentResponse?.content?.properties?.provisioningState;

                        if (provisioningState === DeploymentProvisioningStates.succeeded) {
                            setIsDeploying(false);
                            return {
                                complete: true,
                                success: true,
                                title: intl.formatMessage(PortalResources.creatingAgentSpace),
                                description: intl.formatMessage(PortalResources.agentSpaceCreatedSuccess, { name: values.name }),
                            };
                        }

                        if (provisioningState === DeploymentProvisioningStates.Failed) {
                            setIsDeploying(false);
                            return {
                                complete: true,
                                success: false,
                                title: intl.formatMessage(PortalResources.creatingAgentSpace),
                                description: intl.formatMessage(PortalResources.agentSpaceCreatedError, { name: values.name }),
                            };
                        }

                        return { complete: false };
                    },
                    interval: 5000,
                    maxAttempts: 120,
                });
            } catch (error) {
                const errorMessage = error instanceof Error ? error.message : String(error);
                notifications.fail(
                    validationNotificationId,
                    intl.formatMessage(PortalResources.creatingAgentSpace),
                    errorMessage
                        ? intl.formatMessage(PortalResources.agentSpaceCreatedErrorDetail, {
                              name: values.name,
                              error: errorMessage,
                          })
                        : intl.formatMessage(PortalResources.agentSpaceCreatedError, { name: values.name })
                );

                logEvent({
                    action: 'createAgentSpace',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: { error: errorMessage },
                });

                setIsDeploying(false);
            }
        },
        [
            generateParameters,
            generateTemplate,
            deploymentClient,
            intl,
            logEvent,
            notifications,
            onDeploymentStarted,
            resourceClient,
            resourceGroupClient,
        ]
    );

    return {
        initialValues,
        validationSchema,
        onSubmit,
        isDeploying,
        deploymentResourceId,
        agentSpaceResourceId,
    };
};

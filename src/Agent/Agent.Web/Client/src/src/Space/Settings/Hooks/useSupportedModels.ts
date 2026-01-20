import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import { LocationClient } from '../../../Common/Clients/LocationClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { Model, ModelProvider } from '../../../Common/Contracts/Azure/SreAgent';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import { SettingNames, useConfigSetting } from '../../../Common/Hooks/ConfigSettings';
import { SettingsTabResources, SreAgentResources } from '../../../Strings/SREAgentResources';

export const useSupportedModels = (agentResourceId: string, location: string) => {
    const intl = useIntl();
    const az = useContext(AzPortalContext);
    const [supportedProviders, setSupportedProviders] = useState<
        {
            key: string;
            text: string;
        }[]
    >();
    const [isSupportedModelsLoading, setIsSupportedModelsLoading] = useState(true);
    const [getSupportedModelsFailure, setGetSupportedModelsFailure] = useState('');
    const [isUpdatingDefaultModel, setIsUpdatingDefaultModel] = useState(false);

    const { subscription } = useMemo(() => new ArmResourceDescriptor(agentResourceId), [agentResourceId]);

    const showDefaultModelPicker = useConfigSetting(SettingNames.ShowDefaultModelPicker);

    const providerMapping: Record<ModelProvider, string> = useMemo(
        () => ({
            [ModelProvider.Anthropic]: intl.formatMessage(SreAgentResources.anthropicProviderLabel),
            [ModelProvider.MicrosoftFoundry]: intl.formatMessage(SreAgentResources.azureOpenAiProviderLabel),
        }),
        [intl]
    );

    const getSupportedModels = useCallback(async () => {
        setIsSupportedModelsLoading(true);
        setGetSupportedModelsFailure('');

        const supportedModelsPromise = await LocationClient.getSupportedModels(subscription, location);

        if (supportedModelsPromise?.metadata?.success && supportedModelsPromise.data) {
            const supportedModels = supportedModelsPromise.data.value;

            const supportedProviders = Array.from(new Set(supportedModels.map(model => model.properties.provider))).map(provider => {
                return { key: provider, text: providerMapping[provider as ModelProvider] || provider };
            });
            setSupportedProviders(supportedProviders);
            setIsSupportedModelsLoading(false);
        } else {
            const error = getErrorMessage(supportedModelsPromise?.metadata?.error);

            az.log({
                action: 'fetch-supported-models',
                actionModifier: 'failed',
                logLevel: 'error',
                resourceId: agentResourceId,
                data: { error },
            });

            setGetSupportedModelsFailure(intl.formatMessage(SettingsTabResources.getSupportedModelsFailedMessage));
            setIsSupportedModelsLoading(false);
        }
    }, [agentResourceId, az, intl, location, providerMapping, subscription]);

    const updateDefaultModel = useCallback(
        async (values: Model) => {
            setIsUpdatingDefaultModel(true);
            const notificationId = az.startNotification(
                intl.formatMessage(SettingsTabResources.defaultModelUpdatingTitle),
                intl.formatMessage(SettingsTabResources.defaultModelUpdatingDescription, { model: values.provider })
            );

            const updatePayload = {
                properties: {
                    defaultModel: {
                        provider: values.provider,
                        model: 'Automatic',
                    },
                },
            };

            const response = await SreAgentClient.patchAgent(agentResourceId, updatePayload);

            if (response.metadata.success) {
                az.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(SettingsTabResources.defaultModelUpdateSuccess, { model: values.provider })
                );
                az.log({
                    action: 'updateDefaultModel',
                    actionModifier: 'succeeded',
                    resourceId: agentResourceId,
                    logLevel: 'info',
                    data: {
                        defaultModel: {
                            provider: values.provider,
                        },
                    },
                });
            } else {
                az.stopNotification(notificationId, false, intl.formatMessage(SettingsTabResources.defaultModelUpdateFailed));
                az.log({
                    action: 'updateDefaultModel',
                    actionModifier: 'failed',
                    resourceId: agentResourceId,
                    logLevel: 'error',
                    data: {
                        error: response.metadata.error,
                    },
                });
            }
            setIsUpdatingDefaultModel(false);
        },
        [agentResourceId, az, intl]
    );

    useEffect(() => {
        if (agentResourceId && showDefaultModelPicker) {
            getSupportedModels();
        }
    }, [agentResourceId, getSupportedModels, showDefaultModelPicker]);

    return {
        supportedProviders,
        isSupportedModelsLoading,
        getSupportedModelsFailure,
        updateDefaultModel,
        isUpdatingDefaultModel,
        refreshSupportedModels: getSupportedModels,
    };
};

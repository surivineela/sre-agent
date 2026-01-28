import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import { LocationClient } from '../../../Common/Clients/LocationClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { Model } from '../../../Common/Contracts/Azure/SreAgent';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import { SettingNames, useConfigSetting } from '../../../Common/Hooks/ConfigSettings';
import { SettingsTabResources } from '../../../Strings/SREAgentResources';

/** @note (wangcynthia): Hardcoded for now until the ARM supported API is finished. */
const supportedModelsResponse = {
    data: {
        value: [
            {
                id: '/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}/locations/{location}/supportedModels/MicrosoftFoundry-gpt-5.2',
                name: 'MicrosoftFoundry-gpt-5.2',
                type: '{resourceProviderNamespace}/locations/supportedModels',
                location: '{location}',
                properties: {
                    providerName: 'MicrosoftFoundry',
                    providerDisplayName: 'Azure OpenAI',
                    modelName: 'gpt-5.2',
                    modelDisplayName: 'GPT-5.2',
                    multiplier: '1x',
                    default: false,
                },
            },
            {
                id: '/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}/locations/{location}/supportedModels/MicrosoftFoundry-gpt-5',
                name: 'MicrosoftFoundry-gpt-5',
                type: '{resourceProviderNamespace}/locations/supportedModels',
                location: '{location}',
                properties: {
                    providerName: 'MicrosoftFoundry',
                    providerDisplayName: 'Azure OpenAI',
                    modelName: 'gpt-5',
                    modelDisplayName: 'GPT-5',
                    multiplier: '1x',
                    default: true,
                },
            },
            {
                id: '/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}/locations/{location}/supportedModels/Anthropic-claude-opus-4-5',
                name: 'Anthropic-claude-opus-4-5',
                type: '{resourceProviderNamespace}/locations/supportedModels',
                location: '{location}',
                properties: {
                    providerName: 'Anthropic',
                    providerDisplayName: 'Anthropic',
                    modelName: 'claude-opus-4-5',
                    modelDisplayName: 'Claude Opus 4.5',
                    multiplier: '3x',
                    default: false,
                },
            },
            {
                id: '/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}/locations/{location}/supportedModels/Anthropic-claude-sonnet-4-5',
                name: 'Anthropic-claude-sonnet-4-5',
                type: '{resourceProviderNamespace}/locations/supportedModels',
                location: '{location}',
                properties: {
                    providerName: 'Anthropic',
                    providerDisplayName: 'Anthropic',
                    modelName: 'claude-sonnet-4-5',
                    modelDisplayName: 'Claude Sonnet 4.5',
                    multiplier: '1x',
                    default: false,
                },
            },
            {
                id: '/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}/locations/{location}/supportedModels/Anthropic-claude-haiku-4-5',
                name: 'Anthropic-claude-haiku-4-5',
                type: '{resourceProviderNamespace}/locations/supportedModels',
                location: '{location}',
                properties: {
                    providerName: 'Anthropic',
                    providerDisplayName: 'Anthropic',
                    modelName: 'claude-haiku-4-5',
                    modelDisplayName: 'Claude Haiku 4.5',
                    multiplier: '0.33x',
                    default: false,
                },
            },
        ],
    },
    metadata: {
        success: true,
        error: null,
    },
};

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

    const getSupportedModels = useCallback(async () => {
        setIsSupportedModelsLoading(true);
        setGetSupportedModelsFailure('');

        const supportedModelsPromise = showDefaultModelPicker
            ? await Promise.resolve(supportedModelsResponse)
            : await LocationClient.getSupportedModels(subscription, location);

        if (supportedModelsPromise?.metadata?.success && supportedModelsPromise.data) {
            const supportedModels = supportedModelsPromise.data.value;

            const providerMap = new Map(
                supportedModels.map(model => [
                    model.properties.providerName,
                    { key: model.properties.providerName, text: model.properties.providerDisplayName },
                ])
            );
            const supportedProviders = Array.from(providerMap.values());
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
    }, [agentResourceId, az, intl, location, subscription, showDefaultModelPicker]);

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
                        name: 'Automatic',
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
        if (agentResourceId) {
            getSupportedModels();
        }
    }, [agentResourceId, getSupportedModels]);

    return {
        supportedProviders,
        isSupportedModelsLoading,
        getSupportedModelsFailure,
        updateDefaultModel,
        isUpdatingDefaultModel,
        refreshSupportedModels: getSupportedModels,
    };
};

import { useFormikContext } from 'formik';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { LocationClient } from '../../../Common/Clients/LocationClient';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { ModelProvider } from '../../../Common/Contracts/SreAgent';
import { LogLevel } from '../../../Common/Contracts/Telemetry';
import { SettingNames, useConfigSetting } from '../../../Common/Hooks/useConfigSettings';
import { useTelemetry } from '../../../Common/Hooks/useTelemetry';
import { getArmErrorMessage } from '../../../Common/Utilities/Client';
import { PortalResources } from '../../../Strings/Resources';
import { SreAgentCreateFormProps } from './CreateAgentDialog';

/** @note (wangcynthia): Hardcoded for now until the ARM supported API is finished. */
const supportedModelsResponse = {
    isSuccessful: true,
    content: {
        value: [
            {
                id: "/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}/locations/{location}/supportedModels/MicrosoftFoundry-gpt-5.2",
                name: "MicrosoftFoundry-gpt-5.2",
                type: "{resourceProviderNamespace}/locations/supportedModels",
                location: "{location}",
                properties: {
                    providerName: "MicrosoftFoundry",
                    providerDisplayName: "Azure OpenAI",
                    modelName: "gpt-5.2",
                    modelDisplayName: "GPT-5.2",
                    multiplier: "1x",
                    default: false
                }
            },
            {
                id: "/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}/locations/{location}/supportedModels/MicrosoftFoundry-gpt-5",
                name: "MicrosoftFoundry-gpt-5",
                type: "{resourceProviderNamespace}/locations/supportedModels",
                location: "{location}",
                properties: {
                    providerName: "MicrosoftFoundry",
                    providerDisplayName: "Azure OpenAI",
                    modelName: "gpt-5",
                    modelDisplayName: "GPT-5",
                    multiplier: "1x",
                    default: true
                }
            },
            {
                id: "/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}/locations/{location}/supportedModels/Anthropic-claude-opus-4-5",
                name: "Anthropic-claude-opus-4-5",
                type: "{resourceProviderNamespace}/locations/supportedModels",
                location: "{location}",
                properties: {
                    providerName: "Anthropic",
                    providerDisplayName: "Anthropic",
                    modelName: "claude-opus-4-5",
                    modelDisplayName: "Claude Opus 4.5",
                    multiplier: "3x",
                    default: false
                }
            },
            {
                id: "/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}/locations/{location}/supportedModels/Anthropic-claude-sonnet-4-5",
                name: "Anthropic-claude-sonnet-4-5",
                type: "{resourceProviderNamespace}/locations/supportedModels",
                location: "{location}",
                properties: {
                    providerName: "Anthropic",
                    providerDisplayName: "Anthropic",
                    modelName: "claude-sonnet-4-5",
                    modelDisplayName: "Claude Sonnet 4.5",
                    multiplier: "1x",
                    default: false
                }
            },
            {
                id: "/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}/locations/{location}/supportedModels/Anthropic-claude-haiku-4-5",
                name: "Anthropic-claude-haiku-4-5",
                type: "{resourceProviderNamespace}/locations/supportedModels",
                location: "{location}",
                properties: {
                    providerName: "Anthropic",
                    providerDisplayName: "Anthropic",
                    modelName: "claude-haiku-4-5",
                    modelDisplayName: "Claude Haiku 4.5",
                    multiplier: "0.33x",
                    default: false
                }
            }
        ]
    },
    error: null,
};

export const useSreSupportedModels = (subscriptionId: string, location: string, telemetrySource: TelemetrySource) => {
    const intl = useIntl();
    const { logEvent } = useTelemetry(telemetrySource, subscriptionId);
    const [supportedProviders, setSupportedProviders] = useState<
        {
            key: string;
            text: string;
            disabled?: boolean;
        }[]
    >();
    const [isSupportedModelsLoading, setIsSupportedModelsLoading] = useState(false);
    const [getSupportedModelsFailure, setGetSupportedModelsFailure] = useState('');
    const [showAnthropicDisabledMessage, setShowAnthropicDisabledMessage] = useState(false);
    const { setFieldValue } = useFormikContext<SreAgentCreateFormProps>();

    const locationClient = useMemo(() => LocationClient.getInstance(telemetrySource), [telemetrySource]);

    const showDefaultModelPicker = useConfigSetting(SettingNames.ShowDefaultModelPicker);

    const getSupportedModels = useCallback(async () => {
        setIsSupportedModelsLoading(true);
        setGetSupportedModelsFailure('');

        const supportedModelsPromise = showDefaultModelPicker
            ? await Promise.resolve(supportedModelsResponse)
            : await locationClient.getSupportedModels(subscriptionId, location);

        if (supportedModelsPromise?.isSuccessful && supportedModelsPromise.content) {
            const supportedModels = supportedModelsPromise.content.value;

            const providerMap = new Map(
                supportedModels.map(model => [
                    model.properties.providerName,
                    { key: model.properties.providerName, text: model.properties.providerDisplayName, disabled: false },
                ])
            );
            const supportedProviders = Array.from(providerMap.values());
            // If Anthropic is not in the supported providers list, it isn't allowed by the administrator
            if (!supportedProviders.find(provider => provider.key === ModelProvider.Anthropic)) {
                supportedProviders.push({ key: ModelProvider.Anthropic, text: intl.formatMessage(PortalResources.anthropicProviderLabel), disabled: true });
                setShowAnthropicDisabledMessage(true);
            }
            const defaultProvider = supportedModels.find(model => model.properties.default)?.properties.providerName;
            setFieldValue(
                'defaultModelProvider',
                defaultProvider !== undefined
                    ? defaultProvider
                    : supportedProviders.length > 0
                      ? supportedProviders[0].key
                      : undefined
            );
            setSupportedProviders(supportedProviders);
            setIsSupportedModelsLoading(false);
        } else {
            const error = getArmErrorMessage(supportedModelsPromise?.error);

            logEvent({
                action: 'fetch-supported-models',
                actionModifier: 'error',
                logLevel: LogLevel.Error,
                additionalData: {
                    error,
                },
            });

            setGetSupportedModelsFailure(intl.formatMessage(PortalResources.getSupportedModelsFailedMessage));
            setIsSupportedModelsLoading(false);
        }
    }, [showDefaultModelPicker, locationClient, subscriptionId, location, setFieldValue, logEvent, intl]);

    useEffect(() => {
        if (subscriptionId && location) {
            getSupportedModels();
        }
    }, [getSupportedModels, location, subscriptionId]);

    return {
        supportedProviders,
        isSupportedModelsLoading,
        getSupportedModelsFailure,
        showAnthropicDisabledMessage
    };
};

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

export const useSreSupportedModels = (subscriptionId: string, location: string, telemetrySource: TelemetrySource) => {
    const intl = useIntl();
    const { logEvent } = useTelemetry(telemetrySource, subscriptionId);
    const [supportedProviders, setSupportedProviders] = useState<
        {
            key: string;
            text: string;
        }[]
    >();
    const [isSupportedModelsLoading, setIsSupportedModelsLoading] = useState(false);
    const [getSupportedModelsFailure, setGetSupportedModelsFailure] = useState('');
    const { setFieldValue } = useFormikContext<SreAgentCreateFormProps>();

    const locationClient = useMemo(() => LocationClient.getInstance(telemetrySource), [telemetrySource]);

    const showDefaultModelPicker = useConfigSetting(SettingNames.ShowDefaultModelPicker);

    const providerMapping: Record<ModelProvider, string> = useMemo(
        () => ({
            [ModelProvider.Anthropic]: intl.formatMessage(PortalResources.anthropicProviderLabel),
            [ModelProvider.MicrosoftFoundry]: intl.formatMessage(PortalResources.azureOpenAiProviderLabel),
        }),
        [intl]
    );

    const getSupportedModels = useCallback(async () => {
        setIsSupportedModelsLoading(true);
        setGetSupportedModelsFailure('');

        const supportedModelsPromise = await locationClient.getSupportedModels(subscriptionId, location);

        if (supportedModelsPromise?.isSuccessful && supportedModelsPromise.content) {
            const supportedModels = supportedModelsPromise.content.value;

            const supportedProviders = Array.from(new Set(supportedModels.map(model => model.properties.provider))).map(provider => {
                return { key: provider, text: providerMapping[provider as ModelProvider] || provider };
            });

            const defaultProvider = supportedModels.find(model => model.properties.default)?.properties.provider;
            setFieldValue(
                'defaultModelProvider',
                defaultProvider !== undefined
                    ? defaultProvider
                    : supportedModels.length > 0
                      ? supportedModels[0].properties.provider
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
    }, [locationClient, subscriptionId, location, setFieldValue, providerMapping, logEvent, intl]);

    useEffect(() => {
        if (subscriptionId && location && showDefaultModelPicker) {
            getSupportedModels();
        }
    }, [getSupportedModels, location, showDefaultModelPicker, subscriptionId]);

    return {
        supportedProviders,
        isSupportedModelsLoading,
        getSupportedModelsFailure,
    };
};

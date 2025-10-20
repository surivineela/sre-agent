import { useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { GenericErrorResources } from '../../Strings/SREAgentResources';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';
import { AgentMemoryClient } from '../Clients/AgentMemoryClient';
import { SettingNames, useConfigSetting } from './ConfigSettings';

export const useKnowledgeBaseConfig = () => {
    const staticConfig = useConfigSetting(SettingNames.KnowledgeBase);
    const [backendEnabled, setBackendEnabled] = useState<boolean | null>(null);
    const intl = useIntl();

    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    useEffect(() => {
        // Only check backend if static config allows it
        if (!staticConfig) {
            return;
        }

        const checkBackendStatus = async () => {
            try {
                const client = AgentMemoryClient.getInstance(sreAgentEndpoint);

                const response = await client.getStatus();

                if (response.isSuccessful && response.content) {
                    // Knowledge Base should be enabled if agent memory is enabled AND document retrieval is enabled
                    setBackendEnabled(response.content.enabled && response.content.documentRetrievalEnabled);
                } else {
                    setBackendEnabled(false);
                }
            } catch (error) {
                console.warn(intl.formatMessage(GenericErrorResources.failedToCheckAgentMemoryStatus), error);
                setBackendEnabled(false);
            }
        };

        checkBackendStatus();
    }, [staticConfig, intl]);

    // Return true only if both static config AND backend allow it
    return staticConfig && (backendEnabled ?? false);
};

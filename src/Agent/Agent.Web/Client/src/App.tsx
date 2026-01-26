import { AzureThemeDark, AzureThemeLight } from '@fluentui/azure-themes';
import { initializeIcons, ThemeProvider } from '@fluentui/react';
import { useEffect, useState } from 'react';
import './App.css';
import AzPortalProxy, { defaultSreAgentEndpoint } from './src/Common/AzPortalProxy/AzPortalProxy';
import { IEnvironmentInfo } from './src/Common/AzPortalProxy/Models/IEnvironmentInfo';
import { ThemeMode } from './src/Common/AzPortalProxy/Models/ITheme';
import { AzPortalContext } from './src/Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from './src/Common/AzPortalProxy/Providers/StartupInfoContext';
import { AgentWarningProvider } from './src/Common/Providers/AgentWarningProvider';
import { KnowledgeGraphBuildStatusProvider } from './src/Common/Providers/KnowledgeGraphBuildStatusProvider';
import { ReactQueryClientProvider } from './src/Common/Providers/ReactQueryClientProvider';
import { StreamingProvider } from './src/Common/Providers/StreamingProvider';
import { UserActivityReporter } from './src/Common/Utils/UserActivityReporter';
import { useAmplitudeSessionReplay } from './src/Space/Hooks/useAmplitudeSessionReplay';
import { useMonacoLoaderConfig } from './src/Space/Hooks/useMonacoLoaderConfig';
import SREAgentSpace from './src/Space/SREAgentSpace';
import { IntlProvider } from './src/Strings/Intl/IntlProvider';
import { AgentSiteCopilotProvider } from './src/Common/Providers/AgentSiteCopilotProvider';

const portalProxy = new AzPortalProxy();

const App: React.FC = () => {
    const [environmentInfo, setEnvironmentInfo] = useState({ sreAgentEndpoint: defaultSreAgentEndpoint } as IEnvironmentInfo);

    useAmplitudeSessionReplay();
    useMonacoLoaderConfig();

    useEffect(() => {
        portalProxy.initialize(setEnvironmentInfo);
        initializeIcons();

        // Initialize user activity reporter for playground session management
        UserActivityReporter.initialize(portalProxy);

        // Cleanup when App unmounts (page unload)
        return () => {
            UserActivityReporter.cleanup();
        };
    }, [setEnvironmentInfo]);

    // When we're running in standalone mode, we won't be getting any environment information
    // so we can load, but any features which depend on ARM won't work.
    const isPortalAndLoaded =
        environmentInfo.armEndpoint &&
        environmentInfo.armToken &&
        environmentInfo.resourceId &&
        environmentInfo.theme &&
        environmentInfo.sreAgentToken;
    const isUiReady = AzPortalProxy.inStandaloneMode || isPortalAndLoaded || environmentInfo.isCrossTenantPortalMode;

    return isUiReady ? (
        <EnvironmentContext.Provider value={environmentInfo}>
            <ThemeProvider theme={environmentInfo.theme?.mode === ThemeMode.Dark ? AzureThemeDark : AzureThemeLight}>
                <AgentSiteCopilotProvider themeMode={environmentInfo.theme?.mode}>
                    <IntlProvider locale={environmentInfo.effectiveLocale}>
                        <AzPortalContext.Provider value={portalProxy}>
                            <StreamingProvider>
                                <ReactQueryClientProvider>
                                    <KnowledgeGraphBuildStatusProvider>
                                        <AgentWarningProvider>
                                            <SREAgentSpace />
                                        </AgentWarningProvider>
                                    </KnowledgeGraphBuildStatusProvider>
                                </ReactQueryClientProvider>
                            </StreamingProvider>
                        </AzPortalContext.Provider>
                    </IntlProvider>
                </AgentSiteCopilotProvider>
            </ThemeProvider>
        </EnvironmentContext.Provider>
    ) : null;
};

export default App;

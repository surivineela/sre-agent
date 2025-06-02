import { AzureThemeDark, AzureThemeLight } from '@fluentui/azure-themes';
import { ThemeProvider } from '@fluentui/react';
import { FluentProvider, webDarkTheme, webLightTheme } from '@fluentui/react-components';
import { useEffect, useState } from 'react';
import './App.css';
import AzPortalProxy, { defaultSreAgentEndpoint } from './src/Common/AzPortalProxy/AzPortalProxy';
import { IEnvironmentInfo } from './src/Common/AzPortalProxy/Models/IEnvironmentInfo';
import { ThemeMode } from './src/Common/AzPortalProxy/Models/ITheme';
import { AzPortalContext } from './src/Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from './src/Common/AzPortalProxy/Providers/StartupInfoContext';
import { KnowledgeGraphBuildStatusProvider } from './src/Common/Providers/KnowledgeGraphBuildStatusProvider';
import SREAgentSpace from './src/Space/SREAgentSpace';
import { IntlProvider } from './src/Strings/Intl/IntlProvider';

const portalProxy = new AzPortalProxy();

const App: React.FC = () => {
    const [environmentInfo, setEnvironmentInfo] = useState({ sreAgentEndpoint: defaultSreAgentEndpoint } as IEnvironmentInfo);

    useEffect(() => {
        portalProxy.initialize(setEnvironmentInfo);
    }, [setEnvironmentInfo]);

    // When we're running in standalone mode, we won't be getting any environment information
    // so we can load, but any features which depend on ARM won't work.
    const uiReady =
        AzPortalProxy.inStandaloneMode ||
        (environmentInfo.armEndpoint &&
            environmentInfo.armToken &&
            environmentInfo.resourceId &&
            environmentInfo.theme &&
            environmentInfo.sreAgentToken);
    return uiReady ? (
        <EnvironmentContext.Provider value={environmentInfo}>
            <ThemeProvider theme={environmentInfo.theme?.mode === ThemeMode.Dark ? AzureThemeDark : AzureThemeLight}>
                <FluentProvider theme={environmentInfo.theme?.mode === ThemeMode.Dark ? webDarkTheme : webLightTheme}>
                    <IntlProvider locale={environmentInfo.effectiveLocale}>
                        <AzPortalContext.Provider value={portalProxy}>
                            <KnowledgeGraphBuildStatusProvider>
                                <SREAgentSpace />
                            </KnowledgeGraphBuildStatusProvider>
                        </AzPortalContext.Provider>
                    </IntlProvider>
                </FluentProvider>
            </ThemeProvider>
        </EnvironmentContext.Provider>
    ) : null;
};

export default App;

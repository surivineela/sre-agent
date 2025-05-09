import Header from './Components/Header';
import { initializeIcons } from '@fluentui/react';
import MainContent from './Components/MainContent';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import AzPortalProxy from './Common/AzPortalProxy/AzPortalProxy';
import { useEffect, useState } from 'react';
import { IEnvironmentInfo } from './Common/AzPortalProxy/Models/IEnvironments';
import { EnvironmentContext } from './Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThemeProvider } from '@fluentui/react';
import { ThemeMode } from './Common/AzPortalProxy/Models/ITheme';
import { AzureThemeDark, AzureThemeLight } from '@fluentui/azure-themes';
import { AzPortalContext } from './Common/AzPortalProxy/Providers/AzPortalProxyContext';


const queryClient = new QueryClient();
const portalProxy = new AzPortalProxy();
const App = () => {
  initializeIcons();
  const [environmentInfo, setEnvironmentInfo] = useState<IEnvironmentInfo>({} as IEnvironmentInfo);

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
    <>
      <QueryClientProvider client={queryClient}>
        <EnvironmentContext.Provider value={environmentInfo}>
          {/* <ThemeProvider theme={environmentInfo.theme?.mode === ThemeMode.Dark ? AzureThemeDark : AzureThemeLight} > */}
            <AzPortalContext.Provider value={portalProxy} >
              <>
                <Header />
                <MainContent />
              </>
            </AzPortalContext.Provider>
          {/* </ThemeProvider> */}
        </EnvironmentContext.Provider >
      </QueryClientProvider>
    </>
  ): null;
}

export default App

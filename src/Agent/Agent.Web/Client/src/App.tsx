import { useEffect, useState } from 'react';
import './App.css'
import AzPortalProxy from './src/Common/AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from './src/Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from './src/Common/AzPortalProxy/Providers/StartupInfoContext';
import SREAgentSpace from './src/Space/SREAgentSpace';
import { IEnvironmentInfo } from './src/Common/AzPortalProxy/Models/IEnvironmentInfo';
const portalProxy = new AzPortalProxy();

const App: React.FC = () => {
  const [environmentInfo, setEnvironmentInfo] = useState({} as IEnvironmentInfo);

  useEffect(() => {
    portalProxy.initialize(setEnvironmentInfo);
  }, [setEnvironmentInfo])

  // When we're running in standalone mode, we won't be getting any environment information
  // so we can load, but any features which depend on ARM won't work.
  const uiReady = AzPortalProxy.inStandaloneMode ||
    (environmentInfo.armEndpoint && environmentInfo.token && environmentInfo.resourceId);

  return uiReady ? (
    <EnvironmentContext.Provider value={environmentInfo}>
      <AzPortalContext.Provider value={portalProxy}>
        <SREAgentSpace />
      </AzPortalContext.Provider>
    </EnvironmentContext.Provider>
  ) : null;

}

export default App;

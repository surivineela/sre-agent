import React from 'react';
import AzPortalProxy from '../AzPortalProxy';

export const AzPortalContext = React.createContext<AzPortalProxy>(new AzPortalProxy());

export const useAzPortalContext = () => React.useContext(AzPortalContext);

import { MsalProvider } from '@azure/msal-react';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { HelmetProvider } from 'react-helmet-async';
import App from './App';
import { msalInstance } from './Common/Auth/msalConfig';
import { AuthProvider } from './Common/Contexts/AuthContext';
import { UserPreferencesProvider } from './Common/Contexts/UserPreferencesContext';
import { UserTenantSettingsProvider } from './Common/Contexts/UserTenantSettingsContext';
import './index.css';

const rootElement = document.getElementById('root');

if (!rootElement) {
    throw new Error('Root element not found');
}

createRoot(rootElement).render(
    <StrictMode>
        <HelmetProvider>
            <MsalProvider instance={msalInstance}>
                <AuthProvider>
                    <UserPreferencesProvider>
                        <UserTenantSettingsProvider>
                            <App />
                        </UserTenantSettingsProvider>
                    </UserPreferencesProvider>
                </AuthProvider>
            </MsalProvider>
        </HelmetProvider>
    </StrictMode>
);

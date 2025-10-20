import { MsalProvider } from '@azure/msal-react';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { HelmetProvider } from 'react-helmet-async';
import App from './App';
import { msalInstance } from './Common/Auth/msalConfig';
import { AuthProvider } from './Common/Contexts/AuthContext';
import './index.css';

const rootElement = document.getElementById('root');

if (!rootElement) {
    throw new Error('Root element not found');
}

void msalInstance.handleRedirectPromise().catch(error => {
    // Surface redirect issues during development without breaking rendering.
    console.error('MSAL redirect handling failed', error);
});

createRoot(rootElement).render(
    <StrictMode>
        <HelmetProvider>
            <MsalProvider instance={msalInstance}>
                <AuthProvider>
                    <App />
                </AuthProvider>
            </MsalProvider>
        </HelmetProvider>
    </StrictMode>
);

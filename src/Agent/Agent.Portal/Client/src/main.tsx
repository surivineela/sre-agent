import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { HelmetProvider } from 'react-helmet-async';
import App from './App';
import { AuthProvider } from './Common/Contexts/AuthContext';
import { UserPreferencesProvider } from './Common/Contexts/UserPreferencesContext';
import './index.css';

const rootElement = document.getElementById('root');

if (!rootElement) {
    throw new Error('Root element not found');
}

createRoot(rootElement).render(
    <StrictMode>
        <HelmetProvider>
            <AuthProvider>
                <UserPreferencesProvider>
                    <App />
                </UserPreferencesProvider>
            </AuthProvider>
        </HelmetProvider>
    </StrictMode>
);

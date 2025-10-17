import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { HelmetProvider } from 'react-helmet-async';
import App from './App';
import './index.css';
import { AuthProvider } from './src/Common/Contexts/AuthContext';

const rootElement = document.getElementById('root');

if (!rootElement) {
    throw new Error('Root element not found');
}

createRoot(rootElement).render(
    <StrictMode>
        <HelmetProvider>
            <AuthProvider>
                <App />
            </AuthProvider>
        </HelmetProvider>
    </StrictMode>
);

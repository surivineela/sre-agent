import { Configuration, LogLevel, PublicClientApplication, RedirectRequest } from '@azure/msal-browser';

const MSAL_CLIENT_ID = import.meta.env.VITE_MSAL_CLIENT_ID;
// Multi-tenant authority - allows users from any tenant to sign in
const MSAL_AUTHORITY = import.meta.env.VITE_MSAL_AUTHORITY || 'https://login.microsoftonline.com/organizations';

export const msalConfig: Configuration = {
    auth: {
        clientId: MSAL_CLIENT_ID,
        authority: MSAL_AUTHORITY,
        redirectUri: window.location.origin + '/auth/callback',
    },
    cache: {
        cacheLocation: 'localStorage',
        storeAuthStateInCookie: false,
    },
    system: {
        loggerOptions: {
            loggerCallback: (level, message, containsPii) => {
                if (containsPii) return;
                const levelName = LogLevel[level] || level;
                console.log(`[MSAL ${levelName}]: ${message}`);
            },
            logLevel: LogLevel.Warning, // Change to LogLevel.Verbose for more detailed logs
        },
    },
};

export const loginRequest: RedirectRequest = {
    prompt: 'select_account',
    scopes: ['User.Read'],
};

/** No need to call `msalInstance.initialize()` as `MsalProvider` does hit under-the-hood */
export const msalInstance = new PublicClientApplication(msalConfig);

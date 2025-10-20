import { Configuration, PublicClientApplication, RedirectRequest } from '@azure/msal-browser';

// TODO: Actually hook up and test. Will tenant/client/etc be SRE Agent 1P app,
// or will one/all of these switch based on the user's selected directory/tenant?

const MSAL_CLIENT_ID = 'your-client-id-here';
const MSAL_TENANT_ID = 'your-tenant-id-here';
const MSAL_AUTHORITY = `https://login.microsoftonline.com/${MSAL_TENANT_ID}`;

export const msalConfig: Configuration = {
    auth: {
        clientId: MSAL_CLIENT_ID,
        authority: MSAL_AUTHORITY,
        // Shouldn't need redirectUri for the time being - just land back on the homepage
        // redirectUri: window.location.origin,
    },
    cache: {
        cacheLocation: 'sessionStorage',
        storeAuthStateInCookie: false,
    },
};

export const loginRequest: RedirectRequest = {
    scopes: ['openid', 'profile', 'email'],
};

export const msalInstance = new PublicClientApplication(msalConfig);

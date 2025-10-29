import { AuthScopeIdentifier } from '../Hooks/useAuthTokenManager';

export type CloudEnvironment = 'public' | 'fairfax' | 'mooncake';

interface CloudEndpoints {
    arm: string;
    graph: string;
    sreAgent: string;
    appInsights: string;
    portal: string;
}

const cloudEndpoints: Record<CloudEnvironment, CloudEndpoints> = {
    public: {
        arm: 'https://management.azure.com',
        graph: 'https://graph.microsoft.com',
        sreAgent: 'https://azuresre.dev',
        appInsights: 'https://api.applicationinsights.io',
        portal: 'https://portal.azure.com',
    },
    fairfax: {
        arm: 'https://management.usgovcloudapi.net',
        graph: 'https://graph.microsoft.us',
        sreAgent: 'https://azuresre.dev', // TODO: Update when gov cloud endpoint available
        appInsights: 'https://api.applicationinsights.us',
        portal: 'https://portal.azure.us',
    },
    mooncake: {
        arm: 'https://management.chinacloudapi.cn',
        graph: 'https://microsoftgraph.chinacloudapi.cn',
        sreAgent: 'https://azuresre.dev', // TODO: Update when china endpoint available
        appInsights: 'https://api.applicationinsights.azure.cn',
        portal: 'https://portal.azure.cn',
    },
};

/**
 * Detects the current cloud environment based on the hostname
 * Can be overridden with VITE_AZURE_CLOUD environment variable
 */
export const detectCloudEnvironment = (): CloudEnvironment => {
    // Check environment variable first
    const envCloud = import.meta.env.VITE_AZURE_CLOUD as CloudEnvironment | undefined;
    if (envCloud && cloudEndpoints[envCloud]) {
        return envCloud;
    }

    // Detect from hostname
    const hostname = window.location.hostname.toLowerCase();

    if (hostname.endsWith('.azure.us')) {
        return 'fairfax';
    }

    if (hostname.endsWith('.azure.cn')) {
        return 'mooncake';
    }

    return 'public';
};

/**
 * Get the cloud-specific endpoints for the current environment
 */
export const getCloudEndpoints = (): CloudEndpoints => {
    const cloud = detectCloudEnvironment();
    return cloudEndpoints[cloud];
};

export const getCloudScopes = () => {
    const endpoints = getCloudEndpoints();

    return {
        graph: [`${endpoints.graph}/.default`],
        arm: [`${endpoints.arm}/.default`],
        sreAgent: [`${endpoints.sreAgent}/.default`],
        appInsights: [`${endpoints.appInsights}/.default`],
    } as const satisfies Record<AuthScopeIdentifier, readonly string[]>;
};

/**
 * Get scopes array for a specific API by identifier
 * @param identifier - The API identifier ('arm', 'graph', etc.)
 * @returns Array of OAuth scopes for that API
 */
export const getScopesForApi = (identifier: AuthScopeIdentifier): string[] => {
    const scopes = getCloudScopes();
    return [...scopes[identifier]];
};

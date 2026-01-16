import ApplicationInsightsIcon from '../../assets/ApplicationInsights.svg?url';
import DeploymentIcon from '../../assets/Deployment.svg?url';
import LogAnalyticsWorkspaceIcon from '../../assets/LogAnalyticsWorkspace.svg?url';
import ManagedIdentityIcon from '../../assets/ManagedIdentity.svg?url';
import SreAgentIcon from '../../assets/SreAgent.svg?url';
import SreAgentSpaceIcon from '../../assets/SreAgentSpace.svg?url';

const RESOURCE_ICONS: Record<string, string> = {
    'microsoft.resources/deployments': DeploymentIcon,
    agents: SreAgentIcon,
    agentspaces: SreAgentSpaceIcon,
    'microsoft.insights/components': ApplicationInsightsIcon,
    userassignedidentities: ManagedIdentityIcon,
    'microsoft.operationalinsights/workspaces': LogAnalyticsWorkspaceIcon,
};

const FRIENDLY_NAMES: Record<string, string> = {
    'microsoft.resources/deployments': 'Deployment',
    agents: 'Azure SRE Agent',
    agentspaces: 'Azure SRE Agent Space',
    'microsoft.insights/components': 'Application Insights',
    userassignedidentities: 'Managed Identity',
    'microsoft.operationalinsights/workspaces': 'Log Analytics Workspace',
};

const DEFAULT_ICON = DeploymentIcon;

/**
 * Resolves the icon path for a given Azure resource type
 * @param resourceType The Azure resource type (e.g., "Microsoft.App/containerApps")
 * @returns The icon path that can be used as an img src
 */
export const resolveResourceIcon = (resourceType?: string): string => {
    if (!resourceType) return DEFAULT_ICON;

    const normalizedType = resourceType.toLowerCase();

    // Sort keys by length (longest first) to match more specific types first
    const sortedKeys = Object.keys(RESOURCE_ICONS).sort((a, b) => b.length - a.length);
    const match = sortedKeys.find(key => normalizedType.includes(key));
    return match ? RESOURCE_ICONS[match] : DEFAULT_ICON;
};

/**
 * Gets the friendly display name for a given Azure resource type
 * @param resourceType The Azure resource type (e.g., "Microsoft.App/containerApps")
 * @returns The friendly name (e.g., "Azure SRE Agent") or the last segment of the resource type as fallback
 */
export const getResourceTypeFriendlyName = (resourceType?: string): string => {
    if (!resourceType) return 'Resource';

    const normalizedType = resourceType.toLowerCase();

    // Try to find a match in the friendly names lookup
    // Sort keys by length (longest first) to match more specific types first
    const sortedKeys = Object.keys(FRIENDLY_NAMES).sort((a, b) => b.length - a.length);
    const match = sortedKeys.find(key => normalizedType.includes(key));

    if (match) {
        return FRIENDLY_NAMES[match];
    }

    // Fallback: extract the last segment of the resource type
    const segments = resourceType.split('/');
    return segments[segments.length - 1];
};

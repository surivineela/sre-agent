/**
 * Utilities for parsing ARM resource IDs from URL paths and constructing resource-based URLs.
 *
 * This module supports "resource ID-based routing" where ARM resource IDs become part of the URL path
 * instead of being URL-encoded as a single parameter.
 *
 * Example URL: /agents/subscriptions/000/resourceGroups/rg/providers/Microsoft.App/agents/my-agent/views/thread/t-1
 *              └─────────────────────────────────────────────────────────────────────────────────┘└───────────────┘
 *                                              ARM Resource ID                                      Deep Link
 */

/**
 * Result of parsing a resource route from a URL path.
 */
export interface ResourceRoute {
    /** The full ARM resource ID (e.g., /subscriptions/.../providers/.../my-agent) */
    resourceId: string;
    /** Optional path segment after the resource ID (e.g., views/thread/t-123) */
    deepLink?: string;
}

/**
 * Regular expression to match ARM resource IDs.
 *
 * ARM resource IDs follow the pattern:
 * /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{namespace}/{resourceType}/{resourceName}
 *
 * This pattern captures:
 * - /subscriptions/{guid}
 * - /resourceGroups/{name}
 * - /providers/{namespace}/{type}/{name} (and optionally nested resources like /{childType}/{childName})
 */
const ARM_RESOURCE_ID_PATTERN = /^(\/subscriptions\/[^/]+\/resourceGroups\/[^/]+\/providers\/[^/]+\/[^/]+\/[^/]+)/;

/**
 * Parses a URL pathname to extract the ARM resource ID and optional deep link.
 *
 * @param pathname - The full URL pathname (e.g., /agents/subscriptions/.../my-agent/views/thread/t-1)
 * @param routePrefix - The route prefix to strip (e.g., "/agents" or "/spaces")
 * @returns The parsed resource route, or null if the path doesn't contain a valid ARM resource ID
 *
 * @example
 * parseResourceRoute('/agents/subscriptions/000/resourceGroups/rg/providers/Microsoft.App/agents/my-agent/views/thread/t-1', '/agents')
 * // Returns: { resourceId: '/subscriptions/000/resourceGroups/rg/providers/Microsoft.App/agents/my-agent', deepLink: 'views/thread/t-1' }
 */
export const parseResourceRoute = (pathname: string, routePrefix: string): ResourceRoute | null => {
    // Normalize the route prefix (ensure it starts with / and doesn't end with /)
    const normalizedPrefix = routePrefix.startsWith('/') ? routePrefix : `/${routePrefix}`;

    // Check if pathname starts with the route prefix
    if (!pathname.startsWith(normalizedPrefix)) {
        return null;
    }

    // Extract the path after the prefix
    const pathAfterPrefix = pathname.slice(normalizedPrefix.length);

    // Match the ARM resource ID pattern
    const match = pathAfterPrefix.match(ARM_RESOURCE_ID_PATTERN);

    if (!match) {
        return null;
    }

    const resourceId = match[1];
    const remainingPath = pathAfterPrefix.slice(resourceId.length);

    // Extract deep link (remove leading slashes)
    const deepLink = remainingPath.replace(/^\/+/, '') || undefined;

    return {
        resourceId,
        deepLink,
    };
};

/**
 * Constructs a URL path for a resource-based route.
 *
 * @param routePrefix - The route prefix (e.g., "/agents" or "/spaces")
 * @param resourceId - The ARM resource ID (must start with /)
 * @param deepLink - Optional deep link path to append
 * @returns The constructed URL path
 *
 * @example
 * buildResourcePath('/agents', '/subscriptions/000/resourceGroups/rg/providers/Microsoft.App/agents/my-agent')
 * // Returns: '/agents/subscriptions/000/resourceGroups/rg/providers/Microsoft.App/agents/my-agent'
 *
 * buildResourcePath('/agents', '/subscriptions/.../my-agent', 'views/thread/t-1')
 * // Returns: '/agents/subscriptions/.../my-agent/views/thread/t-1'
 */
export const buildResourcePath = (routePrefix: string, resourceId: string, deepLink?: string): string => {
    // Normalize the route prefix (ensure it starts with / and doesn't end with /)
    const normalizedPrefix = routePrefix.startsWith('/') ? routePrefix : `/${routePrefix}`;

    // Resource ID should start with /, so we can concatenate directly
    // If it doesn't start with /, add one
    const normalizedResourceId = resourceId.startsWith('/') ? resourceId : `/${resourceId}`;

    let path = `${normalizedPrefix}${normalizedResourceId}`;

    if (deepLink) {
        // Ensure deep link doesn't start with /
        const normalizedDeepLink = deepLink.startsWith('/') ? deepLink.slice(1) : deepLink;
        path = `${path}/${normalizedDeepLink}`;
    }

    return path;
};

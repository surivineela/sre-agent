import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ResourceClient } from '../Clients/ResourceClient';
import { TelemetrySource } from '../Constants/Telemetry';
import { parseArmId } from '../Utilities/ArmId';

interface UseResourceApiVersionsResult {
    apiVersions: string[];
    latestVersion: string | null;
    isLoading: boolean;
    error: string | null;
}

// Cache for provider API versions to avoid repeated calls
const apiVersionsCache = new Map<string, string[]>();

/**
 * Hook to fetch available API versions for an ARM resource type.
 * Uses ResourceClient.getProvider() to get the provider manifest and extracts
 * the apiVersions for the specific resource type.
 *
 * @param resourceId - The full ARM resource ID
 * @param telemetrySource - Telemetry source for logging
 * @returns API versions sorted newest first, loading state, and error
 */
export const useResourceApiVersions = (resourceId: string, telemetrySource: TelemetrySource): UseResourceApiVersionsResult => {
    const [apiVersions, setApiVersions] = useState<string[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const callIdRef = useRef(0);

    const parsedId = useMemo(() => parseArmId(resourceId), [resourceId]);

    const fetchApiVersions = useCallback(async () => {
        if (!parsedId.subscription || !parsedId.provider || !parsedId.resourceType) {
            setApiVersions([]);
            setIsLoading(false);
            setError('Invalid resource ID: missing subscription, provider, or resource type');
            return;
        }

        // Check cache first
        const cacheKey = `${parsedId.provider}/${parsedId.resourceType}`;
        const cached = apiVersionsCache.get(cacheKey);
        if (cached) {
            setApiVersions(cached);
            setIsLoading(false);
            setError(null);
            return;
        }

        const currentCallId = ++callIdRef.current;
        setIsLoading(true);
        setError(null);

        const resourceClient = ResourceClient.getInstance(telemetrySource);
        const response = await resourceClient.getProvider(parsedId.subscription, parsedId.provider);

        // Check if this is still the latest call
        if (currentCallId !== callIdRef.current) {
            return;
        }

        if (!response.isSuccessful || !response.content) {
            setError(response.error ?? 'Failed to fetch provider information');
            setApiVersions([]);
            setIsLoading(false);
            return;
        }

        // Extract the resource type (without the provider prefix) for matching
        // e.g., "Microsoft.App/containerApps" -> "containerApps"
        const resourceTypeParts = parsedId.resourceType.split('/');
        const resourceTypeWithoutProvider = resourceTypeParts.slice(1).join('/');

        const matchingResourceType = response.content.resourceTypes.find(
            rt => rt.resourceType.toLowerCase() === resourceTypeWithoutProvider.toLowerCase()
        );

        if (!matchingResourceType) {
            setError(`Resource type "${parsedId.resourceType}" not found in provider manifest`);
            setApiVersions([]);
            setIsLoading(false);
            return;
        }

        // Sort versions newest first (lexicographic sort works for YYYY-MM-DD format)
        const sortedVersions = [...matchingResourceType.apiVersions].sort((a, b) => b.localeCompare(a));

        // Cache the result
        apiVersionsCache.set(cacheKey, sortedVersions);

        setApiVersions(sortedVersions);
        setIsLoading(false);
        setError(null);
    }, [parsedId.subscription, parsedId.provider, parsedId.resourceType, telemetrySource]);

    useEffect(() => {
        fetchApiVersions();
    }, [fetchApiVersions]);

    const latestVersion = useMemo(() => (apiVersions.length > 0 ? apiVersions[0] : null), [apiVersions]);

    return {
        apiVersions,
        latestVersion,
        isLoading,
        error,
    };
};

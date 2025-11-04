import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';
import { ResourceGroupClient } from '../../Clients/ResourceGroupClient';
import { TelemetrySource } from '../../Constants/Telemetry';
import { useSubscriptions } from '../../Contexts/SubscriptionsContext';
import { getArmErrorMessage } from '../../Utilities/Client';
import { LabelKeyPair } from '../PillFilter/ListWithSearch';

export interface UseResourceGroupPillProps {
    selectedSubscriptionIds: string[];
    selectedResourceGroupNames: string[];
    onSelectedResourceGroupNamesChange: (names: string[]) => void;
    disabled?: boolean;
}

export interface UseResourceGroupPillResult {
    options: LabelKeyPair[];
    selectedKeys: string[];
    onApply: (keys: string[]) => void;
    displayValue: string;
    isLoading: boolean;
    error: string | null;
    onSearchChange: (text: string) => void;
}

export const useResourceGroupPill = ({
    selectedSubscriptionIds,
    selectedResourceGroupNames,
    onSelectedResourceGroupNamesChange,
}: UseResourceGroupPillProps): UseResourceGroupPillResult => {
    const intl = useIntl();
    const { subscriptions } = useSubscriptions();

    const [resourceGroups, setResourceGroups] = useState<Array<{ name: string; subscriptionId: string }>>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [searchText, setSearchText] = useState<string>('');
    const [debouncedSearchText, setDebouncedSearchText] = useState<string>('');

    const previousSubscriptionIdsRef = useRef<string[]>([]);

    const resourceGroupClient = useMemo(() => ResourceGroupClient.getInstance(TelemetrySource.HomeBrowseView), []);

    // When selectedSubscriptionIds is empty, use all available subscriptions
    const subscriptionIdsToQuery = useMemo(() => {
        return selectedSubscriptionIds.length > 0 ? selectedSubscriptionIds : subscriptions.map(sub => sub.subscriptionId);
    }, [selectedSubscriptionIds, subscriptions]);

    // Serialize subscription IDs for stable comparison
    const subscriptionIdsKey = useMemo(() => subscriptionIdsToQuery.slice().sort().join(','), [subscriptionIdsToQuery]);

    useEffect(() => {
        const timer = setTimeout(() => {
            setDebouncedSearchText(searchText);
        }, 300);
        return () => clearTimeout(timer);
    }, [searchText]);

    // Fetch resource groups when subscriptions or search text change
    useEffect(() => {
        const fetchResourceGroups = async () => {
            const previousKey = previousSubscriptionIdsRef.current.slice().sort().join(',');
            const subscriptionsChanged = subscriptionIdsKey !== previousKey;

            // Reset selected resource groups when subscriptions change
            if (subscriptionsChanged && previousSubscriptionIdsRef.current.length > 0) {
                onSelectedResourceGroupNamesChange([]);
            }

            previousSubscriptionIdsRef.current = subscriptionIdsToQuery;

            if (subscriptionIdsToQuery.length === 0) {
                setResourceGroups([]);
                setIsLoading(false);
                setError(null);
                return;
            }

            setIsLoading(true);
            setError(null);

            const response = await resourceGroupClient.getAllResourceGroupsFromSubscriptions(subscriptionIdsToQuery, debouncedSearchText);

            if (response.isSuccessful && response.content) {
                const rgs = response.content.map(rg => ({
                    name: rg.name,
                    subscriptionId: rg.id.split('/')[2], // Extract subscription ID from resource group ID
                }));
                setResourceGroups(rgs);
            } else {
                setError(getArmErrorMessage(response.error));
                setResourceGroups([]);
            }

            setIsLoading(false);
        };

        fetchResourceGroups();
    }, [subscriptionIdsKey, subscriptionIdsToQuery, debouncedSearchText, resourceGroupClient, onSelectedResourceGroupNamesChange]);

    // Format resource groups as LabelKeyPair options
    const options = useMemo<LabelKeyPair[]>(() => {
        // Get unique resource group names
        const uniqueNames = Array.from(new Set(resourceGroups.map(rg => rg.name)));

        return uniqueNames
            .map(name => ({
                key: name,
                label: name,
            }))
            .sort((a, b) => a.label.localeCompare(b.label));
    }, [resourceGroups]);

    // Handle apply action - convert "All" selection to empty array
    const onApply = useCallback(
        (keys: string[]) => {
            // Empty array from ListWithSearch means "All" was explicitly selected (no search active)
            // Only convert to empty array if there's no active search term
            if (keys.length === 0 && !searchText.trim()) {
                // No search active - "All" means truly all resource groups
                onSelectedResourceGroupNamesChange([]);
            } else if (keys.length === options.length && options.length > 0 && !searchText.trim()) {
                // All available options selected and no search - treat as "All"
                onSelectedResourceGroupNamesChange([]);
            } else {
                // Partial selection or search active - store specific resource group names
                onSelectedResourceGroupNamesChange(keys);
            }
        },
        [onSelectedResourceGroupNamesChange, options.length, searchText]
    );

    const onSearchChange = useCallback((text: string) => {
        setSearchText(text);
    }, []);

    // Calculate display value: "All" if none selected, single name if one, or just the count
    const displayValue = useMemo(() => {
        if (selectedResourceGroupNames.length === 0) {
            return intl.formatMessage(PortalResources.allResourceGroups);
        }

        if (selectedResourceGroupNames.length === 1) {
            return selectedResourceGroupNames[0];
        }

        return selectedResourceGroupNames.length.toString();
    }, [selectedResourceGroupNames, intl]);

    // When empty array (meaning "All"), show all available options as selected in the UI
    // This makes the "All" checkbox appear checked
    const selectedKeysForUI = useMemo(() => {
        if (selectedResourceGroupNames.length === 0) {
            return options.map(opt => opt.key);
        }
        return selectedResourceGroupNames;
    }, [selectedResourceGroupNames, options]);

    return {
        options,
        selectedKeys: selectedKeysForUI,
        onApply,
        displayValue,
        isLoading,
        error,
        onSearchChange,
    };
};

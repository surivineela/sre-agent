import { useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';
import { useSubscriptions } from '../../Contexts/SubscriptionsContext';
import { LabelKeyPair } from '../PillFilter/ListWithSearch';

export interface UseSubscriptionPillProps {
    selectedSubscriptionIds: string[];
    onSelectedSubscriptionIdsChange: (ids: string[]) => void;
    disabled?: boolean;
}

export interface UseSubscriptionPillResult {
    options: LabelKeyPair[];
    selectedKeys: string[];
    onApply: (keys: string[]) => void;
    displayValue: string;
    isLoading: boolean;
    error: string | null;
}

export const useSubscriptionPill = ({
    selectedSubscriptionIds,
    onSelectedSubscriptionIdsChange,
}: UseSubscriptionPillProps): UseSubscriptionPillResult => {
    const intl = useIntl();
    const { subscriptions, isLoading, error } = useSubscriptions();

    // Format subscriptions as LabelKeyPair options
    const options = useMemo<LabelKeyPair[]>(() => {
        return subscriptions
            .map(sub => ({
                key: sub.subscriptionId,
                label: sub.displayName || sub.subscriptionId,
            }))
            .sort((a, b) => a.label.localeCompare(b.label));
    }, [subscriptions]);

    // Handle apply action - convert "All" selection to empty array
    const onApply = useCallback(
        (keys: string[]) => {
            // Empty array from ListWithSearch means "All" was explicitly selected
            // Or if all available options are selected, treat as "All" (empty array signifies all subscriptions)
            if (keys.length === 0 || (keys.length === options.length && options.length > 0)) {
                onSelectedSubscriptionIdsChange([]);
            } else {
                // Partial selection - store specific subscription IDs
                onSelectedSubscriptionIdsChange(keys);
            }
        },
        [onSelectedSubscriptionIdsChange, options.length]
    );

    // Calculate display value: "All" if none or all selected, single name if one, or just the count
    const displayValue = useMemo(() => {
        if (selectedSubscriptionIds.length === 0 || selectedSubscriptionIds.length === subscriptions.length) {
            return intl.formatMessage(PortalResources.allSubscriptions);
        }

        if (selectedSubscriptionIds.length === 1) {
            const sub = subscriptions.find(s => s.subscriptionId === selectedSubscriptionIds[0]);
            return sub?.displayName || selectedSubscriptionIds[0];
        }

        return selectedSubscriptionIds.length.toString();
    }, [selectedSubscriptionIds, subscriptions, intl]);

    // When empty array (meaning "All"), show all available options as selected in the UI
    // This makes the "All" checkbox appear checked
    const selectedKeysForUI = useMemo(() => {
        if (selectedSubscriptionIds.length === 0) {
            return options.map(opt => opt.key);
        }
        return selectedSubscriptionIds;
    }, [selectedSubscriptionIds, options]);

    return {
        options,
        selectedKeys: selectedKeysForUI,
        onApply,
        displayValue,
        isLoading,
        error,
    };
};

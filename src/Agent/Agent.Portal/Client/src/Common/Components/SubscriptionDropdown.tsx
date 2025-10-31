import {
    Dropdown,
    DropdownProps,
    Field,
    Input,
    makeStyles,
    mergeClasses,
    OnOpenChangeData,
    OpenPopoverEvents,
    Option,
    OptionOnSelectData,
    SelectionEvents,
    Skeleton,
    SkeletonItem,
} from '@fluentui/react-components';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';
import { useSubscriptions } from '../Contexts/SubscriptionsContext';
import { Subscription } from '../Contracts/Arm';

// TODO: If we filter subscriptions or only show selected or something, show a hint on the Field indicating such

type SubscriptionDropdownProps = {
    /**
     * Callback on subscription change.
     * If selectedSubscriptionId is specified and invalid, this callback will be called with an empty string and null.
     *
     * @param subscription - Subscription object with all available properties, including subscriptionId;
     */
    readonly onSubscriptionChange: (subscription?: Subscription) => void;
    /**
     * The subscription id to be set as a value for the dropdown if it's a member of the values fetched from ARM.
     */
    readonly selectedSubscriptionId: string | undefined;
    readonly 'aria-label'?: string;
    readonly 'aria-labelledby'?: string;
    readonly 'aria-required'?: boolean;
    readonly className?: string;
    disabled?: boolean;
};

const useStyles = makeStyles({
    combobox: {
        minWidth: '250px',
    },
    filterInput: {
        marginBottom: '8px',
    },
});

export const SubscriptionDropdown = (props: SubscriptionDropdownProps) => {
    const {
        'aria-label': ariaLabel,
        'aria-labelledby': ariaLabelledBy,
        'aria-required': ariaRequired,
        className,
        onSubscriptionChange,
        selectedSubscriptionId,
        disabled,
    } = props;

    const intl = useIntl();

    // TODO: bypassDefaultFilter: true ??
    const { subscriptions, selectedSubscriptions, error, isLoading } = useSubscriptions();

    const styles = useStyles();
    const [filterValue, setFilterValue] = useState('');

    const errorMessage = error ? `${intl.formatMessage(PortalResources.requestError)}: ${error}` : undefined;

    const selectedSubscription = useMemo(
        () => subscriptions.find(subscription => subscription.subscriptionId === selectedSubscriptionId),
        [subscriptions, selectedSubscriptionId]
    );

    // Filter subscriptions based on filter input
    const filteredSubscriptions = useMemo(() => {
        if (!filterValue) {
            return subscriptions;
        }
        const lowerFilter = filterValue.toLowerCase();
        return subscriptions.filter(sub => sub.displayName.toLowerCase().includes(lowerFilter));
    }, [subscriptions, filterValue]);

    const onOptionSelect = useCallback<NonNullable<DropdownProps['onOptionSelect']>>(
        (_event: SelectionEvents, data: OptionOnSelectData) => {
            const subscriptionId = data.optionValue;
            const subscription = subscriptions.find(sub => sub.subscriptionId === subscriptionId);
            onSubscriptionChange(subscription);
        },
        [subscriptions, onSubscriptionChange]
    );

    const handleOpenChange = useCallback<NonNullable<DropdownProps['onOpenChange']>>(
        (_event: OpenPopoverEvents, data: OnOpenChangeData) => {
            // Clear filter when dropdown closes
            if (!data.open) {
                setFilterValue('');
            }
        },
        []
    );

    // Auto-select first subscription on load if none is selected
    useEffect(() => {
        if (!isLoading && subscriptions.length > 0 && !selectedSubscriptionId) {
            const subscriptionToSelect = selectedSubscriptions[0] ?? subscriptions[0];
            if (subscriptionToSelect) {
                onSubscriptionChange(subscriptionToSelect);
            }
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [isLoading, subscriptions.length, selectedSubscriptionId]);

    const renderOption = useCallback((subscription: Subscription) => {
        return (
            <Option
                aria-label={`${subscription.displayName}: ${subscription.subscriptionId}`}
                disabled={subscription.state?.toLowerCase() !== 'enabled'}
                key={subscription.subscriptionId}
                text={subscription.displayName}
                value={subscription.subscriptionId}
            >
                {subscription.displayName}
            </Option>
        );
    }, []);

    return (
        <Field
            label={intl.formatMessage(PortalResources.subscription)}
            validationMessage={errorMessage}
            validationState={errorMessage ? 'error' : undefined}
        >
            {isLoading ? (
                <Skeleton aria-label={intl.formatMessage(PortalResources.loading)}>
                    <SkeletonItem size={32} />
                </Skeleton>
            ) : (
                <Dropdown
                    aria-label={ariaLabel || intl.formatMessage(PortalResources.subscription)}
                    aria-labelledby={ariaLabelledBy}
                    aria-required={ariaRequired}
                    className={mergeClasses(styles.combobox, className)}
                    onOptionSelect={onOptionSelect}
                    onOpenChange={handleOpenChange}
                    placeholder={intl.formatMessage(PortalResources.selectASubscription)}
                    value={selectedSubscription?.displayName ?? ''}
                    disabled={disabled}
                >
                    <Input
                        className={styles.filterInput}
                        placeholder={intl.formatMessage(PortalResources.filterItems)}
                        value={filterValue}
                        onChange={(_e, data) => setFilterValue(data.value)}
                    />
                    {filteredSubscriptions.length === 0 ? (
                        <Option disabled key="no-results" text={intl.formatMessage(PortalResources.noResultsFound)}>
                            {intl.formatMessage(PortalResources.noResultsFound)}
                        </Option>
                    ) : (
                        filteredSubscriptions.map(subscription => renderOption(subscription))
                    )}
                </Dropdown>
            )}
        </Field>
    );
};

SubscriptionDropdown.displayName = 'SubscriptionDropdown';

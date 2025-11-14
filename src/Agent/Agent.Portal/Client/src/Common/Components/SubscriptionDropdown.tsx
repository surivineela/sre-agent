import {
    Combobox,
    ComboboxProps,
    Field,
    makeStyles,
    mergeClasses,
    OptionOnSelectData,
    SelectionEvents,
    Skeleton,
    SkeletonItem,
    useComboboxFilter,
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

    const { subscriptions, selectedSubscriptions, error, isLoading } = useSubscriptions();

    const styles = useStyles();
    const [query, setQuery] = useState('');
    const [hasUserChanged, setHasUserChanged] = useState(false);

    const errorMessage = error ? `${intl.formatMessage(PortalResources.requestError)}: ${error}` : undefined;

    const subscriptionOptions = useMemo(
        () =>
            subscriptions.map(subscription => ({
                children: subscription.displayName,
                value: subscription.subscriptionId,
                disabled: subscription.state?.toLowerCase() !== 'enabled',
            })),
        [subscriptions]
    );

    const children = useComboboxFilter(hasUserChanged ? query : '', subscriptionOptions, {
        optionToText: option => option.children as string,
        noOptionsMessage: intl.formatMessage(PortalResources.noResultsFound),
    });

    const onOptionSelect = useCallback<NonNullable<ComboboxProps['onOptionSelect']>>(
        (_event: SelectionEvents, data: OptionOnSelectData) => {
            const subscriptionId = data.optionValue;
            const subscription = subscriptions.find(sub => sub.subscriptionId === subscriptionId);
            onSubscriptionChange(subscription);
            setQuery(data.optionText ?? '');
        },
        [subscriptions, onSubscriptionChange]
    );

    // Auto-select first subscription on load if none is selected
    useEffect(() => {
        if (!isLoading && subscriptions.length > 0 && !selectedSubscriptionId) {
            const subscriptionToSelect = selectedSubscriptions[0] ?? subscriptions[0];
            if (subscriptionToSelect) {
                onSubscriptionChange(subscriptionToSelect);
                setQuery(subscriptionToSelect.displayName);
            }
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [isLoading, subscriptions.length, selectedSubscriptionId]);

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
                <Combobox
                    aria-label={ariaLabel || intl.formatMessage(PortalResources.subscription)}
                    aria-labelledby={ariaLabelledBy}
                    aria-required={ariaRequired}
                    className={mergeClasses(styles.combobox, className)}
                    onOptionSelect={onOptionSelect}
                    placeholder={intl.formatMessage(PortalResources.selectASubscription)}
                    value={query}
                    onChange={ev => {
                        setQuery(ev.target.value);
                        setHasUserChanged(true);
                    }}
                    disabled={disabled}
                >
                    {children}
                </Combobox>
            )}
        </Field>
    );
};

SubscriptionDropdown.displayName = 'SubscriptionDropdown';

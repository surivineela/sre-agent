import { FC } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';
import { PillFilter } from '../PillFilter/PillFilter';
import { useSubscriptionPill, UseSubscriptionPillProps } from './useSubscriptionPill';

export interface SubscriptionPillProps extends UseSubscriptionPillProps {
    className?: string;
}

export const SubscriptionPill: FC<SubscriptionPillProps> = ({
    selectedSubscriptionIds,
    onSelectedSubscriptionIdsChange,
    disabled = false,
}) => {
    const intl = useIntl();
    const { options, selectedKeys, onApply, displayValue, isLoading } = useSubscriptionPill({
        selectedSubscriptionIds,
        onSelectedSubscriptionIdsChange,
        disabled,
    });

    if (isLoading) {
        return null;
    }

    return (
        <PillFilter
            filterType="combobox"
            label={intl.formatMessage(PortalResources.subscription)}
            options={options}
            selectedKeys={selectedKeys}
            onApply={onApply}
            multiSelect
            addAllOption
            allOptionLabel={intl.formatMessage(PortalResources.allSubscriptions)}
            disabled={disabled}
            displayValue={displayValue}
        />
    );
};

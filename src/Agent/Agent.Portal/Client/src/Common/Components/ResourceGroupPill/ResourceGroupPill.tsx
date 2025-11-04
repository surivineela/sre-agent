import { FC } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';
import { PillFilter } from '../PillFilter/PillFilter';
import { useResourceGroupPill, UseResourceGroupPillProps } from './useResourceGroupPill';

export interface ResourceGroupPillProps extends UseResourceGroupPillProps {
    className?: string;
}

export const ResourceGroupPill: FC<ResourceGroupPillProps> = ({
    selectedSubscriptionIds,
    selectedResourceGroupNames,
    onSelectedResourceGroupNamesChange,
    disabled = false,
}) => {
    const intl = useIntl();
    const { options, selectedKeys, onApply, displayValue, isLoading, onSearchChange } = useResourceGroupPill({
        selectedSubscriptionIds,
        selectedResourceGroupNames,
        onSelectedResourceGroupNamesChange,
        disabled,
    });

    const isDisabled = disabled || isLoading || options.length === 0;

    return (
        <PillFilter
            filterType="combobox"
            label={intl.formatMessage(PortalResources.resourceGroup)}
            options={options}
            selectedKeys={selectedKeys}
            onApply={onApply}
            multiSelect
            addAllOption
            allOptionLabel={intl.formatMessage(PortalResources.allResourceGroups)}
            disabled={isDisabled}
            displayValue={displayValue}
            onSearchChange={onSearchChange}
        />
    );
};

import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../../Strings/Resources';
import { FilterProps, RemovableFilterProps, UseComboboxPillFilterProps } from '../Contracts';
import { useComboboxPillFilter } from './useComboboxPillFilter';

export function splitUpProps(props?: RemovableFilterProps | FilterProps) {
    if (!props) {
        return {
            commonProps: undefined,
            comboboxFilterProps: undefined,
        };
    }

    const propsCopy = { ...props, onRemove: (props as any).onRemove };
    const { filterType, label, labelDelimiter, displayValue, onRemove, disabled, valueMaxWidth, ...rest } = propsCopy;

    const commonProps = {
        filterType,
        label,
        labelDelimiter: labelDelimiter === undefined ? ':' : labelDelimiter,
        displayValue,
        onRemove,
        disabled,
        valueMaxWidth,
    };

    const comboboxFilterProps = (filterType === 'combobox' ? { ...rest, label } : undefined) as UseComboboxPillFilterProps | undefined;

    return { commonProps, comboboxFilterProps };
}

export function usePillFilter(props?: FilterProps | RemovableFilterProps) {
    const intl = useIntl();

    const { commonProps, comboboxFilterProps } = useMemo(() => splitUpProps(props), [props]);

    const comboboxPillFilterHook = useComboboxPillFilter(comboboxFilterProps);
    const filterHook = useMemo(
        () => (commonProps?.filterType === 'combobox' ? comboboxPillFilterHook : undefined),
        [commonProps?.filterType, comboboxPillFilterHook]
    );

    return commonProps && filterHook
        ? {
              ...commonProps,
              ...filterHook,
              ariaLabel: intl.formatMessage(PortalResources.pillFilterAriaLabel, {
                  columnName: commonProps.label,
                  delimiter: commonProps.labelDelimiter ? ` ${commonProps.labelDelimiter} ` : '',
                  filterValue: filterHook?.pillDisplayValue,
              }),
              removeButtonAriaLabel: intl.formatMessage(PortalResources.pillFilterRemoveAriaLabel, { columnName: commonProps.label }),
          }
        : undefined;
}

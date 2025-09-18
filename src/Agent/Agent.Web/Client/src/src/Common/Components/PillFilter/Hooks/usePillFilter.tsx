import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../../../Strings/SREAgentResources';
import { FilterProps, RemovableFilterProps, UseComboboxPillFilterProps, UseTimeRangePillFilterProps } from '../Contracts';
import { useComboboxPillFilter } from './useComboboxPillFilter';
import { useTimeRangePillFilter } from './useTimeRangePillFilter';

export function splitUpProps(props?: RemovableFilterProps | FilterProps) {
    if (!props) {
        return {
            commonProps: undefined,
            comboboxFilterProps: undefined,
            timeRangeFilterProps: undefined,
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
    const timeRangeFilterProps = (filterType === 'timeRange' ? rest : undefined) as UseTimeRangePillFilterProps | undefined;

    return { commonProps, comboboxFilterProps, timeRangeFilterProps };
}

export function usePillFilter(props?: FilterProps | RemovableFilterProps) {
    const intl = useIntl();

    const { commonProps, comboboxFilterProps, timeRangeFilterProps } = useMemo(() => splitUpProps(props), [props]);

    const timeRangePillFilterHook = useTimeRangePillFilter(timeRangeFilterProps);
    const comboboxPillFilterHook = useComboboxPillFilter(comboboxFilterProps);
    const filterHook = useMemo(
        () =>
            commonProps?.filterType === 'timeRange'
                ? timeRangePillFilterHook
                : commonProps?.filterType === 'combobox'
                  ? comboboxPillFilterHook
                  : undefined,
        [commonProps?.filterType, timeRangePillFilterHook, comboboxPillFilterHook]
    );

    return commonProps && filterHook
        ? {
              ...commonProps,
              ...filterHook,
              ariaLabel: intl.formatMessage(SreAgentResources.pillFilterAriaLabel, {
                  columnName: commonProps.label,
                  delimiter: commonProps.labelDelimiter ? ` ${commonProps.labelDelimiter} ` : '',
                  filterValue: filterHook?.pillDisplayValue,
              }),
              removeButtonAriaLabel: intl.formatMessage(SreAgentResources.pillFilterRemoveAriaLabel, { columnName: commonProps.label }),
          }
        : undefined;
}

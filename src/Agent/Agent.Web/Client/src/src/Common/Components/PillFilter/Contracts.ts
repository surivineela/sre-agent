import { LabelKeyPair } from './ListWithSearch';

export interface PillProps {
    label: string;
    ariaLabel: string;
    value: string;
    onApply: () => void;
    applyDisabled?: boolean;
    applyLabel?: string;
    disabled?: boolean;
    cancelLabel?: string;
    onCancelOrDismiss?: () => void;
    removeButtonAriaLabel?: string;
    onRemove?: () => void;
    labelDelimiter?: string;
    valueMaxWidth?: number | string;
    useInDialog?: boolean;
    onRenderButtonContent?: (props: {
        label: string;
        value: string;
        contentClass: string;
        labelClass: string;
        valueClass: string;
    }) => React.ReactNode;
}

export interface CommonFilterProps {
    label: string;
    labelDelimiter?: string;
    displayValue?: string;
    disabled?: boolean;
    valueMaxWidth?: number | string;
    useInDialog?: boolean;
}

export interface TimeRangeValue {
    key: string;
    start?: Date;
    end?: Date;
}

export enum TimespanKeys {
    All = 'All',
    OneHour = 'OneHour',
    SixHours = 'SixHours',
    TwelveHours = 'TwelveHours',
    TwentyFourHours = 'TwentyFourHours',
    ThreeDays = 'ThreeDays',
    SevenDays = 'SevenDays',
    Custom = 'Custom',
}

export interface TimeRangeKeyLabelPair {
    label: string;
    key: TimespanKeys;
}

export interface CustomTimeRangeProps {
    addCustomOption?: boolean;
    customOptionLabel?: string;
    minDateTime?: Date;
    maxDateTime?: Date;
}

export interface UseTimeRangePillFilterProps {
    options: TimeRangeKeyLabelPair[];
    onApply: (value: TimeRangeValue) => void;
    selectedValue: TimeRangeValue;
    customTimeRangeProps?: CustomTimeRangeProps;
    disabled?: boolean;
}

export interface UseComboboxPillFilterProps {
    label: string;
    options: LabelKeyPair[];
    onApply: (keys: string[]) => void;
    selectedKeys: string[];
    multiSelect?: boolean;
    addAllOption?: boolean;
    allOptionLabel?: string;
    showValueAs?: 'list' | 'count';
    disabled?: boolean;
}

export type FilterProps =
    | ({ filterType: 'timeRange' } & CommonFilterProps & UseTimeRangePillFilterProps)
    | ({ filterType: 'combobox' } & CommonFilterProps & Omit<UseComboboxPillFilterProps, 'label'>);

export type RemovableFilterProps = FilterProps & { onRemove: () => void };

export interface FilterPropsWithKey {
    key: string;
    props: RemovableFilterProps;
}

export type DynamicPillFilterProps = {
    options?: FilterPropsWithKey[];
    selectedKey?: string;
    onSelectedKeyChange: (optionKey?: string) => void;
    disabled?: boolean;
};

export interface PillFilterSetProps {
    staticFilters?: FilterProps[] | undefined;
    dynamicFilters?: FilterPropsWithKey[] | undefined;
    disabled?: boolean;
}

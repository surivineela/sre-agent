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
    onSearchChange?: (searchText: string) => void;
}

export type FilterProps = { filterType: 'combobox' } & CommonFilterProps & Omit<UseComboboxPillFilterProps, 'label'>;

export type RemovableFilterProps = FilterProps & { onRemove: () => void };

import {
    Combobox,
    ComboboxProps,
    makeStyles,
    mergeClasses,
    OptionOnSelectData,
    SelectionEvents,
    Skeleton,
    SkeletonItem,
    useComboboxFilter,
} from '@fluentui/react-components';
import { useCallback, useState } from 'react';
import { FieldWrapper, FieldWrapperProps } from '../Field/FieldWrapper';

export interface ComboboxOption {
    value: string;
    text: string;
    disabled?: boolean;
}

export type ComboboxWithFilterNoFormikProps = Omit<ComboboxProps, 'children'> &
    Omit<FieldWrapperProps, 'children'> & {
        options: ComboboxOption[];
        selectedValue?: string;
        onSelectionChange?: (value: string, text: string) => void;
        noOptionsMessage?: string;
        isLoading?: boolean;
        comboboxClassName?: string;
    };

const useStyles = makeStyles({
    combobox: {
        minWidth: '250px',
    },
});

export const ComboboxWithFilterNoFormik: React.FC<ComboboxWithFilterNoFormikProps> = ({
    label,
    required,
    tooltip,
    error,
    orientation,
    fieldProps,
    options,
    selectedValue,
    onSelectionChange,
    noOptionsMessage,
    isLoading,
    comboboxClassName,
    placeholder,
    disabled,
    value: controlledValue,
    onChange: controlledOnChange,
    ...comboboxProps
}) => {
    const styles = useStyles();
    const [query, setQuery] = useState(controlledValue ?? '');
    const [hasUserChanged, setHasUserChanged] = useState(false);

    const comboboxOptions = options.map(option => ({
        children: option.text,
        value: option.value,
        disabled: option.disabled,
    }));

    const children = useComboboxFilter(hasUserChanged ? query : '', comboboxOptions, {
        optionToText: option => option.children as string,
        noOptionsMessage: noOptionsMessage,
    });

    const onOptionSelect = useCallback<NonNullable<ComboboxProps['onOptionSelect']>>(
        (_event: SelectionEvents, data: OptionOnSelectData) => {
            const value = data.optionValue;
            const text = data.optionText ?? '';
            setQuery(text);
            onSelectionChange?.(value ?? '', text);
        },
        [onSelectionChange]
    );

    const handleChange = useCallback(
        (ev: React.ChangeEvent<HTMLInputElement>) => {
            setQuery(ev.target.value);
            setHasUserChanged(true);
            controlledOnChange?.(ev);
        },
        [controlledOnChange]
    );

    const comboboxContent = isLoading ? (
        <Skeleton>
            <SkeletonItem size={32} />
        </Skeleton>
    ) : (
        <Combobox
            {...comboboxProps}
            className={mergeClasses(styles.combobox, comboboxClassName)}
            onOptionSelect={onOptionSelect}
            placeholder={placeholder}
            value={controlledValue ?? query}
            onChange={handleChange}
            disabled={disabled}
        >
            {children}
        </Combobox>
    );

    if (label) {
        return (
            <FieldWrapper
                label={label}
                required={required}
                tooltip={tooltip}
                error={error}
                orientation={orientation}
                fieldProps={fieldProps}
            >
                {comboboxContent}
            </FieldWrapper>
        );
    }

    return comboboxContent;
};

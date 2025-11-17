import { FieldHookConfig, useField } from 'formik';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { ComboboxWithFilterNoFormik, ComboboxWithFilterNoFormikProps } from './ComboboxWithFilterNoFormik';

export type ComboboxWithFilterFormikProps = Omit<
    ComboboxWithFilterNoFormikProps,
    'name' | 'error' | 'selectedValue' | 'onSelectionChange'
> &
    Pick<FieldHookConfig<string | undefined>, 'name' | 'validate'> & {
        showUntouchedFieldError?: boolean;
    };

export const ComboboxWithFilterFormik: React.FC<ComboboxWithFilterFormikProps> = ({
    name,
    validate,
    showUntouchedFieldError,
    options,
    ...props
}) => {
    const [field, meta, helper] = useField({ name, validate });
    const [displayText, setDisplayText] = useState('');

    // Find the display text for the current value
    const selectedOption = useMemo(() => {
        return options.find(opt => opt.value === field.value);
    }, [options, field.value]);

    // Update display text when field value or options change
    useEffect(() => {
        if (selectedOption) {
            setDisplayText(selectedOption.text);
        } else if (!field.value) {
            setDisplayText('');
        }
    }, [selectedOption, field.value]);

    const onBlur = useCallback(
        (e?: React.FocusEvent<HTMLInputElement>) => {
            field.onBlur(e);
            helper.setTouched(true);
        },
        [field, helper]
    );

    const onSelectionChange = useCallback(
        (value: string, text: string) => {
            helper.setValue(value);
            setDisplayText(text);
        },
        [helper]
    );

    const onChange = useCallback((ev: React.ChangeEvent<HTMLInputElement>) => {
        setDisplayText(ev.target.value);
    }, []);

    return (
        <ComboboxWithFilterNoFormik
            {...props}
            options={options}
            selectedValue={field.value}
            value={displayText}
            error={meta.touched || showUntouchedFieldError ? meta.error : undefined}
            onBlur={onBlur}
            onSelectionChange={onSelectionChange}
            onChange={onChange}
        />
    );
};

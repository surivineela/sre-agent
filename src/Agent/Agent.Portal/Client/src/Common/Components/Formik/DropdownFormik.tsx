import { DropdownProps, OptionOnSelectData, SelectionEvents } from '@fluentui/react-components';
import { FieldHookConfig, useField } from 'formik';
import { useCallback } from 'react';
import { DropdownNoFormik, DropdownNoFormikProps } from './DropdownNoFormik';

export type DropdownFormikProps = Omit<DropdownNoFormikProps, 'name' | 'error'> &
    Pick<FieldHookConfig<string | undefined>, 'name' | 'validate'> & {
        showUntouchedFieldError?: boolean;
    };

export const DropdownFormik: React.FC<DropdownFormikProps> = ({ name, validate, onOptionSelect, showUntouchedFieldError, ...props }) => {
    const [field, meta, helper] = useField({ name, validate });

    const onBlur = useCallback(
        (e?: React.FocusEvent<HTMLElement>) => {
            field.onBlur(e);
            helper.setTouched(true);
        },
        [field, helper]
    );

    const onOptionSelectWrapper: DropdownProps['onOptionSelect'] = useCallback(
        (ev: SelectionEvents, data: OptionOnSelectData) => {
            const { setValue } = helper;
            setValue(data.optionValue);
            onOptionSelect?.(ev, data);
        },
        [helper, onOptionSelect]
    );

    return (
        <DropdownNoFormik
            {...props}
            value={props.value || field.value}
            error={meta.touched || showUntouchedFieldError ? meta.error : undefined}
            onBlur={onBlur}
            onOptionSelect={onOptionSelectWrapper}
        />
    );
};

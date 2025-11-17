import { RadioGroupOnChangeData } from '@fluentui/react-components';
import { FieldHookConfig, useField } from 'formik';
import { useCallback } from 'react';
import { RadioGroupNoFormik, RadioGroupNoFormikProps } from './RadioGroupNoFormik';

export type RadioGroupFormikProps = Omit<RadioGroupNoFormikProps, 'name' | 'error' | 'value'> &
    Pick<FieldHookConfig<string | undefined>, 'name' | 'validate'> & {
        showUntouchedFieldError?: boolean;
    };

export const RadioGroupFormik: React.FC<RadioGroupFormikProps> = ({ name, validate, onChange, showUntouchedFieldError, ...props }) => {
    const [field, meta, helper] = useField({ name, validate });

    const onBlur = useCallback(
        (e?: React.FocusEvent<HTMLDivElement>) => {
            field.onBlur(e);
            helper.setTouched(true);
        },
        [field, helper]
    );

    const onChangeWrapper = useCallback(
        (ev: React.FormEvent<HTMLDivElement>, data: RadioGroupOnChangeData) => {
            helper.setValue(data.value);
            onChange?.(ev, data);
        },
        [helper, onChange]
    );

    return (
        <RadioGroupNoFormik
            {...props}
            value={field.value}
            error={meta.touched || showUntouchedFieldError ? meta.error : undefined}
            onBlur={onBlur}
            onChange={onChangeWrapper}
        />
    );
};

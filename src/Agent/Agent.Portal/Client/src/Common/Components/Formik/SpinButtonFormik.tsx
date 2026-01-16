import { SpinButtonChangeEvent, SpinButtonOnChangeData } from '@fluentui/react-components';
import { FieldHookConfig, useField } from 'formik';
import { useCallback, useMemo } from 'react';
import { SpinButtonNoFormik, SpinButtonNoFormikProps } from './SpinButtonNoFormik';

export type SpinButtonFormikProps = Omit<SpinButtonNoFormikProps, 'name' | 'error' | 'value'> &
    Pick<FieldHookConfig<number | undefined>, 'name' | 'validate'> & {
        showUntouchedFieldError?: boolean;
    };

export const SpinButtonFormik: React.FC<SpinButtonFormikProps> = ({
    name,
    showUntouchedFieldError,
    validate,
    onChange: onValueChange,
    ...props
}) => {
    const [field, meta, helper] = useField<number | undefined>({ name, validate });

    const fieldOnBlur = useMemo(() => field.onBlur, [field.onBlur]);
    const helperSetTouched = useMemo(() => helper.setTouched, [helper.setTouched]);
    const helperSetValue = useMemo(() => helper.setValue, [helper.setValue]);
    const propsOnBlur = useMemo(() => props.onBlur, [props.onBlur]);

    const onBlur = useCallback(
        (e?: React.FocusEvent<HTMLInputElement>) => {
            if (e) {
                propsOnBlur?.(e);
            }
            fieldOnBlur(e);
            helperSetTouched(true);
        },
        [fieldOnBlur, helperSetTouched, propsOnBlur]
    );

    const onChange = useCallback(
        (ev: SpinButtonChangeEvent, data: SpinButtonOnChangeData) => {
            helperSetValue(data.value ?? (data.displayValue ? Number(data.displayValue) : undefined));
            onValueChange?.(ev, data);
        },
        [helperSetValue, onValueChange]
    );

    return (
        <SpinButtonNoFormik
            {...props}
            error={meta.touched || showUntouchedFieldError ? meta.error : undefined}
            onBlur={onBlur}
            onChange={onChange}
            value={field.value}
        />
    );
};

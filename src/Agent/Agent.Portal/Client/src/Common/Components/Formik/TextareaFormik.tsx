import { TextareaOnChangeData } from '@fluentui/react-components';
import { FieldHookConfig, useField } from 'formik';
import { ChangeEvent, useCallback, useMemo } from 'react';
import { TextareaNoFormik, TextareaNoFormikProps } from './TextareaNoFormik';

export type TextareaFormikProps = Omit<TextareaNoFormikProps, 'name' | 'error'> &
    Pick<FieldHookConfig<string | undefined>, 'name' | 'validate'> & {
        showUntouchedFieldError?: boolean;
    };

export const TextareaFormik: React.FC<TextareaFormikProps> = ({
    name,
    showUntouchedFieldError,
    validate,
    onChange: onTextChange,
    ...props
}) => {
    const [field, meta, helper] = useField<string | undefined>({ name, validate });

    const fieldOnBlur = useMemo(() => field.onBlur, [field.onBlur]);
    const helperSetTouched = useMemo(() => helper.setTouched, [helper.setTouched]);
    const helperSetValue = useMemo(() => helper.setValue, [helper.setValue]);
    const propsOnBlur = useMemo(() => props.onBlur, [props.onBlur]);

    const onBlur = useCallback(
        (e?: React.FocusEvent<HTMLTextAreaElement>) => {
            if (e) {
                propsOnBlur?.(e);
            }
            fieldOnBlur(e);
            helperSetTouched(true);
        },
        [fieldOnBlur, helperSetTouched, propsOnBlur]
    );

    const onChange = useCallback(
        (ev: ChangeEvent<HTMLTextAreaElement>, data: TextareaOnChangeData) => {
            helperSetValue(data.value);
            onTextChange?.(ev, data);
        },
        [helperSetValue, onTextChange]
    );

    return (
        <TextareaNoFormik
            {...props}
            error={meta.touched || showUntouchedFieldError ? meta.error : undefined}
            onBlur={onBlur}
            onChange={onChange}
            value={field.value ?? ''}
        />
    );
};

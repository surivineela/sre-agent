import { DropdownProps, OptionOnSelectData, SelectionEvents } from '@fluentui/react-components';
import { FieldHookConfig, useField } from 'formik';
import { useCallback, useContext, useMemo } from 'react';
import { AzPortalContext } from '../../AzPortalProxy/Providers/AzPortalProxyContext';
import { logFieldValueChange } from '../../Helpers/Telemetry';
import DropdownNoFormik, { DropdownNoFormikProps, DropdownOptionBase } from './DropdownNoFormik';

// String variant props
export type DropdownFormikProps<T extends DropdownOptionBase | string> = DropdownNoFormikProps<T> &
    Pick<FieldHookConfig<string | undefined>, 'name' | 'validate'> & {
        showUntouchedFieldError?: boolean;
    };

// TODO (evtheodo): support multiselect
const DropdownFormik = <T extends DropdownOptionBase | string = DropdownOptionBase>({
    name,
    validate,
    onOptionSelect,
    showUntouchedFieldError,
    multiselect,
    ...props
}: DropdownFormikProps<T>) => {
    const [field, meta, helper] = useField({ name, validate });
    const { log } = useContext(AzPortalContext);

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
            if (multiselect) {
                setValue(data.selectedOptions);
                logFieldValueChange(name, data.selectedOptions.join(', '), log);
            } else {
                setValue(data.optionValue);
                logFieldValueChange(name, data.optionText, log);
            }
            onOptionSelect?.(ev, data);
        },
        [helper, log, name, onOptionSelect]
    );

    const selectedOptions = useMemo(() => {
        if (!field.value) {
            return [];
        }
        return multiselect ? field.value : [field.value];
    }, [field.value, multiselect]);

    return (
        <DropdownNoFormik
            value={field.value}
            selectedOptions={selectedOptions}
            error={meta.touched || showUntouchedFieldError ? meta.error : undefined}
            onBlur={onBlur}
            onOptionSelect={onOptionSelectWrapper}
            multiselect={multiselect}
            {...props}
        />
    );
};

export default DropdownFormik;

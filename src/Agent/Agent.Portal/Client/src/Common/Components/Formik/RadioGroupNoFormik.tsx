import { Radio, RadioGroup, RadioGroupProps, Skeleton, SkeletonItem } from '@fluentui/react-components';
import { FieldWrapper, FieldWrapperProps } from './FieldWrapper';

export interface RadioGroupOption {
    key: string;
    text: string;
    disabled?: boolean;
}

export type RadioGroupNoFormikProps = Omit<RadioGroupProps, 'children'> &
    Omit<FieldWrapperProps, 'children'> & {
        options: RadioGroupOption[];
        isLoading?: boolean;
        stackVertical?: boolean;
    };

export const RadioGroupNoFormik: React.FC<RadioGroupNoFormikProps> = ({
    label,
    required,
    tooltip,
    error,
    orientation,
    fieldProps,
    options,
    isLoading,
    stackVertical = true,
    ...radioGroupProps
}) => {
    const radioGroupContent = isLoading ? (
        <Skeleton>
            <SkeletonItem />
        </Skeleton>
    ) : (
        <RadioGroup {...radioGroupProps} layout={stackVertical ? 'vertical' : 'horizontal'}>
            {options.map(option => (
                <Radio key={option.key} value={option.key} label={option.text} disabled={option.disabled || radioGroupProps.disabled} />
            ))}
        </RadioGroup>
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
                {radioGroupContent}
            </FieldWrapper>
        );
    }

    return radioGroupContent;
};

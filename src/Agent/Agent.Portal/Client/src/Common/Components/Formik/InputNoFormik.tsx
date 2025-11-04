import { Input, InputProps, Skeleton, SkeletonItem } from '@fluentui/react-components';
import { FieldWrapper, FieldWrapperProps } from './FieldWrapper';

export type InputNoFormikProps = InputProps &
    Omit<FieldWrapperProps, 'children'> & {
        isLoading?: boolean;
    };

export const InputNoFormik: React.FC<InputNoFormikProps> = ({
    isLoading,
    label,
    required,
    tooltip,
    error,
    orientation,
    fieldProps,
    ...inputProps
}) => {
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
                {isLoading ? (
                    <Skeleton>
                        <SkeletonItem size={32} animation={'wave'} />
                    </Skeleton>
                ) : (
                    <Input {...inputProps} />
                )}
            </FieldWrapper>
        );
    }

    return isLoading ? (
        <Skeleton>
            <SkeletonItem size={32} animation={'wave'} />
        </Skeleton>
    ) : (
        <Input {...inputProps} />
    );
};

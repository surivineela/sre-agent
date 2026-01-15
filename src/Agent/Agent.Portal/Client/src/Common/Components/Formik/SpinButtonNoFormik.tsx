import { Skeleton, SkeletonItem, SpinButton, SpinButtonProps } from '@fluentui/react-components';
import { FieldWrapper, FieldWrapperProps } from './FieldWrapper';

export type SpinButtonNoFormikProps = SpinButtonProps &
    Omit<FieldWrapperProps, 'children'> & {
        isLoading?: boolean;
    };

export const SpinButtonNoFormik: React.FC<SpinButtonNoFormikProps> = ({
    isLoading,
    label,
    required,
    tooltip,
    error,
    orientation,
    fieldProps,
    ...spinButtonProps
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
                    <SpinButton {...spinButtonProps} />
                )}
            </FieldWrapper>
        );
    }

    return isLoading ? (
        <Skeleton>
            <SkeletonItem size={32} animation={'wave'} />
        </Skeleton>
    ) : (
        <SpinButton {...spinButtonProps} />
    );
};

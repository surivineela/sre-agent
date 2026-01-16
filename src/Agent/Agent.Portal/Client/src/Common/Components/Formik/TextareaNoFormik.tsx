import { Skeleton, SkeletonItem, Textarea, TextareaProps } from '@fluentui/react-components';
import { FieldWrapper, FieldWrapperProps } from './FieldWrapper';

export type TextareaNoFormikProps = TextareaProps &
    Omit<FieldWrapperProps, 'children'> & {
        isLoading?: boolean;
    };

export const TextareaNoFormik: React.FC<TextareaNoFormikProps> = ({
    isLoading,
    label,
    required,
    tooltip,
    error,
    orientation,
    fieldProps,
    ...textareaProps
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
                        <SkeletonItem style={{ height: '80px' }} animation={'wave'} />
                    </Skeleton>
                ) : (
                    <Textarea {...textareaProps} />
                )}
            </FieldWrapper>
        );
    }

    return isLoading ? (
        <Skeleton>
            <SkeletonItem style={{ height: '80px' }} animation={'wave'} />
        </Skeleton>
    ) : (
        <Textarea {...textareaProps} />
    );
};

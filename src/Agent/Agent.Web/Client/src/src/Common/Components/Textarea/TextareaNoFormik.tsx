import { Skeleton, SkeletonItem, Textarea, TextareaProps } from '@fluentui/react-components';
import { FieldWrapper, FieldWrapperProps } from '../Field/FieldWrapper';

export type TextareaNoFormikProps = TextareaProps &
    Omit<FieldWrapperProps, 'children'> & {
        isLoading?: boolean;
    };

const TextareaNoFormik: React.FC<TextareaNoFormikProps> = ({ isLoading, label, ...props }) => {
    if (label) {
        return (
            <FieldWrapper label={label} {...props}>
                {isLoading ? (
                    <Skeleton>
                        <SkeletonItem size={64} animation={'wave'} />
                    </Skeleton>
                ) : (
                    <Textarea {...props} />
                )}
            </FieldWrapper>
        );
    }

    return isLoading ? (
        <Skeleton>
            <SkeletonItem size={64} animation={'wave'} />
        </Skeleton>
    ) : (
        <Textarea {...props} />
    );
};

export default TextareaNoFormik;

import { Input, InputProps, Skeleton, SkeletonItem } from '@fluentui/react-components';
import { FieldWrapper, FieldWrapperProps } from '../Field/FieldWrapper';

export type InputNoFormikProps = InputProps &
    Omit<FieldWrapperProps, 'children'> & {
        isLoading?: boolean;
    };

const InputNoFormik: React.FC<InputNoFormikProps> = ({ isLoading, label, error, ...props }) => {
    if (label || error) {
        return (
            <FieldWrapper label={label} error={error} {...props}>
                {isLoading ? (
                    <Skeleton>
                        <SkeletonItem size={32} animation={'wave'} />
                    </Skeleton>
                ) : (
                    <Input {...props} />
                )}
            </FieldWrapper>
        );
    }

    return isLoading ? (
        <Skeleton>
            <SkeletonItem size={32} animation={'wave'} />
        </Skeleton>
    ) : (
        <Input {...props} />
    );
};

export default InputNoFormik;

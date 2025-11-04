import { Dropdown, DropdownProps, Skeleton, SkeletonItem } from '@fluentui/react-components';
import { ReactNode } from 'react';
import { FieldWrapper, FieldWrapperProps } from './FieldWrapper';

export type DropdownNoFormikProps = DropdownProps &
    Omit<FieldWrapperProps, 'children'> & {
        isLoading?: boolean;
        sublabel?: React.ReactNode;
        children: ReactNode;
    };

export const DropdownNoFormik: React.FC<DropdownNoFormikProps> = ({
    label,
    required,
    tooltip,
    error,
    orientation,
    fieldProps,
    isLoading,
    sublabel,
    children,
    ...dropdownProps
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
                        <SkeletonItem />
                    </Skeleton>
                ) : (
                    <>
                        <Dropdown {...dropdownProps}>{children}</Dropdown>
                        {sublabel}
                    </>
                )}
            </FieldWrapper>
        );
    }

    return isLoading ? (
        <Skeleton>
            <SkeletonItem />
        </Skeleton>
    ) : (
        <>
            <Dropdown {...dropdownProps}>{children}</Dropdown>
            {sublabel}
        </>
    );
};

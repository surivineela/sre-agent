import { Dropdown, DropdownProps, Option, OptionGroup, Skeleton, SkeletonItem } from '@fluentui/react-components';
import { useCallback } from 'react';
import { FieldWrapper, FieldWrapperProps } from '../Field/FieldWrapper';

export enum OptionType {
    Option,
    OptionGroup,
}

export type DropdownOptionBase = { id: string; text: string; type: OptionType; children?: DropdownOptionBase[] };

export type DropdownNoFormikProps<T extends DropdownOptionBase | string> = DropdownProps &
    Omit<FieldWrapperProps, 'children'> & {
        options: T[];
        disabled?: boolean;
        isLoading?: boolean;
        sublabel?: React.ReactNode;
    };

// TODO (evtheodo): support grouped
// TODO (evtheodo): support custom Option content
// TODO (evtheodo): support custom renderers for Option and OptionGroup
const DropdownNoFormik = <T extends DropdownOptionBase | string = DropdownOptionBase>({
    label,
    options,
    disabled,
    isLoading,
    sublabel,
    ...props
}: DropdownNoFormikProps<T>) => {
    const renderOptions = useCallback(() => {
        return options.map(option => {
            if (typeof option === 'string' || option.type === OptionType.Option) {
                return (
                    <Option
                        key={typeof option === 'string' ? option : option.id}
                        value={typeof option === 'string' ? option : option.id}
                        disabled={disabled}
                    >
                        {typeof option === 'string' ? option : option.text}
                    </Option>
                );
            } else if (option.type === OptionType.OptionGroup && option.children) {
                return (
                    <OptionGroup key={option.id} label={option.text}>
                        {option.children.map(childOption => (
                            <Option
                                key={typeof option === 'string' ? option : childOption.id}
                                value={typeof option === 'string' ? option : childOption.id}
                                disabled={disabled}
                            >
                                {typeof option === 'string' ? option : childOption.text}
                            </Option>
                        ))}
                    </OptionGroup>
                );
            }
        });
    }, [options, disabled]);

    if (label) {
        return (
            <FieldWrapper label={label} {...props}>
                {isLoading ? (
                    <Skeleton>
                        <SkeletonItem />
                    </Skeleton>
                ) : (
                    <>
                        <Dropdown {...props} disabled={disabled}>
                            {renderOptions()}
                        </Dropdown>
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
            <Dropdown {...props} disabled={disabled}>
                {renderOptions()}
            </Dropdown>
            {sublabel}
        </>
    );
};

export default DropdownNoFormik;

import { Dropdown, DropdownMenuItemType, IDropdownOption, IDropdownProps } from '@fluentui/react/lib/Dropdown';
import { SelectableOptionMenuItemType } from '@fluentui/react/lib/SelectableOption';
import { Spinner, SpinnerSize } from '@fluentui/react/lib/Spinner';
import { mergeStyleSets } from '@fluentui/react/lib/Styling';
import { TextField } from '@fluentui/react/lib/TextField';
import * as React from 'react';
import { useIntl } from 'react-intl';
import { ManagedResourcesStringResources } from '../../Strings/SREAgentResources';

export const dropdownStyles = mergeStyleSets({
    LoadingDropdownSpinnerStyle: {
        marginTop: '4px',
    },
});

function isDividerOrHeader<T>(option: IDropdownOptionForFilter<T>): option is IDropdownOptionDividerOrHeader {
    return option.itemType === SelectableOptionMenuItemType.Divider || option.itemType == SelectableOptionMenuItemType.Header;
}

export type IDropdownOptionDividerOrHeader = Omit<IDropdownOption, 'itemType'> & {
    itemType: SelectableOptionMenuItemType.Divider | SelectableOptionMenuItemType.Header;
};
export type IDropdownOptionForFilterNormal<T> = Omit<IDropdownOption, 'data'> & { data: T };
export type IDropdownOptionForFilter<T> = IDropdownOptionDividerOrHeader | IDropdownOptionForFilterNormal<T>;

type CommonFilterProps<T> = Omit<IDropdownProps, 'options'> & {
    options: IDropdownOptionForFilter<T>[];
    isLoading?: boolean;
    searchBoxPlaceHolderText?: string;
};
type FieldFilterProps<T> = {
    filterFields: (keyof T)[];
    filterValue?: string;
    onFilterChange?: (filterValue?: string) => void;
};
type FuncFilterProps<T> = {
    filterFunc: (option: IDropdownOptionForFilterNormal<T>, filterText: string) => boolean;
};
type FilterProps<T> = T extends string ? Partial<FuncFilterProps<T>> : FieldFilterProps<T> | FuncFilterProps<T>;

export type IDropdownWithFilterProps<T> = CommonFilterProps<T> & FilterProps<T>;

export const DropdownWithFilter = <T extends {} | string = string>(props: IDropdownWithFilterProps<T>) => {
    // eslint-disable-line @typescript-eslint/no-empty-object-type
    const {
        onRenderItem: onRenderItemFromProps,
        options: optionsFromProps,
        selectedKey: selectedKeyFromProps,
        isLoading,
        searchBoxPlaceHolderText,
        filterFields,
        filterValue,
        filterFunc,
        onFilterChange,
        ...rest
    } = props as IDropdownWithFilterProps<T> & Partial<FieldFilterProps<T> & FuncFilterProps<T>>;
    const intl = useIntl();

    const loadingProps = isLoading
        ? {
              onRenderCaretDown: () => {
                  return <Spinner className={dropdownStyles.LoadingDropdownSpinnerStyle} size={SpinnerSize.xSmall} ariaLive="assertive" />;
              },
              onRenderPlaceholder: () => {
                  return <>{intl.formatMessage(ManagedResourcesStringResources.loading)}</>;
              },
              disabled: true,
          }
        : {};

    enum FieldType {
        filter = 0,
        noOption = 1,
    }

    const [filterText, setFilterText] = React.useState(filterValue ?? '');

    const filterBox = (
        <TextField
            onChange={(_, filter) => {
                setFilterText(filter ?? '');
                if (onFilterChange) {
                    onFilterChange(filter);
                }
            }}
            styles={{ root: { margin: '10px 16px 5px' } }}
            key="Filter"
            placeholder={searchBoxPlaceHolderText || intl.formatMessage(ManagedResourcesStringResources.filterItems)}
        />
    );

    const onRenderItem = (option: IDropdownOption, defaultRender: (option: IDropdownOption) => JSX.Element) => {
        if (!option) {
            return '';
        }
        switch (option.data) {
            case FieldType.filter:
                return filterBox;
            case FieldType.noOption:
                return (
                    <p key="noOptions" style={{ textAlign: 'center', margin: '5px' }}>
                        {intl.formatMessage(ManagedResourcesStringResources.noResults)}
                    </p>
                );
            default:
                return onRenderItemFromProps ? onRenderItemFromProps(option) : defaultRender(option);
        }
    };

    const shouldInclude = (option: IDropdownOptionForFilter<T>) => {
        if (isDividerOrHeader(option)) {
            return true;
        }

        if (filterFunc) {
            return filterFunc(option, filterText);
        } else {
            const lower = (value?: any) => (typeof value === 'string' ? value.toLowerCase() : '');
            const lowFilter = lower(filterText);

            // For string comparisons
            if (typeof option.data === 'string') {
                return lower(option.data).includes(lowFilter);
            } else if (filterFields) {
                const fieldValues = filterFields.map((field: keyof T) => option.data[field]);
                return fieldValues.map(lower).filter(value => value.indexOf(lowFilter) > -1).length;
            }
        }
    };

    const getOptions = () => {
        const options: IDropdownOption[] = [
            { key: 'Filter', text: '', data: FieldType.filter, itemType: DropdownMenuItemType.Header },
            {
                key: 'Divider',
                text: '',
                itemType: SelectableOptionMenuItemType.Divider,
            },
        ];

        const filteredOptions = optionsFromProps?.filter(option => shouldInclude(option)) ?? [];

        if (filteredOptions.length > 0) {
            options.push(...filteredOptions);
        } else {
            options.push({ key: 'NoOptions', text: '', data: FieldType.noOption });
        }

        return options;
    };

    const [options, setOptions] = React.useState(getOptions());

    React.useEffect(() => {
        setOptions(getOptions());
    }, [filterText, filterFields, filterFunc, optionsFromProps, props.disabled, getOptions]);

    return props.multiSelect ? (
        <Dropdown
            //   onRenderItem={onRenderItem}
            onRenderOption={onRenderItem}
            options={options}
            selectedKeys={props.multiSelect ? props.selectedKeys : null}
            onDismiss={() => setFilterText('')}
            {...loadingProps}
            {...rest}
        />
    ) : (
        <Dropdown
            onRenderOption={onRenderItem}
            options={options}
            selectedKey={!filterText ? selectedKeyFromProps : null}
            onDismiss={() => setFilterText('')}
            {...loadingProps}
            {...rest}
        />
    );
};

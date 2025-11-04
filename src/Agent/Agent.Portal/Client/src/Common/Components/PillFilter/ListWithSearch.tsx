import { List, ListItem, makeStyles, SearchBox, tokens } from '@fluentui/react-components';
import { Checkmark16Filled } from '@fluentui/react-icons';
import { FC, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';

export interface LabelKeyPair {
    label: string;
    key: string;
    icon?: JSX.Element;
    iconSrc?: string;
    sublabel?: string;
}

export const ALL_OPTION = 'all';

const itemLabelStyles = {
    whiteSpace: 'nowrap',
    textOverflow: 'ellipsis',
    overflow: 'hidden',
};

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '10px',
        maxWidth: '300px',
        overflow: 'hidden',
    },
    searchBox: {
        marginLeft: '16px',
        marginRight: '16px',
        maxWidth: 'unset',
        '& .fui-SearchBox__contentAfter': {
            display: 'none',
        },
    },
    listWrapper: {
        position: 'relative',
        overflowY: 'auto',
        marginLeft: '16px',
        marginRight: '16px',
    },
    listItem: {
        position: 'relative',
        '& .fui-Checkbox__indicator': {
            display: 'none', // Hide default checkmark
        },
        '& .custom-checkmark': {
            position: 'absolute',
            left: '2px',
            top: '50%',
            transform: 'translateY(-50%)',
        },
        padding: '4px 2px 4px 18px',
        margin: '2px',
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
    itemLabelWrapper: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-start',
        gap: '2px',
    },
    itemLabelTopWrapper: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        gap: '8px',
    },
    itemLabel: itemLabelStyles,
    itemSubLabel: {
        ...itemLabelStyles,
        fontSize: '12px',
        color: tokens.colorNeutralForeground3,
        wordBreak: 'break-word',
        whiteSpace: 'unset',
    },
    allItemLabel: {
        ...itemLabelStyles,
        fontWeight: '600',
    },
});

export interface ListWithFilterProps {
    options: LabelKeyPair[];
    selectedKeys: string[];
    setSelectedKeys: (keys: string[]) => void;
    multiSelect?: boolean;
    addAllOption?: boolean;
    allOptionLabel?: string;
    ariaLabel?: string;
    disabled?: boolean;
    onSearchChange?: (searchText: string) => void;
}

export const ListWithSearch: FC<ListWithFilterProps> = ({
    options,
    selectedKeys,
    setSelectedKeys,
    multiSelect,
    addAllOption,
    allOptionLabel,
    ariaLabel,
    disabled,
    onSearchChange,
}) => {
    const intl = useIntl();
    const styles = useStyles();
    const [searchText, setSearchText] = useState<string>('');
    const allLabel = useMemo(() => allOptionLabel || intl.formatMessage(PortalResources.all), [allOptionLabel, intl]);

    const [allOptionSelected, setAllOptionSelected] = useState<boolean>(false);

    // Handle search text change - call callback if provided (for server-side filtering)
    // or filter locally if no callback (for client-side filtering)
    const handleSearchChange = useCallback(
        (newSearchText: string) => {
            setSearchText(newSearchText);
            if (onSearchChange) {
                onSearchChange(newSearchText);
            }
        },
        [onSearchChange]
    );

    const filteredOptions = useMemo(() => {
        // If onSearchChange is provided, don't filter locally (server-side filtering)
        if (onSearchChange) {
            return options;
        }
        // Otherwise, filter locally (client-side filtering)
        if (!searchText) {
            return options;
        }
        return options.filter(option => option.label.toLowerCase().includes(searchText.toLowerCase()));
    }, [options, searchText, onSearchChange]);

    const selectedKeysPlusAll = useMemo(() => {
        if (multiSelect && addAllOption && allOptionSelected) {
            return [...selectedKeys, ALL_OPTION];
        }
        return selectedKeys;
    }, [selectedKeys, multiSelect, addAllOption, allOptionSelected]);

    const onSelectionChange = useCallback(
        (values: string[]) => {
            if (multiSelect && addAllOption) {
                const isAllOptionNowSelected = values.some(value => value === ALL_OPTION);
                const wasAllOptionPreviouslySelected = allOptionSelected;

                // Remove the "All" option from the values, since we handle it separately.
                const adjustedValues = values.filter(value => value !== ALL_OPTION);

                if (isAllOptionNowSelected) {
                    // The "All" option is selected.

                    if (wasAllOptionPreviouslySelected) {
                        // The "All" was already selected. This means something else changed (was deselected), so we should also deselect the "All" option.
                        setAllOptionSelected(false);
                        setSelectedKeys(adjustedValues);
                    } else {
                        // The "All" option was not already selected, and now it is.
                        // If there's a search term, select only filtered items.
                        // Otherwise, pass empty array to signal "All" conceptually.
                        setAllOptionSelected(true);
                        if (searchText.trim()) {
                            // Search active - select all filtered options
                            filteredOptions.forEach(option => {
                                if (!adjustedValues.includes(option.key)) {
                                    adjustedValues.push(option.key);
                                }
                            });
                            setSelectedKeys(adjustedValues);
                        } else {
                            // No search - select "All" conceptually (empty array)
                            setSelectedKeys([]);
                        }
                    }
                } else {
                    // The "All" option is not selected.

                    if (wasAllOptionPreviouslySelected) {
                        // The "All" option was selected, but now it isn't. This means we should deselect all filtered options.
                        setAllOptionSelected(false);
                        setSelectedKeys(adjustedValues.filter(value => !filteredOptions.some(option => option.key === value)));
                    } else {
                        // The "All" option was not already selected, and still isn't selected.
                        // If all options (not just filtered) are selected, we should select the "ALL" option as well.
                        const allOptionsSelected = options.every(option => values.includes(option.key));
                        setAllOptionSelected(allOptionsSelected);
                        setSelectedKeys(adjustedValues);
                    }
                }
            } else {
                setSelectedKeys(values);
            }
        },
        [multiSelect, addAllOption, allOptionSelected, filteredOptions, options, setSelectedKeys, searchText]
    );

    useEffect(() => {
        if (multiSelect && addAllOption) {
            // Empty array with no search means "All" is selected conceptually
            // Otherwise check if all options are selected
            const isAllSelected =
                (selectedKeys.length === 0 && !searchText.trim()) || options.every(option => selectedKeys.includes(option.key));
            setAllOptionSelected(isAllSelected);
        }
    }, [options, multiSelect, addAllOption, selectedKeys, searchText]);

    return (
        <div className={styles.root}>
            <SearchBox
                placeholder={intl.formatMessage(PortalResources.search)}
                value={searchText}
                onChange={(_, data) => handleSearchChange(data.value)}
                className={styles.searchBox}
            />
            <div className={styles.listWrapper}>
                {filteredOptions.length === 0 ? (
                    <div>{intl.formatMessage(PortalResources.noResults)}</div>
                ) : (
                    <List
                        selectionMode={multiSelect ? 'multiselect' : 'single'}
                        aria-label={ariaLabel}
                        selectedItems={selectedKeysPlusAll}
                        onSelectionChange={(_, data) => onSelectionChange(data.selectedItems.map(item => item.toString()))}
                    >
                        {multiSelect && addAllOption && (
                            <ListItem
                                key={ALL_OPTION}
                                value={ALL_OPTION}
                                aria-label={allLabel}
                                checkmark={{
                                    'aria-label': allLabel,
                                    style: { visibility: 'hidden' },
                                    disabled: disabled,
                                }}
                                className={styles.listItem}
                            >
                                <Checkmark16Filled
                                    className="custom-checkmark"
                                    style={{ opacity: allOptionSelected && !disabled ? 1 : 0 }}
                                    data-testid={ALL_OPTION}
                                />
                                <span className={styles.allItemLabel}>{allLabel}</span>
                            </ListItem>
                        )}
                        {filteredOptions.map(option => (
                            <ListItem
                                key={option.key}
                                value={option.key}
                                aria-label={option.label}
                                checkmark={{
                                    'aria-label': option.label,
                                    style: { visibility: 'hidden' },
                                    disabled: disabled,
                                }}
                                className={styles.listItem}
                            >
                                <Checkmark16Filled
                                    className="custom-checkmark"
                                    style={{ opacity: selectedKeys.includes(option.key) && !disabled ? 1 : 0 }}
                                    data-testid={option.key}
                                />
                                <div className={styles.itemLabelWrapper}>
                                    <div className={styles.itemLabelTopWrapper}>
                                        {option.iconSrc ? (
                                            <img src={option.iconSrc} alt="" style={{ width: 16, height: 16 }} />
                                        ) : (
                                            (option.icon ?? null)
                                        )}
                                        <span className={styles.itemLabel}>{option.label}</span>
                                    </div>
                                    {option.sublabel && <span className={styles.itemSubLabel}>{option.sublabel}</span>}
                                </div>
                            </ListItem>
                        ))}
                    </List>
                )}
            </div>
        </div>
    );
};

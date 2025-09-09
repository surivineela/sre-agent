import { List, ListItem, makeStyles, SearchBox } from '@fluentui/react-components';
import { Checkmark16Filled } from '@fluentui/react-icons';
import { FC, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../../Strings/SREAgentResources';

export interface LabelKeyPair {
    label: string;
    key: string;
    iconSrc?: string;
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
    },
    searchBox: {
        position: 'absolute',
        left: '16px',
        right: '16px',
        maxWidth: 'unset',
    },
    searchBoxSpacer: {
        height: '32px',
        flex: 'none',
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
    itemLabel: itemLabelStyles,
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
}) => {
    const intl = useIntl();
    const styles = useStyles();
    const [searchText, setSearchText] = useState<string>('');
    const allLabel = useMemo(() => allOptionLabel || intl.formatMessage(SreAgentResources.all), [allOptionLabel, intl]);

    const [allOptionSelected, setAllOptionSelected] = useState<boolean>(false);

    const filteredOptions = useMemo(() => {
        if (!searchText) {
            return options;
        }
        return options.filter(option => option.label.toLowerCase().includes(searchText.toLowerCase()));
    }, [options, searchText]);

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
                        // The "All" option was not already selected, and now it is. This means we should select all filtered options.
                        filteredOptions.forEach(option => {
                            if (!adjustedValues.includes(option.key)) {
                                adjustedValues.push(option.key);
                            }
                        });
                        setAllOptionSelected(true);
                        setSelectedKeys(adjustedValues);
                    }
                } else {
                    // The "All" option is not selected.

                    if (wasAllOptionPreviouslySelected) {
                        // The "All" option was selected, but now it isn't. This means we should deselect all filtered options.
                        setAllOptionSelected(false);
                        setSelectedKeys(adjustedValues.filter(value => !filteredOptions.some(option => option.key === value)));
                    } else {
                        // The "All" option was not already selected, and still isn't selected.
                        // If all filtered options are selected, we should select the "ALL" option as well.
                        setAllOptionSelected(filteredOptions.every(option => values.includes(option.key)));
                        setSelectedKeys(adjustedValues);
                    }
                }
            } else {
                setSelectedKeys(values);
            }
        },
        [multiSelect, addAllOption, allOptionSelected, filteredOptions, setSelectedKeys]
    );

    useEffect(() => {
        if (multiSelect && addAllOption) {
            setAllOptionSelected(filteredOptions.every(option => selectedKeys.includes(option.key)));
        }
    }, [filteredOptions, multiSelect, addAllOption, selectedKeys]);

    return (
        <div className={styles.root}>
            <SearchBox
                placeholder={intl.formatMessage(SreAgentResources.search)}
                value={searchText}
                onChange={(_, data) => setSearchText(data.value)}
                className={styles.searchBox}
            />
            <div className={styles.searchBoxSpacer} />
            <div className={styles.listWrapper}>
                {filteredOptions.length === 0 ? (
                    <div>{intl.formatMessage(SreAgentResources.noResults)}</div>
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
                                {option.iconSrc && <img src={option.iconSrc} alt="" style={{ width: 16, height: 16 }} />}
                                <span className={styles.itemLabel}>{option.label}</span>
                            </ListItem>
                        ))}
                    </List>
                )}
            </div>
        </div>
    );
};

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../../Strings/Resources';
import { UseComboboxPillFilterProps } from '../Contracts';
import { ALL_OPTION, ListWithSearch } from '../ListWithSearch';

export const useComboboxPillFilter = (props: UseComboboxPillFilterProps | undefined) => {
    const label = useMemo(() => props?.label || '', [props?.label]);
    const options = useMemo(() => props?.options || [], [props?.options]);
    const onApply = useCallback((keys: string[]) => props?.onApply(keys), [props?.onApply]);
    const selectedKeys = useMemo(() => props?.selectedKeys || [], [props?.selectedKeys]);
    const multiSelect = useMemo(() => props?.multiSelect || false, [props?.multiSelect]);
    const addAllOption = useMemo(() => props?.addAllOption || false, [props?.addAllOption]);
    const allOptionLabel = useMemo(() => props?.allOptionLabel, [props?.allOptionLabel]);
    const showValueAs = useMemo(() => props?.showValueAs || 'count', [props?.showValueAs]);
    const disabled = useMemo(() => props?.disabled || false, [props?.disabled]);

    const intl = useIntl();
    const [currentSelectedKeys, setCurrentSelectedKeys] = useState<string[]>(selectedKeys || []);
    const [pendingSelectedKeys, setPendingSelectedKeys] = useState<string[]>(selectedKeys || []);
    const allLabel = useMemo(() => allOptionLabel || intl.formatMessage(PortalResources.all), [allOptionLabel, intl]);

    const getOptionText = useCallback(
        (value: string): string => {
            if (multiSelect && addAllOption && value === ALL_OPTION) {
                return allLabel;
            }
            const option = options.find(option => option.key === value);
            return option ? option.label : value.toString();
        },
        [multiSelect, addAllOption, options, allLabel]
    );

    const pillDisplayValue = useMemo((): string => {
        if (!multiSelect) {
            const value = currentSelectedKeys[0];
            return value ? getOptionText(value) : '';
        }

        const currentSelectionAdjusted = addAllOption ? currentSelectedKeys.filter(key => key !== ALL_OPTION) : currentSelectedKeys;

        const selectionLength = currentSelectionAdjusted.length;
        const optionsLength = options.length;

        if (selectionLength === 0 || selectionLength === optionsLength) {
            return allLabel;
        }

        if (showValueAs === 'list') {
            return currentSelectionAdjusted.map(key => getOptionText(key)).join(', ');
        }

        return intl.formatMessage(PortalResources.selectedOutOfTotal, {
            selectedCount: selectionLength,
            totalCount: optionsLength,
        });
    }, [intl, multiSelect, currentSelectedKeys, getOptionText, addAllOption, allLabel, options, showValueAs]);

    const onApplyClick = useCallback(() => {
        setCurrentSelectedKeys(pendingSelectedKeys);
        onApply(pendingSelectedKeys);
    }, [pendingSelectedKeys, onApply]);

    const initializeLocalState = useCallback(() => {
        setCurrentSelectedKeys(selectedKeys || []);
        setPendingSelectedKeys(selectedKeys || []);
    }, [selectedKeys]);

    useEffect(() => {
        initializeLocalState();
    }, [initializeLocalState]);

    const onRenderPopoverContent = useCallback(() => {
        return (
            <ListWithSearch
                options={options}
                selectedKeys={pendingSelectedKeys}
                setSelectedKeys={setPendingSelectedKeys}
                multiSelect={multiSelect}
                addAllOption={addAllOption}
                allOptionLabel={allLabel}
                ariaLabel={intl.formatMessage(PortalResources.optionsListAriaLabel, { fieldName: label })}
                disabled={disabled}
                onSearchChange={props?.onSearchChange}
            />
        );
    }, [options, pendingSelectedKeys, setPendingSelectedKeys, multiSelect, addAllOption, allLabel, intl, label, disabled, props?.onSearchChange]);

    return {
        pillDisplayValue,
        onApplyClick,
        isComplete: true,
        initializeLocalState,
        onRenderPopoverContent,
    };
};

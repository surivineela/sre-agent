import { FC, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { IncidentManagementResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ALL_OPTION, LabelKeyPair, ListWithSearch } from './ListWithSearch';
import { Pill } from './Pill';

export type { LabelKeyPair };

export interface ComboboxPillFilterProps {
    label: string;
    options: LabelKeyPair[];
    onApply: (keys: string[]) => void;
    selectedKeys: string[];
    displayValue?: string;
    showValueAs?: 'count' | 'list';
    valueMaxWidth?: number | string;
    multiSelect?: boolean;
    addAllOption?: boolean;
    allOptionLabel?: string;
    disabled?: boolean;
    onRemove?: () => void;
    labelDelimiter?: string;
}

export const ComboboxPillFilter: FC<ComboboxPillFilterProps> = ({
    label,
    options,
    onApply,
    selectedKeys,
    displayValue,
    showValueAs = 'count',
    valueMaxWidth,
    multiSelect,
    addAllOption,
    allOptionLabel,
    disabled,
    onRemove,
    labelDelimiter = ':',
}) => {
    const intl = useIntl();
    const [currentSelectedKeys, setCurrentSelectedKeys] = useState<string[]>(selectedKeys || []);
    const [pendingSelectedKeys, setPendingSelectedKeys] = useState<string[]>(selectedKeys || []);
    const allLabel = useMemo(() => allOptionLabel || intl.formatMessage(SreAgentResources.all), [allOptionLabel, intl]);

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

        return intl.formatMessage(IncidentManagementResources.selectedOutOfTotal, {
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

    return (
        <Pill
            label={label}
            ariaLabel={intl.formatMessage(SreAgentResources.pillFilterAriaLabel, {
                columnName: label,
                delimiter: labelDelimiter ? ` ${labelDelimiter} ` : '',
                filterValue: pillDisplayValue,
            })}
            value={displayValue || pillDisplayValue}
            onApply={onApplyClick}
            onCancelOrDismiss={() => initializeLocalState()}
            removeButtonAriaLabel={intl.formatMessage(SreAgentResources.pillFilterRemoveAriaLabel, { columnName: label })}
            onRemove={onRemove}
            disabled={disabled}
            labelDelimiter={labelDelimiter}
            valueMaxWidth={valueMaxWidth}
        >
            <ListWithSearch
                options={options}
                selectedKeys={pendingSelectedKeys}
                setSelectedKeys={setPendingSelectedKeys}
                multiSelect={multiSelect}
                addAllOption={addAllOption}
                allOptionLabel={allLabel}
                ariaLabel={intl.formatMessage(SreAgentResources.optionsListAriaLabel, { fieldName: label })}
                disabled={disabled}
            />
        </Pill>
    );
};

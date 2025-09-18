import { FC, useCallback, useState } from 'react';
import { PillFilterSetProps } from './Contracts';
import { DynamicPillFilter } from './DynamicPillFilter';
import { PillFilter } from './PillFilter';

export const PillFilterSet: FC<PillFilterSetProps> = ({ staticFilters, dynamicFilters, disabled }) => {
    const [selectedDynamicOptions, setSelectedDynamicOptions] = useState<string[]>([]);

    const onSelectedKeyChange = useCallback((index: number, key?: string) => {
        setSelectedDynamicOptions(previous => {
            if (index === -1) {
                return key === undefined ? [...previous] : [...previous, key];
            }

            const newSelection = [...previous];
            if (key === undefined) {
                newSelection.splice(index, 1);
            } else {
                newSelection[index] = key;
            }
            return newSelection;
        });
    }, []);

    if (!dynamicFilters?.length) {
        return null;
    }

    return (
        <>
            {staticFilters?.map(staticFilterProps => (
                <PillFilter {...staticFilterProps} />
            ))}
            {selectedDynamicOptions.map((optionKey, index) => (
                <DynamicPillFilter
                    key={index}
                    options={dynamicFilters.filter(option => option.key === optionKey || !selectedDynamicOptions.includes(option.key))}
                    selectedKey={optionKey}
                    onSelectedKeyChange={key => onSelectedKeyChange(index, key)}
                    disabled={disabled}
                />
            ))}
            {selectedDynamicOptions.length < dynamicFilters.length && (
                <DynamicPillFilter
                    key={selectedDynamicOptions.length}
                    options={dynamicFilters.filter(option => !selectedDynamicOptions.includes(option.key))}
                    onSelectedKeyChange={key => onSelectedKeyChange(-1, key)}
                    disabled={disabled}
                />
            )}
        </>
    );
};

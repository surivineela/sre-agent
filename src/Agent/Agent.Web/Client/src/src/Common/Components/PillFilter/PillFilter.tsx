import { FC } from 'react';
import { FilterProps } from './Contracts';
import { usePillFilter } from './Hooks/usePillFilter';
import { Pill } from './Pill';

export const PillFilter: FC<FilterProps> = props => {
    const hook = usePillFilter(props);

    if (!hook) {
        return null;
    }

    return (
        <Pill
            label={hook.label}
            ariaLabel={hook.ariaLabel}
            value={hook.displayValue || hook.pillDisplayValue}
            onApply={hook.onApplyClick}
            applyDisabled={!hook.isComplete}
            onCancelOrDismiss={hook.initializeLocalState}
            removeButtonAriaLabel={hook.removeButtonAriaLabel}
            disabled={hook.disabled}
            labelDelimiter={hook.labelDelimiter}
            valueMaxWidth={hook.valueMaxWidth}
            useInDialog={props.useInDialog}
            maxDialogPopoverHeight={props.maxDialogPopoverHeight}
        >
            {hook.onRenderPopoverContent()}
        </Pill>
    );
};

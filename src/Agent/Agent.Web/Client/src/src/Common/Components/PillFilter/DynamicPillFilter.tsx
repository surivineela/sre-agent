import { Dropdown, Field, Option } from '@fluentui/react-components';
import { Filter20Regular } from '@fluentui/react-icons';
import { FC, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { DynamicPillFilterProps } from './Contracts';
import { usePillFilter } from './Hooks/usePillFilter';
import { Pill } from './Pill';

export const DynamicPillFilter: FC<DynamicPillFilterProps> = props => {
    const intl = useIntl();
    const { options, selectedKey, onSelectedKeyChange, disabled } = props;

    const [currentSelectedKey, setCurrentSelectedKey] = useState<string | undefined>(selectedKey);
    const [pendingSelectedKey, setPendingSelectedKey] = useState<string | undefined>(selectedKey);

    const currentFilterProps = useMemo(() => {
        return options?.find(option => option.key === currentSelectedKey)?.props;
    }, [options, currentSelectedKey]);

    const pendingFilterProps = useMemo(() => {
        return options?.find(option => option.key === pendingSelectedKey)?.props;
    }, [options, pendingSelectedKey]);

    const currentFilterHook = usePillFilter(currentFilterProps);

    const pendingFilterHook = usePillFilter(pendingFilterProps);

    const onApplyClick = useCallback(() => {
        if (pendingSelectedKey) {
            if (pendingSelectedKey !== currentSelectedKey) {
                // Reset the current state when changing filter type
                currentFilterHook?.initializeLocalState();

                setCurrentSelectedKey(pendingSelectedKey);

                pendingFilterHook?.onApplyClick();
                onSelectedKeyChange(pendingSelectedKey);
            } else {
                pendingFilterHook?.onApplyClick();
            }
        }
    }, [
        pendingSelectedKey,
        currentSelectedKey,
        pendingFilterHook?.onApplyClick,
        onSelectedKeyChange,
        currentFilterHook?.initializeLocalState,
    ]);

    const onRemove = useMemo(() => {
        if (currentFilterHook?.onRemove) {
            return () => {
                currentFilterHook.onRemove();
                onSelectedKeyChange(undefined);
            };
        }
        return undefined;
    }, [currentFilterHook?.onRemove, onSelectedKeyChange]);

    const initializeLocalState = useCallback(() => {
        setCurrentSelectedKey(selectedKey);
        setPendingSelectedKey(selectedKey);
    }, [selectedKey]);

    const onCancelOrDismiss = useCallback(() => {
        currentFilterHook?.initializeLocalState();
        pendingFilterHook?.initializeLocalState();
        initializeLocalState();
    }, [currentFilterHook?.initializeLocalState, initializeLocalState, pendingFilterHook?.initializeLocalState]);

    const disableFilterDropdown = useMemo(() => {
        return options?.filter(option => option.key !== currentSelectedKey).length === 0;
    }, [options, currentSelectedKey]);

    const onRenderButtonContent = useMemo(() => {
        if (currentFilterProps) {
            return undefined;
        }
        return () => (
            <div style={{ display: 'flex', height: '100%', aspectRatio: 1, alignItems: 'center', justifyContent: 'center' }}>
                <Filter20Regular />
            </div>
        );
    }, [currentFilterProps]);

    useEffect(() => {
        initializeLocalState();
    }, [initializeLocalState]);

    return (
        <Pill
            label={currentFilterHook?.label || ''}
            ariaLabel={currentFilterHook ? currentFilterHook.ariaLabel : intl.formatMessage(SreAgentResources.addFilter)}
            onRenderButtonContent={onRenderButtonContent}
            value={currentFilterHook?.displayValue || currentFilterHook?.pillDisplayValue || ''}
            onApply={onApplyClick}
            applyDisabled={!pendingFilterHook?.isComplete}
            onCancelOrDismiss={onCancelOrDismiss}
            removeButtonAriaLabel={currentFilterHook?.removeButtonAriaLabel}
            onRemove={onRemove}
            disabled={currentFilterHook?.disabled || disabled}
            labelDelimiter={currentFilterHook?.labelDelimiter}
            valueMaxWidth={currentFilterHook?.valueMaxWidth}
        >
            {options?.length && (
                <Field label={intl.formatMessage(SreAgentResources.filterBy)} style={{ marginLeft: '16px', marginRight: '16px' }}>
                    <Dropdown
                        placeholder={intl.formatMessage(SreAgentResources.selectFilter)}
                        value={pendingFilterHook?.label || ''}
                        onOptionSelect={(_, data) => setPendingSelectedKey?.(data.optionValue as string)}
                        style={{ marginBottom: '8px' }}
                        disabled={disableFilterDropdown || disabled}
                    >
                        {options?.map(option => (
                            <Option key={option.key} onClick={() => setPendingSelectedKey?.(option.key)}>
                                {option.props.label}
                            </Option>
                        ))}
                    </Dropdown>
                </Field>
            )}
            {pendingFilterHook?.onRenderPopoverContent()}
        </Pill>
    );
};

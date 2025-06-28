import {
    Button,
    makeStyles,
    mergeClasses,
    Popover,
    PopoverSurface,
    PopoverTrigger,
    Spinner,
    Text,
    tokens,
    Tooltip,
} from '@fluentui/react-components';
import { CheckmarkFilled, Settings16Regular } from '@fluentui/react-icons';
import { memo, useCallback, useMemo, useState } from 'react';
import { IAgentModeSelectorProps } from '../Contracts/Activities';
import { useAgentMode } from '../Hooks/useAgentMode';

const useAgentModeSelectorStyles = makeStyles({
    popoverSurface: {
        width: '280px',
        padding: '12px',
        display: 'flex',
        flexDirection: 'column',
        boxShadow: '0 2px 8px rgba(0,0,0,0.15)',
    },
    sectionHeader: {
        fontWeight: 600,
        fontSize: '14px',
        marginBottom: '8px',
        paddingLeft: '2px',
        color: tokens.colorNeutralForeground1,
    },
    currentModeSection: {
        marginBottom: '12px',
        paddingBottom: '12px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    currentModeText: {
        fontSize: '13px',
        color: tokens.colorNeutralForeground2,
        marginBottom: '4px',
    },
    currentModeValue: {
        fontSize: '14px',
        fontWeight: 500,
        color: tokens.colorBrandForeground1,
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
    },
    modeItem: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        cursor: 'pointer',
        padding: '8px 6px',
        borderRadius: '4px',
        fontSize: '13px',
        marginBottom: '2px',
        '&:hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
        '&:last-child': {
            marginBottom: 0,
        },
    },
    modeItemSelected: {
        backgroundColor: tokens.colorBrandBackground2,
        color: tokens.colorBrandForeground1,
        '&:hover': {
            backgroundColor: tokens.colorBrandBackground2Hover,
        },
    },
    modeContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
        flex: 1,
    },
    modeName: {
        fontWeight: 500,
        fontSize: '13px',
    },
    modeDescription: {
        fontSize: '11px',
        color: tokens.colorNeutralForeground3,
        lineHeight: '1.3',
    },
    checkIcon: {
        fontSize: '14px',
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
    },
    restrictionMessage: {
        fontSize: '11px',
        color: tokens.colorPaletteRedForeground2,
        fontStyle: 'italic',
        marginTop: '8px',
        padding: '6px 8px',
        backgroundColor: tokens.colorPaletteRedBackground1,
        borderRadius: '4px',
    },
    loadingContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '12px',
    },
    errorMessage: {
        fontSize: '11px',
        color: tokens.colorPaletteRedForeground2,
        marginTop: '8px',
    },
});

const AgentModeSelector = ({
    threadId,
    currentAgentMode,
    onAgentModeChange,
    disabled = false,
    disabledReason,
}: IAgentModeSelectorProps) => {
    const {
        availableAgentModes,
        isLoading,
        error,
        updateThreadAgentMode,
        getAgentModeInfo,
        validateAgentModeChange,
        getEffectiveAgentMode,
    } = useAgentMode();

    console.log('[AgentModeSelector] Component state:', {
        availableAgentModes,
        isLoading,
        error,
        threadId,
        currentAgentMode,
    });

    const [open, setOpen] = useState(false);
    const [isUpdating, setIsUpdating] = useState(false);

    const {
        popoverSurface,
        sectionHeader,
        currentModeSection,
        currentModeText,
        currentModeValue,
        modeItem,
        modeItemSelected,
        modeContent,
        modeName,
        modeDescription,
        checkIcon,
        restrictionMessage,
        loadingContainer,
        errorMessage,
    } = useAgentModeSelectorStyles();

    const validationResult = useMemo(() => {
        return validateAgentModeChange(availableAgentModes);
    }, [availableAgentModes, validateAgentModeChange]);

    const effectiveCurrentMode = useMemo(() => {
        return getEffectiveAgentMode(currentAgentMode);
    }, [currentAgentMode, getEffectiveAgentMode]);

    const isButtonDisabled = disabled || !validationResult.isValid || isUpdating;

    const handleModeClick = useCallback(
        async (mode: string) => {
            if (!threadId || mode === effectiveCurrentMode || isUpdating) {
                return;
            }

            setIsUpdating(true);
            try {
                const updatedThread = await updateThreadAgentMode(threadId, mode);
                // Call onAgentModeChange with the updated thread object from the API response
                if (updatedThread) {
                    onAgentModeChange?.(updatedThread);
                }
                setOpen(false);
            } catch (error) {
                // Error is already handled in the hook
                console.error('Failed to update agent mode:', error);
            } finally {
                setIsUpdating(false);
            }
        },
        [threadId, effectiveCurrentMode, isUpdating, updateThreadAgentMode, onAgentModeChange]
    );

    const getTooltipText = useCallback(() => {
        if (disabledReason) {
            return disabledReason;
        }
        if (!validationResult.isValid) {
            return validationResult.errorMessage || 'Agent mode changes are restricted';
        }
        return 'Change agent mode for this thread';
    }, [disabledReason, validationResult]);

    const renderPopoverContent = useCallback(() => {
        if (isLoading) {
            return (
                <div className={loadingContainer}>
                    <Spinner size="small" />
                </div>
            );
        }

        if (error) {
            return (
                <div>
                    <Text className={sectionHeader}>Agent Mode</Text>
                    <div className={errorMessage}>Error: {error}</div>
                </div>
            );
        }

        const currentModeInfo = getAgentModeInfo(effectiveCurrentMode);

        return (
            <>
                <div className={currentModeSection}>
                    <Text className={sectionHeader}>Current Mode</Text>
                    <div className={currentModeText}>Active agent mode for this thread:</div>
                    <div className={currentModeValue}>
                        <CheckmarkFilled className={checkIcon} />
                        {currentModeInfo.displayName}
                    </div>
                </div>

                <div>
                    <Text className={sectionHeader}>Available Modes</Text>
                    {validationResult.availableModes.map(mode => {
                        const modeInfo = getAgentModeInfo(mode);
                        const isSelected = mode === effectiveCurrentMode;
                        const itemClasses = mergeClasses(modeItem, isSelected ? modeItemSelected : undefined);

                        return (
                            <div
                                key={mode}
                                className={itemClasses}
                                onClick={() => !isSelected && handleModeClick(mode)}
                                style={{ cursor: isSelected ? 'default' : 'pointer' }}
                            >
                                <div className={modeContent}>
                                    <div className={modeName}>{modeInfo.displayName}</div>
                                    <div className={modeDescription}>{modeInfo.description}</div>
                                </div>
                                {isSelected && <CheckmarkFilled className={checkIcon} />}
                            </div>
                        );
                    })}

                    {!validationResult.isValid && validationResult.errorMessage && (
                        <div className={restrictionMessage}>{validationResult.errorMessage}</div>
                    )}
                </div>
            </>
        );
    }, [
        isLoading,
        error,
        effectiveCurrentMode,
        validationResult,
        getAgentModeInfo,
        handleModeClick,
        loadingContainer,
        sectionHeader,
        errorMessage,
        currentModeSection,
        currentModeText,
        currentModeValue,
        checkIcon,
        modeItem,
        modeItemSelected,
        modeContent,
        modeName,
        modeDescription,
        restrictionMessage,
    ]);

    return (
        <Popover positioning={'after-top'} open={open} onOpenChange={(_, data) => setOpen(data.open)}>
            <PopoverTrigger>
                <Tooltip content={getTooltipText()} relationship="label">
                    <Button
                        style={{ fontSize: '13px', padding: '2px 4px', paddingRight: '8px' }}
                        appearance="outline"
                        icon={isUpdating ? <Spinner size="tiny" /> : <Settings16Regular />}
                        onClick={() => !isButtonDisabled && setOpen(!open)}
                        disabled={isButtonDisabled}
                    >
                        Agent Mode
                    </Button>
                </Tooltip>
            </PopoverTrigger>
            <PopoverSurface className={popoverSurface}>{renderPopoverContent()}</PopoverSurface>
        </Popover>
    );
};

export default memo(AgentModeSelector);

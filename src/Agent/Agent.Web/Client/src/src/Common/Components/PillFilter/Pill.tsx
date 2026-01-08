import {
    Button,
    DialogActions,
    GriffelStyle,
    makeStyles,
    Popover,
    PopoverSurface,
    tokens,
    useRestoreFocusSource,
    useRestoreFocusTarget,
} from '@fluentui/react-components';
import { Dismiss16Filled } from '@fluentui/react-icons';
import { FC, PropsWithChildren, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { DirtyStateContext } from '../../../Space/Contracts/Context';
import { DirtyStateOnClickConfirmationWrapper } from '../../../Space/IncidentManagement/CreateIncidentHandler/DirtyStateConfirmationDialog';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { PillProps } from './Contracts';

const buttonDisabledStyles: GriffelStyle = {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    background: `${tokens.colorNeutralBackgroundDisabled} !important`,
    color: `${tokens.colorNeutralForegroundDisabled} !important`,
};

const buttonStyles: GriffelStyle = {
    borderRadius: `${tokens.borderRadiusCircular} !important`,
    padding: '0px',
    height: '32px',
    whiteSpace: 'nowrap',
    textOverflow: 'ellipsis',
    overflow: 'hidden',
    minWidth: 'fit-content',
    border: `1px solid ${tokens.colorBrandStroke2}`,
    color: `${tokens.colorNeutralForeground1} !important`,
    background: `${tokens.colorBrandBackground2} !important`,
    '&:hover': {
        border: `1px solid ${tokens.colorBrandStroke2}`,
        background: `${tokens.colorBrandBackground2Hover} !important`,
    },
    '&:disabled': buttonDisabledStyles,
    '&:disabled:hover': buttonDisabledStyles,
};

const usePillStyles = makeStyles({
    root: {
        display: 'flex',
        position: 'relative',
    },
    button: buttonStyles,
    buttonContent: {
        display: 'flex',
        padding: '0px 12px 0px 12px',
    },
    removableButtonContent: {
        display: 'flex',
        padding: '0px 32px 0px 12px',
    },
    fieldLabel: {
        color: 'inherit',
        fontWeight: 400,
    },
    fieldValue: {
        color: 'inherit',
        fontWeight: 600,
        overflowX: 'hidden',
        textOverflow: 'ellipsis',
    },
    closeButton: {
        position: 'absolute',
        top: '2px',
        right: '2px',
        height: '28px !important',
        width: '28px !important',
        minWidth: 'unset',
        padding: '0px',
        borderRadius: `${tokens.borderRadiusCircular} !important`,
        background: 'transparent !important',
        zIndex: 1,
    },
    surface: {
        zIndex: 100,
        paddingLeft: '0px',
        paddingRight: '0px',
        display: 'flex',
        flexDirection: 'column',
        overflowY: 'hidden',
    },
    body: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        paddingBottom: '16px',
        overflowY: 'hidden',
    },
    dialogActions: {
        justifySelf: 'start',
        paddingLeft: '16px',
        paddingRight: '16px',
    },
    filterDropdown: {
        justifySelf: 'start',
        paddingLeft: '16px',
        paddingRight: '16px',
    },
    portalContainer: {
        zIndex: '1000',
    },
});

export const Pill: FC<PropsWithChildren<PillProps>> = ({
    label,
    ariaLabel,
    value,
    onApply,
    applyDisabled,
    applyLabel,
    disabled,
    cancelLabel,
    onCancelOrDismiss,
    removeButtonAriaLabel,
    onRemove,
    onRenderButtonContent,
    children,
    maxDialogPopoverHeight,
    labelDelimiter = ':',
    valueMaxWidth = 200,
    useInDialog = false,
    blockOnDirtyContext = false,
}) => {
    const { isDirty } = useContext(DirtyStateContext);
    const intl = useIntl();
    const styles = usePillStyles();
    const [dialogOpen, setDialogOpen] = useState(false);
    const buttonRef = useRef<HTMLDivElement>(null);
    const restoreFocusSourceAttributes = useRestoreFocusSource();
    const restoreFocusTargetAttributes = useRestoreFocusTarget();

    const buttonContent = useMemo(() => {
        const buttonContentClass = onRemove ? styles.removableButtonContent : styles.buttonContent;
        return onRenderButtonContent ? (
            onRenderButtonContent({
                label,
                value,
                contentClass: buttonContentClass,
                labelClass: styles.fieldLabel,
                valueClass: styles.fieldValue,
            })
        ) : (
            <div className={buttonContentClass}>
                <div className={styles.fieldLabel}>
                    {label}
                    {labelDelimiter && <span>&nbsp;{labelDelimiter}</span>}
                    &nbsp;
                </div>
                <div className={styles.fieldValue} style={valueMaxWidth ? { maxWidth: valueMaxWidth } : undefined}>
                    {value}
                </div>
            </div>
        );
    }, [
        onRemove,
        onRenderButtonContent,
        label,
        value,
        styles.buttonContent,
        styles.removableButtonContent,
        styles.fieldLabel,
        styles.fieldValue,
        labelDelimiter,
        valueMaxWidth,
    ]);

    const [maxPopoverHeight, setMaxPopoverHeight] = useState<string>('calc(100vh - 200px)');

    useEffect(() => {
        if (buttonRef.current) {
            const rect = buttonRef.current?.getBoundingClientRect();
            setMaxPopoverHeight(`calc(100vh - ${rect.bottom + 44}px)`);
        }
    }, [buttonRef]);

    return (
        <>
            <div className={styles.root} ref={buttonRef}>
                <Button
                    {...restoreFocusTargetAttributes}
                    appearance="transparent"
                    className={styles.button}
                    onClick={() => setDialogOpen(currentOpen => !currentOpen)}
                    disabled={disabled}
                    aria-label={ariaLabel}
                >
                    {buttonContent}
                </Button>
                {onRemove && (
                    <Button
                        appearance="transparent"
                        className={styles.closeButton}
                        icon={<Dismiss16Filled />}
                        onClick={onRemove}
                        disabled={disabled}
                        aria-label={removeButtonAriaLabel}
                    />
                )}
            </div>
            <Popover
                {...restoreFocusSourceAttributes}
                trapFocus={true}
                withArrow={true}
                open={dialogOpen}
                onOpenChange={(_, data) => {
                    if (!data.open) {
                        onCancelOrDismiss?.();
                    }
                    setDialogOpen(data.open);
                }}
                positioning={{
                    target: buttonRef.current,
                    position: 'below',
                    align: 'start',
                }}
                {...(!useInDialog && { mountNode: { className: styles.portalContainer } })}
            >
                <PopoverSurface
                    className={styles.surface}
                    style={{
                        maxHeight: maxDialogPopoverHeight ? maxDialogPopoverHeight : maxPopoverHeight,
                        ...(useInDialog && { zIndex: 2000000 }),
                    }}
                >
                    <div className={styles.body}>{children}</div>
                    <DialogActions className={styles.dialogActions}>
                        <DirtyStateOnClickConfirmationWrapper isDirty={isDirty && blockOnDirtyContext}>
                            <Button
                                appearance="primary"
                                disabled={applyDisabled}
                                onClick={() => {
                                    onApply();
                                    setDialogOpen(false);
                                }}
                            >
                                {applyLabel || intl.formatMessage(SreAgentResources.apply)}
                            </Button>
                        </DirtyStateOnClickConfirmationWrapper>
                        <Button
                            appearance="secondary"
                            onClick={() => {
                                onCancelOrDismiss?.();
                                setDialogOpen(false);
                            }}
                        >
                            {cancelLabel || intl.formatMessage(SreAgentResources.cancel)}
                        </Button>
                    </DialogActions>
                </PopoverSurface>
            </Popover>
        </>
    );
};

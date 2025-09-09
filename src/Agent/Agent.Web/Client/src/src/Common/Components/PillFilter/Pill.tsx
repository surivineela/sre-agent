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
import { Dismiss24Filled } from '@fluentui/react-icons';
import { FC, PropsWithChildren, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../../Strings/SREAgentResources';

const buttonDisabledStyles: GriffelStyle = {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    background: `${tokens.colorNeutralBackgroundDisabled} !important`,
    color: `${tokens.colorNeutralForegroundDisabled} !important`,
};

const buttonStyles: GriffelStyle = {
    borderRadius: `${tokens.borderRadiusCircular} !important`,
    padding: '0px 12px 0px 12px',
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
    removableButton: {
        ...buttonStyles,
        padding: '0px 40px 0px 12px',
    },
    fieldLabel: {
        color: 'inherit',
        fontWeight: 400,
    },
    fieldValue: {
        color: 'inherit',
        fontWeight: 600,
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
    },
    body: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        paddingBottom: '16px',
    },
    dialogActions: {
        justifySelf: 'start',
        paddingLeft: '16px',
        paddingRight: '16px',
    },
    portalContainer: {
        zIndex: '1000',
    },
});

export interface PillProps {
    label: string;
    ariaLabel?: string;
    value: string;
    onApply: () => void;
    applyDisabled?: boolean;
    applyLabel?: string;
    disabled?: boolean;
    cancelLabel?: string;
    onCancelOrDismiss?: () => void;
    removeButtonAriaLabel?: string;
    onRemove?: () => void;
    showColon?: boolean;
}

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
    children,
    showColon = true,
}) => {
    const intl = useIntl();
    const styles = usePillStyles();
    const [dialogOpen, setDialogOpen] = useState(false);
    const buttonRef = useRef<HTMLDivElement>(null);
    const restoreFocusSourceAttributes = useRestoreFocusSource();
    const restoreFocusTargetAttributes = useRestoreFocusTarget();

    return (
        <>
            <div className={styles.root} ref={buttonRef}>
                <Button
                    {...restoreFocusTargetAttributes}
                    appearance="transparent"
                    className={onRemove ? styles.removableButton : styles.button}
                    onClick={() => setDialogOpen(currentOpen => !currentOpen)}
                    disabled={disabled}
                    aria-label={ariaLabel}
                >
                    <div className={styles.fieldLabel}>
                        {label}
                        {showColon ? ':' : ''}&nbsp;
                    </div>
                    <div className={styles.fieldValue}>{value}</div>
                </Button>
                {onRemove && (
                    <Button
                        appearance="transparent"
                        className={styles.closeButton}
                        icon={<Dismiss24Filled />}
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
                mountNode={{ className: styles.portalContainer }}
            >
                <PopoverSurface className={styles.surface}>
                    <div className={styles.body}>{children}</div>
                    <DialogActions className={styles.dialogActions}>
                        <Button
                            appearance="primary"
                            onClick={() => {
                                onApply();
                                setDialogOpen(false);
                            }}
                            disabled={applyDisabled}
                        >
                            {applyLabel || intl.formatMessage(SreAgentResources.apply)}
                        </Button>
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

import { DefaultButton, PrimaryButton } from '@fluentui/react/lib/Button';
import { Dialog, DialogFooter, DialogType, IDialogProps } from '@fluentui/react/lib/Dialog';
import * as React from 'react';
import {
    buttonDivStyles,
    buttonStyles,
    leftButtonDivStyles,
    modalContentStyles,
    modalFooterStyles,
    modalStyles,
} from './ConfirmDialog.styles';

interface ConfirmDialogProps {
    primaryActionButton: {
        title: string;
        disabled?: boolean;
        onClick: () => void;
        className?: string;
    };
    title: string;
    content: string | JSX.Element | JSX.Element[];
    onDismiss: () => void;
    defaultActionButton?: {
        title: string;
        disabled?: boolean;
        onClick: () => void;
    };
    showCloseModal?: any;
    modalStyles?: any;
    showPrimaryButtonToTheRight?: boolean;
    showOnlyPrimaryButton?: boolean;
}

const ConfirmDialog: React.FC<ConfirmDialogProps & Omit<IDialogProps, 'title'>> = props => {
    const {
        primaryActionButton,
        defaultActionButton,
        hidden,
        title,
        content,
        onDismiss,
        showCloseModal,
        modalStyles: customModalStyles,
        showPrimaryButtonToTheRight,
        showOnlyPrimaryButton,
    } = props;

    return (
        <Dialog
            hidden={hidden}
            dialogContentProps={{
                title,
                type: (showCloseModal ?? true) ? DialogType.close : DialogType.normal,
                styles: modalContentStyles,
            }}
            modalProps={{
                styles: customModalStyles || modalStyles,
                isBlocking: true,
            }}
            onDismiss={onDismiss}
        >
            {content}
            {showOnlyPrimaryButton ? (
                <DialogFooter styles={modalFooterStyles}>
                    <div style={buttonDivStyles}>
                        <div style={leftButtonDivStyles}>
                            <PrimaryButton
                                onClick={primaryActionButton.onClick}
                                text={primaryActionButton.title}
                                disabled={!!primaryActionButton.disabled}
                                className={primaryActionButton.className}
                            />
                        </div>
                    </div>
                </DialogFooter>
            ) : (
                <DialogFooter styles={modalFooterStyles}>
                    {showPrimaryButtonToTheRight ? (
                        <div style={buttonDivStyles}>
                            <div style={leftButtonDivStyles}>
                                <DefaultButton
                                    onClick={defaultActionButton?.onClick}
                                    text={defaultActionButton?.title}
                                    disabled={!!defaultActionButton?.disabled}
                                    styles={buttonStyles}
                                />
                            </div>
                            <PrimaryButton
                                onClick={primaryActionButton.onClick}
                                text={primaryActionButton.title}
                                disabled={!!primaryActionButton.disabled}
                                className={primaryActionButton.className}
                            />
                        </div>
                    ) : (
                        <div style={buttonDivStyles}>
                            <div style={leftButtonDivStyles}>
                                <PrimaryButton
                                    onClick={primaryActionButton.onClick}
                                    text={primaryActionButton.title}
                                    disabled={!!primaryActionButton.disabled}
                                    className={primaryActionButton.className}
                                />
                            </div>
                            <DefaultButton
                                onClick={defaultActionButton?.onClick}
                                text={defaultActionButton?.title}
                                disabled={!!defaultActionButton?.disabled}
                                styles={buttonStyles}
                            />
                        </div>
                    )}
                </DialogFooter>
            )}
        </Dialog>
    );
};

export default ConfirmDialog;

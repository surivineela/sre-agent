import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
} from '@fluentui/react-components';
import { cloneElement, FC, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../../Strings/SREAgentResources';

interface DirtyStateDialogSurfaceProps {
    onConfirm: () => void;
    onCancel?: () => void;
}

const DirtyStateDialogSurface: FC<DirtyStateDialogSurfaceProps> = ({ onConfirm, onCancel }) => {
    const intl = useIntl();
    return (
        <DialogSurface>
            <DialogBody>
                <DialogTitle>{intl.formatMessage(SreAgentResources.dirtyStateConfirmationTitle)}</DialogTitle>
                <DialogContent>{intl.formatMessage(SreAgentResources.dirtyStateConfirmationMessage)}</DialogContent>
                <DialogActions>
                    <DialogTrigger>
                        <Button appearance="primary" onClick={onConfirm}>
                            {intl.formatMessage(SreAgentResources.discard)}
                        </Button>
                    </DialogTrigger>
                    <DialogTrigger disableButtonEnhancement>
                        <Button appearance="secondary" onClick={onCancel || (() => {})}>
                            {intl.formatMessage(SreAgentResources.keepWorking)}
                        </Button>
                    </DialogTrigger>
                </DialogActions>
            </DialogBody>
        </DialogSurface>
    );
};
interface DirtyStateConfirmationDialogPropsCommon extends DirtyStateDialogSurfaceProps {
    isDirty: boolean;
}

export interface DirtyStateConfirmationWrapperProps extends DirtyStateConfirmationDialogPropsCommon {
    children: React.ReactElement<any, string | React.JSXElementConstructor<any>>;
}

export const DirtyStateConfirmationWrapper: FC<DirtyStateConfirmationWrapperProps> = ({ isDirty, onConfirm, onCancel, children }) => {
    if (!isDirty) {
        // The component is not dirty so we can return the child directly, but we need to update the child's onClick to call onConfirm
        const updatedChildrenProps = {
            ...children.props,
            onClick: () => {
                if (children.props.onClick) {
                    children.props.onClick();
                }
                onConfirm();
            },
        };
        return cloneElement(children, updatedChildrenProps);
    }

    // The component is dirty, so we need to show the confirmation dialog
    return (
        <Dialog modalType="alert">
            <DialogTrigger disableButtonEnhancement>{children}</DialogTrigger>
            <DirtyStateDialogSurface onConfirm={onConfirm} onCancel={onCancel} />
        </Dialog>
    );
};

export interface DirtyStateConfirmationDialogProps extends DirtyStateConfirmationDialogPropsCommon {
    condition: boolean;
}

export const DirtyStateConfirmationDialog: FC<DirtyStateConfirmationDialogProps> = ({ isDirty, onConfirm, onCancel, condition }) => {
    return (
        <Dialog modalType="alert" open={isDirty && condition}>
            <DirtyStateDialogSurface onConfirm={onConfirm} onCancel={onCancel} />
        </Dialog>
    );
};

export interface DirtyStateOnChangeConfirmationWrapperProps {
    isDirty: boolean;
    children: React.ReactElement<any, string | React.JSXElementConstructor<any>>;
}

export const DirtyStateOnChangeConfirmationWrapper: FC<DirtyStateOnChangeConfirmationWrapperProps> = ({ isDirty, children }) => {
    const [pendingConfirmation, setPendingConfirmation] = useState(false);
    const [onConfirmArgs, setOnConfirmArgs] = useState<any>(null);

    const updatedChildrenProps = useMemo(
        () => ({
            ...children.props,
            onChange: (...args: any) => {
                setPendingConfirmation(true);
                setOnConfirmArgs(args);
            },
        }),
        [children.props]
    );

    const onConfirm = useCallback(() => {
        if (pendingConfirmation && children.props.onChange) {
            children.props.onChange(...onConfirmArgs);
        }
        setPendingConfirmation(false);
        setOnConfirmArgs(null);
    }, [pendingConfirmation, children.props.onChange, onConfirmArgs]);

    const onCancel = useCallback(() => {
        setPendingConfirmation(false);
        setOnConfirmArgs(null);
    }, []);

    const clonedChildren = useMemo(() => cloneElement(children, updatedChildrenProps), [children, updatedChildrenProps]);

    if (!isDirty) {
        // The component is not dirty so we can return the child directly
        return children;
    }

    // The component is dirty, so we need to show the confirmation dialog
    return (
        <Dialog modalType="alert">
            <DialogTrigger disableButtonEnhancement>{clonedChildren}</DialogTrigger>
            <DirtyStateDialogSurface onConfirm={onConfirm} onCancel={onCancel} />
        </Dialog>
    );
};

export interface DirtyStateOnClickConfirmationWrapperProps {
    isDirty: boolean;
    children: React.ReactElement<any, string | React.JSXElementConstructor<any>>;
}

export const DirtyStateOnClickConfirmationWrapper: FC<DirtyStateOnClickConfirmationWrapperProps> = ({ isDirty, children }) => {
    const [pendingConfirmation, setPendingConfirmation] = useState(false);
    const [onConfirmArgs, setOnConfirmArgs] = useState<any>(null);

    const updatedChildrenProps = useMemo(
        () => ({
            ...children.props,
            onClick: (...args: any) => {
                setPendingConfirmation(true);
                setOnConfirmArgs(args);
            },
        }),
        [children.props]
    );

    const onConfirm = useCallback(() => {
        if (pendingConfirmation && children.props.onClick) {
            children.props.onClick(...onConfirmArgs);
        }
        setPendingConfirmation(false);
        setOnConfirmArgs(null);
    }, [pendingConfirmation, children.props.onClick, onConfirmArgs]);

    const onCancel = useCallback(() => {
        setPendingConfirmation(false);
        setOnConfirmArgs(null);
    }, []);

    const clonedChildren = useMemo(() => cloneElement(children, updatedChildrenProps), [children, updatedChildrenProps]);

    if (!isDirty) {
        // The component is not dirty so we can return the child directly
        return children;
    }
    // The component is dirty, so we need to show the confirmation dialog
    return (
        <Dialog modalType="alert">
            <DialogTrigger disableButtonEnhancement>{clonedChildren}</DialogTrigger>
            <DirtyStateDialogSurface onConfirm={onConfirm} onCancel={onCancel} />
        </Dialog>
    );
};

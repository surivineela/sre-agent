import type {
    DialogActionsProps,
    DialogBodyProps,
    DialogContentProps,
    DialogSurfaceProps,
    DialogTitleProps,
    DialogProps as FluentDialogProps,
} from '@fluentui/react-components';
import {
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    Divider,
    Dialog as FluentDialog,
    mergeClasses,
    Subtitle1,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import type { ComponentProps, FormEvent, JSX, Key, ReactElement, ReactNode } from 'react';
import { forwardRef, Fragment, useCallback, useId, useMemo, useState } from 'react';
import { flushSync } from 'react-dom';
import useIntl from 'react-intl/src/components/useIntl';
import { ThreadTraceResources } from '../../../../../../Strings/SREAgentResources';
import { IMessageService } from '../../../../packages/core/contexts/message/src/IMessageService';
import { MessageProvider } from '../../../../packages/core/contexts/message/src/MessageProvider';
import type { ButtonProps } from '../Button/Button';
import { Button } from '../Button/Button';
import { LazyDialogContext, useLazyDialogContext } from '../LazyDialogTrigger/LazyDialogContext';
import { MessageBarWithContext } from '../MessageBarWithContext/MessageBarWithContext';
import { useDialogStyles } from './Dialog.Styles';

export type TriggerButtonProps = Omit<ButtonProps, 'onClick'>;

export type DialogButtonProps = (ButtonProps | { render: () => ReactElement }) & {
    /**
     * If true, the dialog's onClose callback will be called when clicked.
     * Focus will be properly restored to the dialog after the close button is clicked.
     */
    closeOnClick?: boolean;
};

export type AdditionalDialogButtonProps = DialogButtonProps & {
    key: Key;
};

interface IFormProps {
    /**
     * A callback function that will be called when the form is submitted.
     */
    onSubmit: (event: FormEvent<HTMLFormElement>) => Promise<void> | void;

    /**
     * Whether to close the dialog when form submission completed successfully.
     * If `onSubmit` returns a promise, it will wait for the promise to resolve before closing the dialog.
     * If `onSubmit` returns void, it will close the dialog immediately.
     * @default false
     */
    closeOnSubmitSuccess?: boolean;

    /**
     * Whether to close the dialog when form submission failed.
     * It will only close the dialog if `onSubmit` returns a rejected promise.
     * @default false
     */
    closeOnSubmitError?: boolean;
}

interface IBaseDialogProps {
    messageService?: IMessageService;
    size: 'nano' | 'extra-small' | 'small' | 'medium' | 'large';
    /**
     * Controls whether the dialog is open or closed. If set to true or false, the dialog is controlled by the parent component;
     * the dialog won't be closed automatically when clicking on any closeOnClick action button or the top-right close button,
     * and it won't be opened automatically when the trigger button is clicked. Use the `onClose` prop to handle the closing of the dialog.
     */
    isOpen?: boolean;
    /**
     * Called when a button with closeOnClick is clicked or when the close button is clicked if showCloseButton is true
     */
    onClose?: () => void;
    /**
     * Shows the close button in the dialog header
     */
    showCloseButton?: boolean;
    /**
     * The title of the dialog
     */
    title: string;
    /**
     * Optional, additional component to render after title
     */
    titleActions?: ReactElement;
    /**
     * Description shown directly under the title
     */
    description?: string | JSX.Element;
    /**
     * Configuration for the trigger button that opens the dialog.
     * If not provided, the button will not be rendered.
     * Required for accessibility if focusRestorationRef is not set.
     */
    triggerButtonProps?: TriggerButtonProps;
    /**
     * Element to restore focus to when the dialog is closed.
     * Required for accessibility if triggerButtonProps is not set.
     * This should always be the element that was focused before the dialog was opened.
     */
    focusRestorationRef?: React.RefObject<HTMLElement>;
    /**
     * Configuration for the primary action button
     */
    primaryButtonProps?: DialogButtonProps;
    /**
     * Configuration for the secondary action button
     */
    secondaryButtonProps?: DialogButtonProps;
    /**
     * Additional buttons to display in the dialog actions
     */
    additionalButtons?: AdditionalDialogButtonProps[];
    /**
     * Props to pass to the Fluent DialogSurface component
     */
    dialogSurfaceProps?: Pick<DialogSurfaceProps, 'className'>;
    /**
     * Props to pass to the Fluent DialogBody component
     */
    dialogBodyProps?: Pick<DialogBodyProps, 'className'>;
    /**
     * Props to pass to the Fluent DialogTitle component
     */
    dialogTitleProps?: Pick<DialogTitleProps, 'className'>;
    /**
     * Props to pass to the Fluent DialogContent component
     */
    dialogContentProps?: Pick<DialogContentProps, 'className'>;
    /**
     * Props to pass to the Fluent DialogActions component
     */
    dialogActionsProps?: Pick<DialogActionsProps, 'className'>;
    /**
     * Children to render inside the dialog body
     */
    children?: ReactNode;
    /**
     * If true, the dialog will not be closed if the user presses the escape key or clicks outside the dialog.
     */
    disableLightDismiss?: boolean;
    /**
     * If set, the dialog will be rendered inside a form element.
     * This is useful to allow user to press Enter to submit the form.
     * Recommended to use with primaryButtonProps.type='submit' and without primaryButtonProps.onClick handler.
     */
    formProps?: IFormProps;
    /**
     * Telemetry activity name for tracking dialog events
     */
    // telemetryActivity: TelemetryActivity;
    /**
     * Custom properties to include in telemetry events
     */
    // telemetryCustomProperties?: ICustomProperties;
    /**
     * @deprecated Marking as deprecated to discourage usage.
     * In most cases this should not be used. Internal trigger with triggerButtonProps is preferred.
     */
    notRecommended_externalTriggerExists?: boolean;
}

type IBaseDialogPropsWithTrigger = Omit<IBaseDialogProps, 'triggerButtonProps'> & Required<Pick<IBaseDialogProps, 'triggerButtonProps'>>;
type IBaseDialogPropsWithFocusRestorationRef = Omit<IBaseDialogProps, 'focusRestorationRef'> &
    Required<Pick<IBaseDialogProps, 'focusRestorationRef'>>;
type IBaseDialogPropsWithExternalTrigger = Omit<IBaseDialogProps, 'notRecommended_externalTriggerExists'> & {
    notRecommended_externalTriggerExists: true;
};

export type IDialogProps = IBaseDialogPropsWithTrigger | IBaseDialogPropsWithFocusRestorationRef | IBaseDialogPropsWithExternalTrigger;

export const Dialog = forwardRef<HTMLDivElement, IDialogProps>(
    (
        {
            messageService,
            size,
            isOpen,
            onClose,
            showCloseButton = false,
            title,
            titleActions,
            description,
            triggerButtonProps,
            primaryButtonProps,
            secondaryButtonProps,
            additionalButtons,
            dialogSurfaceProps,
            dialogBodyProps,
            dialogTitleProps,
            dialogContentProps,
            dialogActionsProps,
            children,
            disableLightDismiss,
            formProps,
            focusRestorationRef,
        },
        ref
    ): JSX.Element => {
        const intl = useIntl();
        const styles = useDialogStyles();

        const { isOpen: lazyDialogIsOpen, onOpenChange: lazyDialogOnOpenChange } = useLazyDialogContext();

        const [isOpenUncontrolled, setIsOpenUncontrolled] = useState(false);

        const handleDialogOpen = useCallback(() => {
            setIsOpenUncontrolled(true);
            lazyDialogOnOpenChange?.(true);
        }, [lazyDialogOnOpenChange]);

        const handleDialogClose = useCallback(() => {
            onClose?.();
            setIsOpenUncontrolled(false);
            lazyDialogOnOpenChange?.(false);
            flushSync(() => {
                focusRestorationRef?.current?.focus();
            });
        }, [focusRestorationRef, onClose, lazyDialogOnOpenChange]);

        const handleOnOpenChange = useCallback<NonNullable<FluentDialogProps['onOpenChange']>>(
            (_event, data) => {
                if (data.open) {
                    handleDialogOpen();
                } else {
                    // If light dismiss is disabled and this is a backdrop click or escape key press, do nothing
                    if (disableLightDismiss && (data.type === 'backdropClick' || data.type === 'escapeKeyDown')) {
                        return;
                    }
                    handleDialogClose();
                }
            },
            [handleDialogOpen, disableLightDismiss, handleDialogClose]
        );

        const FormWrapper = formProps ? 'form' : Fragment;

        const handleFormSubmit = useCallback(
            (event: FormEvent<HTMLFormElement>) => {
                event.preventDefault();
                Promise.resolve(formProps?.onSubmit(event))
                    .then(() => {
                        if (formProps?.closeOnSubmitSuccess) {
                            handleDialogClose();
                        }
                    })
                    .catch(() => {
                        if (formProps?.closeOnSubmitError) {
                            handleDialogClose();
                        }
                    });
            },
            [formProps, handleDialogClose]
        );

        const formWrapperProps = useMemo(() => {
            if (formProps === undefined) {
                return {};
            }

            const props: ComponentProps<'form'> = {
                autoComplete: 'off', // unless it is truly necessary, all form fields should have autoComplete disabled default
                noValidate: true,
                onSubmit: handleFormSubmit,
            };

            return props;
        }, [formProps, handleFormSubmit]);

        const titleId = useId();

        const resetLazyDialogContext = useMemo(() => ({}), []);

        return (
            // Wrap the dialog in a new LazyDialogContext provider to reset the context.
            // This is necessary so that any possible nested dialogs do not think they are lazy dialogs.
            <LazyDialogContext.Provider value={resetLazyDialogContext}>
                <FluentDialog onOpenChange={handleOnOpenChange} open={lazyDialogIsOpen ?? isOpen ?? isOpenUncontrolled}>
                    {triggerButtonProps ? (
                        <DialogTrigger disableButtonEnhancement={true}>
                            <Button {...triggerButtonProps} />
                        </DialogTrigger>
                    ) : (
                        <></>
                    )}
                    <DialogSurface
                        {...dialogSurfaceProps}
                        ref={ref}
                        aria-labelledby={titleId}
                        className={mergeClasses(
                            dialogSurfaceProps?.className,
                            styles.dialogSurface,
                            size === 'nano' && styles.dialogSurfaceNano,
                            size === 'extra-small' && styles.dialogSurfaceExtraSmall,
                            size === 'small' && styles.dialogSurfaceSmall,
                            size === 'medium' && styles.dialogSurfaceMedium,
                            size === 'large' && styles.dialogSurfaceLarge
                        )}
                    >
                        <MessageProvider messageService={messageService}>
                            <FormWrapper {...formWrapperProps}>
                                <DialogBody {...dialogBodyProps}>
                                    {title ? (
                                        <DialogTitle
                                            as={titleActions ? 'div' : 'h1'}
                                            className={mergeClasses(dialogTitleProps?.className, styles.dialogTitle)}
                                            id={titleActions ? undefined : titleId}
                                            {...dialogTitleProps}
                                        >
                                            {titleActions ? (
                                                <div className={styles.titleWithActions}>
                                                    <Subtitle1 as="h1" id={titleId}>
                                                        {title}
                                                    </Subtitle1>
                                                    {titleActions}
                                                    {showCloseButton && (
                                                        <div className={styles.closeButtonAndDivider}>
                                                            <Divider vertical={true} className={styles.titleDivider} />
                                                            <DialogTrigger action="close">
                                                                <Button
                                                                    appearance="subtle"
                                                                    aria-label={intl.formatMessage(ThreadTraceResources.close)}
                                                                    icon={<Dismiss24Regular aria-hidden={true} />}
                                                                />
                                                            </DialogTrigger>
                                                        </div>
                                                    )}
                                                </div>
                                            ) : (
                                                title
                                            )}
                                        </DialogTitle>
                                    ) : null}

                                    <DialogContent
                                        className={mergeClasses(dialogContentProps?.className, styles.dialogContent)}
                                        {...dialogContentProps}
                                    >
                                        {description != null && description !== '' ? <div>{description}</div> : null}
                                        <MessageBarWithContext />
                                        {children}
                                    </DialogContent>
                                    {(primaryButtonProps ?? secondaryButtonProps ?? additionalButtons) ? (
                                        <>
                                            <div className={styles.divider} />
                                            <DialogActions
                                                {...dialogActionsProps}
                                                className={mergeClasses(dialogActionsProps?.className, styles.dialogActions)}
                                                fluid={true}
                                            >
                                                {primaryButtonProps ? (
                                                    <DialogActionButton
                                                        {...primaryButtonProps}
                                                        appearance={
                                                            'appearance' in primaryButtonProps && primaryButtonProps.appearance
                                                                ? primaryButtonProps.appearance
                                                                : 'primary'
                                                        }
                                                    />
                                                ) : null}

                                                {secondaryButtonProps ? <DialogActionButton {...secondaryButtonProps} /> : null}

                                                {additionalButtons?.map(({ key, ...buttonProps }) => (
                                                    <DialogActionButton key={key} {...buttonProps} />
                                                ))}
                                            </DialogActions>
                                        </>
                                    ) : null}
                                </DialogBody>
                            </FormWrapper>
                        </MessageProvider>
                    </DialogSurface>
                </FluentDialog>
            </LazyDialogContext.Provider>
        );
    }
);

Dialog.displayName = 'Dialog';

function DialogActionButton({ closeOnClick, ...buttonProps }: DialogButtonProps): JSX.Element {
    const buttonElement = 'render' in buttonProps ? buttonProps.render() : <Button {...buttonProps} />;

    return closeOnClick ? <DialogTrigger disableButtonEnhancement={true}>{buttonElement}</DialogTrigger> : buttonElement;
}

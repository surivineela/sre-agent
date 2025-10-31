import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useCallback } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { useWizardStyles } from './WizardDialog.styles';
import WizardStepper, { WizardStep } from './WizardStepper';

interface WizardProps {
    title: React.ReactNode | string;
    steps: WizardStep[];
    children?: React.ReactNode;
    isDialogOpen: boolean;
    setIsDialogOpen: (isOpen: boolean) => void;
    withDismissIcon?: boolean;
    actions?: React.ReactNode;
    // Default actions props
    currentStep?: number;
    setCurrentStep?: (step: number) => void;
    onNext?: () => void;
    onBack?: () => void;
    onCancel?: () => void;
    isNextDisabled?: boolean;
    reviewButtonText?: string;
    nextButtonText?: string;
    backButtonText?: string;
    cancelButtonText?: string;
}

export const WizardDialog: FC<WizardProps> = props => {
    const {
        title,
        steps,
        actions,
        children,
        isDialogOpen,
        setIsDialogOpen,
        withDismissIcon = true,
        currentStep = 0,
        setCurrentStep = () => {},
        onNext,
        onBack,
        isNextDisabled = false,
        reviewButtonText,
        nextButtonText,
        backButtonText,
        cancelButtonText,
    } = props;
    const intl = useIntl();
    const styles = useWizardStyles();

    const onCancel = useCallback(() => {
        setIsDialogOpen(false);
        props.onCancel?.();
    }, [props, setIsDialogOpen]);

    const defaultActions = (
        <DefaultActions
            currentStep={currentStep}
            setCurrentStep={setCurrentStep}
            totalSteps={steps.length}
            onBack={onBack}
            onNext={onNext}
            onCancel={onCancel}
            isNextDisabled={isNextDisabled}
            reviewButtonText={reviewButtonText}
            nextButtonText={nextButtonText}
            backButtonText={backButtonText}
            cancelButtonText={cancelButtonText}
        />
    );

    return (
        <Dialog open={isDialogOpen} onOpenChange={(_, data) => setIsDialogOpen(data.open)}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody className={styles.dialogBodyGrid}>
                    <div className={styles.dialogTitle}>
                        <DialogTitle>{title}</DialogTitle>
                        {withDismissIcon ? (
                            <Button
                                appearance="transparent"
                                icon={<Dismiss24Regular />}
                                onClick={onCancel}
                                aria-label={intl.formatMessage(SreAgentResources.close)}
                            />
                        ) : undefined}
                    </div>
                    <WizardStepper steps={steps} className={styles.stepper} />
                    <DialogContent className={styles.dialogContent}>{children}</DialogContent>
                    <DialogActions className={styles.dialogActions}>{actions || defaultActions}</DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

interface DefaultActionsProps {
    currentStep: number;
    setCurrentStep: (step: number) => void;
    totalSteps: number;
    onBack?: () => void;
    onNext?: () => void;
    onCancel: () => void;
    isNextDisabled?: boolean;
    nextButtonText?: string;
    reviewButtonText?: string;
    backButtonText?: string;
    cancelButtonText?: string;
}

const DefaultActions: FC<DefaultActionsProps> = ({
    currentStep,
    totalSteps,
    setCurrentStep,
    onCancel,
    isNextDisabled,
    nextButtonText,
    reviewButtonText,
    backButtonText,
    cancelButtonText,
    ...props
}) => {
    const intl = useIntl();
    const styles = useWizardStyles();
    const { submitForm } = useFormikContext();

    const isFirstStep = currentStep === 0;
    const isLastStep = currentStep === totalSteps - 1;

    const onNext = () => {
        if (currentStep < totalSteps - 1) {
            setCurrentStep(currentStep + 1);
        } else {
            submitForm();
        }
    };

    const onBack = () => {
        if (currentStep > 0) {
            setCurrentStep(currentStep - 1);
        }
    };

    return (
        <div className={styles.defaultActionsContainer}>
            <div className={styles.leftActions}>
                <Button appearance="secondary" onClick={props.onBack || onBack} disabled={isFirstStep}>
                    {backButtonText || intl.formatMessage(SreAgentResources.back)}
                </Button>
                <Button appearance="primary" onClick={props.onNext || onNext} disabled={isNextDisabled}>
                    {isLastStep
                        ? reviewButtonText || intl.formatMessage(SreAgentResources.add)
                        : nextButtonText || intl.formatMessage(SreAgentResources.next)}
                </Button>
            </div>
            <div className={styles.rightActions}>
                <Button appearance="secondary" onClick={onCancel}>
                    {cancelButtonText || intl.formatMessage(SreAgentResources.cancel)}
                </Button>
            </div>
        </div>
    );
};

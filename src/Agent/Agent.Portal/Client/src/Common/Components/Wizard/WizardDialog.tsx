import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { FC, useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';
import { useWizardStyles } from './WizardDialog.styles';
import WizardStepper, { StepStatus, WizardStep } from './WizardStepper';

interface WizardProps {
    title: React.ReactNode | string;
    steps: WizardStep[];
    isDialogOpen: boolean;
    onClose: () => void;
    withDismissIcon?: boolean;
    actions?: React.ReactNode;
    /** If true, all step content remains mounted (but hidden) to preserve state. Default: false */
    keepRendered?: boolean;
    // Default actions props
    currentStep?: number;
    onNext?: () => void;
    onBack?: () => void;
    isNextDisabled?: boolean;
    isBackDisabled?: boolean;
    isCancelDisabled?: boolean;
    nextButtonText?: string;
    reviewButtonText?: string;
    backButtonText?: string;
    cancelButtonText?: string;
}

export const WizardDialog: FC<WizardProps> = props => {
    const {
        title,
        steps,
        actions,
        isDialogOpen,
        onClose,
        withDismissIcon = true,
        keepRendered = false,
        currentStep = 0,
        onNext,
        onBack,
        isNextDisabled = false,
        isBackDisabled = false,
        isCancelDisabled = false,
        nextButtonText,
        reviewButtonText,
        backButtonText,
        cancelButtonText,
    } = props;
    const intl = useIntl();
    const styles = useWizardStyles();

    // Calculate step statuses based on currentStep
    const stepsWithStatus = useMemo<WizardStep[]>(
        () =>
            steps.map((step, index) => ({
                ...step,
                status: index < currentStep ? StepStatus.Completed : index === currentStep ? StepStatus.Active : StepStatus.Pending,
            })),
        [steps, currentStep]
    );

    const handleOpenChange = useCallback(
        (_: unknown, data: { open: boolean }) => {
            if (!data.open) {
                onClose();
            }
        },
        [onClose]
    );

    const defaultActions = (
        <DefaultActions
            currentStep={currentStep}
            totalSteps={steps.length}
            onBack={onBack}
            onNext={onNext}
            onCancel={onClose}
            isNextDisabled={isNextDisabled}
            isBackDisabled={isBackDisabled}
            isCancelDisabled={isCancelDisabled}
            nextButtonText={nextButtonText}
            reviewButtonText={reviewButtonText}
            backButtonText={backButtonText}
            cancelButtonText={cancelButtonText}
        />
    );

    return (
        <Dialog open={isDialogOpen} onOpenChange={handleOpenChange}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody className={styles.dialogBodyGrid}>
                    <div className={styles.dialogTitle}>
                        <DialogTitle>{title}</DialogTitle>
                        {withDismissIcon ? (
                            <Button
                                appearance="transparent"
                                icon={<Dismiss24Regular />}
                                onClick={onClose}
                                aria-label={intl.formatMessage(PortalResources.close)}
                                disabled={isCancelDisabled}
                            />
                        ) : undefined}
                    </div>
                    <WizardStepper steps={stepsWithStatus} className={styles.stepper} />
                    <DialogContent className={styles.dialogContent}>
                        {keepRendered
                            ? steps.map((step, index) => (
                                  <div key={step.id || index} style={{ display: index === currentStep ? 'block' : 'none' }}>
                                      {step.content}
                                  </div>
                              ))
                            : steps[currentStep]?.content}
                    </DialogContent>
                    <DialogActions className={styles.dialogActions}>{actions || defaultActions}</DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

interface DefaultActionsProps {
    currentStep: number;
    totalSteps: number;
    onBack?: () => void;
    onNext?: () => void;
    onCancel: () => void;
    isNextDisabled?: boolean;
    nextButtonText?: string;
    reviewButtonText?: string;
    backButtonText?: string;
    cancelButtonText?: string;
    isBackDisabled?: boolean;
    isCancelDisabled?: boolean;
}

const DefaultActions: FC<DefaultActionsProps> = ({
    currentStep,
    totalSteps,
    onBack,
    onNext,
    onCancel,
    isNextDisabled,
    nextButtonText,
    reviewButtonText,
    backButtonText,
    cancelButtonText,
    isBackDisabled,
    isCancelDisabled,
}) => {
    const intl = useIntl();
    const styles = useWizardStyles();
    const isFirstStep = currentStep === 0;
    const isLastStep = currentStep === totalSteps - 1;

    return (
        <div className={styles.defaultActionsContainer}>
            <div className={styles.leftActions}>
                <Button appearance="secondary" onClick={onBack} disabled={isFirstStep || isBackDisabled || !onBack}>
                    {backButtonText || intl.formatMessage(PortalResources.back)}
                </Button>
                <Button appearance="primary" onClick={onNext} disabled={isNextDisabled || !onNext}>
                    {isLastStep
                        ? reviewButtonText || intl.formatMessage(PortalResources.add)
                        : nextButtonText || intl.formatMessage(PortalResources.next)}
                </Button>
            </div>
            <div className={styles.rightActions}>
                <Button appearance="secondary" onClick={onCancel} disabled={isCancelDisabled}>
                    {cancelButtonText || intl.formatMessage(PortalResources.cancel)}
                </Button>
            </div>
        </div>
    );
};

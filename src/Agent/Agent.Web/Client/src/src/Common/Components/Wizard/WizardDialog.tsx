import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { FC, useCallback } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { useWizardStyles } from './WizardDialog.styles';
import WizardStepper, { WizardStep } from './WizardStepper';

interface WizardProps {
    title: React.ReactNode | string;
    steps: WizardStep[];
    children: JSX.Element[];
    isDialogOpen: boolean;
    setIsDialogOpen: (isOpen: boolean) => void;
    withDismissIcon?: boolean;
    actions?: React.ReactNode;
    // Default actions props
    currentStep?: number;
    onNext?: () => void;
    onBack?: () => void;
    onCancel?: () => void;
    isNextDisabled?: boolean;
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
        onNext,
        onBack,
        isNextDisabled = false,
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
            totalSteps={steps.length}
            onBack={onBack}
            onNext={onNext}
            onCancel={onCancel}
            isNextDisabled={isNextDisabled}
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
                    <DialogContent className={styles.dialogContent}>{...children}</DialogContent>
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
}) => {
    const intl = useIntl();
    const styles = useWizardStyles();
    const isFirstStep = currentStep === 0;
    const isLastStep = currentStep === totalSteps - 1;

    return (
        <div className={styles.defaultActionsContainer}>
            <div className={styles.leftActions}>
                <Button appearance="secondary" onClick={onBack} disabled={isFirstStep || !onBack}>
                    {backButtonText || intl.formatMessage(SreAgentResources.back)}
                </Button>
                <Button appearance="primary" onClick={onNext} disabled={isNextDisabled || !onNext}>
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

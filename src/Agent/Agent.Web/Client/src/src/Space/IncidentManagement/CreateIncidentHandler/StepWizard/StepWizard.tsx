import { FC, Fragment, useCallback } from 'react';
import { StepState } from './StepWizard.contracts';
import { getCircleStyles, getLabelStyles, separatorStyles, stepContainerStyles } from './StepWizard.styles';

interface StepProps {
    stepNumber: number;
    stepTitle: string;
    state: StepState;
}

const Step: FC<StepProps> = ({ stepNumber, stepTitle, state }) => {
    return (
        <div style={stepContainerStyles}>
            <div style={getCircleStyles(state)}>{state === 'complete' ? '\u2713' : stepNumber}</div>
            <div style={getLabelStyles(state)}>{stepTitle}</div>
        </div>
    );
};

export const Separator: FC = () => {
    return <div style={separatorStyles} />;
};

interface StepWizardStep {
    stepKey: string;
    stepTitle: string;
}

interface StepWizardProps {
    steps: StepWizardStep[];
    currentStep: string;
}

export const StepWizard: FC<StepWizardProps> = ({ steps, currentStep }) => {
    const getStepNumber = useCallback(
        (stepKey: string): number => {
            const stepIndex = steps.findIndex(step => step.stepKey === stepKey);
            return stepIndex !== -1 ? stepIndex + 1 : 0;
        },
        [steps]
    );

    const getStepState = useCallback(
        (stepKey: string): StepState => {
            const currentStepNumber = getStepNumber(currentStep);
            const stepNumber = getStepNumber(stepKey);
            if (currentStepNumber === stepNumber) {
                return 'current';
            }
            return currentStepNumber > stepNumber ? 'complete' : 'upcoming';
        },
        [currentStep]
    );

    return (
        <div style={{ display: 'content' }}>
            {steps.map((step, index) => (
                <Fragment key={step.stepKey}>
                    <Step stepNumber={getStepNumber(step.stepKey)} stepTitle={step.stepTitle} state={getStepState(step.stepKey)} />
                    {index < steps.length - 1 && <Separator />}
                </Fragment>
            ))}
        </div>
    );
};

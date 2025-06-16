import { CheckmarkCircle24Filled, NumberCircle124Filled, NumberCircle224Filled } from '@fluentui/react-icons';
import { FC, Fragment, useCallback, useMemo } from 'react';
import { IncidentHandlerCreateSteps } from '../IncidentHandlerCreateContext';
import { StepState } from './StepWizard.contracts';
import { getCircleStyles, getLabelStyles, separatorStyles, stepContainerStyles } from './StepWizard.styles';

interface StepProps {
    stepNumber: number;
    stepTitle: string;
    state: StepState;
}

const Step: FC<StepProps> = ({ stepNumber, stepTitle, state }) => {
    const stepIcon = useMemo(() => {
        if (state === 'complete' || state === 'skipped') {
            return <CheckmarkCircle24Filled style={getCircleStyles(state)} />;
        }
        if (stepNumber === 1) {
            return <NumberCircle124Filled style={getCircleStyles(state)} />;
        }
        return <NumberCircle224Filled style={getCircleStyles(state)} />;
    }, [stepNumber, state]);

    return (
        <div style={stepContainerStyles}>
            {stepIcon}
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
    skippedSteps: IncidentHandlerCreateSteps[];
    currentStep: string;
}

export const StepWizard: FC<StepWizardProps> = ({ steps, skippedSteps, currentStep }) => {
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
            return currentStepNumber < stepNumber ? 'upcoming' : skippedSteps.some(step => step === stepKey) ? 'skipped' : 'complete';
        },
        [currentStep, skippedSteps, getStepNumber]
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

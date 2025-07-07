import {
    CheckmarkCircle24Filled,
    NumberCircle024Filled,
    NumberCircle124Filled,
    NumberCircle224Filled,
    NumberCircle324Filled,
    NumberCircle424Filled,
    NumberCircle524Filled,
    NumberCircle624Filled,
    NumberCircle724Filled,
    NumberCircle824Filled,
    NumberCircle924Filled,
} from '@fluentui/react-icons';
import { FC, Fragment, useCallback, useMemo } from 'react';
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
        if (stepNumber === 2) {
            return <NumberCircle224Filled style={getCircleStyles(state)} />;
        }
        if (stepNumber === 3) {
            return <NumberCircle324Filled style={getCircleStyles(state)} />;
        }
        if (stepNumber === 4) {
            return <NumberCircle424Filled style={getCircleStyles(state)} />;
        }
        if (stepNumber === 5) {
            return <NumberCircle524Filled style={getCircleStyles(state)} />;
        }
        if (stepNumber === 6) {
            return <NumberCircle624Filled style={getCircleStyles(state)} />;
        }
        if (stepNumber === 7) {
            return <NumberCircle724Filled style={getCircleStyles(state)} />;
        }
        if (stepNumber === 8) {
            return <NumberCircle824Filled style={getCircleStyles(state)} />;
        }
        if (stepNumber === 9) {
            return <NumberCircle924Filled style={getCircleStyles(state)} />;
        }
        return <NumberCircle024Filled style={getCircleStyles(state)} />;
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
    skippedSteps: string[];
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

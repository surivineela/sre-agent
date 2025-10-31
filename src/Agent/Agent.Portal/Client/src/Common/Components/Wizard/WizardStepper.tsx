import { mergeClasses, Text } from '@fluentui/react-components';
import { CheckmarkCircle20Filled } from '@fluentui/react-icons';
import { FC } from 'react';
import { useWizardStepperStyles } from './WizardStepper.styles';

export enum StepStatus {
    Pending = 'pending',
    Active = 'active',
    Completed = 'completed',
}

export interface WizardStep {
    id: string;
    title: string;
    /** Internal use only - calculated by WizardDialog */
    status?: StepStatus;
    content?: React.ReactNode;
}

export interface WizardStepperProps {
    steps: WizardStep[];
    className?: string;
}

export const WizardStepper: FC<WizardStepperProps> = ({ steps, className }) => {
    const styles = useWizardStepperStyles();

    const getIconClass = (status: StepStatus) => {
        switch (status) {
            case StepStatus.Completed:
                return styles.completedIcon;
            case StepStatus.Active:
                return styles.activeIcon;
            case StepStatus.Pending:
            default:
                return styles.pendingIcon;
        }
    };

    const getTitleClass = (status: StepStatus) => {
        switch (status) {
            case StepStatus.Completed:
                return styles.completedTitleText;
            case StepStatus.Active:
                return styles.activeTitleText;
            case StepStatus.Pending:
            default:
                return styles.pendingTitleText;
        }
    };

    const renderStepIcon = (step: WizardStep, index: number) => {
        if (step.status === StepStatus.Completed) {
            return <CheckmarkCircle20Filled />;
        }
        return <span>{index + 1}</span>;
    };

    return (
        <div className={mergeClasses(styles.container, className)}>
            {steps?.map((step, index) => (
                <div className={styles.stepRow} key={step.id}>
                    <div className={mergeClasses(styles.stepColumn, index === steps.length - 1 && styles.lastStep)}>
                        <div className={mergeClasses(styles.iconContainer, getIconClass(step.status ?? StepStatus.Pending))}>
                            {renderStepIcon(step, index)}
                        </div>
                        {index < steps.length - 1 && <div className={styles.connector} />}
                    </div>
                    <Text className={mergeClasses(styles.stepTitle, getTitleClass(step.status ?? StepStatus.Pending))}>{step.title}</Text>
                </div>
            ))}
        </div>
    );
};

export default WizardStepper;

import { Caption1, Caption2, mergeClasses } from '@fluentui/react-components';
import { CheckmarkFilled } from '@fluentui/react-icons';
import { FC, Fragment } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { useWizardStepperHorizontalStyles } from './WizardStepperHorizontal.styles';
import { StepStatus } from './WizardStepper';

export interface WizardStepHorizontal {
    id: number;
    title: string;
    description?: string;
    status: StepStatus;
}

export interface WizardStepperHorizontalProps {
    steps: WizardStepHorizontal[];
    className?: string;
}

export const WizardStepperHorizontal: FC<WizardStepperHorizontalProps> = ({
    steps,
    className,
}) => {
    const styles = useWizardStepperHorizontalStyles();
    const intl = useIntl();

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

    const renderStepIcon = (step: WizardStepHorizontal) => {
        if (step.status === StepStatus.Completed) {
            return <CheckmarkFilled />;
        }
        return null;
    };

    return (
        <div className={mergeClasses(styles.root, className)}>
            <div className={styles.stepperContainer}>
                <div className={styles.stepsRow} role="list" aria-label={intl.formatMessage(SreAgentResources.progress)}>
                    {steps.map((step, index) => {
                        const isLast = index === steps.length - 1;
                        const isCompleted = step.status === StepStatus.Completed;
                        const isCurrent = step.status === StepStatus.Active;

                        return (
                            <Fragment key={step.id}>
                                <div
                                    className={mergeClasses(styles.stepWrapper, isLast && styles.stepWrapperLast)}
                                >
                                    <div className={styles.step} role="listitem" aria-current={isCurrent ? 'step' : undefined}>
                                        <div className={mergeClasses(styles.iconContainer, getIconClass(step.status))}>
                                            {renderStepIcon(step)}
                                        </div>
                                        <Caption1 className={mergeClasses(styles.stepTitle, getTitleClass(step.status))}>
                                            {step.title}
                                        </Caption1>
                                        {step.description && (
                                            <Caption2 className={styles.stepDescription}>{step.description}</Caption2>
                                        )}
                                    </div>
                                    {!isLast && (
                                        <div
                                            className={mergeClasses(
                                                styles.connector,
                                                isCompleted && styles.connectorCompleted
                                            )}
                                            aria-hidden="true"
                                        />
                                    )}
                                </div>
                            </Fragment>
                        );
                    })}
                </div>
            </div>
        </div>
    );
};

export default WizardStepperHorizontal;

import { makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import { Checkmark24Regular } from '@fluentui/react-icons';
import { FC, Fragment } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../../Strings/SREAgentResources';

type StepStatus = 'upcoming' | 'current' | 'completed';

export interface StepperStep {
    id: number;
    label: string;
    description?: string;
}

export interface StepperProps {
    steps: StepperStep[];
    currentStep: number;
    className?: string;
}

const useStepperStyles = makeStyles({
    root: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalL,
        justifyContent: 'space-between',
        width: '100%',
    },
    step: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '8px',
        flex: '0 0 auto',
        maxWidth: '150px',
        textAlign: 'center',
    },
    circle: {
        width: '40px',
        height: '40px',
        borderRadius: '50%',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: tokens.colorNeutralBackground3,
        color: tokens.colorNeutralForeground3,
        fontWeight: tokens.fontWeightSemibold,
        transitionProperty: 'background-color, color, transform, box-shadow',
        transitionDuration: tokens.durationFast,
        transitionTimingFunction: 'ease',
    },
    circleCurrent: {
        backgroundColor: tokens.colorBrandBackground,
        color: tokens.colorNeutralForegroundInverted,
        transform: 'scale(1.08)',
        boxShadow: tokens.shadow8,
    },
    circleCompleted: {
        backgroundColor: tokens.colorPaletteGreenBackground3,
        color: tokens.colorNeutralForegroundInverted,
    },
    label: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
    },
    labelCurrent: {
        color: tokens.colorNeutralForeground1,
        fontWeight: tokens.fontWeightSemibold,
    },
    description: {
        fontSize: tokens.fontSizeBase100,
        color: tokens.colorNeutralForeground3,
    },
    connector: {
        height: '2px',
        flex: '1 1 0%',
        backgroundColor: tokens.colorNeutralStroke2,
        borderRadius: tokens.borderRadiusMedium,
        transition: 'background-color 0.3s ease',
    },
    connectorCompleted: {
        backgroundColor: tokens.colorNeutralStroke2,
    },
});

const getStepStatus = (currentStep: number, stepId: number): StepStatus => {
    if (currentStep > stepId) {
        return 'completed';
    }

    if (currentStep === stepId) {
        return 'current';
    }

    return 'upcoming';
};

export const Stepper: FC<StepperProps> = ({ steps, currentStep, className }) => {
    const styles = useStepperStyles();
    const intl = useIntl();

    return (
        <div className={mergeClasses(styles.root, className)} role="list" aria-label={intl.formatMessage(SreAgentResources.progress)}>
            {steps.map((step, index) => {
                const status = getStepStatus(currentStep, step.id);
                const isCompleted = status === 'completed';
                const isCurrent = status === 'current';

                return (
                    <Fragment key={step.id}>
                        {index > 0 && (
                            <div
                                className={mergeClasses(
                                    styles.connector,
                                    currentStep > steps[index - 1].id ? styles.connectorCompleted : undefined
                                )}
                                aria-hidden
                            />
                        )}
                        <div className={styles.step} role="listitem" aria-current={isCurrent ? 'step' : undefined}>
                            <div
                                className={mergeClasses(
                                    styles.circle,
                                    isCurrent ? styles.circleCurrent : undefined,
                                    isCompleted ? styles.circleCompleted : undefined
                                )}
                            >
                                {isCompleted ? <Checkmark24Regular /> : step.id}
                            </div>
                            <div className={mergeClasses(styles.label, isCurrent ? styles.labelCurrent : undefined)}>{step.label}</div>
                            {step.description ? <div className={styles.description}>{step.description}</div> : null}
                        </div>
                    </Fragment>
                );
            })}
        </div>
    );
};

export default Stepper;

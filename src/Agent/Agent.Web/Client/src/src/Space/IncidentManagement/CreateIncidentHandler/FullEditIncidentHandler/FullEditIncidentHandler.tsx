import { tokens } from '@fluentui/react-components';
import { FC, useContext } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { IncidentHandlerCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerCreateContext';
import { GenerateHandler } from '../Steps/GenerateHandler';
import { ReviewAndEdit } from '../Steps/ReviewAndEdit';
import { StepWizard } from '../StepWizard/StepWizard';

export const FullEditIncidentHandler: FC = () => {
    const intl = useIntl();
    const { currentStep, generateInstructionsStepSkipped } = useContext(IncidentHandlerCreateContext);

    return (
        <div
            style={{
                display: 'flex',
                flexDirection: 'row',
                gap: 12,
            }}
        >
            <div
                style={{
                    padding: 20,
                    borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
                    minWidth: 280,
                    overflowY: 'auto',
                }}
            >
                <StepWizard
                    currentStep={currentStep}
                    skippedSteps={generateInstructionsStepSkipped ? [IncidentHandlerCreateSteps.GenerateHandler] : []}
                    steps={[
                        {
                            stepKey: IncidentHandlerCreateSteps.GenerateHandler,
                            stepTitle: intl.formatMessage(IncidentHandlerCreateResources.generateCustomHandler),
                        },
                        {
                            stepKey: IncidentHandlerCreateSteps.ReviewAndEdit,
                            stepTitle: intl.formatMessage(IncidentHandlerCreateResources.reviewAndEdit),
                        },
                    ]}
                />
            </div>
            <div
                style={{
                    height: '100%',
                    width: '100%',
                    overflowY: 'auto',
                }}
            >
                {currentStep === IncidentHandlerCreateSteps.GenerateHandler ? (
                    <GenerateHandler />
                ) : currentStep === IncidentHandlerCreateSteps.ReviewAndEdit ? (
                    <ReviewAndEdit />
                ) : null}
            </div>
        </div>
    );
};

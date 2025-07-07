import { tokens } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';
import { DeployStep } from '../Steps/DeployStep';
import { FilterStep } from '../Steps/FilterStep';
import { IncidentsAndGuidanceStep } from '../Steps/IncidentsAndGuidanceStep';
import { PreviewIncidentsStep } from '../Steps/PreviewIncidentsStep';
import { ReviewAndTestStep } from '../Steps/ReviewAndTestStep';
import { StepWizard } from '../StepWizard/StepWizard';

export const FullEditIncidentHandlerConsolidated: FC = () => {
    const intl = useIntl();
    const { currentStep, generateInstructionsStepSkipped } = useContext(IncidentHandlerConsolidatedCreateContext);
    const { values } = useFormikContext<IncidentHandlerCreateFormValues>();

    const steps = useMemo(() => {
        if (values.useCustomHandler) {
            return [
                {
                    stepKey: IncidentHandlerCreateSteps.FilterStep,
                    stepTitle: intl.formatMessage(IncidentHandlerCreateResources.filterStep),
                },
                {
                    stepKey: IncidentHandlerCreateSteps.IncidentsAndGuidanceStep,
                    stepTitle: intl.formatMessage(IncidentHandlerCreateResources.incidentsAndGuidanceStep),
                },
                {
                    stepKey: IncidentHandlerCreateSteps.ReviewAndTestStep,
                    stepTitle: intl.formatMessage(IncidentHandlerCreateResources.reviewAndTestStep),
                },
                {
                    stepKey: IncidentHandlerCreateSteps.DeployStep,
                    stepTitle: intl.formatMessage(IncidentHandlerCreateResources.deployStep),
                },
            ];
        }

        return [
            {
                stepKey: IncidentHandlerCreateSteps.FilterStep,
                stepTitle: intl.formatMessage(IncidentHandlerCreateResources.filterStep),
            },
            {
                stepKey: IncidentHandlerCreateSteps.PreviewIncidentsStep,
                stepTitle: intl.formatMessage(IncidentHandlerCreateResources.previewIncidentsStep),
            },
            {
                stepKey: IncidentHandlerCreateSteps.DeployStep,
                stepTitle: intl.formatMessage(IncidentHandlerCreateResources.deployStep),
            },
        ];
    }, [intl, values.useCustomHandler]);

    return (
        <div
            style={{
                display: 'flex',
                flexDirection: 'row',
                gap: 12,
                height: '100%',
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
                    skippedSteps={generateInstructionsStepSkipped ? [IncidentHandlerCreateSteps.IncidentsAndGuidanceStep] : []}
                    steps={steps}
                />
            </div>
            <div
                style={{
                    height: '100%',
                    width: '100%',
                    overflowY: 'auto',
                }}
            >
                {currentStep === IncidentHandlerCreateSteps.FilterStep ? (
                    <FilterStep />
                ) : currentStep === IncidentHandlerCreateSteps.IncidentsAndGuidanceStep ? (
                    <IncidentsAndGuidanceStep />
                ) : currentStep === IncidentHandlerCreateSteps.PreviewIncidentsStep ? (
                    <PreviewIncidentsStep />
                ) : currentStep === IncidentHandlerCreateSteps.ReviewAndTestStep ? (
                    <ReviewAndTestStep />
                ) : currentStep === IncidentHandlerCreateSteps.DeployStep ? (
                    <DeployStep />
                ) : null}
            </div>
        </div>
    );
};

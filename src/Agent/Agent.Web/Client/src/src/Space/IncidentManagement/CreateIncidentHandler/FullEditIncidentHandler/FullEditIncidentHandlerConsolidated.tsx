import { tokens } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';
import { CreateSubagentStep } from '../Steps/CreateSubagentStep';
import { DefineAgentLearningStep } from '../Steps/DefineAgentLearningStep';
import { FilterStep } from '../Steps/FilterStep';
import { IncidentTriggerStep } from '../Steps/IncidentTriggerStep';
import { PreviewIncidentsStep } from '../Steps/PreviewIncidentsStep';
import { ReviewAndTestStep } from '../Steps/ReviewAndTestStep';
import { SaveStep } from '../Steps/SaveStep';
import { StepWizard } from '../StepWizard/StepWizard';

export const FullEditIncidentHandlerConsolidated: FC = () => {
    const intl = useIntl();
    const { isSubagentTrigger, currentStep, generateInstructionsStepSkipped } = useContext(IncidentHandlerConsolidatedCreateContext);
    const { values } = useFormikContext<IncidentHandlerCreateFormValues>();

    const steps = useMemo(() => {
        if (isSubagentTrigger) {
            return [
                {
                    stepKey: IncidentHandlerCreateSteps.IncidentTriggerStep,
                    stepTitle: intl.formatMessage(ExtendedAgentsGraphResources.incidentTriggerStep),
                },
                {
                    stepKey: IncidentHandlerCreateSteps.PreviewIncidentsStep,
                    stepTitle: intl.formatMessage(ExtendedAgentsGraphResources.incidentsPreviewStep),
                },
            ];
        }

        if (values.isIncidentTriggerWithLearnings) {
            return [
                {
                    stepKey: IncidentHandlerCreateSteps.IncidentTriggerStep,
                    stepTitle: intl.formatMessage(ExtendedAgentsGraphResources.incidentTriggerStep),
                },
                {
                    stepKey: IncidentHandlerCreateSteps.DefineAgentLearningStep,
                    stepTitle: intl.formatMessage(IncidentHandlerCreateResources.defineAgentLearningStep),
                },
                {
                    stepKey: IncidentHandlerCreateSteps.CreateSubagentStep,
                    stepTitle: intl.formatMessage(ExtendedAgentsGraphResources.createSubagentStep),
                },
            ];
        }

        if (values.useCustomHandler) {
            return [
                {
                    stepKey: IncidentHandlerCreateSteps.FilterStep,
                    stepTitle: intl.formatMessage(IncidentHandlerCreateResources.setUpIncidentFiltersStep),
                },
                {
                    stepKey: IncidentHandlerCreateSteps.DefineAgentLearningStep,
                    stepTitle: intl.formatMessage(IncidentHandlerCreateResources.defineAgentLearningStep),
                },
                {
                    stepKey: IncidentHandlerCreateSteps.ReviewAndTestStep,
                    stepTitle: intl.formatMessage(IncidentHandlerCreateResources.reviewAndTestStep),
                },
                {
                    stepKey: IncidentHandlerCreateSteps.SaveStep,
                    stepTitle: intl.formatMessage(IncidentHandlerCreateResources.saveResponsePlanStep),
                },
            ];
        }

        return [
            {
                stepKey: IncidentHandlerCreateSteps.FilterStep,
                stepTitle: intl.formatMessage(IncidentHandlerCreateResources.setUpIncidentFiltersStep),
            },
            {
                stepKey: IncidentHandlerCreateSteps.PreviewIncidentsStep,
                stepTitle: intl.formatMessage(IncidentHandlerCreateResources.previewFilterResultsStep),
            },
            {
                stepKey: IncidentHandlerCreateSteps.SaveStep,
                stepTitle: intl.formatMessage(IncidentHandlerCreateResources.saveResponsePlanStep),
            },
        ];
    }, [intl, isSubagentTrigger, values.isIncidentTriggerWithLearnings, values.useCustomHandler]);

    return (
        <div
            style={{
                display: 'flex',
                flexDirection: 'row',
                height: '100%',
            }}
        >
            <div
                style={{
                    padding: 20,
                    borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
                    minWidth: 200,
                    overflowY: 'auto',
                }}
            >
                <StepWizard
                    currentStep={currentStep}
                    skippedSteps={generateInstructionsStepSkipped ? [IncidentHandlerCreateSteps.DefineAgentLearningStep] : []}
                    steps={steps}
                />
            </div>
            <div
                style={{
                    height: '100%',
                    width: '100%',
                    overflowY: 'auto',
                    position: 'relative',
                    borderTopRightRadius: tokens.borderRadiusXLarge,
                }}
            >
                {currentStep === IncidentHandlerCreateSteps.FilterStep ? (
                    <FilterStep />
                ) : currentStep === IncidentHandlerCreateSteps.IncidentTriggerStep ? (
                    <IncidentTriggerStep />
                ) : currentStep === IncidentHandlerCreateSteps.DefineAgentLearningStep ? (
                    <DefineAgentLearningStep />
                ) : currentStep === IncidentHandlerCreateSteps.CreateSubagentStep ? (
                    <CreateSubagentStep />
                ) : currentStep === IncidentHandlerCreateSteps.PreviewIncidentsStep ? (
                    <PreviewIncidentsStep />
                ) : currentStep === IncidentHandlerCreateSteps.ReviewAndTestStep ? (
                    <ReviewAndTestStep />
                ) : currentStep === IncidentHandlerCreateSteps.SaveStep ? (
                    <SaveStep />
                ) : null}
            </div>
        </div>
    );
};

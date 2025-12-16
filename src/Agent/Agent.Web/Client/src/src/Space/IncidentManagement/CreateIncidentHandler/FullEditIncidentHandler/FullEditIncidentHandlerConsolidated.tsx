import { tokens } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';
import { CreateSubagentStep } from '../Steps/CreateSubagentStep';
import { FilterStep } from '../Steps/FilterStep';
import { IncidentsAndGuidanceStep } from '../Steps/IncidentsAndGuidanceStep';
import { IncidentTriggerStep } from '../Steps/IncidentTriggerStep';
import { PreviewIncidentsStep } from '../Steps/PreviewIncidentsStep';
import { ReviewAndTestStep } from '../Steps/ReviewAndTestStep';
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
                    stepKey: IncidentHandlerCreateSteps.IncidentsAndGuidanceStep,
                    stepTitle: intl.formatMessage(IncidentHandlerCreateResources.incidentsAndGuidanceStep),
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
                    skippedSteps={generateInstructionsStepSkipped ? [IncidentHandlerCreateSteps.IncidentsAndGuidanceStep] : []}
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
                ) : currentStep === IncidentHandlerCreateSteps.IncidentsAndGuidanceStep ? (
                    <IncidentsAndGuidanceStep />
                ) : currentStep === IncidentHandlerCreateSteps.CreateSubagentStep ? (
                    <CreateSubagentStep />
                ) : currentStep === IncidentHandlerCreateSteps.PreviewIncidentsStep ? (
                    <PreviewIncidentsStep />
                ) : currentStep === IncidentHandlerCreateSteps.ReviewAndTestStep ? (
                    <ReviewAndTestStep />
                ) : null}
            </div>
        </div>
    );
};

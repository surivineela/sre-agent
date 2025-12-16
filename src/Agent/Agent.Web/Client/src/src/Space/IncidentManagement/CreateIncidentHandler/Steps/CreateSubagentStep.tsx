import { Button } from '@fluentui/react-components';
import { Formik, useFormikContext } from 'formik';
import { FC, useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { useAgentCreateDialogStyles } from '../../../Graph/AgentCreateDialog/AgentCreateDialog.Styles';
import { AgentCreateFormValues } from '../../../Graph/AgentCreateDialog/Contracts';
import { FormView } from '../../../Graph/AgentCreateDialog/FormView';
import { useAgentCreateDialog } from '../../../Graph/AgentCreateDialog/Hooks/useAgentCreateDialog';
import { useAgentCreateDialogFormik } from '../../../Graph/AgentCreateDialog/Hooks/useAgentCreateDialogFormik';
import { useIncidentManagementStyles } from '../../../Styles/IncidentManagement.styles';
import { DirtyStateConfirmationWrapper } from '../DirtyStateConfirmationDialog';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';

interface CreateSubagentStepContentProps {
    isSubmitting: boolean;
}

const CreateSubagentStepContent: FC<CreateSubagentStepContentProps> = ({ isSubmitting }) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const dialogStyles = useAgentCreateDialogStyles();

    const { dirty, isValid, submitForm } = useFormikContext<AgentCreateFormValues>();

    const { exitToHome, setCurrentStep, incidentTriggerWithLearningsMetadata } = useContext(IncidentHandlerConsolidatedCreateContext);
    const { extendedAgents, extendedTools: existingTools, systemTools, mcpConnections } = incidentTriggerWithLearningsMetadata || {};
    const { openedPanel, setOpenedPanel, handoffAgentsHook, toolsPickerHook, improvementsAndSuggestionsHook, testPanelProps } =
        useAgentCreateDialogFormik(extendedAgents, existingTools, systemTools, mcpConnections, undefined, [], false, '', false);

    return (
        <>
            <div className={styles.stepContent}>
                <div className={dialogStyles.dialogBody}>
                    <FormView
                        handoffAgentsHook={handoffAgentsHook}
                        improvementsAndSuggestionsHook={improvementsAndSuggestionsHook}
                        toolsPickerHook={toolsPickerHook}
                        openedPanel={openedPanel}
                        openPanel={setOpenedPanel}
                        closePanel={() => setOpenedPanel(undefined)}
                        isEditScenario={false}
                        isOverrideScenario={false}
                        testPanelProps={testPanelProps}
                    />
                </div>
            </div>
            <div className={styles.stepFooter}>
                <Button
                    onClick={() => {
                        setCurrentStep(IncidentHandlerCreateSteps.IncidentsAndGuidanceStep);
                    }}
                    disabled={isSubmitting}
                >
                    {intl.formatMessage(IncidentHandlerCreateResources.back)}
                </Button>
                <Button appearance="primary" onClick={submitForm} disabled={!isValid || isSubmitting}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.create)}
                </Button>
                <DirtyStateConfirmationWrapper isDirty={dirty} onConfirm={() => exitToHome()}>
                    <Button disabled={isSubmitting}>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
                </DirtyStateConfirmationWrapper>
            </div>
        </>
    );
};

export const CreateSubagentStep: FC = () => {
    const { saveHandler } = useContext(IncidentHandlerConsolidatedCreateContext);
    const { values: parentValues } = useFormikContext<IncidentHandlerCreateFormValues>();

    const onAgentCreated = useCallback(
        async (agentName?: string) => {
            if (agentName) {
                await saveHandler(agentName);
            }
        },
        [saveHandler]
    );

    const agentCreateOrEditInfo = useMemo(
        () => ({
            mode: 'create' as const,
            agent: undefined,
        }),
        []
    );

    const { initialValues, validationSchema, onSubmit, isSubmitting } = useAgentCreateDialog(onAgentCreated, agentCreateOrEditInfo);

    const initialValuesWithParent: AgentCreateFormValues = useMemo(
        () => ({
            ...initialValues,
            agentName: parentValues.subagentName || initialValues.agentName,
            instructions: parentValues.subagentInstructions || initialValues.instructions,
            handoffInstructions: parentValues.subagentHandoffInstructions || initialValues.handoffInstructions,
            handoffSubagents: parentValues.subagentHandoffSubagents || initialValues.handoffSubagents,
            tools: parentValues.subagentToolNames || initialValues.tools,
        }),
        [
            initialValues,
            parentValues.subagentName,
            parentValues.subagentInstructions,
            parentValues.subagentHandoffInstructions,
            parentValues.subagentHandoffSubagents,
            parentValues.subagentToolNames,
        ]
    );

    return (
        <Formik<AgentCreateFormValues>
            initialValues={initialValuesWithParent}
            validationSchema={validationSchema}
            validateOnMount={true}
            onSubmit={onSubmit}
        >
            <CreateSubagentStepContent isSubmitting={isSubmitting} />
        </Formik>
    );
};

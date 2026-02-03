import { Dialog } from '@fluentui/react-components';
import { Formik } from 'formik';
import { FC } from 'react';
import { AgentCreateDialogFormik } from './AgentCreateDialogFormik';
import { AgentCreateDialogProps, AgentCreateFormValues } from './Contracts';
import { useAgentCreateDialog } from './Hooks/useAgentCreateDialog';

export const AgentCreateDialog: FC<AgentCreateDialogProps> = props => {
    const { onDismiss, refresh, agents, existingTools, systemTools, mcpConnections, skills, agentCreateOrEditInfo } = props;

    const {
        isOpen,
        isEditScenario,
        existingAgentGuid,
        isOverrideScenario,
        initialValues,
        validationSchema,
        onSubmit,
        excludedHandoffAgent,
        additionalHandoffAgents,
        submissionError,
        setSubmissionError,
    } = useAgentCreateDialog(refresh, agentCreateOrEditInfo, skills);

    return (
        <Dialog
            open={isOpen}
            onOpenChange={(_, data) => {
                if (!data.open) {
                    props.onDismiss();
                }
            }}
            modalType="alert"
        >
            <Formik<AgentCreateFormValues>
                enableReinitialize={true}
                initialValues={initialValues}
                validationSchema={validationSchema}
                validateOnMount={true}
                onSubmit={onSubmit}
            >
                <AgentCreateDialogFormik
                    onDismiss={onDismiss}
                    agents={agents}
                    existingTools={existingTools}
                    systemTools={systemTools}
                    mcpConnections={mcpConnections}
                    skills={skills}
                    excludedHandoffAgent={excludedHandoffAgent}
                    additionalHandoffAgents={additionalHandoffAgents}
                    isEditScenario={isEditScenario}
                    existingAgentGuid={existingAgentGuid}
                    isOverrideScenario={isOverrideScenario}
                    submissionError={submissionError}
                    setSubmissionError={setSubmissionError}
                />
            </Formik>
        </Dialog>
    );
};

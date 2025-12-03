import { Dialog } from '@fluentui/react-components';
import { Formik } from 'formik';
import { FC } from 'react';
import { AgentCreateDialogFormik } from './AgentCreateDialogFormik';
import { AgentCreateDialogProps, AgentCreateFormValues } from './Contracts';
import { useAgentCreateDialog } from './Hooks/useAgentCreateDialog';

export const AgentCreateDialog: FC<AgentCreateDialogProps> = props => {
    const { onDismiss, refresh, agents, existingTools, systemTools, mcpConnections, agentCreateOrEditInfo } = props;

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
    } = useAgentCreateDialog(refresh, agentCreateOrEditInfo);

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
                    excludedHandoffAgent={excludedHandoffAgent}
                    additionalHandoffAgents={additionalHandoffAgents}
                    isEditScenario={isEditScenario}
                    existingAgentGuid={existingAgentGuid}
                    isOverrideScenario={isOverrideScenario}
                />
            </Formik>
        </Dialog>
    );
};

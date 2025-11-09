import { Dialog } from '@fluentui/react-components';
import { Formik } from 'formik';
import { FC } from 'react';
import { AgentCreateDialogFormik } from './AgentCreateDialogFormik';
import { AgentCreateDialogProps, AgentCreateFormValues } from './Contracts';
import { useAgentCreateDialog } from './Hooks/useAgentCreateDialog';

export const AgentCreateDialog: FC<AgentCreateDialogProps> = props => {
    const { onDismiss, refresh, agents, existingTools, systemTools, agentCreateOrEditInfo } = props;

    const { isOpen, isEditScenario, initialValues, validationSchema, onSubmit, excludedHandoffAgent } = useAgentCreateDialog(
        onDismiss,
        refresh,
        agentCreateOrEditInfo
    );

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
                    excludedHandoffAgent={excludedHandoffAgent}
                    isEditScenario={isEditScenario}
                />
            </Formik>
        </Dialog>
    );
};

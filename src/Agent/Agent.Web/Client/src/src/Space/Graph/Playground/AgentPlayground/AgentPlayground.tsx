import { Formik } from 'formik';
import { FC } from 'react';
import { AgentPlaygroundFormik } from './AgentPlaygroundFormik';
import { AgentPlaygroundFormValues, AgentPlaygroundProps } from './Contracts';
import { useAgentPlayground } from './Hooks/useAgentPlayground';

export const AgentPlayground: FC<AgentPlaygroundProps> = props => {
    const { refresh, agents, existingTools, systemTools, mcpConnections, agent } = props;

    const {
        isExistingAgent,
        existingAgentGuid,
        isOverrideScenario,
        initialValues,
        validationSchema,
        onSubmit,
        excludedHandoffAgent,
        additionalHandoffAgents,
    } = useAgentPlayground(refresh, agent);

    return (
        <Formik<AgentPlaygroundFormValues>
            enableReinitialize={true}
            initialValues={initialValues}
            validationSchema={validationSchema}
            validateOnMount={true}
            onSubmit={onSubmit}
        >
            <AgentPlaygroundFormik
                agent={agent}
                agents={agents}
                existingTools={existingTools}
                systemTools={systemTools}
                mcpConnections={mcpConnections}
                excludedHandoffAgent={excludedHandoffAgent}
                additionalHandoffAgents={additionalHandoffAgents}
                isExistingAgent={isExistingAgent}
                existingAgentGuid={existingAgentGuid}
                isOverrideScenario={isOverrideScenario}
            />
        </Formik>
    );
};

import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { object, string } from 'yup';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessageOrStringify } from '../../../../Common/Clients/ArmClient';
import { ExtendedAgentClient } from '../../../../Common/Clients/ExtendedAgentClient';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgent } from '../../../Contracts/ExtendedAgentGraph';
import { AgentCreateFormValues, AgentCreateOrEditInfo } from '../Contracts';

const defaultInitialValues: AgentCreateFormValues = {
    agentName: '',
    instructions: '',
    handoffInstructions: '',
    handoffSubagents: [],
    tools: [],
};

export const useAgentCreateDialog = (
    onDismiss: () => void,
    onAgentCreated: (selectedAgent?: string) => void,
    agentCreateOrEditInfo: AgentCreateOrEditInfo | undefined
) => {
    const intl = useIntl();
    const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
    const azPortalContext = useContext(AzPortalContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const extendedAgentClient = ExtendedAgentClient.getInstance(sreAgentEndpoint);

    const isEditScenario = useMemo(() => {
        return agentCreateOrEditInfo?.mode === 'edit';
    }, [agentCreateOrEditInfo]);

    const excludedHandoffAgent = useMemo(() => {
        return agentCreateOrEditInfo?.mode === 'createTarget' ? agentCreateOrEditInfo?.agent?.name : undefined;
    }, [agentCreateOrEditInfo]);

    const [initialValues, setInitialValues] = useState<AgentCreateFormValues>(() => ({ ...defaultInitialValues }));

    const validationSchema = useMemo(() => {
        return object({
            agentName: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
            instructions: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
            handoffInstructions: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
        });
    }, [intl]);

    const onCreate = useCallback(
        async (values: AgentCreateFormValues, sourceAgent: ExtendedAgent | undefined, selectCreatedAgent: boolean | undefined) => {
            setIsSubmitting(true);

            const agentCreateBody: ExtendedAgent = {
                name: values.agentName,
                instructions: values.instructions,
                handoffDescription: values.handoffInstructions,
                handoffs: values.handoffSubagents,
                tools: values.tools,
            };
            const agentCreateNotificationId = azPortalContext.startNotification(
                intl.formatMessage(ExtendedAgentsGraphResources.createSubagentNotificationTitle, { agentName: values.agentName }),
                intl.formatMessage(ExtendedAgentsGraphResources.createSubagentNotificationInProgress, { agentName: values.agentName })
            );
            try {
                const response = await extendedAgentClient.applyEntity(agentCreateBody, 'agent');
                if (response.isSuccessful) {
                    const message = intl.formatMessage(ExtendedAgentsGraphResources.createSubagentNotificationSuccess, {
                        agentName: values.agentName,
                    });
                    azPortalContext.stopNotification(agentCreateNotificationId, true, message);
                } else {
                    const message = intl.formatMessage(ExtendedAgentsGraphResources.createSubagentNotificationFailure, {
                        agentName: values.agentName,
                        errorMessage: response.error,
                    });
                    azPortalContext.stopNotification(agentCreateNotificationId, false, message);
                    setIsSubmitting(false);
                    onDismiss();
                    return;
                }
            } catch (error) {
                const message = intl.formatMessage(ExtendedAgentsGraphResources.createSubagentNotificationFailure, {
                    agentName: values.agentName,
                    errorMessage: getErrorMessageOrStringify(error),
                });
                azPortalContext.stopNotification(agentCreateNotificationId, false, message);
                setIsSubmitting(false);
                onDismiss();
                return;
            }

            if (!sourceAgent) {
                setIsSubmitting(false);
                onDismiss();
                onAgentCreated(selectCreatedAgent ? values.agentName : undefined);
                return;
            }

            const agentLinkBody: ExtendedAgent = {
                ...sourceAgent,
                handoffs: [...(sourceAgent.handoffs || []), values.agentName],
            };
            const agentLinkNotificationId = azPortalContext.startNotification(
                intl.formatMessage(ExtendedAgentsGraphResources.addHandoffNotificationTitle, {
                    sourceAgent: sourceAgent.name,
                    targetAgent: values.agentName,
                }),
                intl.formatMessage(ExtendedAgentsGraphResources.addHandoffNotificationInProgress, {
                    sourceAgent: sourceAgent.name,
                    targetAgent: values.agentName,
                })
            );
            try {
                const response = await extendedAgentClient.applyEntity(agentLinkBody, 'agent');
                if (response.isSuccessful) {
                    const message = intl.formatMessage(ExtendedAgentsGraphResources.addHandoffNotificationSuccess, {
                        sourceAgent: sourceAgent.name,
                        targetAgent: values.agentName,
                    });
                    azPortalContext.stopNotification(agentLinkNotificationId, true, message);
                    setIsSubmitting(false);
                    onDismiss();
                    onAgentCreated(selectCreatedAgent ? values.agentName : undefined);
                } else {
                    const message = intl.formatMessage(ExtendedAgentsGraphResources.addHandoffNotificationFailure, {
                        sourceAgent: sourceAgent.name,
                        targetAgent: values.agentName,
                        errorMessage: response.error,
                    });
                    azPortalContext.stopNotification(agentLinkNotificationId, false, message);
                    setIsSubmitting(false);
                    onDismiss();
                }
            } catch (error) {
                const message = intl.formatMessage(ExtendedAgentsGraphResources.failedToCreateTool, {
                    errorMessage: getErrorMessageOrStringify(error),
                });
                azPortalContext.stopNotification(agentLinkNotificationId, false, message);
                setIsSubmitting(false);
                onDismiss();
            }
        },
        [azPortalContext, extendedAgentClient, intl, ExtendedAgentsGraphResources, onAgentCreated, onDismiss]
    );

    const onUpdate = useCallback(
        async (values: AgentCreateFormValues) => {
            setIsSubmitting(true);

            const agentUpdateBody: ExtendedAgent = {
                name: values.agentName,
                instructions: values.instructions,
                handoffDescription: values.handoffInstructions,
                handoffs: values.handoffSubagents,
                tools: values.tools,
            };
            const agentCreateNotificationId = azPortalContext.startNotification(
                intl.formatMessage(ExtendedAgentsGraphResources.updateSubagentNotificationTitle, { agentName: values.agentName }),
                intl.formatMessage(ExtendedAgentsGraphResources.updateSubagentNotificationInProgress, { agentName: values.agentName })
            );
            try {
                const response = await extendedAgentClient.applyEntity(agentUpdateBody, 'agent');
                if (response.isSuccessful) {
                    const message = intl.formatMessage(ExtendedAgentsGraphResources.updateSubagentNotificationSuccess, {
                        agentName: values.agentName,
                    });
                    azPortalContext.stopNotification(agentCreateNotificationId, true, message);
                    setIsSubmitting(false);
                    onDismiss();
                    onAgentCreated();
                    return;
                } else {
                    const message = intl.formatMessage(ExtendedAgentsGraphResources.updateSubagentNotificationFailure, {
                        agentName: values.agentName,
                        errorMessage: response.error,
                    });
                    azPortalContext.stopNotification(agentCreateNotificationId, false, message);
                    setIsSubmitting(false);
                    onDismiss();
                    return;
                }
            } catch (error) {
                const message = intl.formatMessage(ExtendedAgentsGraphResources.updateSubagentNotificationFailure, {
                    agentName: values.agentName,
                    errorMessage: getErrorMessageOrStringify(error),
                });
                azPortalContext.stopNotification(agentCreateNotificationId, false, message);
                setIsSubmitting(false);
                onDismiss();
                return;
            }
        },
        [azPortalContext, extendedAgentClient, intl, ExtendedAgentsGraphResources, onDismiss]
    );

    const onSubmit = useCallback(
        (values: AgentCreateFormValues) => {
            if (agentCreateOrEditInfo?.mode === 'edit') {
                onUpdate(values);
            } else if (agentCreateOrEditInfo?.mode === 'createTarget') {
                onCreate(values, agentCreateOrEditInfo.agent, false);
            } else {
                const shouldSelectCreatedAgent = agentCreateOrEditInfo?.mode === 'create';
                onCreate(values, undefined, shouldSelectCreatedAgent);
            }
        },
        [onCreate, onUpdate, agentCreateOrEditInfo]
    );

    useEffect(() => {
        if (!agentCreateOrEditInfo) {
            setInitialValues({ ...defaultInitialValues });
        } else if (agentCreateOrEditInfo.mode === 'edit') {
            const agentToEdit = agentCreateOrEditInfo.agent;
            setInitialValues({
                agentName: agentToEdit.name,
                instructions: agentToEdit.instructions || '',
                handoffInstructions: agentToEdit.handoffDescription || '',
                handoffSubagents: agentToEdit.handoffs || [],
                tools: agentToEdit.tools || [],
            });
        } else if (agentCreateOrEditInfo.mode === 'createSource') {
            const sourceAgent = agentCreateOrEditInfo.agent;
            setInitialValues(prevValues => ({
                ...prevValues,
                handoffSubagents: [sourceAgent.name],
            }));
        } else {
            setInitialValues({ ...defaultInitialValues });
        }
    }, [agentCreateOrEditInfo]);

    return {
        isOpen: !!agentCreateOrEditInfo,
        isEditScenario,
        initialValues,
        validationSchema,
        excludedHandoffAgent,
        onSubmit,
        isSubmitting,
    };
};

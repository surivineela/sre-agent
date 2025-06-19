import { isEqual } from 'lodash';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import { IncidentDocument, IncidentHandler, ToolInfo, WithSelection } from '../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../../Common/Helpers/Guid';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import { IncidentHandlerCreateResources } from '../../../Strings/SREAgentResources';
import { CreateOrEditMode, IncidentHandlerCreateSteps, OperationStatus, TimeDuration } from './IncidentHandlerCreateContext';

export const useCreateIncidentHandler = (
    exitToHome: () => void,
    setHandlerOperationStatus: React.Dispatch<React.SetStateAction<OperationStatus | undefined>>,
    handlerCreateOrEditInfo: { filterId: string; handlerId?: string }
) => {
    const intl = useIntl();
    const azPortalContext = useContext(AzPortalContext);
    const { resourceId, sreAgentEndpoint } = useContext(EnvironmentContext);
    const agentName = useMemo(() => (resourceId ? (new ArmResourceDescriptor(resourceId).resourceName ?? '') : ''), [resourceId]);
    const incidentHandlerClient = useMemo(
        () => IncidentHandlerClient.getInstance(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
        [azPortalContext, sreAgentEndpoint]
    );

    const [handler, setHandler] = useState<IncidentHandler>();
    const [handlerLoading, setHandlerLoading] = useState<boolean>(true);
    const [handlerLoaded, setHandlerLoaded] = useState<boolean>(false);

    const [mode, setMode] = useState<CreateOrEditMode | undefined>(handlerCreateOrEditInfo.handlerId ? 'quickEdit' : 'create');
    const [currentStep, setCurrentStep] = useState<IncidentHandlerCreateSteps>(IncidentHandlerCreateSteps.GenerateHandler);

    const [isDirty, setIsDirty] = useState<boolean>(false);

    // Name field
    const [name, setName] = useState<string>('');
    const onNameChange = useCallback((value: string) => {
        setIsDirty(true);
        setName(value);
    }, []);

    // Description field
    const [description, setDescription] = useState<string>('');
    const onDescriptionChange = useCallback((value: string) => {
        setIsDirty(true);
        setDescription(value);
    }, []);

    // Timespan field
    const [selectedTimespan, setSelectedTimespan] = useState<TimeDuration>(TimeDuration.Last60Days);
    const onSelectedTimespanChange = useCallback((value: TimeDuration) => {
        setIsDirty(true);
        setSelectedTimespan(value);
    }, []);

    // Incidents field
    const [incidents, setIncidents] = useState<WithSelection<IncidentDocument>[]>();
    const [loadingIncidents, setLoadingIncidents] = useState<boolean>(true);
    const selectedIncidents = useMemo(() => incidents?.filter(incident => incident.selected) || [], [incidents]);
    const [selectedIncidentIds, setSelectedIncidentIds] = useState<string[]>();

    const onSelectedIncidentsChange = useCallback((newSelectedIncidentIds: string[]) => {
        setIsDirty(true);
        setSelectedIncidentIds(currentValue => {
            if (isEqual(currentValue, newSelectedIncidentIds)) {
                return currentValue; // No change, return current state
            }
            return newSelectedIncidentIds; // Update state with new selected incident IDs
        });
        setIncidents(currentValue => {
            if (!currentValue) {
                return [];
            }
            const updatedIncidents = currentValue.map(incident => ({
                ...incident,
                selected: newSelectedIncidentIds.includes(incident.id),
            }));
            if (isEqual(currentValue, updatedIncidents)) {
                return currentValue; // No change, return current state
            }
            return updatedIncidents; // Update state with new incident documents
        });
    }, []);

    // Tools field
    const [tools, setTools] = useState<WithSelection<ToolInfo>[] | undefined>([]);
    const [toolsLoading, setToolsLoading] = useState<boolean>(true);
    const selectedTools = useMemo(() => tools?.filter(tool => tool.selected) || [], [tools]);
    const [selectedToolNames, setSelectedToolNames] = useState<string[]>();

    const onSelectedToolsChange = useCallback((newSelectedToolNames: string[]) => {
        setIsDirty(true);
        setSelectedToolNames(currentValue => {
            if (isEqual(currentValue, newSelectedToolNames)) {
                return currentValue; // No change, return current state
            }
            return newSelectedToolNames; // Update state with new selected tool names
        });
        setTools(currentValue => {
            if (!currentValue) {
                return [];
            }
            const updatedTools = currentValue.map(tool => ({ ...tool, selected: newSelectedToolNames.includes(tool.name) }));
            if (isEqual(currentValue, updatedTools)) {
                return currentValue; // No change, return current state
            }
            return updatedTools; // Update state with new incident documents
        });
    }, []);

    // Custom instructions field
    const [customInstructions, setCustomInstructions] = useState<string>('');
    const onCustomInstructionsChange = useCallback((value: string) => {
        setIsDirty(true);
        setCustomInstructions(value);
    }, []);

    // Incident processing guide field
    const [incidentProcessingGuide, setIncidentProcessingGuide] = useState<string>('');

    const [generatingInstructions, setGeneratingInstructions] = useState<boolean>(false);
    const [generateInstructionsStepSkipped, setGenerateInstructionsStepSkipped] = useState<boolean>(false);

    const generateInstructions = useCallback(() => {
        setGeneratingInstructions(true);

        const additionalInfo = {
            incidentFilterId: handlerCreateOrEditInfo.filterId,
        };

        azPortalContext.log({
            action: 'generate-instructions',
            actionModifier: 'start',
            logLevel: 'info',
            resourceId: resourceId,
            data: additionalInfo,
        });

        return incidentHandlerClient
            .generateInstructions({
                agentName,
                incidents: selectedIncidents?.map(incident => incident.id) ?? [],
                tools: selectedTools?.map(tool => tool.name) ?? [],
                customInstructions: customInstructions,
            })
            .then(instructionsResult => {
                setGeneratingInstructions(false);
                if (!instructionsResult.isSuccessful || !instructionsResult.content) {
                    // TODO (andimarc): Surface errors to the user.
                    const error = !instructionsResult.isSuccessful ? getErrorMessage(instructionsResult.error) : 'No content returned';
                    azPortalContext.log({
                        action: 'generate-instructions',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId: resourceId,
                        data: { ...additionalInfo, error },
                    });
                } else {
                    azPortalContext.log({
                        action: 'generate-instructions',
                        actionModifier: 'success',
                        logLevel: 'info',
                        resourceId: resourceId,
                        data: additionalInfo,
                    });
                    setIsDirty(true);
                    setGenerateInstructionsStepSkipped(false);
                    setSelectedToolNames(instructionsResult.content.tools);
                    setIncidentProcessingGuide(instructionsResult.content.generatedInstructions);
                    setCurrentStep(IncidentHandlerCreateSteps.ReviewAndEdit);
                }
            });
    }, [
        handlerCreateOrEditInfo.filterId,
        azPortalContext,
        resourceId,
        incidentHandlerClient,
        agentName,
        selectedIncidents,
        selectedTools,
        customInstructions,
    ]);

    const [editorDisplayValue, setEditorDisplayValue] = useState<string>();
    const [isEditorValueValid, setIsEditorValueValid] = useState<boolean>(true);

    const onEditorValueChange = useCallback((value: string | undefined) => {
        setIsDirty(true);
        setEditorDisplayValue(value);
    }, []);

    const deleteHandler = useCallback(() => {
        const {
            customHandlerDeleteNotificationTitle,
            customHandlerDeleteNotificationDescription,
            customHandlerDeleteNotificationError,
            customHandlerDeleteNotificationSuccess,
        } = IncidentHandlerCreateResources;

        if (handlerCreateOrEditInfo?.handlerId) {
            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(customHandlerDeleteNotificationTitle),
                intl.formatMessage(customHandlerDeleteNotificationDescription)
            );

            exitToHome();
            setHandlerOperationStatus('inprogress');

            const additionalInfo = { handlerId: handlerCreateOrEditInfo?.handlerId };

            azPortalContext.log({
                action: 'delete-incidentHandler',
                actionModifier: 'start',
                logLevel: 'info',
                resourceId: resourceId,
                data: additionalInfo,
            });

            incidentHandlerClient.deleteHandler(handlerCreateOrEditInfo?.handlerId).then(deleteResult => {
                if (!deleteResult.isSuccessful) {
                    azPortalContext.log({
                        action: 'delete-incidentHandler',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId: resourceId,
                        data: { ...additionalInfo, error: deleteResult.error },
                    });
                    setHandlerOperationStatus('failed');
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(customHandlerDeleteNotificationError, {
                            errorMessage: getErrorMessage(deleteResult.error),
                        })
                    );
                } else {
                    azPortalContext.log({
                        action: 'delete-incidentHandler',
                        actionModifier: 'success',
                        logLevel: 'info',
                        resourceId: resourceId,
                        data: additionalInfo,
                    });
                    setHandlerOperationStatus('succeeded');
                    azPortalContext.stopNotification(notificationId, true, intl.formatMessage(customHandlerDeleteNotificationSuccess));
                }
            });
        }
    }, [
        handlerCreateOrEditInfo?.handlerId,
        azPortalContext,
        intl,
        exitToHome,
        setHandlerOperationStatus,
        resourceId,
        incidentHandlerClient,
    ]);

    const save = useCallback(
        (handler: IncidentHandler) => {
            const [
                action,
                handlerPayload,
                createOrUpdateHandler,
                notificationTitle,
                notificationDescription,
                notificationErrorMessage,
                notificationSuccessMessage,
            ] = handler.id
                ? [
                      'update-incidentHandler',
                      handler,
                      incidentHandlerClient.updateHandler,
                      IncidentHandlerCreateResources.customHandlerUpdateNotificationTitle,
                      IncidentHandlerCreateResources.customHandlerUpdateNotificationDescription,
                      IncidentHandlerCreateResources.customHandlerUpdateNotificationError,
                      IncidentHandlerCreateResources.customHandlerUpdateNotificationSuccess,
                  ]
                : [
                      'create-incidentHandler',
                      { ...handler, id: Guid.newShortGuid() },
                      incidentHandlerClient.createHandler,
                      IncidentHandlerCreateResources.customHandlerAddNotificationTitle,
                      IncidentHandlerCreateResources.customHandlerAddNotificationDescription,
                      IncidentHandlerCreateResources.customHandlerAddNotificationError,
                      IncidentHandlerCreateResources.customHandlerAddNotificationSuccess,
                  ];

            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(notificationTitle),
                intl.formatMessage(notificationDescription)
            );

            exitToHome();
            setHandlerOperationStatus('inprogress');

            const additionalInfo = {
                handlerName: handlerPayload.name,
                incidentFilterId: handlerPayload.incidentFilterId,
            };

            azPortalContext.log({
                action,
                actionModifier: 'start',
                logLevel: 'info',
                resourceId: resourceId,
                data: additionalInfo,
            });

            createOrUpdateHandler(handlerPayload).then(saveResult => {
                if (!saveResult.isSuccessful) {
                    azPortalContext.log({
                        action,
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId: resourceId,
                        data: { ...additionalInfo, error: saveResult.error },
                    });
                    setHandlerOperationStatus('failed');
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(notificationErrorMessage, {
                            errorMessage: getErrorMessage(saveResult.error),
                        })
                    );
                } else {
                    azPortalContext.log({
                        action,
                        actionModifier: 'success',
                        logLevel: 'info',
                        resourceId: resourceId,
                        data: additionalInfo,
                    });
                    setHandlerOperationStatus('succeeded');
                    azPortalContext.stopNotification(notificationId, true, intl.formatMessage(notificationSuccessMessage));
                }
            });
        },
        [
            incidentHandlerClient.updateHandler,
            incidentHandlerClient.createHandler,
            azPortalContext,
            intl,
            exitToHome,
            setHandlerOperationStatus,
            resourceId,
        ]
    );

    const saveHandler = useCallback(() => {
        if (editorDisplayValue) {
            let configObject: IncidentHandler | undefined;

            try {
                configObject = JSON.parse(editorDisplayValue) as IncidentHandler;
                configObject.id = handlerCreateOrEditInfo?.handlerId || '';
            } catch (error) {
                // This should never happen because we block the user from saving invalid JSON in the editor.
                return;
            }

            save(configObject);
        }
    }, [editorDisplayValue, save, handlerCreateOrEditInfo?.handlerId]);

    const initializeEditorDisplayValue = useCallback(() => {
        const config = {
            name,
            description,
            incidentFilterId: handlerCreateOrEditInfo?.filterId,
            incidentProcessingGuide: incidentProcessingGuide.replace('\r\n', '\n').replace('\r', '\n').split('\n'),
            tools: selectedTools.map(tool => tool.name),
            incidents: selectedIncidents.map(incident => incident.id),
            customInstructions: customInstructions,
        };
        setEditorDisplayValue(JSON.stringify(config, null, 4));
    }, [
        name,
        description,
        handlerCreateOrEditInfo?.filterId,
        incidentProcessingGuide,
        selectedTools,
        selectedIncidents,
        customInstructions,
    ]);

    const exportHandler = useCallback(() => {
        if (handler) {
            const blob = new Blob([JSON.stringify(handler, null, 2)], { type: 'application/json' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `handler-${handler.id}.json`;
            a.click();
            URL.revokeObjectURL(url);
        }
    }, [handler]);

    const goToFullEditMode = useCallback(() => setMode('edit'), []);

    useEffect(() => {
        let subscribed = true;

        setToolsLoading(true);
        setTools([]);
        incidentHandlerClient.listTools().then(response => {
            if (subscribed) {
                setToolsLoading(false);
                if (response.isSuccessful && response.content) {
                    const sortedTools = response.content.sort((a, b) => a.name.localeCompare(b.name));
                    setTools(sortedTools.map(tool => ({ ...tool, selected: false }) as WithSelection<ToolInfo>));
                }
            }
        });

        return () => {
            subscribed = false;
        };
    }, [incidentHandlerClient, incidentHandlerClient.listTools]);

    useEffect(() => {
        let subscribed = true;

        if (selectedTimespan) {
            const queryPayload = {
                filter: {
                    id: handlerCreateOrEditInfo?.filterId,
                },
                durationInDays: selectedTimespan,
                statuses: [IncidentStatus.resolved, IncidentStatus.mitigated],
            };
            setLoadingIncidents(true);
            setIncidents([]);
            incidentHandlerClient.queryIncidents(queryPayload).then(response => {
                if (subscribed) {
                    if (response.isSuccessful && response.content) {
                        const sortedIncidents = response.content.sort((a, b) => b.createdAt.localeCompare(a.createdAt));
                        setIncidents(
                            sortedIncidents.map(incident => ({ ...incident, selected: false }) as WithSelection<IncidentDocument>)
                        );
                    }
                    setLoadingIncidents(false);
                }
            });
        } else {
            setLoadingIncidents(false);
            setIncidents([]);
        }

        return () => {
            subscribed = false;
        };
    }, [incidentHandlerClient, handlerCreateOrEditInfo?.filterId, selectedTimespan]);

    useEffect(() => {
        if (handlerCreateOrEditInfo.handlerId) {
            setMode('quickEdit');
        } else {
            setMode('create');
        }
    }, [handlerCreateOrEditInfo.handlerId]);

    useEffect(() => {
        setHandlerOperationStatus(undefined);
    }, [setHandlerOperationStatus]);

    useEffect(() => {
        let subscribed = true;

        setHandler(undefined);
        if (handlerCreateOrEditInfo.handlerId) {
            setHandlerLoading(false);

            const additionalInfo = {
                handlerId: handlerCreateOrEditInfo.handlerId,
                incidentFilterId: handlerCreateOrEditInfo.filterId,
            };

            azPortalContext.log({
                action: 'get-incidentHandler',
                actionModifier: 'start',
                logLevel: 'info',
                resourceId: resourceId,
                data: additionalInfo,
            });

            incidentHandlerClient.getHandler(handlerCreateOrEditInfo.handlerId).then(getResult => {
                if (subscribed) {
                    if (!getResult.isSuccessful || !getResult.content) {
                        const error = !getResult.isSuccessful ? getErrorMessage(getResult.error) : 'No content returned';
                        azPortalContext.log({
                            action: 'get-incidentHandler',
                            actionModifier: 'failed',
                            logLevel: 'error',
                            resourceId: resourceId,
                            data: { ...additionalInfo, error },
                        });
                        // TODO (andimarc): Surface errors to the user.
                        setSelectedToolNames([]);
                        setIncidentProcessingGuide('');
                        setSelectedIncidentIds([]);
                    } else {
                        azPortalContext.log({
                            action: 'get-incidentHandler',
                            actionModifier: 'success',
                            logLevel: 'info',
                            resourceId: resourceId,
                            data: additionalInfo,
                        });
                        setHandler(getResult.content);

                        setSelectedToolNames(getResult.content.tools);
                        setSelectedIncidentIds(getResult.content.incidents);
                        setCustomInstructions(getResult.content.customInstructions || '');
                        setIncidentProcessingGuide(getResult.content.incidentProcessingGuide.join('\n'));
                        setName(getResult.content.name);
                        setDescription(getResult.content.description || '');

                        setHandlerLoaded(true);
                    }
                    setHandlerLoading(false);
                }
            });
        } else {
            setHandlerLoaded(true);
            setSelectedToolNames(undefined);
            setSelectedIncidentIds(undefined);
            setHandlerLoading(false);
        }

        return () => {
            subscribed = false;
        };
    }, [
        incidentHandlerClient.getHandler,
        handlerCreateOrEditInfo.handlerId,
        handlerCreateOrEditInfo.filterId,
        azPortalContext.log,
        resourceId,
        azPortalContext,
        incidentHandlerClient,
    ]);

    useEffect(() => {
        if (!loadingIncidents && incidents && handlerLoaded) {
            const updatedIncidents = incidents?.map((incident, index) => ({
                ...incident,
                selected: !selectedIncidentIds
                    ? index < 3 // Default to selecting the first 3 incidents in create scenario
                    : selectedIncidentIds.includes(incident.id),
            }));
            setIncidents(currentValue => {
                if (isEqual(currentValue, updatedIncidents)) {
                    return currentValue; // No change, return current state
                }
                return updatedIncidents; // Update state with new incident documents
            });
            setSelectedIncidentIds(currentValue => {
                const newSelectedIncidentIds = updatedIncidents.filter(incident => incident.selected).map(incident => incident.id);
                if (isEqual(currentValue, newSelectedIncidentIds)) {
                    return currentValue; // No change, return current state
                }
                return newSelectedIncidentIds; // Update state with new selected incident IDs
            });
        }
    }, [selectedIncidentIds, incidents, loadingIncidents, handlerLoaded]);

    useEffect(() => {
        if (!toolsLoading && tools && handlerLoaded) {
            const updatedToolsList = tools?.map(tool => ({
                ...tool,
                selected: !selectedToolNames
                    ? true // Default to selecting all tools in create scenario
                    : selectedToolNames.includes(tool.name),
            }));
            setTools(currentValue => {
                if (isEqual(currentValue, updatedToolsList)) {
                    return currentValue; // No change, return current state
                }
                return updatedToolsList; // Update state with new tool infos
            });
            setSelectedToolNames(currentSelectedToolNames => {
                const newSelectedToolNames = updatedToolsList.filter(tool => tool.selected).map(tool => tool.name);
                if (isEqual(currentSelectedToolNames, newSelectedToolNames)) {
                    return currentSelectedToolNames; // No change, return current state
                }
                return newSelectedToolNames; // Update state with new selected tool names
            });
        }
    }, [selectedToolNames, tools, toolsLoading, handlerLoaded]);

    return {
        exitToHome,
        goToFullEditMode,
        isDirty,
        agentName,
        incidentFilterId: handlerCreateOrEditInfo?.filterId,
        currentStep,
        setCurrentStep,
        generateInstructionsStepSkipped,
        setGenerateInstructionsStepSkipped,
        mode,
        loadingIncidents,
        toolsLoading,
        handlerLoading,
        handlerLoaded,
        generatingInstructions,
        generateInstructions,
        initializeEditorDisplayValue,
        editorDisplayValue,
        isEditorValueValid,
        setIsEditorValueValid,
        onEditorValueChange,
        deleteHandler,
        saveHandler,
        exportHandler,

        // Name field
        name,
        onNameChange,

        // Description field
        description,
        onDescriptionChange,

        // Timespan field
        selectedTimespan,
        onSelectedTimespanChange,

        // Incidents field
        incidents,
        onSelectedIncidentsChange,

        // Tools field
        tools: tools || [],
        onSelectedToolsChange,

        // Custom instructions field
        customInstructions,
        onCustomInstructionsChange,

        // Incident processing guide field
        incidentProcessingGuide,
    };
};

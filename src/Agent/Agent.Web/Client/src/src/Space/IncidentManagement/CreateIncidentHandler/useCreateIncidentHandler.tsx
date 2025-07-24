import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import { IncidentDocument, IncidentHandler, IncidentQueryRequest, ToolInfo } from '../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../../Common/Helpers/Guid';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import { IncidentHandlerCreateResources } from '../../../Strings/SREAgentResources';
import { HandlerCreateOrEditInfo, HandlerMode, OperationStatus, TimeDuration } from './Contracts';
import { IncidentHandlerCreateSteps } from './IncidentHandlerCreateContext';

const pageSize = 10;

const defaultNumberOfIncidentsToLoad = 10;

/**
 * Return 1.5 times of the number of incidents that can fill the incidents list div to make sure the div is overflowed. Return 5 if the result is less than 5.
 * @param incidentsListContainerHeight
 * @param numberOfincidentsInDiv the existing number of incidents in the div
 * @returns
 */
const getNumberOfincidentsToOverflowincidentsListDiv = (
    incidentsListDivHeightInPx: number | undefined,
    numberOfincidentsInDiv: number
): number => {
    if (incidentsListDivHeightInPx === undefined) return defaultNumberOfIncidentsToLoad;

    const incidentItemHeightInPx = 32;

    const numberOfincidentsToLoad = Math.ceil(1.5 * (incidentsListDivHeightInPx / incidentItemHeightInPx)) - numberOfincidentsInDiv;

    return Math.max(numberOfincidentsToLoad, defaultNumberOfIncidentsToLoad);
};

export const useCreateIncidentHandler = (
    exitToHome: () => void,
    setHandlerOperationStatus: React.Dispatch<React.SetStateAction<OperationStatus | undefined>>,
    handlerCreateOrEditInfo: HandlerCreateOrEditInfo
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

    const [mode, setMode] = useState<HandlerMode | undefined>(handlerCreateOrEditInfo.handlerId ? 'quickEdit' : 'create');
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
    const [selectedTimespan, setSelectedTimespan] = useState<TimeDuration>(
        mode === 'create' ? TimeDuration.Last60Days : TimeDuration.Last90Days
    );
    const onSelectedTimespanChange = useCallback((value: TimeDuration) => {
        setIsDirty(true);
        setSelectedTimespan(value);
    }, []);

    // Incidents field

    const [prefetchedIncidents, setPrefetchedIncidents] = useState<IncidentDocument[]>([]);
    const [incidents, setIncidents] = useState<IncidentDocument[]>();
    const [loadingIncidents, setLoadingIncidents] = useState<boolean>(true);
    const [selectedIncidentIds, setSelectedIncidentIds] = useState<string[]>();
    const [selectedIncidents, setSelectedIncidents] = useState<IncidentDocument[]>();

    const incidentsInitialized = useRef(false);
    const [initialSelectedIncidentIds, setInitialSelectedIncidentIds] = useState<string[]>();

    const onSelectedIncidentsChange = useCallback(
        (newSelectedIncidentIds: string[]) => {
            setIsDirty(true);
            setSelectedIncidentIds(newSelectedIncidentIds);
            setSelectedIncidents(previousSelectedIncidents => {
                const allIncidents = [...(previousSelectedIncidents || []), ...prefetchedIncidents, ...(incidents || [])];
                const newSelectedIncidents: IncidentDocument[] = [];
                newSelectedIncidentIds.forEach(id => {
                    const incident = allIncidents.find(inc => inc.id === id);
                    if (incident) {
                        newSelectedIncidents.push(incident);
                    }
                });
                return newSelectedIncidents.sort((a, b) => b.createdAt.localeCompare(a.createdAt));
            });
        },
        [prefetchedIncidents, incidents]
    );

    // Tools field
    const [tools, setTools] = useState<ToolInfo[] | undefined>();
    const [toolsLoading, setToolsLoading] = useState<boolean>(true);
    const [selectedToolNames, setSelectedToolNames] = useState<string[]>();

    const onSelectedToolsChange = useCallback((newSelectedToolNames: string[]) => {
        setIsDirty(true);
        setSelectedToolNames(newSelectedToolNames);
    }, []);

    useEffect(() => {
        if (mode === 'create' && !selectedToolNames && tools) {
            setSelectedToolNames(tools.map(tool => tool.name));
        }
    }, [tools, selectedToolNames, mode]);

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
            incidentFilterId: handlerCreateOrEditInfo.filter?.id,
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
                incidents: selectedIncidentIds ?? [],
                tools: selectedToolNames ?? [],
                customInstructions: customInstructions,
                existingInstructions: '',
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
        handlerCreateOrEditInfo.filter?.id,
        azPortalContext,
        resourceId,
        incidentHandlerClient.generateInstructions,
        agentName,
        selectedIncidentIds,
        selectedToolNames,
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
            incidentFilterId: handlerCreateOrEditInfo?.filter?.id,
            incidentProcessingGuide: incidentProcessingGuide.replace('\r\n', '\n').replace('\r', '\n').split('\n'),
            tools: selectedToolNames,
            incidents: selectedIncidentIds || [],
            customInstructions: customInstructions,
        };
        setEditorDisplayValue(JSON.stringify(config, null, 4));
    }, [
        name,
        description,
        handlerCreateOrEditInfo?.filter?.id,
        incidentProcessingGuide,
        selectedToolNames,
        selectedIncidentIds,
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

    const incidentsListDivRef = useRef<HTMLDivElement | null>(null);
    const [isLoadingInitialIncidents, setIsLoadingInitialIncidents] = useState<boolean>(true);
    const [hasMoreOldIncidents, setHasMoreOldIncidents] = useState<boolean>(true);
    const isLoadingOldIncidents = useRef<boolean>(false);
    const loadOldIncidentCallId = useRef<string>(Guid.newShortGuid());
    const incidentsPageNumber = useRef<number>(0);

    const loadMoreOldIncidents = useCallback(
        async (overflowDiv: boolean): Promise<boolean | undefined> => {
            if (!isLoadingInitialIncidents && !isLoadingOldIncidents.current) {
                const callId = loadOldIncidentCallId.current;
                isLoadingOldIncidents.current = true;

                const numberOfIncidentsToLoad = overflowDiv
                    ? getNumberOfincidentsToOverflowincidentsListDiv(incidentsListDivRef.current?.clientHeight, incidents?.length || 0)
                    : defaultNumberOfIncidentsToLoad;

                const oldIncidentsResponse = await incidentHandlerClient.queryIncidents({
                    filter: {
                        id: handlerCreateOrEditInfo?.filter?.id,
                    },
                    durationInDays: selectedTimespan,
                    pageSize: pageSize,
                    pageNumber: ++incidentsPageNumber.current,
                    statuses: [IncidentStatus.resolved, IncidentStatus.mitigated],
                });

                if (callId === loadOldIncidentCallId.current) {
                    const olderIncidents = oldIncidentsResponse.content?.items ?? [];
                    if (oldIncidentsResponse.isSuccessful && olderIncidents.length < numberOfIncidentsToLoad) {
                        setHasMoreOldIncidents(false);
                    }
                    setIncidents(prevIncidents => {
                        if (!prevIncidents) {
                            return olderIncidents;
                        }

                        const olderIncidentsToAdd = olderIncidents.filter(
                            oldIncident => !prevIncidents.some(prevIncident => prevIncident.id === oldIncident.id)
                        );

                        const newIncidents = [...prevIncidents, ...olderIncidentsToAdd].sort((a, b) =>
                            b.createdAt.localeCompare(a.createdAt)
                        );
                        return newIncidents;
                    });

                    isLoadingOldIncidents.current = false;

                    return oldIncidentsResponse.isSuccessful;
                } else {
                    isLoadingOldIncidents.current = false;
                    return undefined;
                }
            }
        },
        [isLoadingInitialIncidents, incidentHandlerClient.queryIncidents, selectedTimespan, handlerCreateOrEditInfo?.filter?.id]
    );

    useEffect(() => {
        let subscribed = true;

        setToolsLoading(true);
        setTools(undefined);
        incidentHandlerClient.listTools().then(response => {
            if (subscribed) {
                setToolsLoading(false);
                if (response.isSuccessful && response.content) {
                    setTools(response.content.sort((a, b) => a.name.localeCompare(b.name)));
                }
            }
        });

        return () => {
            subscribed = false;
        };
    }, [incidentHandlerClient, incidentHandlerClient.listTools]);

    useEffect(() => {
        let subscribed = true;

        if (mode === 'create' || !!initialSelectedIncidentIds) {
            loadOldIncidentCallId.current = Guid.newShortGuid();
            setHasMoreOldIncidents(true);
            setIsLoadingInitialIncidents(true);
            setLoadingIncidents(true);
            incidentsPageNumber.current = 0;

            const queryPayload: IncidentQueryRequest = {
                filter: {
                    id: handlerCreateOrEditInfo?.filter?.id,
                },
                durationInDays: selectedTimespan,
                pageSize: pageSize,
                pageNumber: ++incidentsPageNumber.current,
                statuses: [IncidentStatus.resolved, IncidentStatus.mitigated],
            };

            const filteredIncidentsPromise = incidentHandlerClient.queryIncidents(queryPayload);

            const shouldPrefetchIncidents = mode !== 'create' && !!initialSelectedIncidentIds && !incidentsInitialized.current;

            const initialSelectionsPromises = shouldPrefetchIncidents
                ? Promise.all(initialSelectedIncidentIds.map(id => incidentHandlerClient.getIncident(id)))
                : Promise.resolve([]);

            Promise.all([filteredIncidentsPromise, initialSelectionsPromises]).then(response => {
                if (!subscribed) {
                    return;
                }

                const [filteredIncidentsResponse, initialSelectionsResponses] = response;

                if (filteredIncidentsResponse.isSuccessful && filteredIncidentsResponse.content) {
                    const filteredIncidents = filteredIncidentsResponse.content.items.sort((a, b) =>
                        b.createdAt.localeCompare(a.createdAt)
                    );

                    setIncidents(filteredIncidents);

                    if (shouldPrefetchIncidents) {
                        const prefetchedIncidents: IncidentDocument[] = [];
                        initialSelectionsResponses.forEach(result => {
                            if (result.isSuccessful && result.content) {
                                prefetchedIncidents.push(result.content);
                            }
                        });
                        setPrefetchedIncidents(prefetchedIncidents);
                        setSelectedIncidents(prefetchedIncidents);
                        setSelectedIncidentIds(prefetchedIncidents.map(incident => incident.id));
                    } else if (!incidentsInitialized.current) {
                        const latestThreeIncidents = filteredIncidents.slice(0, 3);
                        setSelectedIncidents(latestThreeIncidents);
                        setSelectedIncidentIds(latestThreeIncidents.map(incident => incident.id));
                    }
                } else {
                    setIncidents([]);
                }

                setIsLoadingInitialIncidents(false);
                setLoadingIncidents(false);
                incidentsInitialized.current = true;
            });
        }

        return () => {
            subscribed = false;
        };
    }, [incidentHandlerClient, handlerCreateOrEditInfo?.filter?.id, selectedTimespan, mode, initialSelectedIncidentIds]);

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
                incidentFilterId: handlerCreateOrEditInfo.filter?.id,
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
                        setInitialSelectedIncidentIds([]);
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
                        setInitialSelectedIncidentIds(getResult.content.incidents);
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
            setInitialSelectedIncidentIds([]);
            setHandlerLoading(false);
        }

        return () => {
            subscribed = false;
        };
    }, [
        incidentHandlerClient.getHandler,
        handlerCreateOrEditInfo.handlerId,
        handlerCreateOrEditInfo.filter?.id,
        azPortalContext.log,
        resourceId,
        azPortalContext,
        incidentHandlerClient,
    ]);

    return {
        exitToHome,
        goToFullEditMode,
        isDirty,
        agentName,
        incidentFilterId: handlerCreateOrEditInfo?.filter?.id,
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

        // For paginating incidents on scroll
        incidentsListDivRef,
        isLoadingInitialIncidents,
        hasMoreOldIncidents,
        loadMoreOldIncidents,

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
        selectedIncidentIds,
        selectedIncidents,
        onSelectedIncidentsChange,

        // Tools field
        tools: tools || [],
        selectedToolNames,
        onSelectedToolsChange,

        // Custom instructions field
        customInstructions,
        onCustomInstructionsChange,

        // Incident processing guide field
        incidentProcessingGuide,
    };
};

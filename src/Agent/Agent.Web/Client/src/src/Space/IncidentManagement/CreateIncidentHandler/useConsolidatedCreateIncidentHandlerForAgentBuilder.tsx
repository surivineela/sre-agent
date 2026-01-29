import { useFormikContext } from 'formik';
import isEqual from 'lodash/isEqual';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getDataPlaneErrorMessage } from '../../../Common/Clients/DataPlaneClient';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import {
    IncidentDocument,
    IncidentFilter,
    IncidentFilterDocumentPayload,
    IncidentHandler,
    IncidentQueryRequest,
    ToolInfo,
} from '../../../Common/Contracts/Azure/IncidentHandler';
import { AgentMode, IncidentManagementType, IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../../Common/Helpers/Guid';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import { IncidentHandlerCreateResources, IncidentManagementNotificationResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { getFilterValues } from '../Utilities';
import { useFilterConflictDetection } from './Common/useFilterConflictDetection';
import { FilterMode, HandlerCreateOrEditInfo, HandlerMode, OperationStatus, TimeDuration } from './Contracts';
import { IncidentHandlerCreateSteps } from './IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from './IncidentHandlerCreateFormValues';
import { usePreviewIncidents } from './usePreviewIncidents';
import { useTestHandler } from './useTestHandler';

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

const getSaveOrUpdateActionForFilter = (
    originalFilter: IncidentFilter | undefined,
    formValues: IncidentHandlerCreateFormValues,
    incidentPlatformType: IncidentManagementType | undefined
): 'create-incidentFilter' | 'update-incidentFilter' | undefined => {
    if (!originalFilter) return 'create-incidentFilter';

    const originalFilterValues = getFilterValues(originalFilter, incidentPlatformType, false, '', []);
    const currentFilterValues = getFilterValues(formValues, incidentPlatformType, true, '', []);
    if (isEqual(originalFilterValues, currentFilterValues)) return undefined;

    return 'update-incidentFilter';
};

const getSaveUpdateOrDeleteActionForHandler = (
    originalHandler: IncidentHandler | undefined,
    formValues: IncidentHandlerCreateFormValues
): 'create-incidentHandler' | 'update-incidentHandler' | 'delete-incidentHandler' | undefined => {
    if (!formValues.useCustomHandler) {
        return !originalHandler ? undefined : 'delete-incidentHandler';
    }

    if (!originalHandler) return 'create-incidentHandler';

    const originalHandlerValues = {
        incidentProcessingGuide: originalHandler.incidentProcessingGuide,
        tools: originalHandler.tools.sort((a, b) => a.localeCompare(b)),
        incidents: originalHandler.incidents.sort((a, b) => a.localeCompare(b)),
        customInstructions: originalHandler.customInstructions,
    };

    const currentHandlerValues = {
        incidentProcessingGuide: formValues.incidentProcessingGuide?.replace('\r\n', '\n').replace('\r', '\n').split('\n') || [],
        tools: (formValues.toolNames || []).sort((a, b) => a.localeCompare(b)),
        incidents: (formValues.incidentIds || []).sort((a, b) => a.localeCompare(b)),
        customInstructions: formValues.customInstructions,
    };

    if (isEqual(originalHandlerValues, currentHandlerValues)) return undefined;

    return 'update-incidentHandler';
};

const splitMcpAndNonMcpTools = (toolNames: string[], allMcpToolNames: Set<string>) => {
    const mcpTools: string[] = [];
    const nonMcpTools: string[] = [];

    toolNames.forEach(name => {
        if (allMcpToolNames.has(name)) {
            mcpTools.push(name);
        } else {
            nonMcpTools.push(name);
        }
    });

    return { mcpTools, nonMcpTools };
};

export const useConsolidatedCreateIncidentHandlerforAgentBuilder = (
    exitToHome: (filterId?: string, handlerId?: string, isNew?: boolean) => void,
    setHandlerOperationStatus: React.Dispatch<React.SetStateAction<OperationStatus | undefined>>,
    handlerCreateOrEditInfo: HandlerCreateOrEditInfo,
    setInitialValues: React.Dispatch<React.SetStateAction<IncidentHandlerCreateFormValues>>
) => {
    const intl = useIntl();
    const {
        incidentManagement: { incidentPlatformType },
    } = useContext(SreAgentContext);
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

    const isSubagentTrigger = useMemo(() => !!handlerCreateOrEditInfo?.subAgentTriggerInfo, [handlerCreateOrEditInfo?.subAgentTriggerInfo]);
    const incidentTriggerWithLearningsMetadata = useMemo(
        () => handlerCreateOrEditInfo?.incidentTriggerWithLearningsInfo,
        [handlerCreateOrEditInfo?.incidentTriggerWithLearningsInfo]
    );

    const subAgentNames = useMemo(
        () => handlerCreateOrEditInfo?.subAgentTriggerInfo?.agents || [],
        [handlerCreateOrEditInfo?.subAgentTriggerInfo?.agents]
    );

    const filterMode = useMemo<FilterMode>(() => (handlerCreateOrEditInfo.filter ? 'edit' : 'create'), [handlerCreateOrEditInfo.filter]);
    const [handlerMode, setHandlerMode] = useState<HandlerMode>(
        !handlerCreateOrEditInfo.handlerId ? 'create' : handlerCreateOrEditInfo.quickEdit ? 'quickEdit' : 'edit'
    );
    const [currentStep, setCurrentStep] = useState<IncidentHandlerCreateSteps>(
        isSubagentTrigger || incidentTriggerWithLearningsMetadata
            ? IncidentHandlerCreateSteps.IncidentTriggerStep
            : IncidentHandlerCreateSteps.FilterStep
    );

    const { values, setFieldValue } = useFormikContext<IncidentHandlerCreateFormValues>();

    const [existingFilters, setExistingFilters] = useState<IncidentFilter[]>([]);

    // Timespan field
    const [selectedTimespan, setSelectedTimespan] = useState<TimeDuration>(
        incidentPlatformType === IncidentManagementType.AzMonitor
            ? TimeDuration.Last15Days
            : handlerMode === 'create'
              ? TimeDuration.Last60Days
              : TimeDuration.Last90Days
    );
    const onSelectedTimespanChange = useCallback((value: TimeDuration) => {
        setSelectedTimespan(value);
    }, []);

    // Incidents field

    const [prefetchedIncidents, setPrefetchedIncidents] = useState<IncidentDocument[]>([]);
    const [incidents, setIncidents] = useState<IncidentDocument[]>();
    const [loadingIncidents, setLoadingIncidents] = useState<boolean>(true);
    const [selectedIncidents, setSelectedIncidents] = useState<IncidentDocument[]>();

    const [initialSelectedIncidentIds, setInitialSelectedIncidentIds] = useState<string[]>();
    const queryFilterCacheKey = useRef<string>();

    const onSelectedIncidentsChange = useCallback(
        (newSelectedIncidentIds: string[]) => {
            setFieldValue('incidentIds', newSelectedIncidentIds);
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
        [prefetchedIncidents, incidents, setFieldValue]
    );

    const allMcpToolNames = useMemo(() => {
        const names = new Set<string>();
        handlerCreateOrEditInfo.incidentTriggerWithLearningsInfo?.mcpConnections?.forEach(connection => {
            connection.tools?.forEach(tool => {
                names.add(tool.name);
            });
        });
        return names;
    }, [handlerCreateOrEditInfo.incidentTriggerWithLearningsInfo?.mcpConnections]);

    // Tools field
    const [tools, setTools] = useState<ToolInfo[] | undefined>();
    const [toolsLoading, setToolsLoading] = useState<boolean>(true);

    const [generatingUpdatedTools, setGeneratingUpdatedTools] = useState<boolean>(false);

    const generateUpdatedTools = useCallback(() => {
        setGeneratingUpdatedTools(true);

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
                incidents: values.incidentIds ?? [],
                tools: tools?.map(tool => tool.name) ?? [],
                customInstructions: values.incidentProcessingGuide ?? '',
                existingInstructions: '',
            })
            .then(toolsUpdateResult => {
                setGeneratingUpdatedTools(false);
                if (!toolsUpdateResult.isSuccessful || !toolsUpdateResult.content) {
                    // TODO (andimarc): Surface errors to the user.
                    const error = !toolsUpdateResult.isSuccessful
                        ? getDataPlaneErrorMessage(toolsUpdateResult.error)
                        : 'No content returned';
                    azPortalContext.log({
                        action: 'generate-updated-tools',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId: resourceId,
                        data: { ...additionalInfo, error },
                    });
                } else {
                    azPortalContext.log({
                        action: 'generate-updated-tools',
                        actionModifier: 'success',
                        logLevel: 'info',
                        resourceId: resourceId,
                        data: additionalInfo,
                    });
                    setFieldValue('toolNames', toolsUpdateResult.content.tools);
                }
            });
    }, [
        handlerCreateOrEditInfo.filter?.id,
        azPortalContext,
        resourceId,
        incidentHandlerClient.generateInstructions,
        agentName,
        tools,
        values.incidentIds,
        values.incidentProcessingGuide,
        setFieldValue,
    ]);

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
                incidents: values.incidentIds ?? [],
                tools: [],
                customInstructions: values.customInstructions ?? '',
            })
            .then(instructionsResult => {
                setGeneratingInstructions(false);
                if (!instructionsResult.isSuccessful || !instructionsResult.content) {
                    // TODO (andimarc): Surface errors to the user.
                    const error = !instructionsResult.isSuccessful
                        ? getDataPlaneErrorMessage(instructionsResult.error)
                        : 'No content returned';
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
                    setGenerateInstructionsStepSkipped(false);
                    setFieldValue('incidentProcessingGuide', instructionsResult.content.generatedInstructions);
                    setFieldValue('toolNames', instructionsResult.content.tools);

                    if (values.isIncidentTriggerWithLearnings) {
                        setFieldValue('subagentInstructions', instructionsResult.content.generatedInstructions);
                        const { mcpTools, nonMcpTools } = splitMcpAndNonMcpTools(instructionsResult.content.tools, allMcpToolNames);
                        setFieldValue('subagentToolNames', nonMcpTools);
                        setFieldValue('subagentMcpToolNames', mcpTools);
                    }

                    setCurrentStep(
                        values.isIncidentTriggerWithLearnings
                            ? IncidentHandlerCreateSteps.CreateSubagentStep
                            : IncidentHandlerCreateSteps.ReviewAndTestStep
                    );
                }
            });
    }, [
        handlerCreateOrEditInfo.filter?.id,
        azPortalContext,
        resourceId,
        incidentHandlerClient.generateInstructions,
        agentName,
        values.incidentIds,
        values.customInstructions,
        values.isIncidentTriggerWithLearnings,
        setFieldValue,
        allMcpToolNames,
    ]);

    const deleteHandler = useCallback(() => {
        const {
            customHandlerDeleteNotificationTitle,
            customHandlerDeleteNotificationInProgress,
            customHandlerDeleteNotificationFailure,
            customHandlerDeleteNotificationSuccess,
        } = IncidentHandlerCreateResources;

        if (handlerCreateOrEditInfo?.handlerId) {
            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(customHandlerDeleteNotificationTitle),
                intl.formatMessage(customHandlerDeleteNotificationInProgress)
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
                    const errorMessage = deleteResult.error;
                    azPortalContext.log({
                        action: 'delete-incidentHandler',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId: resourceId,
                        data: { ...additionalInfo, errorMessage },
                    });
                    setHandlerOperationStatus('failed');
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(customHandlerDeleteNotificationFailure, {
                            errorMessage,
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

    const saveHandler = useCallback(
        async (handlingAgentOverride?: string) => {
            const saveOrUpdateFilterAction = getSaveOrUpdateActionForFilter(handlerCreateOrEditInfo?.filter, values, incidentPlatformType);
            const saveUpdateOrDeleteHandlerAction = getSaveUpdateOrDeleteActionForHandler(handler, values);

            if (!saveOrUpdateFilterAction && !saveUpdateOrDeleteHandlerAction) {
                exitToHome();
                return;
            }

            const [notificationTitle, notificationDescription, notificationErrorMessage, notificationSuccessMessage] =
                saveOrUpdateFilterAction === 'create-incidentFilter'
                    ? [
                          IncidentManagementNotificationResources.createFilterTitle,
                          IncidentManagementNotificationResources.createFilterInProgress,
                          IncidentManagementNotificationResources.createFilterError,
                          IncidentManagementNotificationResources.createFilterSuccess,
                      ]
                    : [
                          IncidentManagementNotificationResources.updateFilterTitle,
                          IncidentManagementNotificationResources.updateFilterInProgress,
                          IncidentManagementNotificationResources.updateFilterError,
                          IncidentManagementNotificationResources.updateFilterSuccess,
                      ];

            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(notificationTitle),
                intl.formatMessage(notificationDescription)
            );

            if (saveOrUpdateFilterAction) {
                const filterValues = getFilterValues(values, incidentPlatformType, true, '', []);
                const filterPayload: IncidentFilterDocumentPayload = {
                    id: values.filterName || '',
                    ...filterValues,
                    ...(handlingAgentOverride ? { handlingAgent: handlingAgentOverride } : {}),
                };

                const additionalInfo = {
                    filterName: values.filterName || '',
                };

                azPortalContext.log({
                    action: saveOrUpdateFilterAction,
                    actionModifier: 'start',
                    logLevel: 'info',
                    resourceId: resourceId,
                    data: additionalInfo,
                });

                setHandlerOperationStatus('inprogress');

                const saveOrUpdateFilterFunction =
                    saveOrUpdateFilterAction === 'create-incidentFilter'
                        ? incidentHandlerClient.createIncidentFilter
                        : incidentHandlerClient.updateIncidentFilter;

                const saveOrUpdateFilterResult = await saveOrUpdateFilterFunction(filterPayload);
                if (!saveOrUpdateFilterResult.isSuccessful) {
                    const errorMessage = saveOrUpdateFilterResult.error;
                    azPortalContext.log({
                        action: saveOrUpdateFilterAction,
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId: resourceId,
                        data: { ...additionalInfo, errorMessage },
                    });
                    setHandlerOperationStatus('failed');
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(notificationErrorMessage, {
                            errorMessage,
                        })
                    );
                    return;
                } else {
                    azPortalContext.log({
                        action: saveOrUpdateFilterAction,
                        actionModifier: 'success',
                        logLevel: 'info',
                        resourceId: resourceId,
                        data: additionalInfo,
                    });
                    setHandlerOperationStatus('succeeded');
                }
            }

            if (!saveUpdateOrDeleteHandlerAction || isSubagentTrigger || incidentTriggerWithLearningsMetadata) {
                azPortalContext.stopNotification(notificationId, true, intl.formatMessage(notificationSuccessMessage));
                exitToHome(values.filterName, handlerCreateOrEditInfo?.handlerId || values.filterName, !handlerCreateOrEditInfo?.filter);
                return;
            }

            const handlerPayload: IncidentHandler = {
                id: handlerCreateOrEditInfo?.handlerId || values.filterName || Guid.newShortGuid(),
                name: '',
                description: '',
                incidentFilterId: values.filterName || '',
                incidentProcessingGuide: values.incidentProcessingGuide?.replace('\r\n', '\n').replace('\r', '\n').split('\n') || [],
                tools: values.toolNames || [],
                incidents: values.incidentIds || [],
                customInstructions: values.customInstructions || '',
            };

            const additionalInfo = {
                filterName: values.filterName || '',
            };

            azPortalContext.log({
                action: saveUpdateOrDeleteHandlerAction,
                actionModifier: 'start',
                logLevel: 'info',
                resourceId: resourceId,
                data: additionalInfo,
            });

            setHandlerOperationStatus('inprogress');

            const saveUpdateOrDeleteHandlerFunction =
                saveUpdateOrDeleteHandlerAction === 'delete-incidentHandler'
                    ? async () => await incidentHandlerClient.deleteHandler(handlerCreateOrEditInfo?.handlerId || '')
                    : saveUpdateOrDeleteHandlerAction === 'create-incidentHandler'
                      ? async () => await incidentHandlerClient.createHandler(handlerPayload)
                      : async () => await incidentHandlerClient.updateHandler(handlerPayload);

            const saveUpdateOrDeleteHandlerResult = await saveUpdateOrDeleteHandlerFunction();
            if (!saveUpdateOrDeleteHandlerResult.isSuccessful) {
                const errorMessage = saveUpdateOrDeleteHandlerResult.error;
                azPortalContext.log({
                    action: saveUpdateOrDeleteHandlerAction,
                    actionModifier: 'failed',
                    logLevel: 'error',
                    resourceId: resourceId,
                    data: { ...additionalInfo, errorMessage },
                });
                setHandlerOperationStatus('failed');
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(notificationErrorMessage, {
                        errorMessage,
                    })
                );
                // TODO (andimarc): Revert/delete the filter if it was created/updated.
            } else {
                azPortalContext.log({
                    action: saveUpdateOrDeleteHandlerAction,
                    actionModifier: 'success',
                    logLevel: 'info',
                    resourceId: resourceId,
                    data: additionalInfo,
                });
                setHandlerOperationStatus('succeeded');
                azPortalContext.stopNotification(notificationId, true, intl.formatMessage(notificationSuccessMessage));
                exitToHome(values.filterName, handlerCreateOrEditInfo?.handlerId || values.filterName, !handlerCreateOrEditInfo?.filter);
            }
        },
        [
            azPortalContext,
            exitToHome,
            incidentHandlerClient,
            intl,
            resourceId,
            setHandlerOperationStatus,

            handlerCreateOrEditInfo,
            handlerCreateOrEditInfo?.filter,
            handlerCreateOrEditInfo?.handlerId,
            handler,
            incidentPlatformType,
            isSubagentTrigger,
            values.filterName,
            values.incidentType,
            values.impactedService,
            values.priorities,
            values.titleContains,
            values.agentMode,
            values.owningTeamId,
            values.createdBy,
            values.monitorId,
            values.handlingAgent,
            values.handlingAgents,
            values.triggers,

            values.incidentIds,
            values.customInstructions,
            values.toolNames,
            values.incidentProcessingGuide,

            values.useCustomHandler,

            values.deepInvestigationEnabled,
        ]
    );

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

    const goToFullEditMode = useCallback(() => {
        setCurrentStep(IncidentHandlerCreateSteps.IncidentsAndGuidanceStep);
        setHandlerMode('edit');
    }, []);

    const queryFilterValues = useMemo(
        () =>
            getFilterValues(
                {
                    impactedService: values.impactedService,
                    priorities: values.priorities,
                    incidentType: values.incidentType,
                    titleContains: values.titleContains,
                    owningTeamId: values.owningTeamId,
                    createdBy: values.createdBy,
                    monitorId: values.monitorId,
                },
                incidentPlatformType,
                true,
                ''
            ),
        [
            values.impactedService,
            values.priorities,
            values.incidentType,
            values.titleContains,
            values.owningTeamId,
            values.createdBy,
            values.monitorId,
            incidentPlatformType,
        ]
    );

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
                    filter: queryFilterValues,
                    durationInDays: selectedTimespan,
                    pageSize: pageSize,
                    pageNumber: ++incidentsPageNumber.current,
                    statuses: [IncidentStatus.resolved, IncidentStatus.mitigated, IncidentStatus.closed],
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
        [
            isLoadingInitialIncidents,
            incidentHandlerClient.queryIncidents,
            selectedTimespan,
            incidentPlatformType,
            queryFilterValues,
            incidents?.length,
        ]
    );

    const handlerTestMetadata = useTestHandler(resourceId, handlerCreateOrEditInfo, values, incidentHandlerClient);
    const incidentsPreviewMetadata = usePreviewIncidents();

    const conflictingFilters = useFilterConflictDetection({
        currentFilterId: handlerCreateOrEditInfo?.filter?.id || values.filterName,
        currentOwningTeamId: values.owningTeamId,
        currentIncidentType: values.incidentType,
        currentImpactedService: values.impactedService,
        currentPriorities: values.priorities,
        currentTriggers: values.triggers,
        existingFilters,
    });

    useEffect(() => {
        let subscribed = true;

        setExistingFilters([]);
        incidentHandlerClient.listIncidentFilters().then(response => {
            if (subscribed) {
                setToolsLoading(false);
                if (response.isSuccessful && response.content) {
                    setExistingFilters(response.content);
                }
            }
        });

        return () => {
            subscribed = false;
        };
    }, [incidentHandlerClient]);

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

    // Load incidents when filter changes or timespan changes
    useEffect(() => {
        let subscribed = true;

        if (handlerMode === 'create' || !!initialSelectedIncidentIds) {
            loadOldIncidentCallId.current = Guid.newShortGuid();
            setHasMoreOldIncidents(true);
            setIsLoadingInitialIncidents(true);
            setLoadingIncidents(true);
            incidentsPageNumber.current = 0;

            const queryPayload: IncidentQueryRequest = {
                filter: queryFilterValues,
                durationInDays: selectedTimespan,
                pageSize: pageSize,
                pageNumber: ++incidentsPageNumber.current,
                statuses: [IncidentStatus.resolved, IncidentStatus.mitigated, IncidentStatus.closed],
            };

            const filteredIncidentsPromise = incidentHandlerClient.queryIncidents(queryPayload);

            const currentFilterCacheKey = JSON.stringify(queryFilterValues);
            const filterChanged = queryFilterCacheKey.current !== currentFilterCacheKey;

            const shouldPrefetchIncidents = handlerMode !== 'create' && !!initialSelectedIncidentIds && filterChanged;

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
                        setFieldValue(
                            'incidentIds',
                            prefetchedIncidents.map(incident => incident.id)
                        );
                    } else if (filterChanged) {
                        const latestThreeIncidents = filteredIncidents.slice(0, 3);
                        setSelectedIncidents(latestThreeIncidents);
                        setFieldValue(
                            'incidentIds',
                            latestThreeIncidents.map(incident => incident.id)
                        );
                    }
                } else {
                    setIncidents([]);
                }

                setIsLoadingInitialIncidents(false);
                setLoadingIncidents(false);
                queryFilterCacheKey.current = currentFilterCacheKey;
            });
        }

        return () => {
            subscribed = false;
        };
    }, [
        selectedTimespan,
        setFieldValue,
        incidentHandlerClient,
        handlerMode,
        initialSelectedIncidentIds,
        incidentPlatformType,
        queryFilterValues,
    ]);

    useEffect(() => {
        if (!handlerCreateOrEditInfo.handlerId) {
            setHandlerMode('create');
        } else if (handlerCreateOrEditInfo.quickEdit) {
            setHandlerMode('quickEdit');
        } else {
            setHandlerMode('edit');
        }
    }, [handlerCreateOrEditInfo.handlerId, handlerCreateOrEditInfo.quickEdit]);

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
                        const error = !getResult.isSuccessful ? getDataPlaneErrorMessage(getResult.error) : 'No content returned';
                        azPortalContext.log({
                            action: 'get-incidentHandler',
                            actionModifier: 'failed',
                            logLevel: 'error',
                            resourceId: resourceId,
                            data: { ...additionalInfo, error },
                        });
                        // TODO (andimarc): Surface errors to the user.
                        setFieldValue('toolNames', []);
                        setFieldValue('incidentProcessingGuide', '');
                        setFieldValue('incidentIds', []);
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

                        setFieldValue('toolNames', getResult.content.tools);
                        setFieldValue('incidentIds', getResult.content.incidents);
                        setInitialSelectedIncidentIds(getResult.content.incidents);
                        setFieldValue('customInstructions', getResult.content.customInstructions || '');
                        setFieldValue('incidentProcessingGuide', getResult.content.incidentProcessingGuide.join('\n'));
                        setFieldValue('name', getResult.content.name);
                        setInitialValues(currentValue => {
                            return {
                                ...currentValue,
                                toolNames: getResult.content!.tools,
                                incidentIds: getResult.content!.incidents,
                                customInstructions: getResult.content!.customInstructions || '',
                                incidentProcessingGuide: getResult.content!.incidentProcessingGuide.join('\n'),
                                name: getResult.content!.name,
                            };
                        });
                        setHandlerLoaded(true);
                    }
                    setHandlerLoading(false);
                }
            });
        } else {
            setHandlerLoaded(true);
            setFieldValue('toolNames', undefined);
            setFieldValue('incidentIds', undefined);
            setInitialSelectedIncidentIds([]);
            setHandlerLoading(false);
        }

        return () => {
            subscribed = false;
        };
    }, [
        setFieldValue,
        setInitialValues,
        incidentHandlerClient.getHandler,
        handlerCreateOrEditInfo.handlerId,
        handlerCreateOrEditInfo.filter?.id,
        azPortalContext.log,
        resourceId,
        azPortalContext,
        incidentHandlerClient,
    ]);

    useEffect(() => {
        setInitialValues(currentValue => {
            const newValue = {
                ...currentValue,
                filterName: handlerCreateOrEditInfo.filter?.id || '',
                incidentType: handlerCreateOrEditInfo.filter?.incidentType,
                impactedService: handlerCreateOrEditInfo.filter?.impactedService,
                priorities: handlerCreateOrEditInfo.filter?.priorities,
                titleContains: handlerCreateOrEditInfo.filter?.titleContains,
                agentMode: handlerCreateOrEditInfo.filter?.agentMode || AgentMode.autonomous,
                deepInvestigationEnabled: handlerCreateOrEditInfo.filter?.deepInvestigationEnabled || false,
                owningTeamId: handlerCreateOrEditInfo.filter?.owningTeamId || '',
                createdBy: handlerCreateOrEditInfo.filter?.createdBy || '',
                monitorId: handlerCreateOrEditInfo.filter?.monitorId || '',
                handlingAgent:
                    handlerCreateOrEditInfo.filter?.handlingAgent || handlerCreateOrEditInfo.subAgentTriggerInfo?.preSelectedAgent,
                useCustomHandler: !!handlerCreateOrEditInfo.handlerId || isSubagentTrigger,
            };

            return newValue;
        });
    }, [
        intl,
        setInitialValues,
        isSubagentTrigger,
        handlerCreateOrEditInfo.filter?.id,
        handlerCreateOrEditInfo.filter?.incidentType,
        handlerCreateOrEditInfo.filter?.impactedService,
        handlerCreateOrEditInfo.filter?.priorities,
        handlerCreateOrEditInfo.filter?.titleContains,
        handlerCreateOrEditInfo.filter?.owningTeamId,
        handlerCreateOrEditInfo.filter?.createdBy,
        handlerCreateOrEditInfo.filter?.monitorId,
        handlerCreateOrEditInfo.filter?.agentMode,
        handlerCreateOrEditInfo.filter?.handlingAgent,
        handlerCreateOrEditInfo.handlerId,
        handlerCreateOrEditInfo.subAgentTriggerInfo?.preSelectedAgent,
    ]);

    return {
        exitToHome,
        goToFullEditMode,
        agentName,
        incidentPlatformType,
        currentStep,
        setCurrentStep,
        generateInstructionsStepSkipped,
        setGenerateInstructionsStepSkipped,
        isSubagentTrigger,
        subAgentNames,
        filterMode,
        handlerMode,
        loadingIncidents,
        toolsLoading,
        handlerLoading,
        handlerLoaded,
        generatingInstructions,
        generateInstructions,
        generatingUpdatedTools,
        generateUpdatedTools,
        deleteHandler,
        exportHandler,
        saveHandler,

        // For paginating incidents on scroll
        incidentsListDivRef,
        isLoadingInitialIncidents,
        hasMoreOldIncidents,
        loadMoreOldIncidents,

        selectedTimespan,
        onSelectedTimespanChange,
        incidents,
        selectedIncidents,
        onSelectedIncidentsChange,
        tools: tools || [],

        handlerTestMetadata,
        incidentsPreviewMetadata,
        conflictingFilters,
        incidentTriggerWithLearningsMetadata,
    };
};

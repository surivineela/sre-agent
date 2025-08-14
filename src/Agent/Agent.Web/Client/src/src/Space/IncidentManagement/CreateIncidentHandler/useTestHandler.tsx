import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getDataPlaneErrorMessage } from '../../../Common/Clients/DataPlaneClient';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import { IncidentDocument, IncidentQueryRequest } from '../../../Common/Contracts/Azure/IncidentHandler';
import { AgentMode, IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../../Common/Helpers/Guid';
import { IncidentHandlerCreateResources } from '../../../Strings/SREAgentResources';
import { HandlerCreateOrEditInfo, TimeDuration } from './Contracts';
import { IncidentHandlerCreateFormValues } from './IncidentHandlerCreateFormValues';

const pageSize = 20;

export const useTestHandler = (
    resourceId: string,
    handlerCreateOrEditInfo: HandlerCreateOrEditInfo,
    values: IncidentHandlerCreateFormValues,
    incidentHandlerClient: IncidentHandlerClient
) => {
    const intl = useIntl();
    const azPortalContext = useContext(AzPortalContext);

    const [searchTerm, setSearchTerm] = useState<string>('');
    const [incidents, setIncidents] = useState<IncidentDocument[]>();
    const [loadingIncidents, setLoadingIncidents] = useState<boolean>(true);

    const loadIncidentsCallId = useRef<string>(Guid.newShortGuid());

    const [creatingTestThread, setCreatingTestThread] = useState<boolean>(false);
    const [createTestThreadFailure, setCreateTestThreadFailure] = useState<string>();
    const [testIncidentThreadId, setTestIncidentThreadId] = useState<string>();

    useEffect(() => {
        let subscribed = true;

        loadIncidentsCallId.current = Guid.newShortGuid();
        setLoadingIncidents(true);

        const queryPayload: IncidentQueryRequest = {
            filter: {
                impactedService: values.impactedService === 'ALL' ? undefined : values.impactedService,
                priority: values.priority === 'ALL' ? undefined : values.priority,
                incidentType: values.incidentType === 'ALL' ? undefined : values.incidentType,
                titleContains: values.titleContains,
            },
            durationInDays: TimeDuration.Last90Days,
            pageSize: pageSize,
            pageNumber: 0,
            statuses: [IncidentStatus.resolved, IncidentStatus.mitigated],
            searchTerm: searchTerm,
        };

        incidentHandlerClient.queryIncidents(queryPayload).then(filteredIncidentsResponse => {
            if (!subscribed) {
                return;
            }

            if (filteredIncidentsResponse.isSuccessful && filteredIncidentsResponse.content) {
                const filteredIncidents = filteredIncidentsResponse.content.items.sort((a, b) => b.createdAt.localeCompare(a.createdAt));
                setIncidents(filteredIncidents);
            } else {
                setIncidents([]);
            }

            setLoadingIncidents(false);
        });

        return () => {
            subscribed = false;
        };
    }, [incidentHandlerClient, values.impactedService, values.priority, values.incidentType, values.titleContains, searchTerm]);

    const createTestThread = useCallback(async () => {
        azPortalContext.log({
            action: 'create-test-thread',
            actionModifier: 'start',
            logLevel: 'info',
            resourceId: resourceId,
        });

        setCreateTestThreadFailure(undefined);
        setCreatingTestThread(true);

        let testIncident = incidents?.find(incident => incident.id === searchTerm);
        if (!testIncident) {
            const getIncidentResult = await incidentHandlerClient.getIncident(searchTerm);
            if (!getIncidentResult.isSuccessful || !getIncidentResult.content) {
                const error = !getIncidentResult.isSuccessful ? getDataPlaneErrorMessage(getIncidentResult.error) : 'Incident not found';
                azPortalContext.log({
                    action: 'create-test-thread',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    resourceId: resourceId,
                    data: { error },
                });
                setCreateTestThreadFailure(
                    intl.formatMessage(IncidentHandlerCreateResources.testHandlerRunIncidentNotFound, { incidentId: searchTerm })
                );
                setCreatingTestThread(false);
                return;
            }
            testIncident = getIncidentResult.content;
        }

        let source: 'pagerDuty' | 'icm' | undefined = undefined;

        switch (testIncident?.documentType) {
            case 'PagerDutyIncident':
                source = 'pagerDuty';
                break;
            case 'IcmIncident':
                source = 'icm';
                break;
            default:
                break;
        }

        const testHandlerResult = await incidentHandlerClient.testHandler({
            title: testIncident?.title || '',
            description: testIncident?.description || '',
            incidentId: testIncident?.id || '',
            source: source,
            isTest: true,
            incidentHandler: {
                id: handlerCreateOrEditInfo?.handlerId || `${values.filterName || ''}-custom-handler`,
                name: '',
                description: '',
                incidentFilterId: values.filterName || '',
                incidentProcessingGuide: values.incidentProcessingGuide?.replace('\r\n', '\n').replace('\r', '\n').split('\n') || [],
                tools: values.toolNames || [],
                incidents: values.incidentIds || [],
                customInstructions: values.customInstructions || '',
            },
            incidentFilter: {
                id: values.filterName || '',
                incidentType: values.incidentType === 'ALL' ? undefined : values.incidentType,
                impactedService: values.impactedService === 'ALL' ? undefined : values.impactedService,
                priority: values.priority === 'ALL' ? undefined : values.priority,
                titleContains: values.titleContains || '',
                agentMode: values.agentMode || AgentMode.review,
            },
        });

        if (!testHandlerResult.isSuccessful || !testHandlerResult.content) {
            const error = !testHandlerResult.isSuccessful ? getDataPlaneErrorMessage(testHandlerResult.error) : 'No content returned';
            azPortalContext.log({
                action: 'create-test-thread',
                actionModifier: 'failed',
                logLevel: 'error',
                resourceId: resourceId,
                data: { error },
            });
            setCreateTestThreadFailure(error);
        } else {
            setTestIncidentThreadId(testHandlerResult.content.threadId);
            azPortalContext.log({
                action: 'create-test-thread',
                actionModifier: 'success',
                logLevel: 'info',
                resourceId: resourceId,
            });
        }
        setCreatingTestThread(false);
    }, [
        intl,
        resourceId,
        incidentHandlerClient.testHandler,
        handlerCreateOrEditInfo?.handlerId,
        searchTerm,
        values.filterName,
        values.incidentProcessingGuide,
        values.toolNames,
        values.incidentIds,
        values.customInstructions,
        values.incidentType,
        values.impactedService,
        values.priority,
        values.titleContains,
        values.agentMode,
    ]);

    return {
        searchTerm,
        setSearchTerm,
        incidents,
        loadingIncidents,
        createTestThread,
        createTestThreadFailure,
        creatingTestThread,
        testIncidentThreadId,
    };
};

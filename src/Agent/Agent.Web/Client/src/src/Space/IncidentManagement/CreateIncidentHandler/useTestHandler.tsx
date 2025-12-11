import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getDataPlaneErrorMessage } from '../../../Common/Clients/DataPlaneClient';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import { IncidentDocument, IncidentQueryRequest } from '../../../Common/Contracts/Azure/IncidentHandler';
import { AgentMode, IncidentManagementType, IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../../Common/Helpers/Guid';
import { SreAgentContext } from '../../Contracts/Context';
import { getFilterValues } from '../Utilities';
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
    const {
        incidentManagement: { incidentPlatformType },
    } = useContext(SreAgentContext);

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

        const filterValues = getFilterValues(values, incidentPlatformType, true, undefined);
        const queryPayload: IncidentQueryRequest = {
            filter: filterValues,
            durationInDays: TimeDuration.Last90Days,
            pageSize: pageSize,
            pageNumber: 1,
            statuses: [IncidentStatus.resolved, IncidentStatus.mitigated, IncidentStatus.closed],
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
    }, [
        incidentHandlerClient,
        values.impactedService,
        values.priority,
        values.incidentType,
        values.titleContains,
        values.owningTeamId,
        values.createdBy,
        values.monitorId,
        searchTerm,
    ]);

    const createTestThread = useCallback(async () => {
        azPortalContext.log({
            action: 'create-test-thread',
            actionModifier: 'start',
            logLevel: 'info',
            resourceId: resourceId,
        });

        setCreateTestThreadFailure(undefined);
        setCreatingTestThread(true);

        const testHandlerResult = await incidentHandlerClient.testHandler({
            incidentId: searchTerm || '',
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
                agentMode: values.agentMode || AgentMode.autonomous,
                owningTeamId: incidentPlatformType === IncidentManagementType.Icm ? values.owningTeamId || '' : undefined,
                createdBy: incidentPlatformType === IncidentManagementType.Icm ? values.createdBy || '' : undefined,
                monitorId: incidentPlatformType === IncidentManagementType.Icm ? values.monitorId || '' : undefined,
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
        values.owningTeamId,
        values.createdBy,
        values.monitorId,
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

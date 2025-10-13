import { useCallback, useContext, useEffect, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { useAzPortalContext } from '../../../../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AppInsightsClient } from '../../../../../../../Common/Clients/AppInsightsClient';
import { getErrorMessage } from '../../../../../../../Common/Clients/ArmClient';
import { useAuthToken } from '../../../../../../../Common/Hooks/useAuthToken';
import { ThreadTraceResources } from '../../../../../../../Strings/SREAgentResources';
import { ISpan, ThreadEventLog } from '../../../../../packages/components/tracing/src/types/trace';
import { generateSpans } from './traceParser';

export const getThreadTracesDataQuery = (threadId: string) => {
    return `
    let threadId = '${threadId}';
    customEvents
    | where customDimensions contains threadId
    | extend
        ThreadId = coalesce(tostring(customDimensions.ThreadId), tostring(customDimensions.ChatThreadId)),
        AgentName = coalesce(tostring(customDimensions.AgentName), tostring(customDimensions.SubAgentName)),
        EventType = tostring(customDimensions.EventType),
        TaskType = tostring(customDimensions.TaskType),
        Message = tostring(customDimensions.Message),
        FromAgent = tostring(customDimensions.FromAgent),
        ToAgent = tostring(customDimensions.ToAgent),
        ToolName = tostring(customDimensions.ToolName),
        ToolDescription = tostring(customDimensions.ToolDescription),
        ToolOutput = tostring(customDimensions.ToolOutput),
        UserId = tostring(customDimensions.UserId),
        DisplayName = tostring(customDimensions.DisplayName),
        ModelId = tostring(customDimensions.ModelId),
        Temperature = tostring(customDimensions.Temperature),
        InputTokens = tostring(customDimensions.InputTokens),
        ModelInput = tostring(customDimensions.ModelInput),
        OutputTokens = tostring(customDimensions.OutputTokens),
        ModelOutput = tostring(customDimensions.ModelOutput),
        Result = tostring(customDimensions.Result)
    | extend
        EventName = iff(name == 'MetaAgent', iff(isempty(Message), 'Incident', 'UserMessage'), name)
    | project
        timestamp,
        Event = bag_pack(
            'eventName', EventName,
            'threadId', ThreadId,
            'agentName', AgentName,
            'eventType', EventType,
            'taskType', TaskType,
            'message', Message,
            'fromAgent', FromAgent,
            'toAgent', ToAgent,
            'toolName', ToolName,
            'toolDescription', ToolDescription,
            'toolOutput', ToolOutput,
            'userId', UserId,
            'displayName', DisplayName,
            'modelId', ModelId,
            'temperature', Temperature,
            'inputTokens', InputTokens,
            'modelInput', ModelInput,
            'outputTokens', OutputTokens,
            'modelOutput', ModelOutput,
            'result', Result
        )
    | order by timestamp asc
    `;
};

export const useTracePanel = (appInsightsAppId: string, threadId: string, isIncidentThread: boolean) => {
    const appInsightsToken = useAuthToken('applicationinsightapi');
    const intl = useIntl();
    const { log } = useAzPortalContext();
    const { resourceId } = useContext(EnvironmentContext);
    const [loading, setLoading] = useState(false);
    const [warnings, setWarnings] = useState<string[]>([]);
    const [errors, setErrors] = useState<string[]>([]);
    const [loadingFailure, setLoadingFailure] = useState('');
    const [noDataMessage, setNoDataMessage] = useState('');
    const [parseErrorMessage, setParseErrorMessage] = useState('');
    const [spans, setSpans] = useState<ISpan[]>([]);

    const fetch = useCallback(async () => {
        if (!appInsightsToken) return;

        setSpans([]);
        setLoading(true);
        setWarnings([]);
        setErrors([]);
        setLoadingFailure('');
        setNoDataMessage('');
        setParseErrorMessage('');

        const response = await AppInsightsClient.getLogQueryResults(appInsightsAppId, appInsightsToken, {
            query: getThreadTracesDataQuery(threadId),
        });

        if (response.isSuccessful) {
            const queryResultRows = response.content?.tables[0]?.rows ?? [];
            const data: ThreadEventLog[] = queryResultRows.map(row => {
                const timeStamp = new Date(row[0] as string);
                const eventObj = JSON.parse(row[1] as string) as Omit<
                    ThreadEventLog,
                    'timeStamp' | 'modelInput' | 'modelOutput' | 'message'
                > & { modelInput?: string; modelOutput?: string; message?: string };
                const { modelInput, modelOutput, message, ...rest } = eventObj;
                let modelInputObj = undefined;
                let modelOutputObj = undefined;
                try {
                    modelInputObj = modelInput ? JSON.parse(modelInput) : undefined;
                } catch (e) {
                    modelInputObj = { value: modelInput };
                }
                try {
                    modelOutputObj = modelOutput ? JSON.parse(modelOutput) : undefined;
                } catch (e) {
                    modelOutputObj = { value: modelOutput };
                }
                return {
                    ...rest,
                    modelInput: modelInputObj,
                    modelOutput: modelOutputObj,
                    message,
                    timeStamp,
                };
            });
            const { spans, parseWarnings, parseErrors } = generateSpans(data);
            if (isIncidentThread && spans.length && spans[0].kind !== 'Incident') {
                spans[0].kind = 'Incident';
            }
            setLoading(false);
            setWarnings(parseWarnings);
            setErrors(parseErrors);
            setSpans(spans);
            setNoDataMessage(
                data.length === 0
                    ? intl.formatMessage(ThreadTraceResources.noTraceDataFound)
                    : spans.length === 0
                      ? intl.formatMessage(ThreadTraceResources.couldNotParseTrace)
                      : ''
            );
            setParseErrorMessage(
                parseErrors.length
                    ? intl.formatMessage(ThreadTraceResources.traceParsedWithErrors)
                    : parseWarnings.length
                      ? intl.formatMessage(ThreadTraceResources.traceParsedWithWarnings)
                      : ''
            );

            if (parseErrors.length) {
                log({
                    action: 'parseThreadTracesData',
                    actionModifier: 'failed',
                    resourceId: resourceId,
                    logLevel: 'error',
                    data: {
                        errors: parseErrors,
                    },
                });
            }

            if (parseWarnings.length) {
                log({
                    action: 'parseThreadTracesData',
                    actionModifier: 'failed',
                    resourceId: resourceId,
                    logLevel: 'error',
                    data: {
                        warnings: parseWarnings,
                    },
                });
            }
        } else {
            setLoading(false);
            setLoadingFailure(intl.formatMessage(ThreadTraceResources.failedToLoadTraceData, { message: getErrorMessage(response.error) }));
            log({
                action: 'fetchThreadTracesData',
                actionModifier: 'failed',
                resourceId: resourceId,
                logLevel: 'error',
                data: {
                    error: getErrorMessage(response.error),
                },
            });
        }
    }, [appInsightsAppId, appInsightsToken, intl, log, resourceId, threadId]);

    useEffect(() => {
        fetch();
    }, [fetch]);

    return {
        spans,
        loading,
        loadingFailure,
        parseErrorMessage,
        noDataMessage,
        warnings,
        errors,
    };
};

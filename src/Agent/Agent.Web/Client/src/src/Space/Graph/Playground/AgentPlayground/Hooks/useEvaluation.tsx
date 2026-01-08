import { useFormikContext } from 'formik';
import isEqual from 'lodash/isEqual';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { IntlShape, useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { PlaygroundResources } from '../../../../../Strings/SREAgentResources';
import { ChatTelemetrySnapshot } from '../../../../Contracts/Activities';
import { ExtendedAgent, ExtendedTool, SystemTool } from '../../../../Contracts/ExtendedAgentGraph';
import { PlaygroundInsightSeverity, PlaygroundInsightsResponse, fetchPlaygroundInsights } from '../../services/playgroundInsightsService';
import {
    AgentPlaygroundFormValues,
    AgentPlaygroundMode,
    PREVIEW_UPDATE_BADGE_TIMEOUT,
    QualityFinding,
    QualityFindingPayload,
    QualityResult,
    QualityStatus,
    QualitySubscore,
} from '../Contracts';

type UseEvaluationProps = {
    mode: AgentPlaygroundMode;
    tools: ExtendedTool[];
    systemTools: SystemTool[];
};

const useEvaluation = ({ mode, tools, systemTools }: UseEvaluationProps) => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const intl = useIntl();
    const { values, setFieldValue, submitForm } = useFormikContext<AgentPlaygroundFormValues>();

    const [previewRecentlyUpdated, setPreviewRecentlyUpdated] = useState(false);
    const [insights, setInsights] = useState<PlaygroundInsightsResponse | null>(null);
    const [insightsLoading, setInsightsLoading] = useState(false);
    const [insightsError, setInsightsError] = useState<string | undefined>(undefined);
    const [chatTelemetry, setChatTelemetry] = useState<ChatTelemetrySnapshot | null>(null);
    const [qualityStatus, setQualityStatus] = useState<QualityStatus>('notAnalyzed');
    const [qualityLastAnalyzed, setQualityLastAnalyzed] = useState<number | null>(null);
    const [qualityResult, setQualityResult] = useState<QualityResult | null>(null);

    useEffect(() => {
        setQualityStatus('notAnalyzed');
        setQualityResult(null);
        setQualityLastAnalyzed(null);
        setInsights(null);
        setInsightsError(undefined);
        setInsightsLoading(false);
    }, []);

    useEffect(() => {
        if (!previewRecentlyUpdated) {
            return;
        }

        const timeout = window.setTimeout(() => setPreviewRecentlyUpdated(false), PREVIEW_UPDATE_BADGE_TIMEOUT);
        return () => window.clearTimeout(timeout);
    }, [previewRecentlyUpdated]);

    const insightSections: Array<{ key: string; title: string; items: string[] }> = useMemo(() => {
        if (!insights) {
            return [] as Array<{ key: string; title: string; items: string[] }>;
        }

        return [
            {
                key: 'prompt',
                title: intl.formatMessage(PlaygroundResources.insightsPromptHighlightsHeader),
                items: insights.promptInsights ?? [],
            },
            {
                key: 'tools',
                title: intl.formatMessage(PlaygroundResources.insightsToolSuggestionsHeader),
                items: insights.toolSuggestions ?? [],
            },
            {
                key: 'chat',
                title: intl.formatMessage(PlaygroundResources.insightsChatDiagnosticsHeader),
                items: insights.chatDiagnostics ?? [],
            },
            {
                key: 'notes',
                title: intl.formatMessage(PlaygroundResources.insightsNotesHeader),
                items: insights.notes ?? [],
            },
        ].filter(section => section.items.length > 0);
    }, [insights, intl]);

    const configHash = useMemo(() => {
        const agentConfigKey = JSON.stringify(values);
        // Generate a hash of the config for a shorter key
        const configHashRaw = encodeBase64(agentConfigKey);
        return configHashRaw || '';
    }, [values]);

    const [lastEvaluationConfigHash, setLastEvaluationConfigHash] = useState<string | undefined>(undefined);
    const [lastEvaluationChatTelemetry, setLastEvaluationChatTelemetry] = useState<ChatTelemetrySnapshot | null>(null);

    const areSuggestionsStale = useMemo(() => {
        if (!lastEvaluationChatTelemetry || !chatTelemetry) {
            return false;
        }
        return !isEqual(lastEvaluationChatTelemetry, chatTelemetry);
    }, [lastEvaluationChatTelemetry, chatTelemetry]);

    const areSuggestionsOutdated = useMemo(() => {
        if (!lastEvaluationConfigHash) {
            return false;
        }
        return !isEqual(lastEvaluationConfigHash, configHash);
    }, [lastEvaluationConfigHash, configHash]);

    const handleInsightsRefresh = useCallback(async () => {
        if (!sreAgentEndpoint) {
            setInsightsError(intl.formatMessage(PlaygroundResources.insightsError));
            return;
        }

        const prompt = values.instructions;
        if (!prompt) {
            setInsights(null);
            setQualityResult(null);
            setInsightsError(intl.formatMessage(PlaygroundResources.insightsNoData));
            setQualityStatus('notAnalyzed');
            return;
        }

        const toolsForRequest = values.tools.filter(name => name && tools.some(t => t.name === name));
        const systemToolsForRequest = values.tools.filter(name => name && systemTools.some(t => t.name === name));

        setQualityStatus('running');
        setInsightsLoading(true);
        setInsightsError(undefined);

        try {
            const { chatFindings, recentMessages, transcriptSummary } =
                chatTelemetry && mode === 'test'
                    ? processChatTelemetryForEvaluation(chatTelemetry, intl)
                    : { chatFindings: [], recentMessages: [], transcriptSummary: undefined };

            const response = await fetchPlaygroundInsights(sreAgentEndpoint, {
                prompt,
                agentName: values.agentName,
                // agentGoal: '',
                tools: toolsForRequest,
                systemTools: systemToolsForRequest,
                availableTools: tools.map(t => t.name).filter((name): name is string => !!name),
                availableSystemTools: systemTools.map(t => t.name).filter((name): name is string => !!name),
                chatFindings,
                toolFindings: [],
                transcriptSummary,
                recentMessages,
            });

            setInsights(response);
            const quality = buildQualityResult(prompt, toolsForRequest, systemToolsForRequest, response, intl);
            setQualityResult(quality);
            setQualityStatus('analyzed');
            setQualityLastAnalyzed(Date.now());

            setLastEvaluationConfigHash(configHash);
            setLastEvaluationChatTelemetry(chatTelemetry);
        } catch (error) {
            const message = error instanceof Error ? error.message : String(error);
            setInsightsError(`${intl.formatMessage(PlaygroundResources.insightsError)} ${message}`.trim());
            setQualityStatus(prev => (prev === 'running' && qualityResult ? 'analyzed' : prev === 'running' ? 'notAnalyzed' : prev));
        } finally {
            setInsightsLoading(false);
        }
    }, [mode, configHash, chatTelemetry, intl, qualityResult, sreAgentEndpoint, values]);

    const [shouldSave, setShouldSave] = useState(false);

    const onApply = useCallback(
        (agent: ExtendedAgent, save: boolean) => {
            setFieldValue('agentConfig', agent.name);
            setFieldValue('instructions', agent.instructions);
            setFieldValue('handoffInstructions', agent.handoffDescription);
            setFieldValue('handoffSubagents', agent.handoffs);
            setFieldValue('tools', agent.tools);
            setFieldValue('mcpTools', agent.mcpTools);
            setFieldValue('enableMemory', agent.enableMemory);
            setFieldValue('enableVanillaMode', agent.enableVanillaMode);
            if (save) {
                setShouldSave(true);
            }
        },
        [setFieldValue]
    );

    useEffect(() => {
        if (shouldSave) {
            submitForm();
            setShouldSave(false);
        }
    }, [shouldSave, submitForm]);

    return {
        qualityResult,
        qualityStatus,
        qualityLastAnalyzed,
        insightSections,
        insightsError,
        insightsLoading,
        handleInsightsRefresh,
        setChatTelemetry,
        areSuggestionsStale,
        areSuggestionsOutdated,
        onApply,
    };
};

export default useEvaluation;

const processChatTelemetryForEvaluation = (chatTelemetry: ChatTelemetrySnapshot, intl: IntlShape) => {
    const chatFindings = (chatTelemetry?.messages ?? []).map(message => {
        const chatSeverity: PlaygroundInsightSeverity = message.hasError ? 'error' : 'info';
        return {
            title:
                message.authorRole === 'SREAgent'
                    ? intl.formatMessage(PlaygroundResources.chatFindingAgentTitle, {
                          time: message.timeStamp ?? intl.formatMessage(PlaygroundResources.chatFindingTimeFallback),
                      })
                    : intl.formatMessage(PlaygroundResources.chatFindingUserTitle, {
                          time: message.timeStamp ?? intl.formatMessage(PlaygroundResources.chatFindingTimeFallback),
                      }),
            detail: message.text || '',
            severity: chatSeverity,
        };
    });

    // Extract recent message content for context
    const recentMessages = (chatTelemetry?.messages ?? [])
        .slice(-10) // Last 10 messages
        .map(msg => {
            const role = msg.authorRole === 'SREAgent' ? 'Agent' : 'User';
            const content = msg.text?.trim() || '';
            return content ? `${role}: ${content}` : '';
        })
        .filter(text => text.length > 0);

    // Create a transcript summary
    const transcriptSummary =
        recentMessages.length > 0 ? `Conversation with ${recentMessages.length} exchanges:\n${recentMessages.join('\n')}` : undefined;

    return { chatFindings, recentMessages, transcriptSummary };
};

const clampScore = (value: number): number => Math.max(0, Math.min(100, Math.round(value)));

const extractToolNameFromSuggestion = (suggestion: string): string | null => {
    if (!suggestion) {
        return null;
    }

    // Try to find text in backticks (most reliable)
    const codeMatch = suggestion.match(/`([^`]+)`/);
    if (codeMatch) {
        return codeMatch[1];
    }

    return null;
};

const buildQualityResult = (
    prompt: string,
    tools: string[],
    systemTools: string[],
    insights: PlaygroundInsightsResponse,
    intl: IntlShape
): QualityResult => {
    const baseScore = clampScore(insights?.confidenceScore ?? 55);
    const availableToolCount = tools.length + systemTools.length;

    const toolPenalty = clampScore(insights.toolSuggestions.length * 10 + (availableToolCount === 0 ? 20 : 0));
    const promptPenalty = clampScore(insights.promptInsights.length * 8 + (prompt.length < 240 ? 15 : 0));
    const chatPenalty = clampScore(insights.chatDiagnostics.length * 6);
    const safetyPenalty = clampScore(insights.actionItems.filter(item => item.severity === 'error').length * 12);
    const actionPenalty = clampScore(insights.actionItems.length * 5);
    const notePenalty = clampScore(insights.notes.length * 4);

    const completeness = clampScore(baseScore - notePenalty);
    const intentMatch = clampScore(baseScore - chatPenalty);
    const toolFit = availableToolCount === 0 ? 0 : clampScore(100 - toolPenalty);
    const promptClarity = clampScore(100 - promptPenalty);
    const safety = clampScore(baseScore - safetyPenalty);
    const actionability = clampScore(baseScore - actionPenalty);

    const overallScore = clampScore((completeness + intentMatch + toolFit + promptClarity + safety + actionability) / 6);

    const evidenceParts: string[] = [];
    if (insights.promptInsights.length) {
        evidenceParts.push(intl.formatMessage(PlaygroundResources.promptOpportunities, { count: insights.promptInsights.length }));
    }
    if (insights.toolSuggestions.length) {
        evidenceParts.push(intl.formatMessage(PlaygroundResources.toolGaps, { count: insights.toolSuggestions.length }));
    }
    if (insights.actionItems.length) {
        evidenceParts.push(intl.formatMessage(PlaygroundResources.followUps, { count: insights.actionItems.length }));
    }
    if (!evidenceParts.length) {
        evidenceParts.push(intl.formatMessage(PlaygroundResources.noMajorGaps));
    }

    const findings: QualityFinding[] = [];

    // Convert actionItems from API to findings
    insights.actionItems.forEach((actionItem, index) => {
        const isRewrite = actionItem.type === 'promptRewrite';
        const isToolAdd = actionItem.type === 'toolAdd';
        const isPromptPatch = actionItem.type === 'promptPatch';
        const expectedLift = actionItem.impact?.scoreIncrease ?? 10;
        const impactDimension = actionItem.impact?.dimension ?? 'completeness';

        let payload: QualityFindingPayload | undefined;
        if (isRewrite) {
            payload = { type: 'prompt-rewrite', fullPromptRewrite: actionItem.patch };
        } else if (isToolAdd) {
            const toolName = extractToolNameFromSuggestion(actionItem.patch ?? '') ?? 'UnknownTool';
            payload = { type: 'tool', toolName, action: 'add', description: actionItem.title };
        } else if (isPromptPatch) {
            payload = { type: 'promptPatch', patch: actionItem.patch ?? '' };
        } else {
            payload = { type: 'instructions', addition: actionItem.patch ?? '' };
        }

        findings.push({
            id: actionItem.id ?? `action-${index}`,
            title: actionItem.title,
            rationale: actionItem.detail ?? actionItem.title,
            expectedLift: expectedLift,
            impactLabel: `+${expectedLift} ${impactDimension}`,
            autoApply: actionItem.autoApplicable ?? false,
            patch: actionItem.patch ?? '',
            shortDiff: actionItem.patch ?? '', // Show entire diff, not truncated
            payload: payload,
            toolHint: actionItem.impact?.description ?? '',
            safetyNote:
                actionItem.severity === 'error'
                    ? intl.formatMessage(PlaygroundResources.criticalFixRequired)
                    : intl.formatMessage(PlaygroundResources.reviewBeforeApplying),
        });
    });

    const hint = findings.length
        ? intl.formatMessage(PlaygroundResources.nextBestStep, {
              title: findings[0].title,
              impactLabel: findings[0].impactLabel,
          })
        : intl.formatMessage(PlaygroundResources.nextBestStepCaptureTranscript);

    const subScores: QualitySubscore[] = [
        {
            id: 'completeness',
            label: intl.formatMessage(PlaygroundResources.completeness),
            score: completeness,
            evidence: intl.formatMessage(PlaygroundResources.completenessEvidence, { count: insights.notes.length || 0 }),
        },
        {
            id: 'intentMatch',
            label: intl.formatMessage(PlaygroundResources.intentMatch),
            score: intentMatch,
            evidence: intl.formatMessage(PlaygroundResources.intentMatchEvidence, { count: insights.chatDiagnostics.length || 0 }),
        },
        {
            id: 'toolFit',
            label: intl.formatMessage(PlaygroundResources.toolFit),
            score: toolFit,
            evidence: intl.formatMessage(PlaygroundResources.toolFitEvidence, { count: insights.toolSuggestions.length || 0 }),
        },
        {
            id: 'promptClarity',
            label: intl.formatMessage(PlaygroundResources.promptClarity),
            score: promptClarity,
            evidence: intl.formatMessage(PlaygroundResources.promptClarityEvidence, { count: insights.promptInsights.length || 0 }),
        },
        {
            id: 'safety',
            label: intl.formatMessage(PlaygroundResources.safety),
            score: safety,
            evidence: intl.formatMessage(PlaygroundResources.safetyEvidence, {
                count: insights.actionItems.filter(item => item.severity === 'error').length || 0,
            }),
        },
        {
            id: 'actionability',
            label: intl.formatMessage(PlaygroundResources.actionability),
            score: actionability,
            evidence: intl.formatMessage(PlaygroundResources.actionabilityEvidence, { count: insights.actionItems.length || 0 }),
        },
    ];

    return {
        overallScore,
        evidence: intl.formatMessage(PlaygroundResources.evidenceList, { evidence: evidenceParts.join(', ') }),
        hint,
        subScores,
        findings,
    };
};

const encodeBase64 = (value: string): string => {
    const encodeBinary = (input: string): string => {
        if (typeof window !== 'undefined' && typeof window.btoa === 'function') {
            return window.btoa(input);
        }
        if (typeof btoa === 'function') {
            return btoa(input);
        }
        throw new Error('No base64 encoder available');
    };

    try {
        return encodeBinary(value);
    } catch {
        try {
            const encoder = new TextEncoder();
            const bytes = encoder.encode(value);
            let binary = '';
            bytes.forEach(byte => {
                binary += String.fromCharCode(byte);
            });
            return encodeBinary(binary);
        } catch {
            return '';
        }
    }
};

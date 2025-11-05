import {
    Badge,
    Body1,
    Button,
    Caption1Strong,
    Checkbox,
    Dialog,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    Dropdown,
    Field,
    Menu,
    MenuDivider,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    Option,
    OptionGroup,
    ProgressBar,
    Spinner,
    Switch,
    Tab,
    TabList,
    TabValue,
    Toast,
    ToastBody,
    ToastTitle,
    Toaster,
    Tooltip,
    makeStyles,
    mergeClasses,
    shorthands,
    tokens,
    useId,
    useToastController,
} from '@fluentui/react-components';
import {
    BeakerFilled,
    ChartMultipleFilled,
    CheckmarkCircle20Regular,
    ChevronDown20Regular,
    Dismiss16Regular,
    Info20Regular,
    MoreHorizontal20Regular,
    PanelLeft20Regular,
    PanelLeftExpand20Regular,
    PanelRightContract20Regular,
    PersonFilled,
    SparkleFilled,
    Square20Regular,
    WrenchFilled,
} from '@fluentui/react-icons';
import { MessageBar, MessageBarBody } from '@fluentui/react-message-bar';
import { useTheme } from '@fluentui/react/lib/Theme';
import MonacoEditor from '@monaco-editor/react';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { ActivitiesResources, PlaygroundResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ChatBox } from '../Activities/ChatBox';
import { ChatTelemetrySnapshot } from '../Contracts/Activities';
import { ExtendedAgent, ExtendedConnector, ExtendedTool, SystemTool } from '../Contracts/ExtendedAgentGraph';
import { AgentDetailsStep } from '../Graph/ExtendedAgentCreationDialog/components/AgentDetailsStep';
import { KustoQueryTesterPanel } from '../Graph/ExtendedAgentCreationDialog/components/KustoQueryTesterPanel';
import { ToolDetailsStep } from '../Graph/ExtendedAgentCreationDialog/components/ToolDetailsStep';
import { ToolTestState, ToolTestStatus } from '../Graph/ExtendedAgentCreationDialog/types';
import { getKustoTestFingerprint } from '../Graph/ExtendedAgentCreationDialog/utils/toolUtils';
import { ChatBoxStyleProps } from '../Styles/Activities.styles';
import { buildAgentYaml, buildToolYaml, tryParseAgentYaml, tryParseToolYaml } from './PlaygroundYamlUtils';
import {
    PlaygroundInsightEvidence,
    PlaygroundInsightSeverity,
    PlaygroundInsightsResponse,
    fetchPlaygroundInsights,
} from './services/playgroundInsightsService';
import { SystemToolConfigurationPanel, SystemToolTesterPanel } from './SystemToolTesterPanel';

export type PlaygroundTarget =
    | { type: 'agent'; agent: ExtendedAgent }
    | { type: 'tool'; tool: ExtendedTool; agent?: ExtendedAgent }
    | { type: 'systemTool'; tool: SystemTool; agent?: ExtendedAgent };

type QualityStatus = 'notAnalyzed' | 'running' | 'fresh' | 'stale';

type QualitySubscore = {
    id: string;
    label: string;
    score: number;
    evidence: string;
};

type QualityFindingPayload =
    | { type: 'instructions'; addition: string }
    | { type: 'tool'; toolName: string; action: 'add' | 'update'; description?: string }
    | { type: 'newTool'; toolName: string; description?: string }
    | { type: 'prompt-rewrite'; fullPromptRewrite?: string }
    | { type: 'promptPatch'; patch: string };

type QualityFinding = {
    id: string;
    title: string;
    rationale: string;
    expectedLift: number;
    impactLabel: string;
    autoApply: boolean;
    patch?: string;
    shortDiff?: string;
    payload?: QualityFindingPayload;
    toolHint?: string;
    safetyNote?: string;
};

type QualityResult = {
    overallScore: number;
    evidence: string;
    hint: string;
    subScores: QualitySubscore[];
    findings: QualityFinding[];
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

const formatRelativeTime = (timestamp: number): string => {
    const deltaSeconds = Math.max(0, Math.floor((Date.now() - timestamp) / 1000));
    if (deltaSeconds < 45) {
        return 'moments ago';
    }
    if (deltaSeconds < 90) {
        return '1m ago';
    }
    if (deltaSeconds < 3600) {
        const minutes = Math.floor(deltaSeconds / 60);
        return `${minutes}m ago`;
    }
    if (deltaSeconds < 5400) {
        return '1h ago';
    }
    if (deltaSeconds < 86400) {
        const hours = Math.floor(deltaSeconds / 3600);
        return `${hours}h ago`;
    }
    const days = Math.floor(deltaSeconds / 86400);
    return `${days}d ago`;
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

const buildQualityResult = (
    prompt: string,
    tools: string[],
    systemTools: string[],
    insights: PlaygroundInsightsResponse
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
        evidenceParts.push(`${insights.promptInsights.length} prompt opportunities`);
    }
    if (insights.toolSuggestions.length) {
        evidenceParts.push(`${insights.toolSuggestions.length} tool gaps`);
    }
    if (insights.actionItems.length) {
        evidenceParts.push(`${insights.actionItems.length} follow-ups`);
    }
    if (!evidenceParts.length) {
        evidenceParts.push('No major gaps detected');
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
            safetyNote: actionItem.severity === 'error' ? 'Critical fix required' : 'Review before applying',
        });
    });

    const hint = findings.length
        ? `Next best step: ${findings[0].title} (${findings[0].impactLabel}).`
        : 'Next best step: Capture a transcript to analyze intent match.';

    const subScores: QualitySubscore[] = [
        { id: 'completeness', label: 'Completeness', score: completeness, evidence: `${insights.notes.length || 0} open notes.` },
        { id: 'intentMatch', label: 'Intent match', score: intentMatch, evidence: `${insights.chatDiagnostics.length || 0} chat flags.` },
        { id: 'toolFit', label: 'Tool fit', score: toolFit, evidence: `${insights.toolSuggestions.length || 0} tool gaps.` },
        {
            id: 'promptClarity',
            label: 'Prompt clarity',
            score: promptClarity,
            evidence: `${insights.promptInsights.length || 0} prompt notes.`,
        },
        {
            id: 'safety',
            label: 'Safety',
            score: safety,
            evidence: `${insights.actionItems.filter(item => item.severity === 'error').length} blocking issues.`,
        },
        { id: 'actionability', label: 'Actionability', score: actionability, evidence: `${insights.actionItems.length} actions queued.` },
    ];

    return {
        overallScore,
        evidence: `Evidence: ${evidenceParts.join(', ')}.`,
        hint,
        subScores,
        findings,
    };
};

type ConfigTabValue = 'form' | 'yaml';
type ViewMode = 'tester' | 'author-test' | 'author-test-evaluate';

type PlaygroundModalProps = {
    open: boolean;
    target?: PlaygroundTarget;
    agents: ExtendedAgent[];
    tools: ExtendedTool[];
    connectors: ExtendedConnector[];
    systemTools: SystemTool[];
    onDismiss: () => void;
};

const PREVIEW_UPDATE_BADGE_TIMEOUT = 6000;
const NEW_KUSTO_TOOL_OPTION = '__new_kusto_tool__';

const useStyles = makeStyles({
    surface: {
        width: 'calc(98vw - 48px)',
        maxWidth: 'calc(98vw - 48px)',
        height: 'calc(98vh - 48px)',
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalS),
        display: 'flex',
        flexDirection: 'column',
    },
    body: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        gap: tokens.spacingVerticalXXS,
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
        paddingTop: tokens.spacingVerticalXS,
        paddingBottom: tokens.spacingVerticalXS,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        minHeight: '48px',
    },
    headerCopy: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalL,
        flex: 1,
    },
    headerBottomRow: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
        flexWrap: 'wrap',
    },
    headerActions: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    qualityBadgeCompact: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        fontSize: tokens.fontSizeBase200,
    },
    headerChips: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        flexWrap: 'wrap',
    },
    headerTitle: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
    },
    headerSubtitle: {
        color: tokens.colorNeutralForeground3,
    },
    headerSummary: {
        color: tokens.colorNeutralForeground2,
    },
    iconCircle: {
        width: '36px',
        height: '36px',
        borderRadius: '50%',
        backgroundColor: tokens.colorBrandBackground2,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        color: tokens.colorBrandForeground2,
        fontSize: '18px',
        flexShrink: 0,
    },
    layout: {
        flex: 1,
        display: 'flex',
        gap: tokens.spacingHorizontalL,
        overflow: 'hidden',
        minHeight: 0,
        alignItems: 'stretch',
    },
    leftColumn: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
        minWidth: '280px',
        overflow: 'auto',
        maxHeight: '100%',
        position: 'relative',
    },
    rightColumn: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
        minWidth: '280px',
        minHeight: 0,
        maxHeight: '100%',
        overflow: 'auto',
        transition: 'all 0.3s ease',
    },
    rightColumnCollapsed: {
        minWidth: '40px',
        maxWidth: '40px',
    },
    collapseButton: {
        position: 'absolute',
        top: '50%',
        left: '50%',
        transform: 'translate(-50%, -50%)',
        zIndex: 1000,
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        borderRadius: '50%',
        width: '24px',
        height: '24px',
        minWidth: '24px',
        boxShadow: tokens.shadow4,
        '&:hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    tabPanel: {
        flex: 1,
        minHeight: 0,
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding(tokens.spacingVerticalM, tokens.spacingHorizontalM),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        overflow: 'auto',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    formSection: {
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusSmall,
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalM),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    sectionHeader: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
        marginBottom: tokens.spacingVerticalXS,
    },
    microCopy: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        lineHeight: tokens.lineHeightBase200,
        maxWidth: '72ch',
    },
    setupCard: {
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.padding(tokens.spacingVerticalM, tokens.spacingHorizontalM),
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    setupActions: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        alignItems: 'center',
    },
    yamlContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        height: '100%',
    },
    yamlEditor: {
        flex: 1,
        minHeight: '280px',
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'hidden',
    },
    placeholder: {
        flex: 1,
        minHeight: 0,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        textAlign: 'center',
        gap: tokens.spacingVerticalS,
    },
    toolTesterContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        height: '100%',
    },
    dropdownRow: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    editorSwitcher: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    inlineList: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
    },
    toolFormContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    infoMessage: {
        color: tokens.colorNeutralForeground3,
    },
    previewHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: tokens.spacingHorizontalS,
    },
    previewBadge: {
        whiteSpace: 'nowrap',
    },
    insightsCard: {
        borderRadius: tokens.borderRadiusXLarge,
        backgroundColor: '#fefce8',
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalM),
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
        ...shorthands.border('1px', 'solid', '#fde68a'),
    },
    insightsHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
    insightsHeaderActions: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    insightsScoreRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    insightsList: {
        margin: 0,
        paddingLeft: tokens.spacingHorizontalL,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    insightsListItem: {
        padding: 0,
    },
    insightsHighlightList: {
        margin: 0,
        padding: 0,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    insightsHighlightItem: {
        listStyle: 'none',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        ...shorthands.padding(tokens.spacingVerticalXS, tokens.spacingHorizontalS),
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    insightsHighlightHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: tokens.spacingHorizontalS,
    },
    insightsSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    insightsEmpty: {
        color: tokens.colorNeutralForeground3,
    },
    insightsError: {
        color: tokens.colorStatusDangerForeground1,
    },
    insightsBadge: {
        whiteSpace: 'nowrap',
    },
    insightsLevelRow: {
        display: 'flex',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalXS,
        marginTop: tokens.spacingVerticalXXS,
    },
    insightsLevelBadge: {
        letterSpacing: '0.08em',
        textTransform: 'uppercase',
    },
    insightsScoreRight: {
        textAlign: 'right',
    },
    confidenceAchievement: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        color: tokens.colorStatusSuccessForeground1,
        fontWeight: tokens.fontWeightSemibold,
    },
    confidenceGoal: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
    },
    insightsTierSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalM),
    },
    insightsTierMessage: {
        color: tokens.colorNeutralForeground2,
    },
    insightsTierProgressHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: tokens.spacingHorizontalS,
        color: tokens.colorNeutralForeground3,
    },
    insightsConfidenceDeltaPositive: {
        color: tokens.colorStatusSuccessForeground1,
    },
    insightsConfidenceDeltaNegative: {
        color: tokens.colorStatusDangerForeground1,
    },
    initialConfidenceBar: {
        backgroundColor: tokens.colorNeutralBackground4,
        height: '8px',
        borderRadius: tokens.borderRadiusSmall,
        position: 'relative',
        overflow: 'hidden',
    },
    intentMetRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        ...shorthands.padding(tokens.spacingVerticalXS, 0),
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    scoreDisplay: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
    },
    confidenceImprovement: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        color: tokens.colorStatusSuccessForeground1,
    },
    thickConfidenceLine: {
        height: '6px',
        borderRadius: '3px',
        overflow: 'hidden',
        backgroundColor: tokens.colorNeutralBackground4,
        position: 'relative',
    },
    thickConfidenceProgress: {
        height: '100%',
        borderRadius: '3px',
        transition: 'width 0.3s ease-in-out',
    },
    skeletonLine: {
        height: '12px',
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusSmall,
        animation: 'pulse 1.5s ease-in-out infinite',
    },
    skeletonContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        ...shorthands.padding(tokens.spacingVerticalS),
    },
    optimisticUpdate: {
        opacity: 0.7,
        transition: 'opacity 0.2s ease',
    },
    scrollSection: {
        flex: 1,
        overflow: 'auto',
        paddingRight: tokens.spacingHorizontalS,
    },
    playgroundChatWrapper: {
        height: '100%',
        overflow: 'hidden',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
    },
    chatEmptyState: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        alignItems: 'center',
        justifyContent: 'center',
        textAlign: 'center',
        ...shorthands.padding(tokens.spacingVerticalXL, tokens.spacingHorizontalL),
        maxWidth: '600px',
        margin: '0 auto',
        height: '100%',
    },
    chatEmptyTitle: {
        fontSize: tokens.fontSizeBase500,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        marginBottom: tokens.spacingVerticalXS,
    },
    chatEmptySubtitle: {
        color: tokens.colorNeutralForeground2,
        maxWidth: '520px',
    },
    chatEmptyDescription: {
        color: tokens.colorNeutralForeground3,
        maxWidth: '560px',
    },
    chatEmptyMeta: {
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        color: tokens.colorNeutralForeground3,
    },
    chatEmptySyncNotice: {
        color: tokens.colorNeutralForeground3,
    },
    chatEmptyPromptList: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: tokens.spacingHorizontalM,
        width: '100%',
    },
    chatEmptyPromptCard: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        boxShadow: tokens.shadow4,
        ...shorthands.padding(tokens.spacingVerticalM, tokens.spacingHorizontalM),
    },
    chatEmptyPromptDescription: {
        color: tokens.colorNeutralForeground3,
    },
    chatEmptyIcon: {
        width: '64px',
        height: '64px',
        borderRadius: '50%',
        backgroundColor: tokens.colorBrandBackground2,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        color: tokens.colorBrandForeground2,
        fontSize: '32px',
        marginBottom: tokens.spacingVerticalS,
    },
    chatEmptyBenefitsTitle: {
        color: tokens.colorBrandForeground1,
        fontWeight: tokens.fontWeightSemibold,
        marginTop: tokens.spacingVerticalS,
    },
    chatEmptyBenefitList: {
        margin: 0,
        paddingLeft: tokens.spacingHorizontalXL,
        color: tokens.colorNeutralForeground2,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    chatApplyingState: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: tokens.spacingVerticalM,
        textAlign: 'center',
        ...shorthands.padding(tokens.spacingVerticalXXL),
        height: '100%',
    },
    applyingSpinner: {
        width: '48px',
        height: '48px',
    },
    applyingTitle: {
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorBrandForeground1,
    },
    applyingSubtitle: {
        color: tokens.colorNeutralForeground2,
        maxWidth: '300px',
    },
    dividerHandle: {
        position: 'relative',
        cursor: 'col-resize',
        flex: '0 0 4px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        margin: 0,
        backgroundColor: tokens.colorNeutralStroke2,
        '&:hover': {
            backgroundColor: tokens.colorBrandStroke1,
        },
    },
    tabsWithShortcut: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
    },
    shortcutHint: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        fontFamily: tokens.fontFamilyMonospace,
    },
    applyingOverlay: {
        position: 'fixed',
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.5)',
        zIndex: 1000000,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
    },
    applyingCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusXLarge,
        ...shorthands.padding(tokens.spacingVerticalXXL, tokens.spacingHorizontalXXL),
        boxShadow: tokens.shadow64,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: tokens.spacingVerticalL,
        minWidth: '400px',
    },
    applyingCardTitle: {
        fontSize: tokens.fontSizeHero700,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorBrandForeground1,
        margin: 0,
        textAlign: 'center',
    },
    applyingCardMessage: {
        fontSize: tokens.fontSizeBase400,
        color: tokens.colorNeutralForeground2,
        textAlign: 'center',
        margin: 0,
        lineHeight: tokens.lineHeightBase400,
    },
    applyingProgress: {
        width: '100%',
    },
    floatingExpandButton: {
        position: 'absolute',
        top: '50%',
        transform: 'translateY(-50%)',
        zIndex: 10,
    },
    floatingQualityButton: {
        position: 'fixed',
        bottom: tokens.spacingVerticalXXL,
        right: tokens.spacingHorizontalXXL,
        zIndex: 100,
        boxShadow: tokens.shadow16,
    },
    watcherHeaderActions: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-end',
        gap: tokens.spacingVerticalXXS,
    },
    watcherStatusRow: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
        justifyContent: 'flex-end',
        maxWidth: '360px',
    },
    watcherSummaryText: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        textAlign: 'right',
    },
    watcherButtonsRow: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
        justifyContent: 'flex-end',
    },
    watcherSecondaryRow: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
        justifyContent: 'flex-end',
    },
    watcherStatusChip: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
    },
    watcherPanel: {
        width: '360px',
        maxWidth: '400px',
        minWidth: '340px',
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusMedium,
        boxShadow: tokens.shadow8,
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        flexShrink: 0,
        maxHeight: '100%',
        height: '100%',
        minHeight: 0,
    },
    watcherPanelHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: tokens.spacingHorizontalS,
        ...shorthands.padding(tokens.spacingVerticalL, tokens.spacingHorizontalL),
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground2,
    },
    watcherPanelBody: {
        flex: 1,
        overflowY: 'auto',
        ...shorthands.padding(tokens.spacingVerticalL, tokens.spacingHorizontalL),
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        minHeight: 0,
    },
    watcherScoresRow: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    watcherSubscoreList: {
        display: 'grid',
        gridTemplateColumns: 'repeat(2, 1fr)',
        gap: tokens.spacingHorizontalS,
        margin: 0,
        padding: 0,
        listStyle: 'none',
    },
    watcherSubscoreItem: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusSmall,
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalS),
    },
    watcherSubscoreLabel: {
        fontSize: tokens.fontSizeBase100,
        color: tokens.colorNeutralForeground3,
        textTransform: 'uppercase',
        letterSpacing: '0.04em',
    },
    watcherFindingsList: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        margin: 0,
        padding: 0,
        listStyle: 'none',
    },
    watcherFindingItem: {
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalS),
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        transition: 'all 0.2s ease',
    },
    watcherFindingItemSelected: {
        backgroundColor: tokens.colorBrandBackground2,
        ...shorthands.borderColor(tokens.colorBrandStroke1),
        boxShadow: `0 0 0 1px ${tokens.colorBrandStroke1}`,
    },
    watcherFindingHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        gap: tokens.spacingHorizontalS,
    },
    watcherFindingTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    watcherFindingRationale: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
    },
    watcherFindingActions: {
        display: 'flex',
        gap: tokens.spacingHorizontalXS,
        flexWrap: 'wrap',
    },
    watcherFindingPreview: {
        fontFamily: tokens.fontFamilyMonospace,
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusSmall,
        ...shorthands.padding(tokens.spacingVerticalXS, tokens.spacingHorizontalXS),
        fontSize: tokens.fontSizeBase200,
        whiteSpace: 'pre-wrap',
        overflow: 'auto',
    },
    diffLineAdded: {
        backgroundColor: 'rgba(46, 160, 67, 0.15)',
        color: tokens.colorPaletteGreenForeground1,
        display: 'block',
        width: '100%',
    },
    diffLineRemoved: {
        backgroundColor: 'rgba(229, 83, 75, 0.15)',
        color: tokens.colorPaletteRedForeground1,
        display: 'block',
        width: '100%',
    },
    diffLineContext: {
        display: 'block',
        width: '100%',
    },
    watcherPanelFooter: {
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalL),
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
    },
    watcherHint: {
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusSmall,
        ...shorthands.padding(tokens.spacingVerticalXS, tokens.spacingHorizontalS),
        color: tokens.colorNeutralForeground2,
    },
    watcherRibbon: {
        position: 'fixed',
        left: '50%',
        bottom: tokens.spacingVerticalXL,
        transform: 'translateX(-50%)',
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusXLarge,
        boxShadow: tokens.shadow28,
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalL),
        zIndex: 1004,
        opacity: 0,
        pointerEvents: 'none',
        transition: 'opacity 0.2s ease',
    },
    watcherRibbonVisible: {
        opacity: 1,
        pointerEvents: 'auto',
    },
    watcherRibbonSummary: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    dirtyBanner: {
        marginTop: tokens.spacingVerticalS,
    },
    viewSwitcher: {
        display: 'flex',
        alignItems: 'stretch',
        gap: '4px',
        height: '32px',
    },
    viewSwitcherButton: {
        minWidth: '40px',
        height: '32px',
        borderRadius: tokens.borderRadiusSmall,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: '2px',
        cursor: 'pointer',
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        backgroundColor: tokens.colorNeutralBackground1,
        color: tokens.colorNeutralForeground2,
        transition: 'all 0.2s ease',
        ...shorthands.padding('4px', '8px'),
        '&:hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    viewSwitcherButtonActive: {
        backgroundColor: tokens.colorBrandBackground,
        ...shorthands.border('2px', 'solid', tokens.colorBrandBackground),
        color: tokens.colorNeutralForegroundOnBrand,
        boxShadow: `0 0 0 1px ${tokens.colorBrandBackground}`,
        '&:hover': {
            backgroundColor: tokens.colorBrandBackgroundHover,
        },
    },
    viewSwitcherIcon: {
        fontSize: '14px',
    },
});

const playgroundChatStyles: ChatBoxStyleProps = {
    chatBoxAndAgentTask: {
        width: '100%',
        boxShadow: 'none !important',
        borderRadius: tokens.borderRadiusLarge,
        height: '100%',
        backgroundColor: `${tokens.colorNeutralBackground1} !important`,
        border: 'none !important',
    },
    chatBox: {
        height: '100%',
        backgroundColor: `${tokens.colorNeutralBackground1} !important`,
        borderRadius: tokens.borderRadiusLarge,
        ...shorthands.padding(0),
        // Optimized for playground - reduce wasted space
        '& > div': {
            ...shorthands.padding('0 !important'),
        },
        '& textarea': {
            minHeight: '28px !important',
            maxHeight: '56px !important',
            fontSize: `${tokens.fontSizeBase200} !important`,
            lineHeight: '1.2 !important',
        },
        '& .ms-TextField-fieldGroup': {
            minHeight: '28px !important',
            maxHeight: '56px !important',
            ...shorthands.margin('0 !important'),
        },
        '& .ms-TextField-field': {
            minHeight: '28px !important',
            fontSize: `${tokens.fontSizeBase200} !important`,
            lineHeight: '1.2 !important',
            ...shorthands.padding('4px', tokens.spacingHorizontalXS),
        },
        '& .ms-Button': {
            minHeight: '28px !important',
            height: '28px !important',
        },
        '& .ms-Stack': {
            minHeight: 'auto !important',
            gap: `${tokens.spacingVerticalXXS} !important`,
        },
        // Reduce padding around input area
        '& [class*="chatInput"]': {
            ...shorthands.padding(tokens.spacingVerticalXXS, '0'),
        },
    },
    chatBoxInner: {
        borderRadius: tokens.borderRadiusLarge,
        backgroundColor: `${tokens.colorNeutralBackground1} !important`,
        ...shorthands.padding(tokens.spacingVerticalXXS, tokens.spacingHorizontalXS),
        border: 'none !important',
        boxShadow: 'none !important',
    },
};

/**
 * Helper function to apply a unified diff patch to text
 */
const applyUnifiedDiff = (originalText: string, patch: string): string => {
    const lines = originalText.split('\n');
    const patchLines = patch.split('\n');

    let lineIndex = 0;
    let i = 0;

    while (i < patchLines.length) {
        const line = patchLines[i];

        // Parse hunk header: @@ -startLine,count +startLine,count @@
        const hunkMatch = line.match(/^@@\s+-(\d+)(?:,(\d+))?\s+\+(\d+)(?:,(\d+))?\s+@@/);
        if (hunkMatch) {
            const oldStart = parseInt(hunkMatch[1], 10) - 1; // Convert to 0-based
            lineIndex = oldStart;
            i++;
            continue;
        }

        // Context line (starts with space or no prefix)
        if (line.startsWith(' ') || (!line.startsWith('+') && !line.startsWith('-') && !line.startsWith('@'))) {
            lineIndex++;
            i++;
            continue;
        }

        // Deletion (starts with -)
        if (line.startsWith('-') && !line.startsWith('---')) {
            lines.splice(lineIndex, 1);
            i++;
            continue;
        }

        // Addition (starts with +)
        if (line.startsWith('+') && !line.startsWith('+++')) {
            lines.splice(lineIndex, 0, line.substring(1));
            lineIndex++;
            i++;
            continue;
        }

        i++;
    }

    return lines.join('\n');
};

/**
 * Helper function to render a diff with GitHub-like color coding
 */
const renderColoredDiff = (diff: string, styles: ReturnType<typeof useStyles>) => {
    const lines = diff.split('\n');
    return (
        <>
            {lines.map((line, index) => {
                let className = styles.diffLineContext;
                if (line.startsWith('+') && !line.startsWith('+++')) {
                    className = styles.diffLineAdded;
                } else if (line.startsWith('-') && !line.startsWith('---')) {
                    className = styles.diffLineRemoved;
                }
                return (
                    <span key={index} className={className}>
                        {line}
                        {index < lines.length - 1 && '\n'}
                    </span>
                );
            })}
        </>
    );
};

/**
 * PlaygroundModal - Streamlined agent testing and configuration interface
 *
 * Recent improvements:
 * 1. Larger dialog (98vw x 98vh) for maximum workspace
 * 2. Reduced padding/spacing throughout for more compact layout
 * 3. Simplified Agent Quality header (smaller icon, compact text)
 * 4. Removed "Chat Preview" label (obvious from context)
 * 5. localStorage preferences for panel ratio and config tab
 * 6. Keyboard shortcuts:
 *    - Ctrl+` : Toggle Form/YAML view
 *    - Ctrl+R : Refresh insights
 *    - Ctrl+F : Toggle Focus Mode (hides left panel entirely)
 * 7. Focus Mode - hide left panel for distraction-free chat testing
 * 8. Panel expansion - right panel collapse makes left panel full-width
 * 9. Floating expand button when right panel is collapsed
 */
export const PlaygroundModal = ({ open, target, agents, tools, connectors, systemTools, onDismiss }: PlaygroundModalProps) => {
    const styles = useStyles();
    const intl = useIntl();
    const theme = useTheme();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    // Load preferences from localStorage
    const loadPreference = <T,>(key: string, defaultValue: T): T => {
        try {
            const stored = localStorage.getItem(`playground_${key}`);
            return stored ? JSON.parse(stored) : defaultValue;
        } catch {
            return defaultValue;
        }
    };

    const savePreference = <T,>(key: string, value: T) => {
        try {
            localStorage.setItem(`playground_${key}`, JSON.stringify(value));
        } catch {
            // Ignore localStorage errors
        }
    };

    const [acknowledgedMode, setAcknowledgedMode] = useState(true);
    const [viewMode, setViewMode] = useState<ViewMode>('author-test'); // Always start with middle button (author-test)
    const [configTab, setConfigTab] = useState<ConfigTabValue>(loadPreference('configTab', 'form'));
    const [yamlContent, setYamlContent] = useState('');
    const [yamlError, setYamlError] = useState<string | undefined>(undefined);
    const [draftAgent, setDraftAgent] = useState<Partial<ExtendedAgent> | undefined>(undefined);
    const [draftTool, setDraftTool] = useState<Partial<ExtendedTool> | undefined>(undefined);
    const [draftSystemTool, setDraftSystemTool] = useState<SystemTool | undefined>(undefined);
    const [selectedToolName, setSelectedToolName] = useState<string | undefined>(undefined);
    const [toolTestStates, setToolTestStates] = useState<Record<string, ToolTestState>>({});
    const [configEntity, setConfigEntity] = useState<'agent' | 'tool'>('agent');
    const [previewRecentlyUpdated, setPreviewRecentlyUpdated] = useState(false);
    const [insights, setInsights] = useState<PlaygroundInsightsResponse | null>(null);
    const [insightsLoading, setInsightsLoading] = useState(false);
    const [insightsError, setInsightsError] = useState<string | undefined>(undefined);
    const [insightsStale, setInsightsStale] = useState(false);
    const [panelRatio, setPanelRatio] = useState(loadPreference('panelRatio', 0.62));
    const [rightPanelCollapsed, setRightPanelCollapsed] = useState(false);
    const [leftPanelCollapsed, setLeftPanelCollapsed] = useState(false);
    const [chatKey, setChatKey] = useState(0); // Key to force chat remount on restart
    const [formKey, setFormKey] = useState(0); // Key to force form remount on apply
    const [chatInitialized, setChatInitialized] = useState(false); // Track if initial message was sent
    const sendMessageRef = useRef<((message: string, agentName?: string) => void) | null>(null);
    const layoutRef = useRef<HTMLDivElement | null>(null);
    const dragInfoRef = useRef({
        active: false,
        startX: 0,
        startRatio: loadPreference('panelRatio', 0.62),
        layoutWidth: 1,
    });
    const applyDebounceRef = useRef<NodeJS.Timeout | null>(null);
    const [chatTelemetry, setChatTelemetry] = useState<ChatTelemetrySnapshot | null>(null);
    const [isAutoApplying, setIsAutoApplying] = useState(false);
    const [focusMode, setFocusMode] = useState(false);
    const [hasPendingChanges, setHasPendingChanges] = useState(false);
    const [autoApplyEnabled, setAutoApplyEnabled] = useState(() => loadPreference('autoApplyEnabled', true));
    const [qualityStatus, setQualityStatus] = useState<QualityStatus>('notAnalyzed');
    const [qualityDrawerOpen, setQualityDrawerOpen] = useState(false);
    const [qualityLastAnalyzed, setQualityLastAnalyzed] = useState<number | null>(null);
    const [qualityResult, setQualityResult] = useState<QualityResult | null>(null);
    const [qualitySelection, setQualitySelection] = useState<string[]>([]);
    const [qualityExpandedPreviews, setQualityExpandedPreviews] = useState<Record<string, boolean>>({});
    const [isApplyingFindings, setIsApplyingFindings] = useState(false);
    const [qualityUndoSnapshot, setQualityUndoSnapshot] = useState<{
        agent?: Partial<ExtendedAgent>;
        timestamp: number;
        selections: string[];
    } | null>(null);
    const undoTimeoutRef = useRef<NodeJS.Timeout | null>(null);
    const viewModeRef = useRef(viewMode);

    useEffect(() => {
        viewModeRef.current = viewMode;
    }, [viewMode]);

    // Auto-open quality panel when switching to author-test-evaluate mode
    useEffect(() => {
        if (viewMode === 'author-test-evaluate') {
            setQualityDrawerOpen(true);
        }
    }, [viewMode]);

    // Toast notifications
    const toasterId = useId('toaster');
    const { dispatchToast } = useToastController(toasterId);

    const markPreviewUpdated = useCallback(() => {
        setPreviewRecentlyUpdated(true);
        if (insights || qualityResult) {
            setInsightsStale(true);
            setQualityStatus(prev => (prev === 'notAnalyzed' ? prev : 'stale'));
        }
    }, [insights, qualityResult]);

    useEffect(() => {
        if (!open) {
            setQualityDrawerOpen(false);
            setQualityStatus('notAnalyzed');
            setQualityResult(null);
            setQualitySelection([]);
            setQualityExpandedPreviews({});
            setQualityLastAnalyzed(null);
            setInsights(null);
            setInsightsError(undefined);
            setInsightsLoading(false);
            setInsightsStale(false);
            if (undoTimeoutRef.current) {
                clearTimeout(undoTimeoutRef.current);
                undoTimeoutRef.current = null;
            }
            setQualityUndoSnapshot(null);
        }
    }, [open]);

    // Save preferences to localStorage
    useEffect(() => {
        savePreference('panelRatio', panelRatio);
    }, [panelRatio]);

    useEffect(() => {
        savePreference('configTab', configTab);
    }, [configTab]);

    useEffect(() => {
        savePreference('autoApplyEnabled', autoApplyEnabled);
    }, [autoApplyEnabled]);

    // Note: viewMode is not saved to localStorage - always starts with 'author-test' (middle button)

    // Keyboard shortcuts
    useEffect(() => {
        if (!open) return;

        const handleKeyDown = (e: KeyboardEvent) => {
            // Ctrl+` to toggle Form/YAML
            if (e.ctrlKey && e.key === '`') {
                e.preventDefault();
                setConfigTab(prev => (prev === 'form' ? 'yaml' : 'form'));
            }
            // Ctrl+R to refresh insights
            if (e.ctrlKey && e.key === 'r') {
                e.preventDefault();
                if (insights) {
                    handleInsightsRefresh();
                }
            }
            // Ctrl+F to toggle focus mode (hide left panel)
            if (e.ctrlKey && e.key === 'f' && !e.shiftKey) {
                e.preventDefault();
                setFocusMode(prev => !prev);
            }
        };

        window.addEventListener('keydown', handleKeyDown);
        return () => window.removeEventListener('keydown', handleKeyDown);
    }, [open, insights]);

    const handleDividerMouseMove = useCallback((event: MouseEvent) => {
        const info = dragInfoRef.current;
        if (!info.active || !layoutRef.current) {
            return;
        }

        if (info.layoutWidth <= 0) {
            return;
        }

        const deltaRatio = (event.clientX - info.startX) / info.layoutWidth;
        const nextRatio = Math.min(0.72, Math.max(0.28, info.startRatio + deltaRatio));
        setPanelRatio(nextRatio);
    }, []);

    const handleDividerMouseUp = useCallback(() => {
        if (!dragInfoRef.current.active) {
            return;
        }

        dragInfoRef.current.active = false;
        document.removeEventListener('mousemove', handleDividerMouseMove);
        document.removeEventListener('mouseup', handleDividerMouseUp);
    }, [handleDividerMouseMove]);

    const handleDividerMouseDown = useCallback(
        (event: React.MouseEvent<HTMLDivElement>) => {
            if (!layoutRef.current) {
                return;
            }

            event.preventDefault();
            const bounds = layoutRef.current.getBoundingClientRect();
            dragInfoRef.current = {
                active: true,
                startX: event.clientX,
                startRatio: panelRatio,
                layoutWidth: bounds.width,
            };

            document.addEventListener('mousemove', handleDividerMouseMove);
            document.addEventListener('mouseup', handleDividerMouseUp);
        },
        [handleDividerMouseMove, handleDividerMouseUp, panelRatio]
    );

    useEffect(() => {
        return () => {
            document.removeEventListener('mousemove', handleDividerMouseMove);
            document.removeEventListener('mouseup', handleDividerMouseUp);
        };
    }, [handleDividerMouseMove, handleDividerMouseUp]);

    useEffect(() => {
        if (!previewRecentlyUpdated) {
            return;
        }

        const timeout = window.setTimeout(() => setPreviewRecentlyUpdated(false), PREVIEW_UPDATE_BADGE_TIMEOUT);
        return () => window.clearTimeout(timeout);
    }, [previewRecentlyUpdated]);

    const isAgentTarget = target?.type === 'agent';
    const isExtendedToolTarget = target?.type === 'tool';
    const isSystemToolTarget = target?.type === 'systemTool';
    const supportsYamlEditing = isAgentTarget || isExtendedToolTarget || isSystemToolTarget;
    const supportsFormEditing = isAgentTarget || isExtendedToolTarget;
    const supportsChatPreview = isAgentTarget;
    const supportsToolPreview = isAgentTarget || isExtendedToolTarget || isSystemToolTarget;

    useEffect(() => {
        if (!open) {
            return;
        }

        setPreviewRecentlyUpdated(false);

        const currentViewMode = viewModeRef.current;
        const shouldSkipSetup = currentViewMode === 'tester';

        if (!target) {
            setInsights(null);
            setInsightsError(undefined);
            setInsightsLoading(false);
            setInsightsStale(false);
            setDraftAgent(undefined);
            setDraftTool(undefined);
            setDraftSystemTool(undefined);
            setYamlContent('');
            setYamlError(undefined);
            setConfigTab('form');
            setSelectedToolName(undefined);
            setAcknowledgedMode(shouldSkipSetup);
            setToolTestStates({});
            setConfigEntity('agent');
            return;
        }

        if (target.type === 'agent') {
            setInsights(null);
            setInsightsError(undefined);
            setInsightsLoading(false);
            setInsightsStale(false);
            const copy = { ...target.agent };

            // Check if SearchMemory is in tools or systemTools (case-insensitive)
            const hasSearchMemoryInTools = copy.tools?.some(tool => tool.toLowerCase() === 'searchmemory') ?? false;
            const hasSearchMemoryInSystemTools = copy.systemTools?.some(tool => tool.toLowerCase() === 'searchmemory') ?? false;
            const hasSearchMemory = hasSearchMemoryInTools || hasSearchMemoryInSystemTools;

            // If SearchMemory is found, automatically enable knowledge base
            if (hasSearchMemory && !copy.enableMemory) {
                copy.enableMemory = true;
            }

            // Ensure SearchMemory is in systemTools if enableMemory is true
            if (copy.enableMemory) {
                const currentSystemTools = copy.systemTools ?? [];
                if (!currentSystemTools.some(tool => tool.toLowerCase() === 'searchmemory')) {
                    copy.systemTools = [...currentSystemTools, 'SearchMemory'];
                }

                // Add memory prompt to instructions if not already present
                const memoryPrompt = 'Use the search tools to incorporate memory in the final result.';
                const currentInstructions = copy.instructions || '';
                if (!currentInstructions.includes(memoryPrompt)) {
                    copy.instructions = currentInstructions ? `${currentInstructions}\n\n${memoryPrompt}` : memoryPrompt;
                }
            }

            setDraftAgent(copy);
            setDraftTool(undefined);
            setDraftSystemTool(undefined);
            setYamlContent(buildAgentYaml(copy));
            setSelectedToolName(copy.tools?.[0] ?? target.agent.tools?.[0]);
            setConfigTab('form');
            setAcknowledgedMode(shouldSkipSetup);
            setToolTestStates({});
            setConfigEntity('agent');
        } else if (target.type === 'tool') {
            setInsights(null);
            setInsightsError(undefined);
            setInsightsLoading(false);
            setInsightsStale(false);
            const copy = { ...target.tool };

            // Handle agent tools for tool targets
            const draftAgentCopy = target.agent ? { ...target.agent } : undefined;
            if (draftAgentCopy) {
                const hasSearchMemoryInTools = draftAgentCopy.tools?.some(tool => tool.toLowerCase() === 'searchmemory') ?? false;
                const hasSearchMemoryInSystemTools =
                    draftAgentCopy.systemTools?.some(tool => tool.toLowerCase() === 'searchmemory') ?? false;
                const hasSearchMemory = hasSearchMemoryInTools || hasSearchMemoryInSystemTools;

                if (hasSearchMemory && !draftAgentCopy.enableMemory) {
                    draftAgentCopy.enableMemory = true;
                }

                if (draftAgentCopy.enableMemory) {
                    const currentSystemTools = draftAgentCopy.systemTools ?? [];
                    if (!currentSystemTools.some(tool => tool.toLowerCase() === 'searchmemory')) {
                        draftAgentCopy.systemTools = [...currentSystemTools, 'SearchMemory'];
                    }

                    // Add memory prompt to instructions if not already present
                    const memoryPrompt = 'Use the search tools to incorporate memory in the final result.';
                    const currentInstructions = draftAgentCopy.instructions || '';
                    if (!currentInstructions.includes(memoryPrompt)) {
                        draftAgentCopy.instructions = currentInstructions ? `${currentInstructions}\n\n${memoryPrompt}` : memoryPrompt;
                    }
                }
            }

            setDraftAgent(draftAgentCopy);
            setDraftTool(copy);
            setDraftSystemTool(undefined);
            setYamlContent(buildToolYaml(copy));
            setSelectedToolName(copy.name);
            setConfigTab('form');
            setAcknowledgedMode(shouldSkipSetup);
            setToolTestStates({});
            setConfigEntity('tool');
        } else {
            setInsights(null);
            setInsightsError(undefined);
            setInsightsLoading(false);
            setInsightsStale(false);
            const copy = { ...target.tool };

            // Handle agent tools for systemTool targets
            const draftAgentCopy = target.agent ? { ...target.agent } : undefined;
            if (draftAgentCopy) {
                const hasSearchMemoryInTools = draftAgentCopy.tools?.some(tool => tool.toLowerCase() === 'searchmemory') ?? false;
                const hasSearchMemoryInSystemTools =
                    draftAgentCopy.systemTools?.some(tool => tool.toLowerCase() === 'searchmemory') ?? false;
                const hasSearchMemory = hasSearchMemoryInTools || hasSearchMemoryInSystemTools;

                if (hasSearchMemory && !draftAgentCopy.enableMemory) {
                    draftAgentCopy.enableMemory = true;
                }

                if (draftAgentCopy.enableMemory) {
                    const currentSystemTools = draftAgentCopy.systemTools ?? [];
                    if (!currentSystemTools.some(tool => tool.toLowerCase() === 'searchmemory')) {
                        draftAgentCopy.systemTools = [...currentSystemTools, 'SearchMemory'];
                    }

                    // Add memory prompt to instructions if not already present
                    const memoryPrompt = 'Use the search tools to incorporate memory in the final result.';
                    const currentInstructions = draftAgentCopy.instructions || '';
                    if (!currentInstructions.includes(memoryPrompt)) {
                        draftAgentCopy.instructions = currentInstructions ? `${currentInstructions}\n\n${memoryPrompt}` : memoryPrompt;
                    }
                }
            }

            setDraftAgent(draftAgentCopy);
            setDraftTool(undefined);
            setDraftSystemTool(copy);
            setYamlContent('# System tools are read-only and do not have YAML configuration');
            setSelectedToolName(copy.name);
            setConfigTab('form');
            setAcknowledgedMode(true);
            setToolTestStates({});
            setConfigEntity('tool');
        }

        setYamlError(undefined);
    }, [open, target]);

    // Handle viewMode changes - when switching TO tester mode, always skip setup
    useEffect(() => {
        if (!open || !target) {
            return;
        }

        // When switching TO tester mode, always skip setup
        if (viewMode === 'tester' && !acknowledgedMode) {
            setAcknowledgedMode(true);
        }
    }, [viewMode, open, target, acknowledgedMode]);

    useEffect(() => {
        if (!isAgentTarget) {
            return;
        }

        // Don't auto-select tools when we're in tool viewing/editing mode
        if (configEntity === 'tool') {
            return;
        }

        const availableTools = draftAgent?.tools ?? target?.agent.tools ?? [];
        const availableSystemTools = draftAgent?.systemTools ?? target?.agent.systemTools ?? [];

        if (!availableTools.length) {
            setSelectedToolName(undefined);
            return;
        }

        // Don't reset if the selected tool is either an extended tool or a system tool
        if (selectedToolName && (availableTools.includes(selectedToolName) || availableSystemTools.includes(selectedToolName))) {
            return;
        }

        setSelectedToolName(availableTools[0]);
    }, [configEntity, draftAgent?.tools, draftAgent?.systemTools, isAgentTarget, selectedToolName, target]);

    useEffect(() => {
        if (!isExtendedToolTarget) {
            return;
        }

        const nextName = draftTool?.name;
        if (selectedToolName === nextName) {
            return;
        }

        setSelectedToolName(nextName);
    }, [draftTool?.name, isExtendedToolTarget, selectedToolName]);

    const getToolKey = useCallback((tool?: Partial<ExtendedTool> | SystemTool) => tool?.name?.trim() || '__draft__', []);

    const linkedExtendedToolNames = useMemo(() => {
        const source = draftAgent?.tools ?? target?.agent?.tools ?? [];
        return Array.from(new Set(source.filter((name): name is string => !!name))).sort((a, b) =>
            a.localeCompare(b, undefined, { sensitivity: 'base' })
        );
    }, [draftAgent?.tools, target]);

    const availableExtendedToolNames = useMemo(() => {
        const names = new Set<string>();
        tools.forEach(tool => {
            if (tool.name) {
                names.add(tool.name);
            }
        });
        linkedExtendedToolNames.forEach(name => names.delete(name));
        return Array.from(names).sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }));
    }, [linkedExtendedToolNames, tools]);

    const availableSystemToolNames = useMemo(() => {
        const names = systemTools
            .map(tool => tool.name?.trim())
            .filter((name): name is string => !!name)
            .sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }));
        return names;
    }, [systemTools]);

    const toolLookup = useMemo(() => {
        const map = new Map<string, ExtendedTool | SystemTool>();
        tools.forEach(tool => {
            if (tool.name) {
                map.set(tool.name, tool);
            }
        });
        systemTools.forEach(tool => {
            if (tool.name) {
                map.set(tool.name, tool);
            }
        });
        return map;
    }, [systemTools, tools]);

    useEffect(() => {
        if (configEntity !== 'tool') {
            return;
        }

        // If a tool is already selected, don't override it
        if (selectedToolName) {
            return;
        }

        // Only use target-based selection if we're switching FROM agent mode
        // Don't use it if we're already in tool mode (user is switching between tools)
        if (isExtendedToolTarget && draftTool?.name) {
            setSelectedToolName(draftTool.name);
            return;
        }

        if (isSystemToolTarget && draftSystemTool?.name) {
            setSelectedToolName(draftSystemTool.name);
            return;
        }

        // If we have a draft system tool, don't fall through to auto-selection
        if (draftSystemTool?.name) {
            return;
        }

        // If we have a draft extended tool, don't fall through to auto-selection
        if (draftTool?.name) {
            return;
        }

        const firstLinked = linkedExtendedToolNames[0];
        if (firstLinked) {
            setSelectedToolName(firstLinked);
            return;
        }

        const firstAvailable = availableExtendedToolNames[0];
        if (firstAvailable) {
            setSelectedToolName(firstAvailable);
            return;
        }

        const firstSystem = availableSystemToolNames[0];
        if (firstSystem) {
            setSelectedToolName(firstSystem);
            return;
        }

        setSelectedToolName(NEW_KUSTO_TOOL_OPTION);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [
        availableExtendedToolNames,
        availableSystemToolNames,
        configEntity,
        linkedExtendedToolNames,
        selectedToolName,
        isExtendedToolTarget,
        isSystemToolTarget,
    ]);

    const selectedTool = useMemo(() => {
        if (selectedToolName === NEW_KUSTO_TOOL_OPTION) {
            return draftTool;
        }

        if (draftTool?.name && selectedToolName === draftTool.name) {
            return draftTool;
        }

        if (draftSystemTool?.name && selectedToolName === draftSystemTool.name) {
            return draftSystemTool;
        }

        if (selectedToolName) {
            const tool = toolLookup.get(selectedToolName);
            return tool;
        }

        if (isExtendedToolTarget) {
            return draftTool;
        }

        if (isSystemToolTarget) {
            return draftSystemTool;
        }

        return undefined;
    }, [draftSystemTool, draftTool, isExtendedToolTarget, isSystemToolTarget, selectedToolName, toolLookup]);

    const selectedToolKey = useMemo(() => (selectedTool ? getToolKey(selectedTool) : undefined), [getToolKey, selectedTool]);

    const selectedToolIsSystemTool = useMemo(() => {
        const isSystemTool = !!selectedTool && !('type' in selectedTool);
        return isSystemTool;
    }, [selectedTool, selectedToolName]);

    const selectedToolType = useMemo(() => {
        if (!selectedTool || selectedToolIsSystemTool) {
            return undefined;
        }

        return (selectedTool as Partial<ExtendedTool>).type?.trim() || undefined;
    }, [selectedTool, selectedToolIsSystemTool]);

    const selectedToolIsKusto = useMemo(() => selectedToolType === 'KustoTool', [selectedToolType]);

    const selectedExtendedTool = useMemo(() => {
        if (selectedToolIsSystemTool || !selectedTool) {
            return undefined;
        }

        return selectedTool as Partial<ExtendedTool>;
    }, [selectedTool, selectedToolIsSystemTool]);

    useEffect(() => {
        if (!selectedTool || selectedToolIsSystemTool) {
            if (!isExtendedToolTarget) {
                setDraftTool(prev => (prev && !prev.name ? prev : undefined));
            }
            return;
        }

        setDraftTool(prev => {
            if (prev && prev.name === selectedTool.name) {
                return prev;
            }
            return { ...(selectedTool as ExtendedTool) };
        });
    }, [isExtendedToolTarget, selectedTool, selectedToolIsSystemTool]);

    const selectedToolFingerprint = useMemo(() => {
        if (!selectedToolIsKusto || !selectedTool) {
            return null;
        }

        return getKustoTestFingerprint(selectedTool as Partial<ExtendedTool>);
    }, [selectedTool, selectedToolIsKusto]);

    const selectedToolTestState = useMemo(() => {
        if (!selectedToolKey) {
            return undefined;
        }

        return toolTestStates[selectedToolKey];
    }, [selectedToolKey, toolTestStates]);

    const insightSections = useMemo(() => {
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

    useEffect(() => {
        if (!selectedToolKey || !selectedToolIsKusto) {
            return;
        }

        const fingerprint = selectedToolFingerprint ?? null;

        setToolTestStates(prev => {
            const current = prev[selectedToolKey];
            if (!current) {
                return {
                    ...prev,
                    [selectedToolKey]: { status: 'idle' },
                };
            }

            const hasMatchingSuccess = current.status === 'success' && current.lastRunFingerprint === fingerprint;
            if (hasMatchingSuccess || current.status === 'running') {
                return prev;
            }

            if (current.lastRunFingerprint !== fingerprint) {
                return {
                    ...prev,
                    [selectedToolKey]: {
                        status: 'idle',
                        lastRunFingerprint: current.lastRunFingerprint,
                    },
                };
            }

            if (current.status === 'error') {
                return {
                    ...prev,
                    [selectedToolKey]: {
                        status: 'idle',
                        lastRunFingerprint: current.lastRunFingerprint,
                    },
                };
            }

            return prev;
        });
    }, [selectedToolFingerprint, selectedToolIsKusto, selectedToolKey]);

    useEffect(() => {
        if (!acknowledgedMode) {
            return;
        }

        if (configEntity === 'agent') {
            if (draftAgent) {
                setYamlContent(buildAgentYaml(draftAgent));
            }
            setYamlError(undefined);
            return;
        }

        if (configEntity === 'tool') {
            if (selectedTool) {
                // System tools don't have YAML representation
                if (selectedToolIsSystemTool) {
                    setYamlContent('# System tools are read-only and do not have YAML configuration');
                } else {
                    setYamlContent(buildToolYaml(selectedTool));
                }
            } else {
                setYamlContent('');
            }
            setYamlError(undefined);
        }
    }, [acknowledgedMode, configEntity, draftAgent, selectedTool, selectedToolIsSystemTool]);

    // Cleanup debounce timeout on unmount
    useEffect(() => {
        return () => {
            if (applyDebounceRef.current) {
                clearTimeout(applyDebounceRef.current);
            }
        };
    }, []);

    // Keyboard shortcut for toggling between Form/YAML tabs (Ctrl+`)
    useEffect(() => {
        const handleKeyDown = (event: KeyboardEvent) => {
            if (event.ctrlKey && event.key === '`') {
                event.preventDefault();
                setConfigTab(prev => (prev === 'form' ? 'yaml' : 'form'));
            }
        };

        document.addEventListener('keydown', handleKeyDown);
        return () => document.removeEventListener('keydown', handleKeyDown);
    }, []);

    // Auto-send initial message when chat loads
    useEffect(() => {
        if (!chatInitialized && sendMessageRef.current && !isAutoApplying && !isApplyingFindings && supportsChatPreview) {
            setChatInitialized(true);
            // Send a simple greeting to start the conversation
            const timer = setTimeout(() => {
                if (sendMessageRef.current) {
                    sendMessageRef.current("Hi! Let's test this agent.");
                }
            }, 500);

            return () => clearTimeout(timer);
        }
    }, [chatInitialized, isAutoApplying, isApplyingFindings, supportsChatPreview, chatKey]);

    const handleToolTestStatusChange = useCallback(
        (key: string | undefined, status: ToolTestStatus, options?: { error?: string; fingerprint?: string | null }) => {
            if (!key) {
                return;
            }

            setToolTestStates(prev => {
                const previous = prev[key];
                return {
                    ...prev,
                    [key]: {
                        status,
                        errorMessage: options?.error,
                        lastRunFingerprint:
                            status === 'success'
                                ? (options?.fingerprint ?? previous?.lastRunFingerprint ?? null)
                                : previous?.lastRunFingerprint,
                    },
                };
            });
        },
        []
    );

    const handleConfigEntityChange = useCallback(
        (_: unknown, data: { value: TabValue }) => {
            if (!acknowledgedMode) {
                return;
            }

            setConfigEntity(data.value as 'agent' | 'tool');
        },
        [acknowledgedMode]
    );

    const handleConfigTabChange = useCallback((_: unknown, data: { value: TabValue }) => {
        setConfigTab(data.value as ConfigTabValue);
    }, []);

    const handleInsightsRefresh = useCallback(
        async (overrideAgent?: Partial<ExtendedAgent>) => {
            if (!sreAgentEndpoint) {
                setInsightsError(intl.formatMessage(PlaygroundResources.insightsError));
                return;
            }

            let currentAgentData = overrideAgent ?? draftAgent;

            if (!overrideAgent && configEntity === 'agent' && configTab === 'yaml' && yamlContent && !yamlError) {
                const { agent } = tryParseAgentYaml(yamlContent, draftAgent);
                if (agent) {
                    currentAgentData = agent;
                }
            }

            const prompt = (currentAgentData?.instructions ?? target?.agent?.instructions ?? '').trim();
            if (!prompt) {
                setInsights(null);
                setQualityResult(null);
                setInsightsError(intl.formatMessage(PlaygroundResources.insightsNoData));
                setInsightsStale(false);
                setQualityStatus('notAnalyzed');
                setQualityDrawerOpen(false);
                return;
            }

            const toolsForRequest = Array.from(
                new Set((currentAgentData?.tools ?? target?.agent?.tools ?? []).filter((name): name is string => !!name))
            );
            const systemToolsForRequest = Array.from(
                new Set((currentAgentData?.systemTools ?? target?.agent?.systemTools ?? []).filter((name): name is string => !!name))
            );

            const failingToolFindings: PlaygroundInsightEvidence[] = Object.entries(toolTestStates)
                .filter(([, state]) => state?.status === 'error')
                .map(([key, state]) => ({
                    title:
                        key === NEW_KUSTO_TOOL_OPTION || key === '__draft__'
                            ? intl.formatMessage(PlaygroundResources.toolFormCreateNewKusto)
                            : key,
                    detail: state?.errorMessage ?? intl.formatMessage(PlaygroundResources.insightsToolFailureFallback),
                    severity: 'error',
                }));

            const wasDrawerOpen = qualityDrawerOpen;
            setQualityDrawerOpen(true);
            setQualityStatus('running');
            setInsightsLoading(true);
            setInsightsError(undefined);

            try {
                // Debug: log the chat telemetry structure
                if (chatTelemetry?.messages && chatTelemetry.messages.length > 0) {
                    console.log('Chat telemetry messages count:', chatTelemetry.messages.length);
                    chatTelemetry.messages.forEach((msg, idx) => {
                        console.log(`Message ${idx}:`, {
                            role: msg.authorRole,
                            text: msg.text,
                            hasText: !!msg.text,
                            textLength: msg.text?.length ?? 0,
                            timestamp: msg.timeStamp,
                        });
                    });
                } else {
                    console.log('No chat messages available for evaluation');
                }

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

                console.log('Sending recentMessages to backend:', recentMessages);

                // Create a transcript summary
                const transcriptSummary =
                    recentMessages.length > 0
                        ? `Conversation with ${recentMessages.length} exchanges:\n${recentMessages.join('\n')}`
                        : undefined;

                const response = await fetchPlaygroundInsights(sreAgentEndpoint, {
                    prompt,
                    agentName: currentAgentData?.name ?? target?.agent?.name,
                    agentGoal: currentAgentData?.metadata?.goal ?? (target?.agent?.metadata as any)?.goal,
                    tools: toolsForRequest,
                    systemTools: systemToolsForRequest,
                    availableTools: tools.map(t => t.name).filter((name): name is string => !!name),
                    availableSystemTools: systemTools.map(t => t.name).filter((name): name is string => !!name),
                    chatFindings,
                    toolFindings: failingToolFindings,
                    transcriptSummary,
                    recentMessages,
                });

                setInsights(response);
                const quality = buildQualityResult(prompt, toolsForRequest, systemToolsForRequest, response);
                setQualityResult(quality);
                setQualityStatus('fresh');
                setQualityLastAnalyzed(Date.now());
                setQualitySelection([]);
                setQualityExpandedPreviews({});
                setQualityUndoSnapshot(null);
                setInsightsStale(false);

                if (!wasDrawerOpen && quality.findings.length > 0) {
                    const projected = quality.findings.reduce((sum, item) => sum + item.expectedLift, 0);
                    dispatchToast(
                        <Toast>
                            <ToastTitle>{`Found ${quality.findings.length} quick fixes`}</ToastTitle>
                            <ToastBody>
                                {`+${projected} projected — `}
                                <Button appearance="subtle" size="small" onClick={() => setQualityDrawerOpen(true)}>
                                    View
                                </Button>
                            </ToastBody>
                        </Toast>,
                        { intent: 'info', timeout: 5000 }
                    );
                }
            } catch (error) {
                const message = error instanceof Error ? error.message : String(error);
                setInsightsError(`${intl.formatMessage(PlaygroundResources.insightsError)} ${message}`.trim());
                setQualityStatus(prev => (prev === 'running' && qualityResult ? 'stale' : prev === 'running' ? 'notAnalyzed' : prev));
            } finally {
                setInsightsLoading(false);
            }
        },
        [
            chatTelemetry,
            configEntity,
            configTab,
            dispatchToast,
            draftAgent,
            intl,
            qualityDrawerOpen,
            qualityResult,
            sreAgentEndpoint,
            target?.agent?.instructions,
            target?.agent?.metadata,
            target?.agent?.name,
            target?.agent?.systemTools,
            target?.agent?.tools,
            toolTestStates,
            yamlContent,
            yamlError,
        ]
    );

    const qualityScore = qualityResult?.overallScore ?? 0;

    const qualityStatusDescriptor = useMemo(() => {
        const scoreLabel = intl.formatMessage(PlaygroundResources.qualityScoreLabel, { score: qualityScore });

        switch (qualityStatus) {
            case 'running':
                return {
                    label: `${scoreLabel} • ${intl.formatMessage(PlaygroundResources.qualityStatusRunning)}`,
                    color: 'brand' as const,
                };
            case 'fresh': {
                const statusLabel = qualityLastAnalyzed
                    ? intl.formatMessage(PlaygroundResources.qualityStatusFreshWithTime, {
                          time: formatRelativeTime(qualityLastAnalyzed),
                      })
                    : intl.formatMessage(PlaygroundResources.qualityStatusFresh);
                return {
                    label: `${scoreLabel} • ${statusLabel}`,
                    color: 'success' as const,
                };
            }
            case 'stale':
                return {
                    label: `${scoreLabel} • ${intl.formatMessage(PlaygroundResources.qualityStatusStale)}`,
                    color: 'warning' as const,
                };
            default:
                return {
                    label: `${scoreLabel} • ${intl.formatMessage(PlaygroundResources.qualityStatusNotAnalyzed)}`,
                    color: 'informative' as const,
                };
        }
    }, [intl, qualityLastAnalyzed, qualityScore, qualityStatus]);

    const selectedFindings = useMemo(() => {
        if (!qualityResult) {
            return [] as QualityFinding[];
        }
        return qualityResult.findings.filter(finding => qualitySelection.includes(finding.id));
    }, [qualityResult, qualitySelection]);

    const projectedLift = useMemo(() => selectedFindings.reduce((sum, finding) => sum + finding.expectedLift, 0), [selectedFindings]);

    const findingsCount = qualityResult?.findings.length ?? 0;

    const handleToggleFindingPreview = useCallback((findingId: string) => {
        setQualityExpandedPreviews(prev => ({
            ...prev,
            [findingId]: !prev[findingId],
        }));
    }, []);

    const handleToggleFindingSelection = useCallback((findingId: string) => {
        setQualitySelection(prev => {
            if (prev.includes(findingId)) {
                return prev.filter(id => id !== findingId);
            }
            return [...prev, findingId];
        });
    }, []);

    const handleToggleSelectAll = useCallback(() => {
        if (!qualityResult?.findings) {
            return;
        }

        const allFindingIds = qualityResult.findings.map(f => f.id);
        const allSelected = allFindingIds.every(id => qualitySelection.includes(id));

        if (allSelected) {
            setQualitySelection([]);
        } else {
            setQualitySelection(allFindingIds);
        }
    }, [qualityResult?.findings, qualitySelection]);

    useEffect(() => {
        if (insightsStale && qualityStatus === 'fresh') {
            setQualityStatus('stale');
        }
        if (!insightsStale && qualityStatus === 'stale' && !qualityResult) {
            setQualityStatus('notAnalyzed');
        }
    }, [insightsStale, qualityResult, qualityStatus]);

    const handleAgentFormChange = useCallback(
        (updatedAgent: Partial<ExtendedAgent>) => {
            // Add memory prompt to instructions if enableMemory is true and prompt not already present
            if (updatedAgent.enableMemory) {
                const memoryPrompt = 'Use the search tools to incorporate memory in the final result.';
                const currentInstructions = updatedAgent.instructions || '';
                if (!currentInstructions.includes(memoryPrompt)) {
                    updatedAgent.instructions = currentInstructions ? `${currentInstructions}\n\n${memoryPrompt}` : memoryPrompt;
                }
            }

            setDraftAgent(updatedAgent);
            if (configEntity === 'agent') {
                setYamlContent(buildAgentYaml(updatedAgent));
                setYamlError(undefined);
            }
            markPreviewUpdated();

            // Mark changes as pending
            setHasPendingChanges(true);

            // Auto-apply if enabled
            if (autoApplyEnabled && sreAgentEndpoint && updatedAgent) {
                if (applyDebounceRef.current) {
                    clearTimeout(applyDebounceRef.current);
                }

                applyDebounceRef.current = setTimeout(async () => {
                    setIsAutoApplying(true);
                    try {
                        // 10 second artificial delay for clear feedback
                        await new Promise(resolve => setTimeout(resolve, 10000));

                        const yamlContent = buildAgentYaml(updatedAgent);
                        const agentHeaders = getAgentHeaders();
                        const { 'Content-Type': _, ...headersWithoutContentType } = agentHeaders;

                        await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/apply`, {
                            method: 'PUT',
                            headers: {
                                ...headersWithoutContentType,
                                'Content-Type': 'application/x-yaml',
                            },
                            body: yamlContent,
                        });

                        console.log('Auto-applied form changes successfully');
                        setHasPendingChanges(false);

                        // Show success toast with restart prompt
                        dispatchToast(
                            <Toast>
                                <ToastTitle media={<CheckmarkCircle20Regular />}>Changes auto-applied!</ToastTitle>
                                <ToastBody>{intl.formatMessage(PlaygroundResources.toastChatRestartReminder)}</ToastBody>
                            </Toast>,
                            { intent: 'success', timeout: 5000 }
                        );
                    } catch (error) {
                        console.error('Failed to auto-apply form changes:', error);
                        dispatchToast(
                            <Toast>
                                <ToastTitle>{intl.formatMessage(PlaygroundResources.toastApplyFailedTitle)}</ToastTitle>
                                <ToastBody>{intl.formatMessage(PlaygroundResources.toastApplyFailedBody)}</ToastBody>
                            </Toast>,
                            { intent: 'error' }
                        );
                    } finally {
                        setIsAutoApplying(false);
                    }
                }, 1000);
            }
        },
        [configEntity, markPreviewUpdated, autoApplyEnabled, sreAgentEndpoint, dispatchToast]
    );

    const handleToolFormChange = useCallback(
        (updatedTool: Partial<ExtendedTool>) => {
            setDraftTool(updatedTool);
            if (isExtendedToolTarget || configEntity === 'tool') {
                // Only generate YAML for extended tools, not system tools
                if ('type' in updatedTool && updatedTool.type) {
                    setYamlContent(buildToolYaml(updatedTool));
                }
                setYamlError(undefined);
            }
            const key = selectedToolKey ?? getToolKey(updatedTool);
            const fingerprint = getKustoTestFingerprint(updatedTool) ?? null;
            setToolTestStates(prev => {
                const current = prev[key];
                if (!current) {
                    return prev;
                }

                const shouldKeepSuccess = current.status === 'success' && current.lastRunFingerprint === fingerprint;
                if (shouldKeepSuccess) {
                    return prev;
                }

                return {
                    ...prev,
                    [key]: {
                        status: 'idle',
                        lastRunFingerprint: current.lastRunFingerprint,
                    },
                };
            });
            markPreviewUpdated();

            // Mark changes as pending
            setHasPendingChanges(true);

            // Auto-apply if enabled
            if (autoApplyEnabled && sreAgentEndpoint) {
                if (applyDebounceRef.current) {
                    clearTimeout(applyDebounceRef.current);
                }

                applyDebounceRef.current = setTimeout(async () => {
                    setIsAutoApplying(true);
                    try {
                        // 10 second artificial delay for clear feedback
                        await new Promise(resolve => setTimeout(resolve, 10000));

                        // Only generate YAML for extended tools, not system tools
                        const yamlContent =
                            'type' in updatedTool && updatedTool.type
                                ? buildToolYaml(updatedTool)
                                : '# System tools are read-only and do not have YAML configuration';
                        const agentHeaders = getAgentHeaders();
                        const { 'Content-Type': _, ...headersWithoutContentType } = agentHeaders;

                        await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/apply`, {
                            method: 'PUT',
                            headers: {
                                ...headersWithoutContentType,
                                'Content-Type': 'application/x-yaml',
                            },
                            body: yamlContent,
                        });

                        console.log('Auto-applied tool changes successfully');
                        setHasPendingChanges(false);

                        // Show success toast with restart prompt
                        dispatchToast(
                            <Toast>
                                <ToastTitle media={<CheckmarkCircle20Regular />}>Changes auto-applied!</ToastTitle>
                                <ToastBody>{intl.formatMessage(PlaygroundResources.toastChatRestartReminder)}</ToastBody>
                            </Toast>,
                            { intent: 'success', timeout: 5000 }
                        );
                    } catch (error) {
                        console.error('Failed to auto-apply tool changes:', error);
                        dispatchToast(
                            <Toast>
                                <ToastTitle>{intl.formatMessage(PlaygroundResources.toastApplyFailedTitle)}</ToastTitle>
                                <ToastBody>{intl.formatMessage(PlaygroundResources.toastApplyFailedBody)}</ToastBody>
                            </Toast>,
                            { intent: 'error' }
                        );
                    } finally {
                        setIsAutoApplying(false);
                    }
                }, 1000);
            }
        },
        [
            configEntity,
            getToolKey,
            isExtendedToolTarget,
            markPreviewUpdated,
            selectedToolKey,
            autoApplyEnabled,
            sreAgentEndpoint,
            dispatchToast,
        ]
    );

    const handleExportAnalysis = useCallback(() => {
        const agentName = draftAgent?.name ?? target?.agent?.name ?? 'agent';
        const payload = {
            generatedAt: new Date().toISOString(),
            agentName,
            qualityStatus,
            overallScore: qualityResult?.overallScore ?? null,
            hint: qualityResult?.hint ?? null,
            evidence: qualityResult?.evidence ?? null,
            subScores: qualityResult?.subScores ?? [],
            findings: qualityResult?.findings ?? [],
            rawInsights: insights,
        };

        const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        const safeName = agentName.replace(/[^a-z0-9-_]+/gi, '-').toLowerCase();
        link.href = url;
        link.download = `${safeName || 'agent'}-quality-analysis.json`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    }, [draftAgent, insights, qualityResult, qualityStatus, target]);

    const handleUndoAppliedFindings = useCallback(() => {
        if (!qualityUndoSnapshot?.agent) {
            return;
        }

        if (undoTimeoutRef.current) {
            clearTimeout(undoTimeoutRef.current);
            undoTimeoutRef.current = null;
        }

        handleAgentFormChange({
            ...qualityUndoSnapshot.agent,
            tools: qualityUndoSnapshot.agent.tools ? [...qualityUndoSnapshot.agent.tools] : qualityUndoSnapshot.agent.tools,
            systemTools: qualityUndoSnapshot.agent.systemTools
                ? [...qualityUndoSnapshot.agent.systemTools]
                : qualityUndoSnapshot.agent.systemTools,
        });

        setFormKey(prev => prev + 1); // Force form to remount with undone values
        setQualityUndoSnapshot(null);
        setQualitySelection([]);
        setQualityStatus('stale');
    }, [handleAgentFormChange, qualityUndoSnapshot]);

    const handleApplySelectedFindings = useCallback(async () => {
        if (!qualityResult || selectedFindings.length === 0) {
            return;
        }

        const baseAgent = draftAgent ?? target?.agent;
        if (!baseAgent) {
            return;
        }

        const snapshot: Partial<ExtendedAgent> = {
            ...baseAgent,
            tools: baseAgent.tools ? [...baseAgent.tools] : baseAgent.tools,
            systemTools: baseAgent.systemTools ? [...baseAgent.systemTools] : baseAgent.systemTools,
        };

        const nextAgent: Partial<ExtendedAgent> = {
            ...snapshot,
            tools: snapshot.tools ? [...snapshot.tools] : [],
            systemTools: snapshot.systemTools ? [...snapshot.systemTools] : [],
            instructions: (snapshot.instructions ?? '').toString(),
        };

        setIsApplyingFindings(true);

        try {
            selectedFindings.forEach(finding => {
                if (!finding.payload) {
                    return;
                }

                if (finding.payload.type === 'promptPatch') {
                    // Apply unified diff patch
                    const currentInstructions = nextAgent.instructions ?? '';
                    try {
                        nextAgent.instructions = applyUnifiedDiff(currentInstructions, finding.payload.patch);
                    } catch (error) {
                        console.error('Failed to apply patch:', error);
                        // Fallback: treat as addition
                        const trimmed = currentInstructions.trimEnd();
                        nextAgent.instructions = trimmed ? `${trimmed}\n\n${finding.payload.patch}` : finding.payload.patch;
                    }
                } else if (finding.payload.type === 'instructions') {
                    const addition = finding.payload.addition.trim();
                    const existing = nextAgent.instructions ?? '';
                    if (!existing.includes(addition)) {
                        const trimmed = existing.trimEnd();
                        nextAgent.instructions = trimmed ? `${trimmed}\n\n${addition}` : addition;
                    }
                } else if (finding.payload.type === 'prompt-rewrite') {
                    // Complete prompt rewrite - replace entire instructions
                    const newPrompt = finding.payload.fullPromptRewrite?.trim() ?? '';
                    // Extract the actual prompt from diff format if present
                    const promptMatch = newPrompt.match(/^\+(.+)$/m);
                    if (promptMatch) {
                        // Parse diff format: extract lines starting with +
                        const lines = newPrompt
                            .split('\n')
                            .filter(line => line.startsWith('+') && !line.startsWith('+++'))
                            .map(line => line.substring(1));
                        nextAgent.instructions = lines.join('\n').trim();
                    } else {
                        // Use as-is if not in diff format
                        nextAgent.instructions = newPrompt;
                    }
                } else if (finding.payload.type === 'tool') {
                    const updatedTools = Array.isArray(nextAgent.tools) ? [...nextAgent.tools] : [];
                    if (!updatedTools.includes(finding.payload.toolName)) {
                        updatedTools.push(finding.payload.toolName);
                        nextAgent.tools = updatedTools;
                    }
                } else if (finding.payload.type === 'newTool') {
                    const placeholder = `${finding.payload.toolName}-stub`;
                    const updatedTools = Array.isArray(nextAgent.tools) ? [...nextAgent.tools] : [];
                    if (!updatedTools.includes(placeholder)) {
                        updatedTools.push(placeholder);
                        nextAgent.tools = updatedTools;
                    }
                }
            });

            if (undoTimeoutRef.current) {
                clearTimeout(undoTimeoutRef.current);
            }

            setQualityUndoSnapshot({ agent: snapshot, timestamp: Date.now(), selections: selectedFindings.map(f => f.id) });
            undoTimeoutRef.current = setTimeout(() => setQualityUndoSnapshot(null), 10_000);

            // Apply changes to form
            handleAgentFormChange(nextAgent);
            setQualitySelection([]);
            setChatKey(prev => prev + 1);
            setFormKey(prev => prev + 1); // Force form to remount with new values
            setChatInitialized(false);

            // Persist changes to backend immediately
            if (sreAgentEndpoint) {
                try {
                    const yamlContent = buildAgentYaml(nextAgent);
                    const agentHeaders = getAgentHeaders();
                    const { 'Content-Type': _, ...headersWithoutContentType } = agentHeaders;

                    await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/apply`, {
                        method: 'PUT',
                        headers: {
                            ...headersWithoutContentType,
                            'Content-Type': 'application/x-yaml',
                        },
                        body: yamlContent,
                    });

                    setHasPendingChanges(false);
                } catch (applyError) {
                    console.error('Failed to persist changes to backend:', applyError);
                    // Don't fail the whole operation if backend apply fails
                }
            }

            // Show success toast
            dispatchToast(
                <Toast>
                    <ToastTitle
                        media={<CheckmarkCircle20Regular />}
                    >{`Applied ${selectedFindings.length} change${selectedFindings.length === 1 ? '' : 's'}`}</ToastTitle>
                    <ToastBody>
                        {`Projected +${projectedLift} overall • Chat restarted • `}
                        <Button appearance="subtle" size="small" onClick={handleUndoAppliedFindings}>
                            {intl.formatMessage(PlaygroundResources.toastUndoLabel)}
                        </Button>
                    </ToastBody>
                </Toast>,
                { intent: 'success', timeout: 10000 }
            );

            // Wait for backend to persist and UI to fully update before re-evaluating
            // This gives the user time to see the applied changes and ensures evaluation runs on committed data
            await new Promise(resolve => setTimeout(resolve, 2000));
            await handleInsightsRefresh(nextAgent);
        } catch (error) {
            console.error('Error applying findings:', error);
            dispatchToast(
                <Toast>
                    <ToastTitle>{intl.formatMessage(PlaygroundResources.toastApplyFailedTitle)}</ToastTitle>
                    <ToastBody>{error instanceof Error ? error.message : 'Unknown error'}</ToastBody>
                </Toast>,
                { intent: 'error' }
            );
        } finally {
            setIsApplyingFindings(false);
        }
    }, [
        dispatchToast,
        draftAgent,
        handleAgentFormChange,
        handleInsightsRefresh,
        handleUndoAppliedFindings,
        projectedLift,
        qualityResult,
        selectedFindings,
        setChatInitialized,
        setChatKey,
        target?.agent,
    ]);

    const handleYamlChange = useCallback(
        (value: string | undefined) => {
            if (!supportsYamlEditing || !target) {
                return;
            }

            const content = value ?? '';
            setYamlContent(content);

            // Mark changes as pending
            setHasPendingChanges(true);

            // Auto-apply if enabled
            if (autoApplyEnabled && content.trim() && sreAgentEndpoint) {
                if (applyDebounceRef.current) {
                    clearTimeout(applyDebounceRef.current);
                }

                applyDebounceRef.current = setTimeout(async () => {
                    setIsAutoApplying(true);
                    try {
                        // 10 second artificial delay for clear feedback
                        await new Promise(resolve => setTimeout(resolve, 10000));

                        const agentHeaders = getAgentHeaders();
                        const { 'Content-Type': _, ...headersWithoutContentType } = agentHeaders;

                        const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/apply`, {
                            method: 'PUT',
                            headers: {
                                ...headersWithoutContentType,
                                'Content-Type': 'application/x-yaml',
                            },
                            body: content.trim(),
                        });

                        if (!response.ok) {
                            throw new Error(`Failed to apply YAML changes: ${response.status}`);
                        }

                        console.log('Auto-applied YAML changes successfully');
                        setHasPendingChanges(false);

                        // Show success toast with restart prompt
                        dispatchToast(
                            <Toast>
                                <ToastTitle media={<CheckmarkCircle20Regular />}>Changes auto-applied!</ToastTitle>
                                <ToastBody>{intl.formatMessage(PlaygroundResources.toastChatRestartReminder)}</ToastBody>
                            </Toast>,
                            { intent: 'success', timeout: 5000 }
                        );
                    } catch (error) {
                        console.error('Failed to auto-apply YAML changes:', error);
                        dispatchToast(
                            <Toast>
                                <ToastTitle>{intl.formatMessage(PlaygroundResources.toastApplyFailedTitle)}</ToastTitle>
                                <ToastBody>{intl.formatMessage(PlaygroundResources.toastApplyFailedBody)}</ToastBody>
                            </Toast>,
                            { intent: 'error' }
                        );
                    } finally {
                        setIsAutoApplying(false);
                    }
                }, 1000);
            }

            if (isAgentTarget) {
                const { agent, error } = tryParseAgentYaml(content, draftAgent);
                setDraftAgent(agent ?? draftAgent);
                setYamlError(error);
            } else if (isExtendedToolTarget) {
                if (configEntity === 'tool' || isExtendedToolTarget) {
                    const { tool, error } = tryParseToolYaml(content, draftTool);
                    setDraftTool(tool ?? draftTool);
                    setYamlError(error);
                    if (tool) {
                        const key = selectedToolKey ?? getToolKey(tool);
                        const fingerprint = getKustoTestFingerprint(tool) ?? null;
                        setToolTestStates(prev => {
                            const current = prev[key];
                            if (!current) {
                                return prev;
                            }

                            const shouldKeepSuccess = current.status === 'success' && current.lastRunFingerprint === fingerprint;
                            if (shouldKeepSuccess) {
                                return prev;
                            }

                            return {
                                ...prev,
                                [key]: {
                                    status: 'idle',
                                    lastRunFingerprint: current.lastRunFingerprint,
                                },
                            };
                        });
                    }
                }
            }
            markPreviewUpdated();
        },
        [
            configEntity,
            draftAgent,
            draftTool,
            getToolKey,
            isAgentTarget,
            isExtendedToolTarget,
            markPreviewUpdated,
            selectedToolKey,
            supportsYamlEditing,
            target,
            autoApplyEnabled,
            sreAgentEndpoint,
            dispatchToast,
        ]
    );

    const handleCommitChanges = useCallback(async () => {
        if (!hasPendingChanges) {
            return;
        }

        setIsAutoApplying(true);

        try {
            if (sreAgentEndpoint) {
                // 10 second artificial delay for clear feedback
                await new Promise(resolve => setTimeout(resolve, 10000));

                const agentHeaders = getAgentHeaders();
                const { 'Content-Type': _, ...headersWithoutContentType } = agentHeaders;

                let body: string;
                if (configTab === 'yaml') {
                    body = yamlContent.trim();
                } else {
                    // From form - use current draft agent
                    if (!draftAgent) {
                        throw new Error('No agent configuration available');
                    }
                    body = buildAgentYaml(draftAgent);
                }

                const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/apply`, {
                    method: 'PUT',
                    headers: {
                        ...headersWithoutContentType,
                        'Content-Type': 'application/x-yaml',
                    },
                    body,
                });

                if (!response.ok) {
                    const errorText = await response.text();
                    throw new Error(`Failed to apply changes: ${response.status} - ${errorText}`);
                }

                console.log('Changes committed successfully');

                // Show success toast
                dispatchToast(
                    <Toast>
                        <ToastTitle media={<CheckmarkCircle20Regular />}>Changes applied successfully!</ToastTitle>
                        <ToastBody>{intl.formatMessage(PlaygroundResources.toastChatRestarted)}</ToastBody>
                    </Toast>,
                    { intent: 'success', timeout: 5000 }
                );

                // Clear pending changes flag
                setHasPendingChanges(false);

                // Restart chat to apply changes
                setChatKey(prev => prev + 1);
                setChatInitialized(false);
            }
        } catch (error) {
            console.error('Failed to commit changes:', error);
            dispatchToast(
                <Toast>
                    <ToastTitle>{intl.formatMessage(PlaygroundResources.toastApplyFailedTitle)}</ToastTitle>
                    <ToastBody>{intl.formatMessage(PlaygroundResources.toastApplyFailedBody)}</ToastBody>
                </Toast>,
                { intent: 'error' }
            );
        } finally {
            setIsAutoApplying(false);
        }
    }, [hasPendingChanges, sreAgentEndpoint, configTab, yamlContent, draftAgent, dispatchToast]);

    const yamlTabDisabled = configEntity === 'agent' ? !supportsYamlEditing : !selectedTool;

    const renderSetupCard = () => (
        <div className={styles.setupCard}>
            <Caption1Strong>{intl.formatMessage(PlaygroundResources.setupImprovedTitle)}</Caption1Strong>
            <Body1>{intl.formatMessage(PlaygroundResources.setupImprovedDescription)}</Body1>
            <div className={styles.setupActions}>
                <Tooltip content={intl.formatMessage(PlaygroundResources.editExistingImprovedTooltip)} relationship="label">
                    <Button appearance="primary" onClick={() => setAcknowledgedMode(true)} autoFocus>
                        {intl.formatMessage(PlaygroundResources.editExistingImprovedButton)}
                    </Button>
                </Tooltip>
                <Tooltip content={intl.formatMessage(PlaygroundResources.copyComingSoonTooltip)} relationship="description">
                    <Button appearance="secondary" disabled>
                        {intl.formatMessage(PlaygroundResources.copyExistingButton)}
                    </Button>
                </Tooltip>
            </div>
        </div>
    );

    const renderConfigurationSwitcher = () => {
        return (
            <div
                style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    marginBottom: tokens.spacingVerticalS,
                    paddingBottom: tokens.spacingVerticalS,
                    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
                }}
            >
                <TabList selectedValue={configEntity} onTabSelect={handleConfigEntityChange} appearance="subtle" size="small">
                    <Tab value="agent" icon={<PersonFilled />}>
                        Agent
                    </Tab>
                    <Tab value="tool" icon={<WrenchFilled />}>
                        Tools
                    </Tab>
                </TabList>
                <Tooltip content="Collapse to tester view" relationship="label">
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<PanelRightContract20Regular style={{ transform: 'rotate(180deg)' }} />}
                        onClick={() => setViewMode('tester')}
                        aria-label={intl.formatMessage(PlaygroundResources.collapsePanelAriaLabel)}
                    />
                </Tooltip>
            </div>
        );
    };

    const renderAgentForm = () => {
        if (!supportsFormEditing || (!isAgentTarget && !draftAgent)) {
            return <Body1>{intl.formatMessage(PlaygroundResources.formComingSoon)}</Body1>;
        }

        const agentToEdit = draftAgent ?? target?.agent;
        if (!agentToEdit) {
            return <Body1>{intl.formatMessage(PlaygroundResources.formComingSoon)}</Body1>;
        }

        const allowAgentNameEdit = !(isAgentTarget && !!target?.agent?.name);

        return (
            <AgentDetailsStep
                key={`agent-form-${formKey}`}
                agent={agentToEdit}
                existingAgents={agents}
                existingTools={tools}
                systemTools={systemTools}
                onChange={handleAgentFormChange}
                intl={intl}
                allowAgentNameEdit={allowAgentNameEdit}
                showMetaAgentOverride={allowAgentNameEdit}
            />
        );
    };

    const renderToolForm = () => {
        if (!supportsToolPreview) {
            return <Body1>{intl.formatMessage(PlaygroundResources.toolFormUnavailable)}</Body1>;
        }

        const handleToolSelection = (_: unknown, data: { optionValue?: string | number | null }) => {
            const value = typeof data.optionValue === 'string' ? data.optionValue : undefined;

            if (!value) {
                return;
            }

            if (value === NEW_KUSTO_TOOL_OPTION) {
                const newTool: Partial<ExtendedTool> = {
                    name: '',
                    type: 'KustoTool',
                    description: '',
                    parameters: [],
                };
                setDraftSystemTool(undefined);
                setDraftTool(newTool);
                setSelectedToolName(NEW_KUSTO_TOOL_OPTION);
                setConfigEntity('tool');
                setAcknowledgedMode(true);
                markPreviewUpdated();
                return;
            }

            const systemToolMatch = systemTools.find(tool => tool.name === value);

            if (systemToolMatch) {
                setDraftTool(undefined);
                setDraftSystemTool({ ...systemToolMatch });
                setSelectedToolName(value);
                setConfigEntity('tool');
                setAcknowledgedMode(true);
                markPreviewUpdated();
                return;
            }

            const extendedToolMatch = tools.find(tool => tool.name === value);
            if (extendedToolMatch) {
                setDraftSystemTool(undefined);
                setDraftTool({ ...extendedToolMatch });
                setSelectedToolName(value);
                setConfigEntity('tool');
                setAcknowledgedMode(true);
                markPreviewUpdated();
                return;
            }

            // If we reach here, keep the selection but do not mutate drafts.
            setSelectedToolName(value);
        };

        const dropdownDisplayValue =
            selectedToolName === NEW_KUSTO_TOOL_OPTION ? intl.formatMessage(PlaygroundResources.toolFormCreateNewKusto) : selectedToolName;

        const hasAnyOptions =
            linkedExtendedToolNames.length > 0 || availableExtendedToolNames.length > 0 || availableSystemToolNames.length > 0;

        return (
            <div className={styles.toolFormContainer}>
                <Field label={intl.formatMessage(PlaygroundResources.toolFormSelectorLabel)} size="small">
                    <Dropdown
                        value={dropdownDisplayValue}
                        selectedOptions={selectedToolName ? [selectedToolName] : []}
                        placeholder={intl.formatMessage(PlaygroundResources.toolFormSelectPrompt)}
                        onOptionSelect={handleToolSelection}
                        disabled={!acknowledgedMode}
                    >
                        <Option value={NEW_KUSTO_TOOL_OPTION} text={intl.formatMessage(PlaygroundResources.toolFormCreateNewKusto)}>
                            {intl.formatMessage(PlaygroundResources.toolFormCreateNewKusto)}
                        </Option>
                        {linkedExtendedToolNames.length > 0 && (
                            <OptionGroup label={intl.formatMessage(PlaygroundResources.toolFormAgentToolsGroup)}>
                                {linkedExtendedToolNames.map(name => (
                                    <Option key={`agent-${name}`} value={name} text={name}>
                                        {name}
                                    </Option>
                                ))}
                            </OptionGroup>
                        )}
                        {availableExtendedToolNames.length > 0 && (
                            <OptionGroup label={intl.formatMessage(PlaygroundResources.toolFormAvailableToolsGroup)}>
                                {availableExtendedToolNames.map(name => (
                                    <Option key={`available-${name}`} value={name} text={name}>
                                        {name}
                                    </Option>
                                ))}
                            </OptionGroup>
                        )}
                        {availableSystemToolNames.length > 0 && (
                            <OptionGroup label={intl.formatMessage(PlaygroundResources.toolFormSystemToolsGroup)}>
                                {availableSystemToolNames.map(name => (
                                    <Option key={`system-${name}`} value={name} text={name}>
                                        {name}
                                    </Option>
                                ))}
                            </OptionGroup>
                        )}
                    </Dropdown>
                </Field>

                {!hasAnyOptions && <Body1 className={styles.infoMessage}>{intl.formatMessage(PlaygroundResources.toolFormNoTools)}</Body1>}

                {selectedToolName && !selectedTool && (
                    <Body1 className={styles.infoMessage}>{intl.formatMessage(PlaygroundResources.toolFormLoading)}</Body1>
                )}

                {selectedToolName === NEW_KUSTO_TOOL_OPTION && draftTool && (
                    <ToolDetailsStep tool={draftTool} existingConnectors={connectors} onChange={handleToolFormChange} intl={intl} />
                )}

                {selectedToolName === NEW_KUSTO_TOOL_OPTION && !draftTool && (
                    <Body1 className={styles.infoMessage}>{intl.formatMessage(PlaygroundResources.toolFormNewToolPrompt)}</Body1>
                )}

                {selectedTool && selectedToolIsSystemTool && selectedToolName !== NEW_KUSTO_TOOL_OPTION && (
                    <>
                        <MessageBar intent="info">
                            <MessageBarBody>
                                {intl.formatMessage(PlaygroundResources.toolFormSystemToolReadOnly, {
                                    name: selectedTool.name,
                                })}
                            </MessageBarBody>
                        </MessageBar>
                        <SystemToolConfigurationPanel tool={selectedTool as SystemTool} />
                    </>
                )}

                {!selectedToolIsSystemTool && selectedExtendedTool && (
                    <ToolDetailsStep
                        tool={selectedExtendedTool}
                        existingConnectors={connectors}
                        onChange={handleToolFormChange}
                        intl={intl}
                    />
                )}
            </div>
        );
    };

    const renderFormPanel = () => {
        if (!target) {
            return <Body1>{intl.formatMessage(PlaygroundResources.noSelectionMessage)}</Body1>;
        }

        return configEntity === 'tool' ? renderToolForm() : renderAgentForm();
    };

    const renderYamlPanel = () => {
        if (configEntity === 'tool' && !selectedTool) {
            return <Body1>{intl.formatMessage(PlaygroundResources.toolFormSelectPrompt)}</Body1>;
        }

        if (configEntity === 'agent' && !supportsYamlEditing) {
            return <Body1>{intl.formatMessage(PlaygroundResources.yamlEditorComingSoon)}</Body1>;
        }

        const isReadOnly = configEntity === 'tool' ? selectedToolIsSystemTool : isSystemToolTarget;

        return (
            <div className={styles.yamlContainer}>
                {yamlError && (
                    <MessageBar intent="error">
                        <MessageBarBody>{yamlError}</MessageBarBody>
                    </MessageBar>
                )}
                {isReadOnly && (
                    <MessageBar intent="info">
                        <MessageBarBody>{intl.formatMessage(PlaygroundResources.yamlReadOnlyNotice)}</MessageBarBody>
                    </MessageBar>
                )}
                <div className={styles.yamlEditor}>
                    <MonacoEditor
                        value={yamlContent}
                        onChange={handleYamlChange}
                        language="yaml"
                        theme={theme.isInverted ? 'vs-dark' : 'vs'}
                        options={{
                            automaticLayout: true,
                            minimap: { enabled: false },
                            scrollBeyondLastLine: false,
                            fontSize: 14,
                            wordWrap: 'on',
                            formatOnType: true,
                            formatOnPaste: true,
                            tabSize: 2,
                            readOnly: isReadOnly,
                        }}
                        height="100%"
                        width="100%"
                    />
                </div>
            </div>
        );
    };

    const renderConfigContent = () => {
        if (!acknowledgedMode) {
            return renderSetupCard();
        }

        if (configTab === 'yaml') {
            return renderYamlPanel();
        }

        return renderFormPanel();
    };

    const renderPlaygroundEmptyState = useCallback(
        ({ sendMessage }: { sendMessage?: (message: string, agentName?: string) => void; forcedAgentName?: string }) => {
            // Store sendMessage in ref for use in useEffect
            if (sendMessage) {
                sendMessageRef.current = sendMessage;
            }

            if (isAutoApplying || isApplyingFindings) {
                return (
                    <div className={styles.chatApplyingState}>
                        <Spinner className={styles.applyingSpinner} />
                        <div className={styles.applyingTitle}>{intl.formatMessage(PlaygroundResources.playgroundApplyingChangesTitle)}</div>
                        <Body1 className={styles.applyingSubtitle}>
                            {intl.formatMessage(PlaygroundResources.playgroundApplyingChangesMessage)}
                        </Body1>
                    </div>
                );
            }

            return (
                <div className={styles.chatEmptyState}>
                    <div className={styles.chatEmptyIcon}>
                        <BeakerFilled />
                    </div>
                    <div className={styles.chatEmptyTitle}>Test & Refine you agent...</div>
                    <Body1 className={styles.chatEmptySubtitle}>
                        {intl.formatMessage(PlaygroundResources.playgroundEmptyStateSubtitle)}
                    </Body1>
                </div>
            );
        },
        [intl, isAutoApplying, isApplyingFindings, styles]
    );

    const renderChatPreview = () => {
        if (!supportsChatPreview) {
            return (
                <div className={styles.placeholder}>
                    <Body1>{intl.formatMessage(PlaygroundResources.agentPreviewUnavailable)}</Body1>
                </div>
            );
        }

        const forcedAgentName = draftAgent?.name || target?.agent?.name || undefined;

        // Create a comprehensive key that includes all agent configuration + chatKey for restart
        const agentConfigKey = JSON.stringify({
            name: draftAgent?.name || target?.agent?.name,
            instructions: draftAgent?.instructions || target?.agent?.instructions,
            tools: draftAgent?.tools || target?.agent?.tools,
            systemTools: draftAgent?.systemTools || target?.agent?.systemTools,
            metadata: draftAgent?.metadata || target?.agent?.metadata,
            timestamp: previewRecentlyUpdated ? Date.now() : 0,
            chatKey: chatKey, // Include chat key to force remount on restart
        });

        // Generate a hash of the config for a shorter key
        const configHashRaw = encodeBase64(agentConfigKey);
        const configHash = (configHashRaw || 'playground-chat').slice(0, 20);

        return (
            <div className={styles.playgroundChatWrapper}>
                <ChatBox
                    key={`playground-chat-${configHash}-${chatKey}`}
                    threadId={undefined}
                    addThread={() => {}}
                    updateThreadLastReadTime={() => {}}
                    threadSource={ThreadSource.playground}
                    stylesProps={playgroundChatStyles}
                    forcedAgentName={forcedAgentName}
                    lockAgentSelection={!!forcedAgentName}
                    onTelemetryUpdate={setChatTelemetry}
                    renderEmptyState={renderPlaygroundEmptyState}
                />
            </div>
        );
    };

    const renderToolPreview = () => {
        if (!supportsToolPreview) {
            return (
                <div className={styles.placeholder}>
                    <Body1>{intl.formatMessage(PlaygroundResources.toolPreviewUnavailable)}</Body1>
                </div>
            );
        }

        if (!selectedTool) {
            return (
                <div className={styles.placeholder}>
                    <Body1>{intl.formatMessage(PlaygroundResources.toolPreviewSelectPlaceholder)}</Body1>
                </div>
            );
        }

        if (selectedToolIsSystemTool) {
            return (
                <div className={styles.toolTesterContainer}>
                    <SystemToolTesterPanel tool={selectedTool as SystemTool} />
                </div>
            );
        }

        if (!selectedToolIsKusto) {
            return (
                <div className={styles.placeholder}>
                    <Body1>{intl.formatMessage(PlaygroundResources.toolPreviewUnsupportedType, { name: selectedTool.name })}</Body1>
                </div>
            );
        }

        return (
            <div className={styles.toolTesterContainer}>
                <KustoQueryTesterPanel
                    tool={selectedExtendedTool as Partial<ExtendedTool>}
                    intl={intl}
                    toolTest={selectedToolTestState}
                    fingerprint={selectedToolFingerprint}
                    onTestStatusChange={(status, options) => handleToolTestStatusChange(selectedToolKey, status, options)}
                />
            </div>
        );
    };

    const renderPreviewContent = () => {
        if (!acknowledgedMode) {
            return (
                <div className={styles.placeholder}>
                    <Body1>{intl.formatMessage(PlaygroundResources.previewRequiresSetup)}</Body1>
                </div>
            );
        }

        if (configEntity === 'tool') {
            return renderToolPreview();
        }

        return renderChatPreview();
    };

    const renderQualityPanel = () => {
        if (!qualityDrawerOpen) {
            return null;
        }

        const hasFindings = !!qualityResult?.findings?.length;

        return (
            <section
                className={styles.watcherPanel}
                role="complementary"
                aria-label={intl.formatMessage(PlaygroundResources.qualityDrawerTitle)}
            >
                <div className={styles.watcherPanelHeader}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXXS, flex: 1 }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalXS }}>
                            <SparkleFilled style={{ fontSize: '16px', color: tokens.colorPaletteYellowForeground1 }} />
                            <Caption1Strong style={{ fontSize: tokens.fontSizeBase300 }}>
                                {intl.formatMessage(PlaygroundResources.qualityPanelTitle)}
                            </Caption1Strong>
                        </div>
                        <Body1 as="p" style={{ color: tokens.colorNeutralForeground3, fontSize: tokens.fontSizeBase100 }}>
                            {qualityStatus === 'running'
                                ? intl.formatMessage(PlaygroundResources.qualityStatusRunning)
                                : qualityStatus === 'fresh' && qualityLastAnalyzed
                                  ? intl.formatMessage(PlaygroundResources.qualityStatusFreshWithTime, {
                                        time: formatRelativeTime(qualityLastAnalyzed),
                                    })
                                  : qualityStatus === 'fresh'
                                    ? intl.formatMessage(PlaygroundResources.qualityStatusFresh)
                                    : qualityStatus === 'stale'
                                      ? intl.formatMessage(PlaygroundResources.qualityStatusStale)
                                      : intl.formatMessage(PlaygroundResources.qualityStatusNotAnalyzed)}
                        </Body1>
                    </div>
                    <Button
                        appearance="subtle"
                        icon={<PanelRightContract20Regular />}
                        size="small"
                        aria-label={intl.formatMessage(PlaygroundResources.collapsePanelAriaLabel)}
                        onClick={() => {
                            setQualityDrawerOpen(false);
                            setViewMode('author-test');
                        }}
                    />
                </div>
                <div className={styles.watcherPanelBody}>
                    {qualityStatus === 'running' && (
                        <div
                            style={{
                                display: 'flex',
                                flexDirection: 'column',
                                gap: tokens.spacingVerticalS,
                                alignItems: 'center',
                                marginTop: tokens.spacingVerticalXL,
                            }}
                        >
                            <Spinner size="tiny" label={intl.formatMessage(PlaygroundResources.qualityDrawerLoadingTitle)} />
                            <Body1 as="p" style={{ color: tokens.colorNeutralForeground3, textAlign: 'center' }}>
                                {intl.formatMessage(PlaygroundResources.qualityDrawerLoadingSubtitle)}
                            </Body1>
                        </div>
                    )}

                    {insightsError && qualityStatus !== 'running' && (
                        <MessageBar intent="error">
                            <MessageBarBody>
                                {insightsError}{' '}
                                <Button appearance="subtle" size="small" onClick={() => handleInsightsRefresh()}>
                                    {intl.formatMessage(PlaygroundResources.insightsRefreshButton)}
                                </Button>
                            </MessageBarBody>
                        </MessageBar>
                    )}

                    {qualityResult && qualityStatus !== 'running' && (
                        <>
                            {/* Overall Score Card */}
                            <div
                                style={{
                                    backgroundColor: tokens.colorBrandBackground2,
                                    borderRadius: tokens.borderRadiusMedium,
                                    padding: `${tokens.spacingVerticalL} ${tokens.spacingHorizontalL}`,
                                    border: `1px solid ${tokens.colorBrandStroke2}`,
                                    textAlign: 'center' as const,
                                }}
                            >
                                <Caption1Strong
                                    style={{
                                        color: tokens.colorNeutralForeground3,
                                        fontSize: tokens.fontSizeBase200,
                                        textTransform: 'uppercase',
                                        letterSpacing: '0.5px',
                                        display: 'block',
                                        marginBottom: tokens.spacingVerticalXS,
                                    }}
                                >
                                    {intl.formatMessage(PlaygroundResources.qualityOverallLabel)}
                                </Caption1Strong>
                                <div
                                    style={{
                                        fontSize: '48px',
                                        fontWeight: tokens.fontWeightBold,
                                        color: tokens.colorBrandForeground1,
                                        lineHeight: '1.2',
                                    }}
                                >
                                    {qualityResult.overallScore}
                                </div>
                                <div
                                    style={{
                                        fontSize: tokens.fontSizeBase200,
                                        color: tokens.colorNeutralForeground2,
                                        marginTop: tokens.spacingVerticalXXS,
                                    }}
                                >
                                    {qualityResult.evidence}
                                </div>
                            </div>

                            {/* Intent Match Score */}
                            {qualityResult.subScores.find(s => s.id === 'intentMatch') &&
                                (() => {
                                    const rawScore = qualityResult.subScores.find(s => s.id === 'intentMatch')?.score || 0;
                                    // Normalize: if score is > 5, assume it's on 0-100 scale and convert to 1-5
                                    const intentMatchScore = rawScore > 5 ? Math.max(1, Math.min(5, Math.round(rawScore / 20))) : rawScore;
                                    const getIntentMatchColor = (score: number) => {
                                        if (score <= 2) return tokens.colorPaletteRedForeground1;
                                        if (score === 3) return tokens.colorPaletteYellowForeground2;
                                        return tokens.colorPaletteGreenForeground1;
                                    };

                                    return (
                                        <div
                                            style={{
                                                display: 'flex',
                                                justifyContent: 'space-between',
                                                alignItems: 'center',
                                                backgroundColor: tokens.colorNeutralBackground2,
                                                borderRadius: tokens.borderRadiusSmall,
                                                padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
                                            }}
                                        >
                                            <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalXXS }}>
                                                <Caption1Strong
                                                    style={{ color: tokens.colorNeutralForeground2, fontSize: tokens.fontSizeBase200 }}
                                                >
                                                    {intl.formatMessage(PlaygroundResources.qualityIntentLabel)}
                                                </Caption1Strong>
                                                <Tooltip
                                                    content={
                                                        <div style={{ maxWidth: '300px' }}>
                                                            <div style={{ fontWeight: tokens.fontWeightSemibold, marginBottom: '4px' }}>
                                                                {intl.formatMessage(PlaygroundResources.qualityIntentTooltip)}
                                                            </div>
                                                            <div style={{ fontSize: tokens.fontSizeBase200 }}>
                                                                How well does the agent's behavior align with its stated goal?
                                                                <ul style={{ margin: '4px 0', paddingLeft: '16px' }}>
                                                                    <li>
                                                                        <strong>5:</strong> Perfect alignment, all on-task
                                                                    </li>
                                                                    <li>
                                                                        <strong>4:</strong> Strong with minor deviations
                                                                    </li>
                                                                    <li>
                                                                        <strong>3:</strong> Moderate, some mixed focus
                                                                    </li>
                                                                    <li>
                                                                        <strong>2:</strong> Weak, significant misalignment
                                                                    </li>
                                                                    <li>
                                                                        <strong>1:</strong> No alignment with goal
                                                                    </li>
                                                                </ul>
                                                            </div>
                                                        </div>
                                                    }
                                                    relationship="description"
                                                >
                                                    <Info20Regular
                                                        style={{
                                                            color: tokens.colorNeutralForeground3,
                                                            cursor: 'pointer',
                                                            width: '12px',
                                                            height: '12px',
                                                        }}
                                                    />
                                                </Tooltip>
                                            </div>
                                            <div
                                                style={{
                                                    fontSize: tokens.fontSizeBase500,
                                                    fontWeight: tokens.fontWeightBold,
                                                    color: getIntentMatchColor(intentMatchScore),
                                                }}
                                            >
                                                {Math.round(intentMatchScore)}/5
                                            </div>
                                        </div>
                                    );
                                })()}

                            {/* Subscores */}
                            <div className={styles.watcherScoresRow}>
                                <ul className={styles.watcherSubscoreList}>
                                    {qualityResult.subScores
                                        .filter(s => s.id !== 'intentMatch' && s.id !== 'actionability')
                                        .map(sub => {
                                            const getScoreDescription = (id: string) => {
                                                switch (id) {
                                                    case 'completeness':
                                                        return 'Is the prompt complete with clear role, goal, and operational guidance? Higher scores indicate well-structured, comprehensive prompts.';
                                                    case 'toolFit':
                                                        return 'Are the right tools linked? Does the agent have all necessary capabilities? Higher scores mean fewer missing tools.';
                                                    case 'promptClarity':
                                                        return 'Is the prompt clear, specific, and actionable for the LLM? Higher scores indicate better instruction quality.';
                                                    case 'safety':
                                                        return 'Does it include error handling, confirmation prompts, and safety checks? Higher scores mean better safeguards.';
                                                    default:
                                                        return 'Quality score for this dimension (0-100)';
                                                }
                                            };

                                            return (
                                                <li key={sub.id} className={styles.watcherSubscoreItem}>
                                                    <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                                                        <Caption1Strong className={styles.watcherSubscoreLabel}>{sub.label}</Caption1Strong>
                                                        <Tooltip
                                                            content={<div style={{ maxWidth: '250px' }}>{getScoreDescription(sub.id)}</div>}
                                                            relationship="description"
                                                        >
                                                            <Info20Regular
                                                                style={{
                                                                    color: tokens.colorNeutralForeground3,
                                                                    cursor: 'pointer',
                                                                    width: '12px',
                                                                    height: '12px',
                                                                }}
                                                            />
                                                        </Tooltip>
                                                    </div>
                                                    <Body1
                                                        as="p"
                                                        style={{
                                                            fontSize: tokens.fontSizeBase500,
                                                            fontWeight: tokens.fontWeightBold,
                                                            margin: 0,
                                                        }}
                                                    >
                                                        {sub.score}
                                                    </Body1>
                                                    <Body1
                                                        as="p"
                                                        style={{
                                                            fontSize: tokens.fontSizeBase100,
                                                            color: tokens.colorNeutralForeground3,
                                                            margin: 0,
                                                        }}
                                                    >
                                                        {sub.evidence}
                                                    </Body1>
                                                </li>
                                            );
                                        })}
                                </ul>
                            </div>

                            {!!insightSections.length && (
                                <div style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM }}>
                                    <Caption1Strong>{intl.formatMessage(PlaygroundResources.qualityDrawerHighlightsTitle)}</Caption1Strong>
                                    {insightSections.map(section => (
                                        <div
                                            key={section.key}
                                            style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS }}
                                        >
                                            <Caption1Strong style={{ color: tokens.colorNeutralForeground2 }}>
                                                {section.title}
                                            </Caption1Strong>
                                            <ul
                                                style={{
                                                    margin: 0,
                                                    paddingLeft: '16px',
                                                    display: 'flex',
                                                    flexDirection: 'column',
                                                    gap: tokens.spacingVerticalS,
                                                }}
                                            >
                                                {section.items.map((item, index) => (
                                                    <li key={`${section.key}-${index}`}>
                                                        <Body1 as="p" style={{ color: tokens.colorNeutralForeground2, margin: 0 }}>
                                                            {item}
                                                        </Body1>
                                                    </li>
                                                ))}
                                            </ul>
                                        </div>
                                    ))}
                                </div>
                            )}

                            <div style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS }}>
                                <div
                                    style={{
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'space-between',
                                        gap: tokens.spacingHorizontalS,
                                    }}
                                >
                                    <Caption1Strong>{intl.formatMessage(PlaygroundResources.qualityDrawerQuickFixesTitle)}</Caption1Strong>
                                    {hasFindings && (
                                        <Checkbox
                                            label="Select All"
                                            checked={qualityResult.findings.every(f => qualitySelection.includes(f.id))}
                                            onChange={handleToggleSelectAll}
                                            disabled={isApplyingFindings}
                                        />
                                    )}
                                </div>

                                {/* Apply Selected Button */}
                                {hasFindings && selectedFindings.length > 0 && (
                                    <Button
                                        appearance="primary"
                                        size="medium"
                                        disabled={isApplyingFindings}
                                        onClick={handleApplySelectedFindings}
                                        style={{ width: '100%' }}
                                    >
                                        {isApplyingFindings
                                            ? `Applying ${selectedFindings.length} fix${selectedFindings.length === 1 ? '' : 'es'}...`
                                            : `Apply ${selectedFindings.length} Selected Fix${selectedFindings.length === 1 ? '' : 'es'}`}
                                    </Button>
                                )}

                                {hasFindings ? (
                                    <ul className={styles.watcherFindingsList}>
                                        {qualityResult.findings.map(finding => {
                                            const previewExpanded = !!qualityExpandedPreviews[finding.id];
                                            const isSelected = qualitySelection.includes(finding.id);

                                            return (
                                                <li
                                                    key={finding.id}
                                                    className={mergeClasses(
                                                        styles.watcherFindingItem,
                                                        isSelected && styles.watcherFindingItemSelected
                                                    )}
                                                >
                                                    <div className={styles.watcherFindingHeader}>
                                                        <Checkbox
                                                            checked={isSelected}
                                                            onChange={() => handleToggleFindingSelection(finding.id)}
                                                            disabled={isApplyingFindings}
                                                            style={{ flexShrink: 0 }}
                                                        />
                                                        <div
                                                            style={{
                                                                display: 'flex',
                                                                flexDirection: 'column',
                                                                gap: tokens.spacingVerticalXXS,
                                                                flex: 1,
                                                            }}
                                                        >
                                                            <div
                                                                style={{
                                                                    display: 'flex',
                                                                    alignItems: 'center',
                                                                    gap: tokens.spacingHorizontalS,
                                                                }}
                                                            >
                                                                <span className={styles.watcherFindingTitle}>{finding.title}</span>
                                                                <Badge
                                                                    appearance="filled"
                                                                    color={
                                                                        finding.expectedLift >= 15
                                                                            ? 'danger'
                                                                            : finding.expectedLift >= 8
                                                                              ? 'warning'
                                                                              : 'informative'
                                                                    }
                                                                    size="small"
                                                                />
                                                            </div>
                                                            <span className={styles.watcherFindingRationale}>{finding.rationale}</span>
                                                        </div>
                                                    </div>
                                                    {finding.toolHint && <div className={styles.watcherHint}>{finding.toolHint}</div>}
                                                    {finding.safetyNote && <div className={styles.watcherHint}>{finding.safetyNote}</div>}
                                                    <div className={styles.watcherFindingActions}>
                                                        {finding.shortDiff && (
                                                            <Button
                                                                appearance="secondary"
                                                                size="small"
                                                                onClick={() => handleToggleFindingPreview(finding.id)}
                                                            >
                                                                {previewExpanded
                                                                    ? intl.formatMessage(PlaygroundResources.qualityDrawerPreviewHide)
                                                                    : intl.formatMessage(PlaygroundResources.qualityDrawerPreviewShow)}
                                                            </Button>
                                                        )}
                                                    </div>
                                                    {previewExpanded && finding.shortDiff && (
                                                        <pre className={styles.watcherFindingPreview}>
                                                            {renderColoredDiff(finding.shortDiff, styles)}
                                                        </pre>
                                                    )}
                                                </li>
                                            );
                                        })}
                                    </ul>
                                ) : (
                                    <div className={styles.watcherHint}>
                                        {intl.formatMessage(PlaygroundResources.qualityDrawerNoFindings)}
                                    </div>
                                )}
                            </div>
                        </>
                    )}

                    {!qualityResult && qualityStatus !== 'running' && !insightsError && (
                        <div className={styles.watcherHint}>{intl.formatMessage(PlaygroundResources.qualityDrawerEmpty)}</div>
                    )}
                </div>
                <div className={styles.watcherPanelFooter}>
                    {qualityLastAnalyzed
                        ? intl.formatMessage(PlaygroundResources.qualityDrawerUpdated, {
                              time: formatRelativeTime(qualityLastAnalyzed),
                          })
                        : intl.formatMessage(PlaygroundResources.qualityDrawerUpdatedNever)}
                </div>
            </section>
        );
    };

    const renderQualityRibbon = () => {
        if (!qualityResult) {
            return null;
        }

        const hasSelection = selectedFindings.length > 0;
        const ribbonClasses = mergeClasses(styles.watcherRibbon, hasSelection && styles.watcherRibbonVisible);
        const projectedScore = clampScore(qualityResult.overallScore + projectedLift);

        return (
            <div className={ribbonClasses} aria-hidden={!hasSelection}>
                <div className={styles.watcherRibbonSummary}>
                    <Caption1Strong>
                        {intl.formatMessage(PlaygroundResources.qualityRibbonSelection, {
                            count: selectedFindings.length,
                        })}
                    </Caption1Strong>
                    <Body1 as="p" style={{ color: tokens.colorNeutralForeground3 }}>
                        {intl.formatMessage(PlaygroundResources.qualityRibbonProjected, {
                            lift: projectedLift,
                        })}
                        {' · '}
                        {intl.formatMessage(PlaygroundResources.qualityRibbonProjectedScore, {
                            score: projectedScore,
                        })}
                    </Body1>
                </div>
                <Button
                    appearance="subtle"
                    size="small"
                    onClick={() => setQualitySelection([])}
                    disabled={isApplyingFindings || !hasSelection}
                >
                    {intl.formatMessage(PlaygroundResources.qualityRibbonClearButton)}
                </Button>
                <Button
                    appearance="primary"
                    size="small"
                    onClick={handleApplySelectedFindings}
                    disabled={isApplyingFindings || !hasSelection}
                >
                    {isApplyingFindings
                        ? intl.formatMessage(PlaygroundResources.qualityRibbonApplyingLabel)
                        : intl.formatMessage(PlaygroundResources.qualityRibbonApplyButton)}
                </Button>
            </div>
        );
    };

    return (
        <>
            {renderQualityRibbon()}
            <Toaster toasterId={toasterId} position="top-end" />
            <Dialog open={open} onOpenChange={(_, data) => !data.open && onDismiss()}>
                <DialogSurface className={styles.surface}>
                    <DialogBody className={styles.body}>
                        <DialogTitle as="div" className={styles.header}>
                            <div className={styles.headerCopy}>
                                {/* Icon + Title */}
                                <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                                    <div
                                        style={{
                                            width: '32px',
                                            height: '32px',
                                            borderRadius: '8px',
                                            backgroundColor: tokens.colorBrandBackground,
                                            display: 'flex',
                                            alignItems: 'center',
                                            justifyContent: 'center',
                                            color: 'white',
                                            flexShrink: 0,
                                        }}
                                    >
                                        <ChartMultipleFilled />
                                    </div>
                                    <div
                                        style={{
                                            fontSize: tokens.fontSizeBase300,
                                            fontWeight: tokens.fontWeightSemibold,
                                            color: tokens.colorNeutralForeground2,
                                        }}
                                    >
                                        {intl.formatMessage(PlaygroundResources.headerTitle)}
                                    </div>
                                </div>

                                {/* View Switcher */}
                                <div className={styles.viewSwitcher}>
                                    <Tooltip content={intl.formatMessage(PlaygroundResources.viewTesterTooltip)} relationship="description">
                                        <button
                                            className={mergeClasses(
                                                styles.viewSwitcherButton,
                                                viewMode === 'tester' && styles.viewSwitcherButtonActive
                                            )}
                                            onClick={() => setViewMode('tester')}
                                            aria-label={intl.formatMessage(PlaygroundResources.viewTesterAriaLabel)}
                                        >
                                            <Square20Regular className={styles.viewSwitcherIcon} />
                                        </button>
                                    </Tooltip>
                                    <Tooltip
                                        content={intl.formatMessage(PlaygroundResources.viewAuthorTestTooltip)}
                                        relationship="description"
                                    >
                                        <button
                                            className={mergeClasses(
                                                styles.viewSwitcherButton,
                                                viewMode === 'author-test' && styles.viewSwitcherButtonActive
                                            )}
                                            onClick={() => setViewMode('author-test')}
                                            aria-label={intl.formatMessage(PlaygroundResources.viewAuthorTestAriaLabel)}
                                        >
                                            <PanelLeft20Regular className={styles.viewSwitcherIcon} />
                                        </button>
                                    </Tooltip>
                                    <Tooltip
                                        content={intl.formatMessage(PlaygroundResources.viewEvaluateTooltip)}
                                        relationship="description"
                                    >
                                        <button
                                            className={mergeClasses(
                                                styles.viewSwitcherButton,
                                                viewMode === 'author-test-evaluate' && styles.viewSwitcherButtonActive
                                            )}
                                            onClick={() => setViewMode('author-test-evaluate')}
                                            aria-label={intl.formatMessage(PlaygroundResources.viewEvaluateAriaLabel)}
                                            style={{ position: 'relative' }}
                                        >
                                            <PanelLeftExpand20Regular className={styles.viewSwitcherIcon} />
                                            {viewMode !== 'author-test-evaluate' && (
                                                <Badge
                                                    appearance="filled"
                                                    color="important"
                                                    size="tiny"
                                                    style={{
                                                        position: 'absolute',
                                                        top: '2px',
                                                        right: '2px',
                                                        fontSize: '8px',
                                                        padding: '1px 3px',
                                                    }}
                                                >
                                                    {intl.formatMessage(SreAgentResources.new)}
                                                </Badge>
                                            )}
                                        </button>
                                    </Tooltip>
                                </div>

                                {/* Quality Score Badge - Only in Evaluate mode */}
                                {viewMode === 'author-test-evaluate' && qualityStatus !== 'notAnalyzed' && (
                                    <div className={styles.qualityBadgeCompact}>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalXS }}>
                                            <div
                                                style={{
                                                    fontSize: tokens.fontSizeBase400,
                                                    fontWeight: tokens.fontWeightSemibold,
                                                    color:
                                                        qualityStatusDescriptor.color === 'success'
                                                            ? tokens.colorPaletteGreenForeground1
                                                            : qualityStatusDescriptor.color === 'warning'
                                                              ? tokens.colorPaletteYellowForeground2
                                                              : tokens.colorNeutralForeground2,
                                                }}
                                            >
                                                {qualityScore}
                                            </div>
                                            {findingsCount > 0 && (
                                                <Badge appearance="tint" color="informative" size="small">
                                                    {findingsCount} {findingsCount === 1 ? 'suggestion' : 'suggestions'}
                                                </Badge>
                                            )}
                                        </div>
                                    </div>
                                )}
                            </div>

                            {/* Right Actions */}
                            <div className={styles.headerActions}>
                                {/* Quality Actions - Shown in Author & Test and Author Test & Evaluate modes */}
                                {(viewMode === 'author-test' || viewMode === 'author-test-evaluate') && (
                                    <>
                                        {viewMode === 'author-test-evaluate' && (
                                            <Tooltip
                                                content={intl.formatMessage(PlaygroundResources.qualityRunTooltip)}
                                                relationship="label"
                                            >
                                                <Button
                                                    appearance="primary"
                                                    size="small"
                                                    disabled={
                                                        qualityStatus === 'running' ||
                                                        insightsLoading ||
                                                        !acknowledgedMode ||
                                                        configEntity !== 'agent'
                                                    }
                                                    onClick={() => handleInsightsRefresh()}
                                                    icon={<SparkleFilled style={{ color: '#FFB900' }} />}
                                                >
                                                    Evaluate
                                                </Button>
                                            </Tooltip>
                                        )}
                                        <Tooltip
                                            content={
                                                autoApplyEnabled
                                                    ? intl.formatMessage(PlaygroundResources.autoApplyEnabledTooltip)
                                                    : !hasPendingChanges
                                                      ? intl.formatMessage(PlaygroundResources.noPendingChangesTooltip)
                                                      : intl.formatMessage(PlaygroundResources.applyChangesTooltip)
                                            }
                                            relationship="label"
                                        >
                                            <Button
                                                appearance="secondary"
                                                size="small"
                                                disabled={!hasPendingChanges || isAutoApplying || autoApplyEnabled}
                                                onClick={handleCommitChanges}
                                            >
                                                {intl.formatMessage(SreAgentResources.apply)}
                                            </Button>
                                        </Tooltip>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalXS }}>
                                            <Tooltip
                                                content={
                                                    autoApplyEnabled
                                                        ? intl.formatMessage(PlaygroundResources.autoApplyEnabledLabel)
                                                        : intl.formatMessage(PlaygroundResources.autoApplyDisabledLabel)
                                                }
                                                relationship="label"
                                            >
                                                <Switch
                                                    checked={autoApplyEnabled}
                                                    disabled={!acknowledgedMode || configEntity !== 'agent'}
                                                    onChange={(_, data) => setAutoApplyEnabled(!!data.checked)}
                                                />
                                            </Tooltip>
                                            <Body1
                                                as="span"
                                                style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground2 }}
                                            >
                                                {intl.formatMessage(PlaygroundResources.qualityAutoApplyLabel)}
                                            </Body1>
                                        </div>
                                    </>
                                )}

                                {/* More Menu */}
                                <Menu>
                                    <MenuTrigger>
                                        <Button
                                            appearance="subtle"
                                            icon={<MoreHorizontal20Regular />}
                                            size="small"
                                            aria-label={intl.formatMessage(PlaygroundResources.moreActionsAriaLabel)}
                                        />
                                    </MenuTrigger>
                                    <MenuPopover>
                                        <MenuList>
                                            <MenuItem
                                                onClick={() => {
                                                    setChatKey(prev => prev + 1);
                                                    setChatInitialized(false);
                                                }}
                                            >
                                                Restart chat
                                            </MenuItem>
                                            {viewMode === 'author-test-evaluate' && (
                                                <MenuItem onClick={handleExportAnalysis} disabled={!qualityResult && !insights}>
                                                    {intl.formatMessage(PlaygroundResources.exportAnalysisLabel)}
                                                </MenuItem>
                                            )}
                                            <MenuDivider />
                                            <MenuItem onClick={handleCommitChanges} disabled={!hasPendingChanges || isAutoApplying}>
                                                Apply changes & restart
                                            </MenuItem>
                                            {viewMode === 'author-test-evaluate' && (
                                                <MenuItem onClick={() => setAutoApplyEnabled(prev => !prev)}>
                                                    {autoApplyEnabled ? 'Disable auto-apply' : 'Enable auto-apply'}
                                                </MenuItem>
                                            )}
                                        </MenuList>
                                    </MenuPopover>
                                </Menu>

                                {/* Close Button */}
                                <DialogTrigger action="close">
                                    <Button
                                        appearance="subtle"
                                        icon={<Dismiss16Regular />}
                                        size="small"
                                        onClick={onDismiss}
                                        aria-label={intl.formatMessage(PlaygroundResources.closeButton)}
                                    />
                                </DialogTrigger>
                            </div>
                        </DialogTitle>
                        <DialogContent className={styles.body}>
                            {/* Promotional banner for evaluation feature */}
                            {viewMode !== 'author-test-evaluate' && acknowledgedMode && (
                                <MessageBar
                                    intent="info"
                                    style={{
                                        marginBottom: tokens.spacingVerticalS,
                                        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalM}`,
                                    }}
                                >
                                    <MessageBarBody>
                                        <div
                                            style={{
                                                display: 'flex',
                                                alignItems: 'center',
                                                justifyContent: 'space-between',
                                                width: '100%',
                                            }}
                                        >
                                            <span style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground2 }}>
                                                {intl.formatMessage(PlaygroundResources.evaluationBannerMessage)}
                                            </span>
                                            <Button
                                                appearance="transparent"
                                                size="small"
                                                onClick={() => setViewMode('author-test-evaluate')}
                                                icon={<SparkleFilled />}
                                                style={{ color: tokens.colorBrandForeground1 }}
                                            >
                                                {intl.formatMessage(PlaygroundResources.evaluationBannerCta)}
                                            </Button>
                                        </div>
                                    </MessageBarBody>
                                </MessageBar>
                            )}
                            {insightsError && qualityStatus !== 'running' && (
                                <MessageBar intent="error" className={styles.dirtyBanner}>
                                    <MessageBarBody>
                                        {insightsError}{' '}
                                        <Button appearance="subtle" size="small" onClick={() => handleInsightsRefresh()}>
                                            {intl.formatMessage(PlaygroundResources.insightsRefreshButton)}
                                        </Button>
                                    </MessageBarBody>
                                </MessageBar>
                            )}
                            <div ref={layoutRef} className={styles.layout}>
                                {/* Left Panel - Shown based on view mode */}
                                {/* Tester: hidden, Author & Test: shown, Author Test & Evaluate: shown */}
                                {viewMode !== 'tester' && !focusMode && !leftPanelCollapsed && (
                                    <div className={styles.leftColumn} style={{ flex: rightPanelCollapsed ? 1 : panelRatio }}>
                                        {renderConfigurationSwitcher()}
                                        {acknowledgedMode && (
                                            <div className={styles.editorSwitcher}>
                                                <TabList
                                                    selectedValue={configTab}
                                                    onTabSelect={handleConfigTabChange}
                                                    appearance="subtle"
                                                    size="small"
                                                >
                                                    <Tab id="playground-config-form" value="form">
                                                        {intl.formatMessage(PlaygroundResources.formTabLabel)}
                                                    </Tab>
                                                    <Tab id="playground-config-yaml" value="yaml" disabled={yamlTabDisabled}>
                                                        {intl.formatMessage(PlaygroundResources.yamlTabLabel)}
                                                    </Tab>
                                                </TabList>
                                            </div>
                                        )}
                                        <div className={styles.tabPanel}>{renderConfigContent()}</div>

                                        {/* Floating expand button when right panel is collapsed */}
                                        {rightPanelCollapsed && (
                                            <Button
                                                appearance="subtle"
                                                size="small"
                                                style={{
                                                    position: 'absolute',
                                                    right: '16px',
                                                    top: '50%',
                                                    transform: 'translateY(-50%)',
                                                    width: '32px',
                                                    height: '32px',
                                                    minWidth: '32px',
                                                    borderRadius: '50%',
                                                    backgroundColor: tokens.colorNeutralBackground1,
                                                    border: `1px solid ${tokens.colorNeutralStroke1}`,
                                                    boxShadow: tokens.shadow4,
                                                    zIndex: 10,
                                                }}
                                                icon={<ChevronDown20Regular style={{ transform: 'rotate(-90deg)' }} />}
                                                onClick={() => setRightPanelCollapsed(false)}
                                                title={intl.formatMessage(PlaygroundResources.expandChatPreviewTitle)}
                                            />
                                        )}
                                    </div>
                                )}
                                {/* Divider - Hidden when either panel collapsed or in focus mode or in tester mode */}
                                {viewMode !== 'tester' && !focusMode && !rightPanelCollapsed && !leftPanelCollapsed && (
                                    <div
                                        role="separator"
                                        aria-orientation="vertical"
                                        aria-valuemin={28}
                                        aria-valuemax={72}
                                        aria-valuenow={Math.round(panelRatio * 100)}
                                        aria-label={intl.formatMessage(PlaygroundResources.playgroundResizeHandleLabel)}
                                        className={styles.dividerHandle}
                                        onMouseDown={handleDividerMouseDown}
                                    />
                                )}
                                {/* Right Panel - Chat Preview */}
                                <div
                                    className={`${styles.rightColumn} ${rightPanelCollapsed ? styles.rightColumnCollapsed : ''}`}
                                    style={{
                                        flex:
                                            viewMode === 'tester' || focusMode || leftPanelCollapsed
                                                ? 1
                                                : rightPanelCollapsed
                                                  ? '0 0 40px'
                                                  : 1 - panelRatio,
                                    }}
                                >
                                    {/* Floating expand button when left panel is collapsed */}
                                    {leftPanelCollapsed && !focusMode && viewMode !== 'tester' && (
                                        <Button
                                            appearance="subtle"
                                            size="small"
                                            style={{
                                                position: 'absolute',
                                                left: '16px',
                                                top: '50%',
                                                transform: 'translateY(-50%)',
                                                width: '32px',
                                                height: '32px',
                                                minWidth: '32px',
                                                borderRadius: '50%',
                                                backgroundColor: tokens.colorNeutralBackground1,
                                                border: `1px solid ${tokens.colorNeutralStroke1}`,
                                                boxShadow: tokens.shadow4,
                                                zIndex: 10,
                                            }}
                                            icon={<ChevronDown20Regular style={{ transform: 'rotate(90deg)' }} />}
                                            onClick={() => setLeftPanelCollapsed(false)}
                                            title={intl.formatMessage(PlaygroundResources.expandConfigurationPanelTitle)}
                                        />
                                    )}

                                    {!rightPanelCollapsed && (
                                        <>
                                            {/* Chat Preview Section with Restart Button */}
                                            <div
                                                style={{
                                                    display: 'flex',
                                                    justifyContent: 'space-between',
                                                    alignItems: 'center',
                                                    marginBottom: tokens.spacingVerticalXXS,
                                                }}
                                            >
                                                <Caption1Strong style={{ color: tokens.colorNeutralForeground2, visibility: 'hidden' }}>
                                                    {intl.formatMessage(ActivitiesResources.chatPivotHeader)}
                                                </Caption1Strong>
                                                <Button
                                                    appearance="subtle"
                                                    size="small"
                                                    onClick={() => {
                                                        setChatKey(prev => prev + 1);
                                                        setChatInitialized(false);
                                                    }}
                                                    title={intl.formatMessage(PlaygroundResources.restartChatTitle)}
                                                >
                                                    Restart Chat
                                                </Button>
                                            </div>
                                            <div className={styles.tabPanel}>{renderPreviewContent()}</div>
                                        </>
                                    )}
                                </div>
                                {/* Quality Panel - Only shown in author-test-evaluate mode */}
                                {viewMode === 'author-test-evaluate' && renderQualityPanel()}
                            </div>
                        </DialogContent>
                    </DialogBody>
                </DialogSurface>
            </Dialog>

            {/* Prominent applying changes overlay */}
            {isAutoApplying ? (
                <div className={styles.applyingOverlay}>
                    <div className={styles.applyingCard}>
                        <Spinner size="extra-large" />
                        <h2 className={styles.applyingCardTitle}>
                            {intl.formatMessage(PlaygroundResources.playgroundApplyingChangesTitle)}
                        </h2>
                        <p className={styles.applyingCardMessage}>
                            {intl.formatMessage(PlaygroundResources.playgroundApplyingChangesMessage)}
                        </p>
                        <ProgressBar className={styles.applyingProgress} />
                    </div>
                </div>
            ) : null}
        </>
    );
};

export default PlaygroundModal;

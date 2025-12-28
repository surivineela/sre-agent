import {
    BrainCircuit20Regular,
    Chat20Regular,
    Document20Regular,
    Lightbulb20Regular,
    TextDescription20Regular,
} from '@fluentui/react-icons';
import { FC, useEffect, useMemo, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { ThreadTraceResources } from '../../../../../../../../Strings/SREAgentResources';
import { ISpan } from '../../../../../../packages/components/tracing/src/types/trace';
import { useTracePanelStyles } from '../TracePanel.Styles';
import { ExpandCollapseButton } from './Common/ExpandCollapseButton';

const getDetailsFromSpan = (span: ISpan) => {
    return {
        modelName: span.usage_info?.modelName ?? '-',
        modelTemperature: span.usage_info?.temperature ?? '-',
        inputTokens: span.usage_info?.prompt_tokens ?? '-',
        outputTokens: span.usage_info?.completion_tokens ?? '-',
        totalTokens: span.usage_info?.total_tokens ?? '-',
        input: span.usage_info?.model_input ? JSON.stringify(span.usage_info.model_input, null, 2) : '-',
        output: span.usage_info?.model_output ? JSON.stringify(span.usage_info.model_output, null, 2) : '-',
        systemPrompt: span.usage_info?.systemPrompt ?? undefined, // Complete system prompt
        modelThinking: span.usage_info?.modelThinking ?? undefined, // Model's internal thinking (Responses API)
        reasoning: span.usage_info?.reasoning ?? undefined, // Agent's structured reasoningScratchPad
        response: span.usage_info?.response ?? undefined, // Agent's structured notifyUserMessage
    };
};

export interface ModelGenerationTraceDetailsProps {
    span: ISpan;
}

export const ModelGenerationTraceDetails: FC<ModelGenerationTraceDetailsProps> = ({ span }) => {
    const intl = useIntl();
    const [inputExpanded, setInputExpanded] = useState(false);
    const [systemPromptExpanded, setSystemPromptExpanded] = useState(false);
    const [modelThinkingExpanded, setModelThinkingExpanded] = useState(false);
    const [reasoningExpanded, setReasoningExpanded] = useState(false);
    const [responseExpanded, setResponseExpanded] = useState(false);
    const [outputExpanded, setOutputExpanded] = useState(false);
    const styles = useTracePanelStyles();
    const {
        modelName,
        modelTemperature,
        inputTokens,
        outputTokens,
        totalTokens,
        input,
        output,
        systemPrompt,
        modelThinking,
        reasoning,
        response,
    } = useMemo(() => getDetailsFromSpan(span), [span]);
    useEffect(() => {
        setInputExpanded(false);
        setSystemPromptExpanded(false);
        setModelThinkingExpanded(false);
        setReasoningExpanded(false);
        setResponseExpanded(false);
        setOutputExpanded(false);
    }, [span]);

    // Determine if we have structured reasoning/response or just raw output
    const hasStructuredOutput = modelThinking || reasoning || response;

    return (
        <>
            <div className={styles.rightPaneSection}>
                <div className={styles.rightPaneSectionHeader}>
                    <BrainCircuit20Regular aria-hidden={true} />
                    <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.modelDetails)}</div>
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>{intl.formatMessage(ThreadTraceResources.modelName)}</div>
                        <div className={styles.rightPaneSubsectionBodyExpanded}>{modelName}</div>
                    </div>
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>{intl.formatMessage(ThreadTraceResources.modelTemperature)}</div>
                        <div className={styles.rightPaneSubsectionBodyExpanded}>{modelTemperature}</div>
                    </div>
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>{intl.formatMessage(ThreadTraceResources.tokenUsage)}</div>
                        <div className={styles.rightPaneSubsectionBodyExpanded}>
                            {intl.formatMessage(ThreadTraceResources.tokenUsageDetails, {
                                input: inputTokens,
                                output: outputTokens,
                                total: totalTokens,
                            })}
                        </div>
                    </div>
                </div>
            </div>
            <div className={styles.rightPaneSection}>
                <div className={styles.rightPaneSectionHeader}>
                    <Chat20Regular aria-hidden={true} />
                    <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.input)}</div>
                    <ExpandCollapseButton isExpanded={inputExpanded} setIsExpanded={setInputExpanded} />
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <pre className={inputExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody}>
                            {input}
                        </pre>
                    </div>
                </div>
            </div>
            {systemPrompt && (
                <div className={styles.rightPaneSection}>
                    <div className={styles.rightPaneSectionHeader}>
                        <Document20Regular aria-hidden={true} />
                        <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.systemPrompt)}</div>
                        <ExpandCollapseButton isExpanded={systemPromptExpanded} setIsExpanded={setSystemPromptExpanded} />
                    </div>

                    <div className={styles.rightPaneSubsectionsContainer}>
                        <div className={styles.rightPaneSubsection}>
                            <pre className={systemPromptExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody}>
                                {systemPrompt}
                            </pre>
                        </div>
                    </div>
                </div>
            )}
            {hasStructuredOutput ? (
                <>
                    {modelThinking && (
                        <div className={styles.rightPaneSection}>
                            <div className={styles.rightPaneSectionHeader}>
                                <BrainCircuit20Regular aria-hidden={true} />
                                <div className={styles.rightPaneSectionHeaderText}>
                                    {intl.formatMessage(ThreadTraceResources.modelThinking)}
                                </div>
                                <ExpandCollapseButton isExpanded={modelThinkingExpanded} setIsExpanded={setModelThinkingExpanded} />
                            </div>

                            <div className={styles.rightPaneSubsectionsContainer}>
                                <div className={styles.rightPaneSubsection}>
                                    <pre
                                        className={
                                            modelThinkingExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody
                                        }
                                    >
                                        {modelThinking}
                                    </pre>
                                </div>
                            </div>
                        </div>
                    )}
                    {reasoning && (
                        <div className={styles.rightPaneSection}>
                            <div className={styles.rightPaneSectionHeader}>
                                <Lightbulb20Regular aria-hidden={true} />
                                <div className={styles.rightPaneSectionHeaderText}>
                                    {intl.formatMessage(ThreadTraceResources.reasoning)}
                                </div>
                                <ExpandCollapseButton isExpanded={reasoningExpanded} setIsExpanded={setReasoningExpanded} />
                            </div>

                            <div className={styles.rightPaneSubsectionsContainer}>
                                <div className={styles.rightPaneSubsection}>
                                    <pre
                                        className={
                                            reasoningExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody
                                        }
                                    >
                                        {reasoning}
                                    </pre>
                                </div>
                            </div>
                        </div>
                    )}
                    {response && (
                        <div className={styles.rightPaneSection}>
                            <div className={styles.rightPaneSectionHeader}>
                                <TextDescription20Regular aria-hidden={true} />
                                <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.response)}</div>
                                <ExpandCollapseButton isExpanded={responseExpanded} setIsExpanded={setResponseExpanded} />
                            </div>

                            <div className={styles.rightPaneSubsectionsContainer}>
                                <div className={styles.rightPaneSubsection}>
                                    <pre
                                        className={
                                            responseExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody
                                        }
                                    >
                                        {response}
                                    </pre>
                                </div>
                            </div>
                        </div>
                    )}
                </>
            ) : (
                <div className={styles.rightPaneSection}>
                    <div className={styles.rightPaneSectionHeader}>
                        <Chat20Regular aria-hidden={true} />
                        <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.output)}</div>
                        <ExpandCollapseButton isExpanded={outputExpanded} setIsExpanded={setOutputExpanded} />
                    </div>

                    <div className={styles.rightPaneSubsectionsContainer}>
                        <div className={styles.rightPaneSubsection}>
                            <pre className={outputExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody}>
                                {output}
                            </pre>
                        </div>
                    </div>
                </div>
            )}
        </>
    );
};

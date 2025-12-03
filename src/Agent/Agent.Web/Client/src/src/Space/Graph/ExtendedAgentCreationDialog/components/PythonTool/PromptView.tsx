import { Badge, Button, MessageBar, MessageBarBody, Spinner, Text, Textarea, tokens, Tooltip } from '@fluentui/react-components';
import { Sparkle24Filled } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../../Strings/SREAgentResources';
import { usePromptViewStyles } from './styles';
import { EXAMPLE_PROMPTS, PromptViewProps } from './types';

export const PromptView: FC<PromptViewProps> = ({
    prompt,
    onPromptChange,
    isGenerating,
    generateError,
    onGenerate,
    onSwitchToCode,
    hasExistingCode,
    iterationContext,
}) => {
    const styles = usePromptViewStyles();
    const intl = useIntl();

    return (
        <div className={styles.container}>
            {/* Header */}
            <div className={styles.header}>
                <div className={styles.iconContainer}>
                    <Sparkle24Filled style={{ color: 'white', fontSize: 28 }} />
                </div>
                <Text size={600} weight="semibold" block>
                    {hasExistingCode ? 'Improve Your Function' : 'Create a Python Function'}
                </Text>
                <Text size={300} style={{ color: tokens.colorNeutralForeground3, marginTop: tokens.spacingVerticalXS }}>
                    {hasExistingCode
                        ? 'Describe what you want to change or fix'
                        : 'Describe what you want the function to do in plain English'}
                </Text>
            </div>

            {/* Iteration Context Banner */}
            {iterationContext && (
                <MessageBar intent="warning" className={styles.contextBanner}>
                    <MessageBarBody>
                        <Text weight="semibold">{intl.formatMessage(ExtendedAgentsGraphResources.pythonToolPreviousTestFailed)} </Text>
                        <Text>{iterationContext.substring(0, 150)}...</Text>
                    </MessageBarBody>
                </MessageBar>
            )}

            {/* Prompt Area */}
            <div className={styles.promptArea}>
                <Textarea
                    value={prompt}
                    onChange={(_, data) => onPromptChange(data.value)}
                    placeholder={
                        iterationContext
                            ? 'Describe how to fix the error, or just click Generate to auto-fix...'
                            : 'e.g., "Check if a website is online and return response time and status code"'
                    }
                    className={styles.textarea}
                    resize="none"
                    disabled={isGenerating}
                />

                {/* Example chips - only show when no iteration context and no existing code */}
                {!iterationContext && !hasExistingCode && (
                    <div className={styles.examples}>
                        <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                            Try:
                        </Text>
                        {EXAMPLE_PROMPTS.map(ex => (
                            <Badge
                                key={ex.label}
                                appearance="outline"
                                className={styles.exampleBadge}
                                onClick={() => onPromptChange(ex.prompt)}
                            >
                                {ex.label}
                            </Badge>
                        ))}
                    </div>
                )}
            </div>

            {/* Generate Error */}
            {generateError && (
                <MessageBar intent="error" style={{ marginBottom: tokens.spacingVerticalM }}>
                    <MessageBarBody>{generateError}</MessageBarBody>
                </MessageBar>
            )}

            {/* Actions */}
            <div className={styles.actions}>
                <Tooltip content="Ctrl/Cmd + Enter" relationship="label">
                    <Button
                        appearance="primary"
                        size="large"
                        icon={isGenerating ? <Spinner size="tiny" /> : <Sparkle24Filled />}
                        onClick={onGenerate}
                        disabled={!prompt.trim() || isGenerating}
                    >
                        {isGenerating
                            ? 'Generating...'
                            : iterationContext
                              ? 'Fix & Regenerate'
                              : hasExistingCode
                                ? 'Regenerate'
                                : 'Generate'}
                    </Button>
                </Tooltip>
                {hasExistingCode && (
                    <Button appearance="subtle" size="large" onClick={onSwitchToCode}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.pythonToolKeepCurrentCode)}
                    </Button>
                )}
            </div>
        </div>
    );
};

import { Button, Label, MessageBar, MessageBarBody, Textarea } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useCallback, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../../Common/Clients/ExtendedAgentClient';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { sanitizeEntityName } from '../ExtendedAgentCreationDialog/utils/nameValidation';
import { usePythonToolDialogStyles } from './PythonToolDialog.Styles';
import { PythonToolFormProps } from './PythonToolUtilities';

interface PythonToolAssistantPanelProps {
    isGenerating: boolean;
    setIsGenerating: (isGenerating: boolean) => void;
    setHasSuccessRunTest: (hasSuccess: boolean) => void;
    onGenerationComplete: () => void;
    prompt: string;
    setPrompt: (prompt: string) => void;
}

export const PythonToolAssistantPanel: FC<PythonToolAssistantPanelProps> = ({
    isGenerating,
    setIsGenerating,
    setHasSuccessRunTest,
    onGenerationComplete,
    prompt,
    setPrompt,
}) => {
    const intl = useIntl();
    const styles = usePythonToolDialogStyles();
    const { values, setValues } = useFormikContext<PythonToolFormProps>();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [generateError, setGenerateError] = useState<string | null>(null);

    const handleGenerate = useCallback(
        async (promptOverride?: string) => {
            const promptToUse = promptOverride ?? prompt;
            if (!promptToUse.trim()) return;

            setIsGenerating(true);
            setGenerateError(null);

            const client = ExtendedAgentClient.getInstance(sreAgentEndpoint);
            const response = await client.generatePythonTool({
                intent: promptToUse,
                suggestedName: values.name || undefined,
                timeoutSeconds: values.timeoutSeconds || 120,
                existingCode: values.functionCode || undefined,
            });

            if (!response.isSuccessful) {
                setGenerateError(typeof response.error === 'string' ? response.error : 'Failed to generate');
                setIsGenerating(false);
                return;
            }

            const result = response.content;
            if (!result?.success) {
                setGenerateError(result?.errorMessage || 'Generation failed');
                setIsGenerating(false);
                return;
            }

            const functionCode = result.function_code || result.functionCode || '';
            const timeoutSeconds = result.timeout_seconds || result.timeoutSeconds || 120;

            if (!functionCode) {
                setGenerateError('Generated function code is empty');
                setIsGenerating(false);
                return;
            }

            setValues(
                {
                    ...values,
                    name: !values.name && result.name ? sanitizeEntityName(result.name) : values.name,
                    description: result.description || values.description,
                    functionCode,
                    parameters: result.parameters || [],
                    timeoutSeconds,
                },
                true
            );

            setPrompt('');
            setHasSuccessRunTest(false);
            setIsGenerating(false);

            // Notify parent that generation is complete (to switch to code tab)
            onGenerationComplete();
        },
        [prompt, sreAgentEndpoint, values, setValues, setIsGenerating, setHasSuccessRunTest, setPrompt, onGenerationComplete]
    );

    return (
        <div className={styles.assistantPanel}>
            <div className={styles.assistantContent}>
                <div className={styles.promptArea}>
                    <Label>{intl.formatMessage(SreAgentResources.pythonToolCreatorPromptLabel)}</Label>
                    <Textarea
                        value={prompt}
                        onChange={(_, data) => setPrompt(data.value)}
                        placeholder={intl.formatMessage(SreAgentResources.pythonToolBuilderIntentPlaceholder)}
                        className={styles.promptTextarea}
                        resize="vertical"
                        disabled={isGenerating}
                    />
                    <Button appearance="primary" disabled={!prompt.trim() || isGenerating} onClick={() => handleGenerate()}>
                        {isGenerating
                            ? intl.formatMessage(SreAgentResources.pythonToolCreatorGeneratingButton)
                            : intl.formatMessage(SreAgentResources.pythonToolCreatorGenerateButton)}
                    </Button>
                </div>

                {generateError && (
                    <MessageBar intent="error">
                        <MessageBarBody>
                            {intl.formatMessage(SreAgentResources.pythonToolCreatorGenerateError, { message: generateError })}
                        </MessageBarBody>
                    </MessageBar>
                )}
            </div>
        </div>
    );
};

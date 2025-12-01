import { Button, InputOnChangeData, makeStyles, Spinner, TextareaProps, tokens, Tooltip } from '@fluentui/react-components';
import { ArrowUndo16Regular, PenSparkle16Regular } from '@fluentui/react-icons';
import { FC, useCallback, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { FieldWrapperProps } from '../../Common/Components/Field/FieldWrapper';
import TextareaNoFormik from '../../Common/Components/Textarea/TextareaNoFormik';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { improvePrompt } from '../Graph/ExtendedAgentCreationDialog/services/promptImprovementService';

type AgentPromptTextareaProps = TextareaProps &
    Omit<FieldWrapperProps, 'children'> & {
        label: string;
        prompt?: string;
        setPrompt: (prompt: string) => void;
        required?: boolean;
    };

export const AgentPromptTextarea: FC<AgentPromptTextareaProps> = ({ label, prompt, setPrompt, required, ...props }) => {
    const intl = useIntl();
    const styles = useStyles();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const [previousPrompt, setPreviousPrompt] = useState<string>();
    const [isApplyingImprovement, setIsApplyingImprovement] = useState<boolean>(false);

    const onPromptChange = useCallback(
        (_: any, data: InputOnChangeData) => {
            setPrompt(data.value ?? '');
        },
        [setPrompt]
    );

    const onClickRefineWithAI = useCallback(async () => {
        if (prompt) {
            setPreviousPrompt(prompt);
            setIsApplyingImprovement(true);
            try {
                const result = await improvePrompt(sreAgentEndpoint, prompt);
                setPrompt(result.improvedPrompt ?? '');
            } catch (error) {
                console.log('Failed to apply AI improvements:', error);
            } finally {
                setIsApplyingImprovement(false);
            }
        }
    }, [prompt, setPrompt, sreAgentEndpoint]);

    const onClickUndo = useCallback(() => {
        setPrompt(previousPrompt ?? '');
        setPreviousPrompt('');
    }, [previousPrompt, setPrompt]);

    return (
        <TextareaNoFormik
            label={
                <div className={styles.fieldLabelRow}>
                    <span>
                        {label}
                        {required && (
                            <span className={styles.fieldRequiredStar} aria-hidden="true">
                                {' '}
                                *
                            </span>
                        )}
                    </span>
                    <div className={styles.fieldActionGroup}>
                        <Button
                            appearance="subtle"
                            size="small"
                            disabled={isApplyingImprovement || !previousPrompt}
                            onClick={onClickUndo}
                            className={styles.promptImprovementButton}
                        >
                            <>
                                <ArrowUndo16Regular />
                                {intl.formatMessage(SreAgentResources.undo)}
                            </>
                        </Button>
                        <Tooltip content={intl.formatMessage(SreAgentResources.refineWithAiTooltip)} relationship="description">
                            <Button
                                appearance="subtle"
                                size="small"
                                disabled={!prompt?.trim() || isApplyingImprovement}
                                onClick={onClickRefineWithAI}
                                className={styles.promptImprovementButton}
                            >
                                {isApplyingImprovement ? (
                                    <>
                                        <Spinner size="extra-tiny" />
                                        {intl.formatMessage(SreAgentResources.refining)}
                                    </>
                                ) : (
                                    <>
                                        <PenSparkle16Regular />
                                        {intl.formatMessage(SreAgentResources.refineWithAi)}
                                    </>
                                )}
                            </Button>
                        </Tooltip>
                    </div>
                </div>
            }
            disabled={isApplyingImprovement}
            onChange={onPromptChange}
            value={prompt ?? ''}
            {...props}
        />
    );
};

const useStyles = makeStyles({
    fieldActionGroup: {
        display: 'inline-flex',
        alignItems: 'center',
    },
    fieldLabelRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        width: '100%',
        gap: tokens.spacingHorizontalM,
    },
    fieldRequiredStar: {
        color: tokens.colorPaletteRedForeground1,
        fontWeight: tokens.fontWeightRegular,
        lineHeight: 1,
    },
    promptImprovementButton: {
        display: 'flex',
        gap: tokens.spacingHorizontalXS,
        alignItems: 'center',
        fontSize: tokens.fontSizeBase200,
    },
});

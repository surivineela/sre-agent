import { Button, Menu, MenuList, MenuPopover, MenuTrigger, mergeClasses, Tooltip } from '@fluentui/react-components';
import { Add20Regular, ChartMultiple20Regular, Lightbulb20Regular, SearchSparkle20Regular } from '@fluentui/react-icons';
import { FC, memo, useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { AgentTaskResources, PromptResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { usePermissionContext } from '../../../Contracts/PermissionContext';
import { AnimatedHistogramIcon } from '../AnimatedHistogramIcon';
import { usePlusMenuStyles } from '../styles';
import { AgentModeSubmenu } from './AgentModeSubmenu';
import { PlusMenuItem } from './PlusMenuItem';

export interface PlusMenuButtonProps {
    // Deep Investigation
    isDeepInvestigationButtonEnabled: boolean;
    isDeepInvestigationTurnedOn: boolean;
    onClickDeepInvestigationButton: () => void;
    // Agent Mode
    showAgentModeSelector: boolean;
    threadId?: string | null;
    // Interaction state
    isTyping: boolean;
    disableInputInteraction: boolean;
    // Prompt Library
    onOpenPromptLibrary: () => void;
    // Generate Insights
    isGeneratingInsights: boolean;
    onGenerateInsights: () => void;
    canGenerateInsights: boolean;
    isSessionInsightsEnabled: boolean;
}

export const PlusMenuButton: FC<PlusMenuButtonProps> = memo(
    ({
        isDeepInvestigationButtonEnabled,
        isDeepInvestigationTurnedOn,
        onClickDeepInvestigationButton,
        showAgentModeSelector,
        threadId,
        isTyping,
        disableInputInteraction,
        onOpenPromptLibrary,
        isGeneratingInsights,
        onGenerateInsights,
        canGenerateInsights,
        isSessionInsightsEnabled,
    }) => {
        const intl = useIntl();
        const styles = usePlusMenuStyles();
        const { canWriteThreads } = usePermissionContext();
        const { logAmplitudeControlEvent } = useAzPortalContext();
        const [open, setOpen] = useState(false);

        const handleDeepInvestigationClick = useCallback(() => {
            onClickDeepInvestigationButton();
            setOpen(false);
            logAmplitudeControlEvent({
                targetType: 'button',
                targetAction: 'clicked',
                targetName: 'deepInvestigationFromPlusMenu',
                targetFriendlyName: 'Deep investigation from plus menu',
                valueObjectName: isDeepInvestigationTurnedOn ? 'off' : 'on',
                valueObjectFriendlyName: isDeepInvestigationTurnedOn ? 'Turn off' : 'Turn on',
            });
        }, [onClickDeepInvestigationButton, isDeepInvestigationTurnedOn, logAmplitudeControlEvent]);

        const handlePromptLibraryClick = useCallback(() => {
            onOpenPromptLibrary();
            setOpen(false);
            logAmplitudeControlEvent({
                targetType: 'button',
                targetAction: 'clicked',
                targetName: 'promptLibraryFromPlusMenu',
                targetFriendlyName: 'Prompt library from plus menu',
                valueObjectName: 'open',
                valueObjectFriendlyName: 'Open prompt library',
            });
        }, [onOpenPromptLibrary, logAmplitudeControlEvent]);

        const handleGenerateInsightsClick = useCallback(() => {
            onGenerateInsights();
            setOpen(false);
            logAmplitudeControlEvent({
                targetType: 'button',
                targetAction: 'clicked',
                targetName: 'generateInsightsFromPlusMenu',
                targetFriendlyName: 'Generate insights from plus menu',
                valueObjectName: 'generate',
                valueObjectFriendlyName: 'Generate insights',
            });
        }, [onGenerateInsights, logAmplitudeControlEvent]);

        return (
            <Menu open={open} onOpenChange={(_, data) => setOpen(data.open)}>
                <MenuTrigger disableButtonEnhancement>
                    <Tooltip content={intl.formatMessage(SreAgentResources.openMenu)} relationship="label">
                        <Button
                            className={mergeClasses(styles.plusButton, open && styles.plusButtonActive)}
                            icon={<Add20Regular />}
                            appearance="subtle"
                            aria-label={intl.formatMessage(SreAgentResources.openMenu)}
                        />
                    </Tooltip>
                </MenuTrigger>
                <MenuPopover className={styles.menuPopover}>
                    <MenuList>
                        {/* Deep Investigation */}
                        <PlusMenuItem
                            icon={<SearchSparkle20Regular />}
                            label={intl.formatMessage(AgentTaskResources.deepInvestigation)}
                            onToggle={handleDeepInvestigationClick}
                            disabled={!isDeepInvestigationButtonEnabled || !canWriteThreads}
                            isChecked={isDeepInvestigationTurnedOn}
                        />

                        {/* Agent Mode Submenu */}
                        {showAgentModeSelector && threadId && (
                            <AgentModeSubmenu threadId={threadId} disabled={isTyping || disableInputInteraction} />
                        )}

                        {/* Prompt Library */}
                        <PlusMenuItem
                            icon={<Lightbulb20Regular />}
                            label={intl.formatMessage(PromptResources.promptExamples)}
                            onClick={handlePromptLibraryClick}
                            disabled={disableInputInteraction || isTyping}
                        />

                        {/* Generate Insights */}
                        {isSessionInsightsEnabled && (
                            <PlusMenuItem
                                icon={<ChartMultiple20Regular />}
                                label={intl.formatMessage(SreAgentResources.generateInsights)}
                                onClick={handleGenerateInsightsClick}
                                disabled={!canGenerateInsights || isGeneratingInsights || isTyping || disableInputInteraction}
                                isLoading={isGeneratingInsights}
                                loadingIcon={<AnimatedHistogramIcon />}
                            />
                        )}
                    </MenuList>
                </MenuPopover>
            </Menu>
        );
    }
);

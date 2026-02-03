import { Button, DialogBody, DialogSurface, mergeClasses, Tab, TabList, ToolbarButton, Tooltip } from '@fluentui/react-components';
import { Beaker20Regular, Dismiss24Regular } from '@fluentui/react-icons';
import { MessageBar, MessageBarBody } from '@fluentui/react-message-bar';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { useAgentCreateDialogStyles } from './AgentCreateDialog.Styles';
import { AgentCreateDialogFormikProps } from './Contracts';
import { FormView } from './FormView';
import { TabNames, useAgentCreateDialogFormik } from './Hooks/useAgentCreateDialogFormik';
import { YamlView } from './YamlView';

export const AgentCreateDialogFormik: FC<AgentCreateDialogFormikProps> = ({
    onDismiss,
    agents,
    existingTools,
    systemTools,
    mcpConnections,
    skills,
    excludedHandoffAgent,
    additionalHandoffAgents,
    isEditScenario,
    existingAgentGuid,
    isOverrideScenario,
    submissionError,
    setSubmissionError,
}) => {
    const intl = useIntl();
    const styles = useAgentCreateDialogStyles();
    const {
        view,
        setView,
        openedPanel,
        setOpenedPanel,
        yamlContent,
        handleYamlChange,
        handoffAgentsHook,
        toolsPickerHook,
        improvementsAndSuggestionsHook,
        disableControls,
        saveDisabled,
        discardDisabled,
        onSubmit,
        onDiscard,
        testPanelProps,
    } = useAgentCreateDialogFormik(
        agents,
        existingTools,
        systemTools,
        mcpConnections,
        excludedHandoffAgent,
        additionalHandoffAgents,
        isEditScenario,
        existingAgentGuid,
        isOverrideScenario
    );

    return (
        <DialogSurface className={styles.dialogSurface}>
            <DialogBody className={styles.dialogBody}>
                <div className={styles.dialogTitleWrapper}>
                    <div className={styles.dialogTitle}>
                        {intl.formatMessage(
                            isEditScenario
                                ? ExtendedAgentsGraphResources.editSubagentTitle
                                : ExtendedAgentsGraphResources.createSubagentTitle
                        )}
                    </div>
                    <div className={styles.tabListWrapper}>
                        <TabList
                            className={styles.tabList}
                            selectedValue={view}
                            onTabSelect={(_, data) => setView(data.value as TabNames)}
                            appearance="subtle"
                            disabled={disableControls}
                        >
                            <Tab
                                className={mergeClasses(styles.tab, view === TabNames.Form ? styles.currentTab : undefined)}
                                value={TabNames.Form}
                            >
                                {intl.formatMessage(ExtendedAgentsGraphResources.formTab)}
                            </Tab>
                            <Tab
                                className={mergeClasses(styles.tab, view === TabNames.Yaml ? styles.currentTab : undefined)}
                                value={TabNames.Yaml}
                            >
                                {intl.formatMessage(ExtendedAgentsGraphResources.yamlTab)}
                            </Tab>
                        </TabList>
                    </div>
                    <Tooltip relationship="label" content={intl.formatMessage(ExtendedAgentsGraphResources.testLiveAgent)}>
                        <ToolbarButton
                            className={styles.dialogCloseButton}
                            aria-label={intl.formatMessage(ExtendedAgentsGraphResources.testLiveAgent)}
                            appearance="transparent"
                            icon={<Beaker20Regular />}
                            onClick={() => {
                                setOpenedPanel('test');
                            }}
                            disabled={disableControls || openedPanel === 'test'}
                        />
                    </Tooltip>
                    <ToolbarButton
                        className={styles.dialogCloseButton}
                        aria-label={intl.formatMessage(SreAgentResources.close)}
                        appearance="transparent"
                        icon={<Dismiss24Regular />}
                        onClick={() => {
                            setSubmissionError?.(undefined);
                            onDismiss();
                        }}
                    />
                </div>
                {submissionError && (
                    <MessageBar intent="error" style={{ margin: '16px 24px' }}>
                        <MessageBarBody>{submissionError}</MessageBarBody>
                    </MessageBar>
                )}
                {view === 'form' ? (
                    <FormView
                        handoffAgentsHook={handoffAgentsHook}
                        improvementsAndSuggestionsHook={improvementsAndSuggestionsHook}
                        toolsPickerHook={toolsPickerHook}
                        openedPanel={openedPanel}
                        openPanel={setOpenedPanel}
                        closePanel={() => setOpenedPanel(undefined)}
                        isEditScenario={isEditScenario}
                        isOverrideScenario={isOverrideScenario}
                        testPanelProps={testPanelProps}
                        skills={skills}
                    />
                ) : (
                    <YamlView
                        yamlContent={yamlContent}
                        handleYamlChange={handleYamlChange}
                        disabled={disableControls}
                        openedPanel={openedPanel}
                        testPanelProps={testPanelProps}
                    />
                )}
                <div className={styles.buttonsContainer}>
                    <Button appearance="primary" onClick={onSubmit} disabled={saveDisabled}>
                        {intl.formatMessage(isEditScenario ? SreAgentResources.save : ExtendedAgentsGraphResources.create)}
                    </Button>
                    {isEditScenario && (
                        <Button appearance="secondary" onClick={() => onDiscard()} disabled={discardDisabled}>
                            {intl.formatMessage(SreAgentResources.discard)}
                        </Button>
                    )}
                    <Button
                        appearance="secondary"
                        onClick={() => {
                            setSubmissionError?.(undefined);
                            onDismiss();
                        }}
                    >
                        {intl.formatMessage(SreAgentResources.close)}
                    </Button>
                </div>
            </DialogBody>
        </DialogSurface>
    );
};

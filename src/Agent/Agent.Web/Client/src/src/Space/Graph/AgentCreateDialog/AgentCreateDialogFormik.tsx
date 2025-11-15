import { Button, DialogBody, DialogSurface, mergeClasses, Tab, TabList, ToolbarButton } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
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
    excludedHandoffAgent,
    additionalHandoffAgents,
    isEditScenario,
    isOverrideScenario,
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
    } = useAgentCreateDialogFormik(
        agents,
        existingTools,
        systemTools,
        mcpConnections,
        excludedHandoffAgent,
        additionalHandoffAgents,
        isEditScenario,
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
                    <ToolbarButton
                        className={styles.dialogCloseButton}
                        aria-label={intl.formatMessage(SreAgentResources.close)}
                        appearance="transparent"
                        icon={<Dismiss24Regular />}
                        onClick={onDismiss}
                    />
                </div>
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
                    />
                ) : (
                    <YamlView yamlContent={yamlContent} handleYamlChange={handleYamlChange} disabled={disableControls} />
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
                    <Button appearance="secondary" onClick={onDismiss}>
                        {intl.formatMessage(SreAgentResources.cancel)}
                    </Button>
                </div>
            </DialogBody>
        </DialogSurface>
    );
};

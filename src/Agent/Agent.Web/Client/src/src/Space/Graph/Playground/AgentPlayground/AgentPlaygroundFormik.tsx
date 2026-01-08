import { Button, Divider, mergeClasses, Tab, TabList } from '@fluentui/react-components';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, PlaygroundResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { useAgentPlaygroundStyles } from './AgentPlayground.Styles';
import { AgentPlaygroundFormikProps } from './Contracts';
import { Evaluation } from './Evaluation';
import { FormView } from './FormView';
import { EditorTabNames, TestTabNames, useAgentPlaygroundFormik } from './Hooks/useAgentPlaygroundFormik';
import { TestPanel } from './TestPanel';
import { YamlView } from './YamlView';

export const AgentPlaygroundFormik: FC<AgentPlaygroundFormikProps> = ({
    agent,
    agents,
    existingTools,
    systemTools,
    mcpConnections,
    excludedHandoffAgent,
    additionalHandoffAgents,
    isExistingAgent,
    existingAgentGuid,
    isOverrideScenario,
}) => {
    const intl = useIntl();
    const styles = useAgentPlaygroundStyles();
    const {
        mode,
        editorPanelView,
        setEditorPanelView,
        testPanelView,
        setTestPanelView,
        yamlContent,
        handleYamlChange,
        evaluationHook,
        handoffAgentsHook,
        toolsPickerHook,
        improvementsAndSuggestionsHook,
        disableControls,
        saveDisabled,
        discardDisabled,
        onSubmit,
        onDiscard,
        testPanelProps,
        showSuggestionsArea,
        setShowSuggestionsArea,
    } = useAgentPlaygroundFormik(
        agents,
        existingTools,
        systemTools,
        mcpConnections,
        excludedHandoffAgent,
        additionalHandoffAgents,
        isExistingAgent,
        existingAgentGuid,
        isOverrideScenario
    );

    return (
        <div className={styles.container}>
            <div className={styles.leftPanel}>
                <div className={styles.titleWrapper}>
                    <TabList
                        className={styles.tabList}
                        selectedValue={editorPanelView}
                        onTabSelect={(_, data) => setEditorPanelView(data.value as EditorTabNames)}
                        appearance="subtle"
                        disabled={disableControls}
                    >
                        <Tab
                            className={mergeClasses(styles.tab, editorPanelView === EditorTabNames.Form ? styles.currentTab : undefined)}
                            value={EditorTabNames.Form}
                        >
                            {intl.formatMessage(ExtendedAgentsGraphResources.formTab)}
                        </Tab>
                        <Tab
                            className={mergeClasses(styles.tab, editorPanelView === EditorTabNames.Yaml ? styles.currentTab : undefined)}
                            value={EditorTabNames.Yaml}
                        >
                            {intl.formatMessage(ExtendedAgentsGraphResources.yamlTab)}
                        </Tab>
                    </TabList>
                </div>
                {editorPanelView === EditorTabNames.Form ? (
                    <FormView
                        handoffAgentsHook={handoffAgentsHook}
                        improvementsAndSuggestionsHook={improvementsAndSuggestionsHook}
                        toolsPickerHook={toolsPickerHook}
                        showSuggestionsArea={showSuggestionsArea}
                        setShowSuggestionsArea={setShowSuggestionsArea}
                        isExistingAgent={isExistingAgent}
                        isOverrideScenario={isOverrideScenario}
                        disableControls={disableControls}
                    />
                ) : (
                    <YamlView yamlContent={yamlContent} handleYamlChange={handleYamlChange} disabled={disableControls} />
                )}
                <div className={styles.buttonsContainer}>
                    <Button appearance="primary" onClick={onSubmit} disabled={saveDisabled}>
                        {intl.formatMessage(SreAgentResources.apply)}
                    </Button>
                    {isExistingAgent && (
                        <Button appearance="secondary" onClick={onDiscard} disabled={discardDisabled}>
                            {intl.formatMessage(SreAgentResources.discard)}
                        </Button>
                    )}
                </div>
            </div>
            <Divider vertical className={styles.dialogContentVerticalDivider} />
            <div className={styles.rightPanel}>
                <div className={styles.titleWrapper}>
                    <TabList
                        className={styles.tabList}
                        selectedValue={testPanelView}
                        onTabSelect={(_, data) => setTestPanelView(data.value as TestTabNames)}
                        appearance="subtle"
                        disabled={disableControls}
                    >
                        <Tab
                            className={mergeClasses(styles.tab, testPanelView === TestTabNames.Chat ? styles.currentTab : undefined)}
                            value={TestTabNames.Chat}
                        >
                            {intl.formatMessage(PlaygroundResources.test)}
                        </Tab>
                        <Tab
                            className={mergeClasses(styles.tab, testPanelView === TestTabNames.Evaluation ? styles.currentTab : undefined)}
                            value={TestTabNames.Evaluation}
                        >
                            {intl.formatMessage(PlaygroundResources.evaluation)}
                        </Tab>
                    </TabList>
                </div>
                <TestPanel {...testPanelProps} mode={mode} hidden={testPanelView !== TestTabNames.Chat} />
                <Evaluation {...evaluationHook} agent={agent} hidden={testPanelView !== TestTabNames.Evaluation} />
            </div>
        </div>
    );
};

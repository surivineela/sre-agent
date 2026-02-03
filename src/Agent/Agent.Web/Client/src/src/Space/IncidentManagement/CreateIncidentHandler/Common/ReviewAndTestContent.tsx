import { IColumn } from '@fluentui/react';
import { Button, Combobox, Field, mergeClasses, MessageBar, Option, Spinner, Text, Textarea, tokens } from '@fluentui/react-components';
import { Beaker20Regular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ToolInfo } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementType } from '../../../../Common/Contracts/Azure/SreAgent';
import { ThreadSource } from '../../../../Common/Contracts/DataPlane/Thread';
import { IncidentHandlerCreateResources, IncidentManagementResources } from '../../../../Strings/SREAgentResources';
import ChatBox from '../../../Activities/ChatBox';
import { MultipleSelectionShimmerDetailsList } from '../../../Components/MultipleSelectionShimmerDetailsList';
import { ChatBoxSidePanelData, ChatBoxSidePanelType } from '../../../Contracts/Activities';
import { useIncidentManagementStyles } from '../../../Styles/IncidentManagement.styles';
import { ToolTableFieldNames } from '../Contracts';
import { IncidentHandlerConsolidatedCreateContext } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';
import { ToolsPickerDialog } from '../ToolsPickerDialog';
import { ToolsToolbar } from '../ToolsToolbar';

export type ReviewAndTestView = 'review' | 'test';
export interface ReviewAndTestContentProps {
    view?: ReviewAndTestView;
    onOpenSidePanel?: (panelType: ChatBoxSidePanelType, data: ChatBoxSidePanelData) => void;
    onCloseSidePanel?: (panelType: ChatBoxSidePanelType) => void;
    initialSidePanelData?: ChatBoxSidePanelData;
}

export const ReviewAndTestContent: FC<ReviewAndTestContentProps> = ({ view, onOpenSidePanel, onCloseSidePanel, initialSidePanelData }) => {
    const { errors, values, setFieldValue } = useFormikContext<IncidentHandlerCreateFormValues>();
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const {
        incidentPlatformType,
        tools,
        toolsLoading,
        generatingUpdatedTools,
        generateUpdatedTools,
        handlerTestMetadata,
        handlerMode,
        filterMode,
    } = useContext(IncidentHandlerConsolidatedCreateContext);

    const {
        searchTerm,
        setSearchTerm,
        incidents,
        loadingIncidents,
        createTestThread,
        createTestThreadFailure,
        creatingTestThread,
        testIncidentThreadId,
    } = handlerTestMetadata || {};

    const selectedToolsList = useMemo(() => {
        return tools.filter(tool => values.toolNames?.includes(tool.name));
    }, [tools, values.toolNames]);

    const [toolsPickerVisible, setToolsPickerVisible] = useState<boolean>(false);

    const toolsTableColumns: IColumn[] = useMemo(() => {
        return [
            {
                key: ToolTableFieldNames.Name,
                fieldName: ToolTableFieldNames.Name,
                name: intl.formatMessage(IncidentHandlerCreateResources.tool),
                minWidth: 100,
                maxWidth: 200,
                isResizable: true,
            },
            {
                key: ToolTableFieldNames.Description,
                fieldName: ToolTableFieldNames.Description,
                name: intl.formatMessage(IncidentHandlerCreateResources.description),
                minWidth: 200,
                isMultiline: true,
                isResizable: true,
            },
        ];
    }, [intl]);

    return (
        <div className={styles.reviewAndTestRoot}>
            {generatingUpdatedTools && (
                <div className={styles.reviewAndTestOverlay}>
                    <Spinner size="large" aria-label={intl.formatMessage(IncidentManagementResources.generating)} />
                </div>
            )}
            {(!view || view === 'review') && (
                <div className={mergeClasses(styles.reviewPanelLeft, !view ? styles.reviewPanelLeftHalf : styles.reviewPanelLeftFull)}>
                    <div className={styles.reviewSectionHeader}>
                        {!view && (
                            <Text size={300} weight="semibold" as="h2" className={styles.reviewSectionTitle}>
                                {intl.formatMessage(IncidentHandlerCreateResources.reviewCustomInstructionsTitle)}
                            </Text>
                        )}
                        {(handlerMode === 'create' || filterMode === 'create') && (
                            <Text size={300}>{intl.formatMessage(IncidentHandlerCreateResources.reviewCustomInstructionsDescription)}</Text>
                        )}
                        <Textarea
                            value={values.incidentProcessingGuide}
                            onChange={(_, data) => setFieldValue('incidentProcessingGuide', data.value)}
                            rows={8}
                            disabled={generatingUpdatedTools}
                            root={{ style: { height: 'auto', flex: '1' } }}
                            textarea={{ style: { maxHeight: '100%' } }}
                            aria-label={intl.formatMessage(IncidentHandlerCreateResources.customInstructionsAriaLabel)}
                        />
                    </div>
                    <div className={styles.reviewToolsSection}>
                        <Text size={300} weight="semibold" className={styles.reviewToolsTitle} as="h2">
                            {intl.formatMessage(IncidentHandlerCreateResources.reviewToolsTitle)}
                        </Text>
                        <Text size={300}>{intl.formatMessage(IncidentHandlerCreateResources.reviewToolsDescription)}</Text>
                        {errors.toolNames && <MessageBar intent="error">{errors.toolNames}</MessageBar>}
                        <ToolsToolbar
                            onUpdateToolsClick={generateUpdatedTools}
                            onAddClick={() => setToolsPickerVisible(true)}
                            disabled={generatingUpdatedTools}
                        />
                        <ToolsPickerDialog
                            visible={toolsPickerVisible}
                            onDismiss={() => setToolsPickerVisible(false)}
                            onSave={(toolNames: string[]) => {
                                setFieldValue('toolNames', toolNames);
                                setToolsPickerVisible(false);
                            }}
                            existingToolsSelection={values.toolNames || []}
                            tools={tools}
                            loading={toolsLoading}
                        />
                        <MultipleSelectionShimmerDetailsList
                            data={selectedToolsList}
                            loading={toolsLoading}
                            columns={toolsTableColumns}
                            disallowSelection={true}
                            disabled={generatingUpdatedTools}
                            onChange={() => {}}
                            getKey={(item: ToolInfo) => item.name}
                            listContainerStyle={{
                                minHeight: selectedToolsList.length < 4 ? 'fit-content' : '200px',
                                maxHeight: 'unset',
                            }}
                        />
                    </div>
                </div>
            )}

            <div className={styles.formDivider}></div>

            {(!view || view === 'test') && (
                <div className={mergeClasses(styles.testPanelRight, !view ? styles.testPanelRightHalf : styles.testPanelRightFull)}>
                    {!view && (
                        <Text size={300} weight="semibold" as="h2" className={styles.reviewSectionTitle}>
                            {intl.formatMessage(IncidentHandlerCreateResources.testHandlerTitle)}
                        </Text>
                    )}
                    <div className={styles.testIncidentInputRow}>
                        <Field
                            id="testIncidentField"
                            label={intl.formatMessage(IncidentHandlerCreateResources.incidentLabel)}
                            className={styles.testIncidentField}
                            required
                        >
                            <Combobox
                                id="testIncidentComboBox"
                                freeform={true}
                                value={searchTerm || ''}
                                placeholder={intl.formatMessage(IncidentHandlerCreateResources.incidentPlaceholder)}
                                onOptionSelect={(_event, data) => {
                                    const selectedOption = incidents?.find(incident => incident.id === data.optionValue);
                                    setSearchTerm(
                                        (incidentPlatformType === IncidentManagementType.AzMonitor
                                            ? selectedOption?.alertId
                                            : selectedOption?.id) || ''
                                    );
                                }}
                                disabled={creatingTestThread}
                                onInput={event => {
                                    const inputValue = (event.target as any).value as string;
                                    setSearchTerm(inputValue);
                                }}
                                positioning={{
                                    position: 'below',
                                    align: 'start',
                                }}
                            >
                                {loadingIncidents ? (
                                    <Spinner size="small" />
                                ) : !incidents?.length ? (
                                    <div className={styles.testIncidentNoResults}>
                                        {intl.formatMessage(IncidentManagementResources.noIncidentsFound)}
                                    </div>
                                ) : (
                                    <div className={styles.testIncidentDropdownContent}>
                                        {incidents?.map(incident => (
                                            <Option
                                                key={incident.id}
                                                value={incident.id}
                                                text={`${incident.id} - ${incident.title}`}
                                                checkIcon={null}
                                                style={{ margin: 2 }}
                                            >
                                                <span
                                                    style={{ overflow: 'hidden', overflowWrap: 'break-word' }}
                                                >{`${incident.id} - ${incident.title}`}</span>
                                            </Option>
                                        ))}
                                    </div>
                                )}
                            </Combobox>
                        </Field>
                        <Button
                            icon={<Beaker20Regular />}
                            appearance="secondary"
                            onClick={createTestThread}
                            disabled={!searchTerm || creatingTestThread}
                        >
                            {intl.formatMessage(IncidentHandlerCreateResources.testHandlerRunButton)}
                        </Button>
                    </div>
                    {creatingTestThread ? (
                        <Spinner size="huge" className={styles.testIncidentSpinner} />
                    ) : createTestThreadFailure ? (
                        <MessageBar intent="error">
                            {intl.formatMessage(IncidentHandlerCreateResources.testHandlerRunFailure, {
                                errorMessage: createTestThreadFailure,
                            })}
                        </MessageBar>
                    ) : testIncidentThreadId ? (
                        <ChatBox
                            threadId={testIncidentThreadId}
                            addThread={() => {}}
                            selectThread={() => {}}
                            updateThreadLastReadTime={() => {}}
                            threadSource={ThreadSource.incident}
                            onOpenSidePanel={onOpenSidePanel}
                            onCloseSidePanel={onCloseSidePanel}
                            canOpenSidePanel={!!view}
                            initialSidePanelData={view ? initialSidePanelData : undefined}
                            stylesProps={{
                                rootStyle: {
                                    height: `0%`,
                                    flex: '1 1 auto',
                                },
                                chatBoxAndAgentTask: {
                                    boxShadow: 'unset',
                                    borderRadius: 'unset',
                                    width: '100%',
                                    height: '100%',
                                    minHeight: '400px',
                                    marginBottom: '0px',
                                },
                                chatBoxInner: {
                                    borderRadius: 'unset',
                                },
                            }}
                            sidePanelStylesProps={{
                                root: {
                                    height: '0%',
                                    flex: '1 1 auto',
                                },
                            }}
                        />
                    ) : (
                        <div className={styles.emptyState}>
                            <img src="./AIChatLM.svg" alt="AI Chat" style={{ height: 128 }} />
                            <Text size={300} align="center" style={{ color: tokens.colorNeutralForeground2, width: '400px' }}>
                                {intl.formatMessage(IncidentHandlerCreateResources.testHandlerEmptyMessage)}
                            </Text>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
};

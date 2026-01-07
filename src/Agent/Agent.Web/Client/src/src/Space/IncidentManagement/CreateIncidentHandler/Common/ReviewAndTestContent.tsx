import { IColumn } from '@fluentui/react';
import { Button, Combobox, Field, MessageBar, Option, Spinner, Text, Textarea } from '@fluentui/react-components';
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
        <div
            style={{
                display: 'flex',
                flexDirection: 'row',
                gap: 16,
                height: '100%',
                width: 'calc(100% - 16px)',
            }}
        >
            {generatingUpdatedTools && (
                <div
                    style={{
                        position: 'absolute',
                        inset: 0,
                        background: 'rgba(255, 255, 255, 0.6)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 1000,
                    }}
                >
                    <Spinner size="large" aria-label={intl.formatMessage(IncidentManagementResources.generating)} />
                </div>
            )}
            {(!view || view === 'review') && (
                <div
                    style={{
                        width: !view ? '50%' : '100%',
                        display: 'flex',
                        flexDirection: 'column',
                        paddingTop: 20,
                        height: 'calc(100% - 20px)',
                    }}
                >
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minHeight: '33%', flex: 'none' }}>
                        {!view && (
                            <Text size={400} weight="semibold">
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
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, height: '0%', flex: '1 1 auto' }}>
                        <Text size={400} weight="semibold" style={{ marginTop: 32 }}>
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
            {(!view || view === 'test') && (
                <div
                    style={{
                        width: !view ? '50%' : '100%',
                        display: 'flex',
                        flexDirection: 'column',
                        gap: 16,
                        paddingTop: 20,
                        height: 'calc(100% - 20px)',
                    }}
                >
                    {!view && (
                        <Text size={400} weight="semibold">
                            {intl.formatMessage(IncidentHandlerCreateResources.testHandlerTitle)}
                        </Text>
                    )}
                    <div style={{ display: 'flex', flexDirection: 'row', gap: 8, alignItems: 'end', position: 'relative' }}>
                        <Field
                            id="testIncidentField"
                            label={intl.formatMessage(IncidentHandlerCreateResources.incidentLabel)}
                            style={{ flexBasis: '500px' }}
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
                                size={'small'}
                            >
                                {loadingIncidents ? (
                                    <Spinner size="small" />
                                ) : !incidents?.length ? (
                                    <div style={{ margin: '2px 0px', paddingLeft: '10px' }}>
                                        {intl.formatMessage(IncidentManagementResources.noIncidentsFound)}
                                    </div>
                                ) : (
                                    <div
                                        style={{
                                            maxHeight: '400px',
                                            overflowY: 'scroll',
                                            overflowX: 'auto',
                                        }}
                                    >
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
                        <Spinner size="huge" style={{ height: '100%' }} />
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
                            <div>
                                <Beaker20Regular style={{ height: '100px', width: '100px' }} />
                            </div>
                            <div className={styles.emptyStateTitle}>
                                {intl.formatMessage(IncidentHandlerCreateResources.testHandlerEmptyMessage)}
                            </div>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
};

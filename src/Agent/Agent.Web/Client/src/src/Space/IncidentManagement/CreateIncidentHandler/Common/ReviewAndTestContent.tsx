import { IColumn } from '@fluentui/react';
import { Button, Dropdown, Field, MessageBar, Option, Spinner, Text, Textarea } from '@fluentui/react-components';
import { Beaker20Regular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ToolInfo } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { ThreadSource } from '../../../../Common/Contracts/DataPlane/Thread';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import ChatBox from '../../../Activities/ChatBox';
import { MultipleSelectionShimmerDetailsList } from '../../../Components/MultipleSelectionShimmerDetailsList';
import { useIncidentManagementStyles } from '../../../Styles/IncidentManagement.styles';
import { ToolTableFieldNames } from '../Contracts';
import { IncidentHandlerConsolidatedCreateContext } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';
import { ToolsPickerDialog } from '../ToolsPickerDialog';
import { ToolsToolbar } from '../ToolsToolbar';

export const ReviewAndTestContent: FC = () => {
    const { values, setFieldValue } = useFormikContext<IncidentHandlerCreateFormValues>();
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const {
        tools,
        toolsLoading,
        generatingUpdatedTools,
        generateUpdatedTools,
        incidents,
        loadingIncidents,
        createTestThread,
        createTestThreadFailure,
        creatingTestThread,
        testIncident,
        setTestIncident,
        testIncidentThreadId,
    } = useContext(IncidentHandlerConsolidatedCreateContext);

    const selectedToolsList = useMemo(() => {
        return tools.filter(tool => values.toolNames?.includes(tool.name));
    }, [tools, values.toolNames]);

    const [activeToolNames, setActiveToolNames] = useState<string[]>([]);
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
                    <Spinner size="large" />
                </div>
            )}
            <div
                style={{
                    width: '50%',
                    display: 'flex',
                    flexDirection: 'column',
                    paddingTop: 20,
                    gap: 16,
                }}
            >
                <Text size={400} weight="semibold">
                    {intl.formatMessage(IncidentHandlerCreateResources.reviewCustomInstructionsTitle)}
                </Text>
                <Textarea
                    value={values.incidentProcessingGuide}
                    onChange={(_, data) => setFieldValue('incidentProcessingGuide', data.value)}
                    rows={8}
                    disabled={generatingUpdatedTools}
                />
                <Text size={400} weight="semibold">
                    {intl.formatMessage(IncidentHandlerCreateResources.reviewToolsTitle)}
                </Text>
                <ToolsToolbar
                    onUpdateToolsClick={generateUpdatedTools}
                    onAddClick={() => setToolsPickerVisible(true)}
                    onDeleteClick={() => {
                        setFieldValue(
                            'toolNames',
                            values.toolNames?.filter(name => !activeToolNames.includes(name))
                        );
                        setActiveToolNames([]);
                    }}
                    disabled={generatingUpdatedTools}
                    addDisabled={!tools.length || tools.length === selectedToolsList.length}
                    hasToolsSelected={activeToolNames.length > 0}
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
                    selectedKeys={activeToolNames}
                    loading={toolsLoading}
                    columns={toolsTableColumns}
                    disabled={generatingUpdatedTools}
                    onChange={selectedKeys => setActiveToolNames(selectedKeys)}
                    getKey={(item: ToolInfo) => item.name}
                    listContainerStyle={{
                        minHeight: '200px',
                        maxHeight: 'calc(100% - 309px)',
                    }}
                />
            </div>
            <div
                style={{
                    width: '50%',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: 16,
                    paddingTop: 20,
                }}
            >
                <Text size={400} weight="semibold" style={{ marginLeft: 20 }}>
                    {intl.formatMessage(IncidentHandlerCreateResources.testHandlerTitle)}
                </Text>
                <div style={{ display: 'flex', flexDirection: 'row', gap: 8, alignItems: 'end', marginLeft: 20, position: 'relative' }}>
                    <Field
                        id="testIncidentField"
                        label={intl.formatMessage(IncidentHandlerCreateResources.incidentLabel)}
                        style={{ flexBasis: '500px' }}
                        required
                    >
                        <Dropdown
                            id="testIncidentDropdown"
                            button={
                                <span style={{ overflowX: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                    {testIncident ? `${testIncident.id} - ${testIncident.title}` : undefined}
                                </span>
                            }
                            value={testIncident?.id}
                            placeholder={intl.formatMessage(IncidentHandlerCreateResources.incidentPlaceholder)}
                            onOptionSelect={(_event, data) => {
                                const selectedOption = incidents?.find(incident => incident.id === data.optionValue);
                                setTestIncident(selectedOption);
                            }}
                            disabled={loadingIncidents || creatingTestThread}
                        >
                            {incidents?.map(incident => (
                                <Option key={incident.id} value={incident.id} text={`${incident.id} - ${incident.title}`} checkIcon={null}>
                                    <span
                                        style={{ overflow: 'hidden', overflowWrap: 'break-word' }}
                                    >{`${incident.id} - ${incident.title}`}</span>
                                </Option>
                            ))}
                        </Dropdown>
                    </Field>
                    <Button
                        icon={<Beaker20Regular />}
                        appearance="secondary"
                        onClick={createTestThread}
                        disabled={loadingIncidents || !testIncident || creatingTestThread}
                    >
                        {intl.formatMessage(IncidentHandlerCreateResources.testHandlerRunButton)}
                    </Button>
                </div>
                {creatingTestThread ? (
                    <Spinner size="huge" style={{ height: '100%' }} />
                ) : createTestThreadFailure ? (
                    <MessageBar intent="error" style={{ marginLeft: 20 }}>
                        {intl.formatMessage(IncidentHandlerCreateResources.testHandlerRunFailure, {
                            errorMessage: createTestThreadFailure,
                        })}
                    </MessageBar>
                ) : testIncidentThreadId ? (
                    <ChatBox
                        threadId={testIncidentThreadId}
                        addThread={() => {}}
                        updateThreadLastReadTime={() => {}}
                        threadSource={ThreadSource.incident}
                        stylesProps={{
                            chatBoxAndAgentTask: {
                                boxShadow: 'unset',
                                borderRadius: 'unset',
                                width: '100%',
                                minHeight: '400px',
                                marginBottom: '0px',
                            },
                            chatBox: {
                                height: '100%',
                            },
                            chatBoxInner: {
                                borderRadius: 'unset',
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
        </div>
    );
};

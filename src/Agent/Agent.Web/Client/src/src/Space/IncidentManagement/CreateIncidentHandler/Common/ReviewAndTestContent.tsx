import { IColumn } from '@fluentui/react';
import { Spinner, Text, Textarea } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ToolInfo } from '../../../../Common/Contracts/Azure/IncidentHandler';
import Url from '../../../../Common/Helpers/Url';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { MultipleSelectionShimmerDetailsList } from '../../../Components/MultipleSelectionShimmerDetailsList';
import { ToolTableFieldNames } from '../Contracts';
import { IncidentHandlerConsolidatedCreateContext } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';
import { ToolsPickerDialog } from '../ToolsPickerDialog';
import { ToolsToolbar } from '../ToolsToolbar';

export const ReviewAndTestContent: FC = () => {
    const { values, setFieldValue } = useFormikContext<IncidentHandlerCreateFormValues>();
    const intl = useIntl();
    const { tools, toolsLoading, generatingUpdatedTools, generateUpdatedTools } = useContext(IncidentHandlerConsolidatedCreateContext);

    const selectedToolsList = useMemo(() => {
        return tools.filter(tool => values.toolNames?.includes(tool.name));
    }, [tools, values.toolNames]);

    const [activeToolNames, setActiveToolNames] = useState<string[]>([]);
    const [toolsPickerVisible, setToolsPickerVisible] = useState<boolean>(false);

    const showHandlerTestUi = useMemo(() => Url.getFeatureValue('showHandlerTestUi') === 'true', []);

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
        <div style={{ display: 'flex', flexDirection: 'row', gap: 16 }}>
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
                    width: showHandlerTestUi ? '50%' : '100%',
                    display: 'flex',
                    flexDirection: 'column',
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
                />
            </div>
            {showHandlerTestUi && (
                <div
                    style={{
                        width: '50%',
                        display: 'flex',
                        flexDirection: 'column',
                        gap: 16,
                    }}
                >
                    <Text size={400} weight="semibold">
                        {intl.formatMessage(IncidentHandlerCreateResources.testHandlerTitle)}
                    </Text>
                </div>
            )}
        </div>
    );
};

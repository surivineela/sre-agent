import {
    DetailsListLayoutMode,
    Dropdown,
    IColumn,
    IDropdownOption,
    Selection,
    SelectionMode,
    ShimmeredDetailsList,
    Stack,
    Text,
    TextField,
} from '@fluentui/react';
import { Button } from '@fluentui/react-components';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { Response } from '../../../../Common/Clients/DataPlaneClient';
import { IncidentHandlerClient } from '../../../../Common/Clients/IncidentHandlerClient';
import { IIncidentDocument, ToolInfo } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { generateHandlerStyles } from '../../../Styles/IncidentManagement.styles';
import { IncidentHandlerCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerCreateContext';

enum TimeDuration {
    Last15Days = 15,
    Last30Days = 30,
    Last60Days = 60,
    Last90Days = 90,
}

enum IncidentTableFieldNames {
    Priority = 'priority',
    CreatedAt = 'createdAt',
    Title = 'title',
    Id = 'id',
    Status = 'status',
}

enum ToolTableFieldNames {
    Name = 'name',
    Description = 'description',
}

const MultipleSelectionTable = <T,>(props: {
    data: Response<T[]> | undefined;
    columns: IColumn[];
    onSelecting: (selectedItems: T[]) => void;
}) => {
    const { data, columns } = props;

    const selection = new Selection({
        onSelectionChanged: () => {
            const selectedItems = selection.getSelection() as T[];
            props.onSelecting(selectedItems);
        },
    });

    return (
        <div className={generateHandlerStyles.detailListContainer} data-is-scrollable="true">
            <ShimmeredDetailsList
                items={data?.content ?? []}
                columns={columns}
                selectionMode={SelectionMode.multiple}
                selection={selection}
                layoutMode={DetailsListLayoutMode.justified}
                enableShimmer={data === undefined}
                useReducedRowRenderer={true}
            />
        </div>
    );
};

export const GenerateHandler = () => {
    const intl = useIntl();
    const context = useContext(IncidentHandlerCreateContext);
    const { setCurrentStep, setInstructions, exitToHome } = context;

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const incidentHandlerClient = IncidentHandlerClient.getInstance(sreAgentEndpoint);

    const [incidentDocuments, setIncidentDocuments] = useState<Response<IIncidentDocument[]> | undefined>(undefined);
    const [toolInfos, setToolInfos] = useState<Response<ToolInfo[]> | undefined>(undefined);

    const [selectedIncidents, setSelectedIncidents] = useState<IIncidentDocument[]>([]);
    const [selectedTools, setSelectedTools] = useState<ToolInfo[]>([]);

    const [isWaitingForGeneration, setIsWaitingForGeneration] = useState<boolean>(false);
    const [customInstructions, setCustomInstructions] = useState<string>('');

    const dropdownOptions: IDropdownOption<{ numberOfDays: number; isDefault?: boolean }>[] = useMemo(() => {
        return [
            {
                key: TimeDuration.Last15Days,
                text: intl.formatMessage(IncidentHandlerCreateResources.last15days),
                data: { numberOfDays: TimeDuration.Last15Days },
            },
            {
                key: TimeDuration.Last30Days,
                text: intl.formatMessage(IncidentHandlerCreateResources.last30days),
                data: { numberOfDays: TimeDuration.Last30Days },
            },
            {
                key: TimeDuration.Last60Days,
                text: intl.formatMessage(IncidentHandlerCreateResources.last60days),
                data: { numberOfDays: TimeDuration.Last60Days, isDefault: true },
            },
            {
                key: TimeDuration.Last90Days,
                text: intl.formatMessage(IncidentHandlerCreateResources.last60days),
                data: { numberOfDays: TimeDuration.Last90Days },
            },
        ];
    }, [intl]);

    const defaultOption = useMemo(() => {
        return dropdownOptions.find(option => option.data?.isDefault === true) ?? undefined;
    }, [dropdownOptions]);

    const [selectedOption, setSelectedOption] = useState(defaultOption);

    useEffect(() => {
        const fetchIncidents = async () => {
            if (!selectedOption) {
                return;
            }
            setIncidentDocuments(undefined);
            const response = await incidentHandlerClient.queryIncidents({
                // Todo, update payload with filter to replace keywords
                filter: {
                    titleContains: 'down',
                },
                durationInDays: selectedOption.data?.numberOfDays,
            });
            if (response.content) {
                response.content.sort((a, b) => a.title.localeCompare(b.title));
            }

            setIncidentDocuments(response);
        };
        fetchIncidents();
    }, [selectedOption]);

    useEffect(() => {
        const fetchTools = async () => {
            const response = await incidentHandlerClient.listTools();
            if (response.content) {
                response.content.sort((a, b) => a.name.localeCompare(b.name));
            }
            setToolInfos(response);
        };
        fetchTools();
    }, []);

    const incidentTableColumns: IColumn[] = useMemo(() => {
        return [
            {
                key: IncidentTableFieldNames.Priority,
                fieldName: IncidentTableFieldNames.Priority,
                name: intl.formatMessage(IncidentHandlerCreateResources.priority),
                minWidth: 50,
                maxWidth: 100,
                isResizable: true,
                isSortable: true,
            },
            {
                key: IncidentTableFieldNames.CreatedAt,
                fieldName: IncidentTableFieldNames.CreatedAt,
                name: intl.formatMessage(IncidentHandlerCreateResources.dateCreated),
                minWidth: 100,
                maxWidth: 200,
                isMultiline: true,
                isResizable: true,
                isSortable: true,
            },
            {
                key: IncidentTableFieldNames.Title,
                fieldName: IncidentTableFieldNames.Title,
                name: intl.formatMessage(IncidentHandlerCreateResources.title),
                minWidth: 200,
                isMultiline: true,
                isResizable: true,
                isSortable: true,
            },
            {
                key: IncidentTableFieldNames.Id,
                fieldName: IncidentTableFieldNames.Id,
                name: intl.formatMessage(IncidentHandlerCreateResources.incidentId),
                minWidth: 100,
                maxWidth: 200,
                isMultiline: true,
                isResizable: true,
                isSortable: true,
            },

            {
                key: IncidentTableFieldNames.Status,
                fieldName: IncidentTableFieldNames.Status,
                name: intl.formatMessage(IncidentHandlerCreateResources.status),
                minWidth: 100,
                maxWidth: 200,
                isMultiline: true,
                isResizable: true,
                isSortable: true,
            },
        ];
    }, [intl]);

    const toolsTableColumns: IColumn[] = useMemo(() => {
        return [
            {
                key: ToolTableFieldNames.Name,
                fieldName: ToolTableFieldNames.Name,
                name: intl.formatMessage(IncidentHandlerCreateResources.tool),
                minWidth: 100,
                maxWidth: 200,
                isMultiline: true,
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

    const navigateToNextStep = useCallback(() => {
        setCurrentStep(IncidentHandlerCreateSteps.ReviewAndEdit);
    }, [setCurrentStep]);

    const handleGenerateInstructions = useCallback(async () => {
        setIsWaitingForGeneration(true);
        const res = await incidentHandlerClient.generateInstructions({
            // Todo, update with agent name when context is available
            agentName: '',
            incidents: selectedIncidents?.map(incident => incident.id) ?? [],
            tools: selectedTools?.map(tool => tool.name) ?? [],
            customInstructions: customInstructions,
        });
        setIsWaitingForGeneration(false);

        if (res.isSuccessful && res.content) {
            setInstructions(res.content.generatedInstructions);
            navigateToNextStep();
        }
    }, [
        setIsWaitingForGeneration,
        incidentHandlerClient.generateInstructions,
        selectedIncidents,
        selectedTools,
        customInstructions,
        setInstructions,
        navigateToNextStep,
    ]);

    return (
        <Stack className={generateHandlerStyles.container} tokens={{ childrenGap: 16 }}>
            <Text variant="mediumPlus">{intl.formatMessage(IncidentHandlerCreateResources.chooseIncidentTitle)}</Text>
            <Text variant="medium">{intl.formatMessage(IncidentHandlerCreateResources.chooseIncidentDescription)}</Text>
            <Dropdown
                options={dropdownOptions}
                selectedKey={selectedOption?.key}
                onChange={(_e, option) => setSelectedOption(option)}
                className={generateHandlerStyles.dropdown}
                disabled={incidentDocuments === undefined}
            />
            <MultipleSelectionTable
                data={incidentDocuments}
                columns={incidentTableColumns}
                onSelecting={incidents => setSelectedIncidents(incidents)}
            />
            <Text variant="mediumPlus">{intl.formatMessage(IncidentHandlerCreateResources.chooseToolsTitle)}</Text>
            <Text variant="medium">{intl.formatMessage(IncidentHandlerCreateResources.chooseToolsDescription)}</Text>
            <MultipleSelectionTable data={toolInfos} columns={toolsTableColumns} onSelecting={tools => setSelectedTools(tools)} />

            <Text variant="mediumPlus">{intl.formatMessage(IncidentHandlerCreateResources.customInstructionTitle)}</Text>
            <Text variant="medium">{intl.formatMessage(IncidentHandlerCreateResources.customInstructionDescription)}</Text>
            <TextField
                placeholder={intl.formatMessage(IncidentHandlerCreateResources.customInstructionPlaceholder)}
                value={customInstructions}
                onChange={(_e, newValue) => setCustomInstructions(newValue ?? '')}
                rows={8}
                multiline
                className={generateHandlerStyles.textField}
            />
            <Stack className={generateHandlerStyles.buttonContainer} horizontalAlign="start" tokens={{ childrenGap: 15 }} horizontal>
                <Button
                    appearance="primary"
                    onClick={() => handleGenerateInstructions()}
                    disabled={(!customInstructions && selectedIncidents.length === 0) || isWaitingForGeneration}
                >
                    {intl.formatMessage(IncidentHandlerCreateResources.next)}
                </Button>
                <Button onClick={() => navigateToNextStep()} disabled={isWaitingForGeneration}>
                    {intl.formatMessage(IncidentHandlerCreateResources.skip)}
                </Button>
                <Button onClick={() => exitToHome()}>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
            </Stack>
        </Stack>
    );
};

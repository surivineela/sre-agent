import {
    DetailsListLayoutMode,
    DetailsRow,
    Dropdown,
    IColumn,
    IDetailsRowProps,
    Selection,
    SelectionMode,
    ShimmeredDetailsList,
    Text,
    TextField,
} from '@fluentui/react';
import { Button, Field, Input, Spinner } from '@fluentui/react-components';
import { useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { generateHandlerStyles } from '../../../Styles/IncidentManagement.styles';
import { IncidentHandlerCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerCreateContext';

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
    key: string;
    selection: Selection;
    data: T[] | undefined;
    loading: boolean;
    columns: IColumn[];
    disabled?: boolean;
}) => {
    const { data, columns } = props;

    return (
        <div className={generateHandlerStyles.detailListContainer} data-is-scrollable="true">
            <ShimmeredDetailsList
                key={`${props.key}${props.disabled ? '-disabled' : ''}`}
                items={data ?? []}
                columns={columns}
                selectionMode={SelectionMode.multiple}
                selection={props.selection}
                layoutMode={DetailsListLayoutMode.justified}
                enableShimmer={props.loading}
                useReducedRowRenderer={true}
                onRenderRow={(rowProps?: IDetailsRowProps): JSX.Element | null => {
                    if (!rowProps) {
                        return null;
                    }

                    const updatedRowProps: IDetailsRowProps = {
                        ...rowProps,
                        disabled: props.disabled,
                    };

                    return <DetailsRow {...updatedRowProps} />;
                }}
            />
        </div>
    );
};

export const GenerateHandler = () => {
    const intl = useIntl();
    const context = useContext(IncidentHandlerCreateContext);
    const {
        setCurrentStep,
        exitToHome,
        name,
        setName,
        description,
        setDescription,
        toolsSelection,
        selectedIncidents,
        incidentsSelection,
        customInstructions,
        setCustomInstructions,

        isGeneratingInstructions,
        timespanDropdownOptions,
        selectedTimespanOption,
        incidentDocuments,
        loadingTools,
        setSelectedTimespanOption,
        toolInfos,
        loadingIncidents,
        handleGenerateInstructions,
    } = context;

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
                isResizable: true,
                isSortable: true,
            },

            {
                key: IncidentTableFieldNames.Status,
                fieldName: IncidentTableFieldNames.Status,
                name: intl.formatMessage(IncidentHandlerCreateResources.status),
                minWidth: 100,
                maxWidth: 200,
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
        <>
            {isGeneratingInstructions && (
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
                    display: 'flex',
                    flexDirection: 'column',
                    margin: '20px',
                    gap: '20px',
                }}
            >
                <Field
                    id="handlerNameField"
                    label={intl.formatMessage(IncidentHandlerCreateResources.customHandlerName)}
                    orientation="vertical"
                    required={true}
                >
                    <Input
                        id="handlerName"
                        style={{ width: 600 }}
                        value={name}
                        onChange={(_event, newValue) => {
                            setName(newValue?.value);
                        }}
                        disabled={isGeneratingInstructions}
                    />
                </Field>

                <Field
                    id="handlerDescriptionField"
                    label={intl.formatMessage(IncidentHandlerCreateResources.customHandlerDescription)}
                    orientation="vertical"
                    required={false}
                >
                    <Input
                        id="handlerDescription"
                        style={{ width: 600 }}
                        value={description}
                        onChange={(_event, newValue) => {
                            setDescription(newValue?.value);
                        }}
                        disabled={isGeneratingInstructions}
                    />
                </Field>
                <Text variant="mediumPlus">{intl.formatMessage(IncidentHandlerCreateResources.chooseIncidentTitle)}</Text>
                <Text variant="medium">{intl.formatMessage(IncidentHandlerCreateResources.chooseIncidentDescription)}</Text>
                <Dropdown
                    options={timespanDropdownOptions}
                    selectedKey={selectedTimespanOption?.key}
                    onChange={(_e, option) => setSelectedTimespanOption(option)}
                    className={generateHandlerStyles.dropdown}
                    disabled={isGeneratingInstructions || loadingIncidents}
                />
                <MultipleSelectionTable
                    key="incidentSelectionTable"
                    selection={incidentsSelection.current!}
                    data={incidentDocuments}
                    loading={loadingIncidents}
                    columns={incidentTableColumns}
                    disabled={isGeneratingInstructions}
                />
                <Text variant="mediumPlus">{intl.formatMessage(IncidentHandlerCreateResources.chooseToolsTitle)}</Text>
                <Text variant="medium">{intl.formatMessage(IncidentHandlerCreateResources.chooseToolsDescription)}</Text>
                <MultipleSelectionTable
                    key="toolSelectionTable"
                    selection={toolsSelection.current!}
                    data={toolInfos}
                    loading={loadingTools}
                    columns={toolsTableColumns}
                    disabled={isGeneratingInstructions}
                />

                <Text variant="mediumPlus">{intl.formatMessage(IncidentHandlerCreateResources.customInstructionTitle)}</Text>
                <Text variant="medium">{intl.formatMessage(IncidentHandlerCreateResources.customInstructionDescription)}</Text>
                <TextField
                    placeholder={intl.formatMessage(IncidentHandlerCreateResources.customInstructionPlaceholder)}
                    value={customInstructions}
                    onChange={(_e, newValue) => setCustomInstructions(newValue ?? '')}
                    rows={8}
                    multiline
                    className={generateHandlerStyles.textField}
                    disabled={isGeneratingInstructions}
                />
                <div
                    style={{
                        display: 'flex',
                        gap: 10,
                    }}
                >
                    <Button
                        appearance="primary"
                        onClick={() => handleGenerateInstructions()}
                        disabled={(!customInstructions && selectedIncidents.length === 0) || isGeneratingInstructions}
                    >
                        {intl.formatMessage(IncidentHandlerCreateResources.next)}
                    </Button>
                    <Button onClick={() => setCurrentStep(IncidentHandlerCreateSteps.ReviewAndEdit)} disabled={isGeneratingInstructions}>
                        {intl.formatMessage(IncidentHandlerCreateResources.skip)}
                    </Button>
                    <Button onClick={() => exitToHome()}>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
                </div>
            </div>
        </>
    );
};

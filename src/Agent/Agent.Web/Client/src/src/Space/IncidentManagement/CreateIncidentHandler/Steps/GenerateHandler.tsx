import { IColumn } from '@fluentui/react';
import { Button, Dropdown, Field, Input, Option, Spinner, Text, Textarea } from '@fluentui/react-components';
import { useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentDocument, ToolInfo } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { MultipleSelectionShimmerDetailsList } from '../../../Components/MultipleSelectionShimmerDetailsList';
import { SelectedItemsList } from '../../../Components/SelectedItemsList';
import { generateHandlerStyles } from '../../../Styles/IncidentManagement.styles';
import { DirtyStateConfirmationWrapper } from '../DirtyStateConfirmationDialog';
import { IncidentHandlerCreateContext, IncidentHandlerCreateSteps, TimeDuration } from '../IncidentHandlerCreateContext';

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

export enum TimeDurationKey {
    Last15Days = 'last15Days',
    Last30Days = 'last30Days',
    Last60Days = 'last60Days',
    Last90Days = 'last90Days',
}

export const GenerateHandler = () => {
    const intl = useIntl();
    const context = useContext(IncidentHandlerCreateContext);
    const {
        isDirty,
        setCurrentStep,
        setGenerateInstructionsStepSkipped,
        exitToHome,
        name,
        onNameChange,
        description,
        onDescriptionChange,
        customInstructions,
        onCustomInstructionsChange,
        generatingInstructions,
        selectedTimespan,
        onSelectedTimespanChange,
        incidents,
        selectedIncidentIds,
        selectedIncidents,
        onSelectedIncidentsChange,
        toolsLoading,
        tools,
        selectedToolNames,
        onSelectedToolsChange,
        loadingIncidents,
        generateInstructions,
        incidentProcessingGuide,
        handlerLoaded,
        incidentsListDivRef,
        isLoadingInitialIncidents,
        hasMoreOldIncidents,
        loadMoreOldIncidents,
    } = context;

    const timespanDropdownOptions = useMemo(
        () => [
            {
                key: TimeDurationKey.Last15Days,
                value: TimeDuration.Last15Days,
                text: intl.formatMessage(IncidentHandlerCreateResources.last15days),
            },
            {
                key: TimeDurationKey.Last30Days,
                value: TimeDuration.Last30Days,
                text: intl.formatMessage(IncidentHandlerCreateResources.last30days),
            },
            {
                key: TimeDurationKey.Last60Days,
                value: TimeDuration.Last60Days,
                text: intl.formatMessage(IncidentHandlerCreateResources.last60days),
            },
            {
                key: TimeDurationKey.Last90Days,
                value: TimeDuration.Last90Days,
                text: intl.formatMessage(IncidentHandlerCreateResources.last90days),
            },
        ],
        [intl]
    );

    const selectedTimespanOption = useMemo(() => {
        return timespanDropdownOptions.find(option => option.value === selectedTimespan);
    }, [selectedTimespan, timespanDropdownOptions]);

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
                minWidth: 100,
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
            {generatingInstructions && (
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
                <Text size={300}>{intl.formatMessage(IncidentHandlerCreateResources.customHandlerCreateDescription)}</Text>
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
                            onNameChange(newValue?.value);
                        }}
                        disabled={generatingInstructions || !handlerLoaded}
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
                            onDescriptionChange(newValue?.value);
                        }}
                        disabled={generatingInstructions || !handlerLoaded}
                    />
                </Field>
                <Text size={400} weight="semibold">
                    {intl.formatMessage(IncidentHandlerCreateResources.chooseIncidentsTitle)}
                </Text>
                <Text size={300}>{intl.formatMessage(IncidentHandlerCreateResources.chooseIncidentDescription)}</Text>
                <Dropdown
                    id="timespanDropdown"
                    style={{ maxWidth: 300 }}
                    value={selectedTimespanOption?.text}
                    onOptionSelect={(_event, data) => {
                        const selectedOption = timespanDropdownOptions.find(option => option.key === data.optionValue);
                        onSelectedTimespanChange(selectedOption?.value || TimeDuration.Last60Days);
                    }}
                    disabled={generatingInstructions || loadingIncidents || !handlerLoaded}
                >
                    {timespanDropdownOptions.map(option => (
                        <Option value={option.key} checkIcon={null}>
                            {option.text}
                        </Option>
                    ))}
                </Dropdown>
                <div style={{ display: 'flex', flexDirection: 'row', gap: 20, marginTop: 10 }}>
                    <MultipleSelectionShimmerDetailsList
                        listContainerStyle={{ width: '100%' }}
                        ref={incidentsListDivRef}
                        data={incidents}
                        selectedKeys={selectedIncidentIds}
                        loading={loadingIncidents}
                        columns={incidentTableColumns}
                        disabled={generatingInstructions || !handlerLoaded}
                        onChange={onSelectedIncidentsChange}
                        getKey={(item: IncidentDocument) => item.id}
                        selectionLimit={5}
                        isLoadingInitialItems={isLoadingInitialIncidents}
                        loadMoreItems={loadMoreOldIncidents}
                        hasMoreItems={hasMoreOldIncidents}
                        isPicker
                    />
                    <SelectedItemsList
                        items={selectedIncidents || []}
                        onRemove={removedIncident =>
                            onSelectedIncidentsChange(selectedIncidentIds?.filter(incidentId => incidentId !== removedIncident.id) || [])
                        }
                        getItemTitle={incident => incident.title}
                        getItemId={incident => incident.id}
                        title={intl.formatMessage(IncidentHandlerCreateResources.selectedIncidents)}
                        emtpyText={intl.formatMessage(IncidentHandlerCreateResources.selectedIncidentsEmptyText)}
                    />
                </div>
                <Text size={400} weight="semibold">
                    {intl.formatMessage(IncidentHandlerCreateResources.chooseToolsTitle)}
                </Text>
                <Text size={300}>{intl.formatMessage(IncidentHandlerCreateResources.chooseToolsDescription)}</Text>
                <MultipleSelectionShimmerDetailsList
                    data={tools}
                    selectedKeys={selectedToolNames}
                    loading={toolsLoading}
                    columns={toolsTableColumns}
                    disabled={generatingInstructions || !handlerLoaded}
                    onChange={onSelectedToolsChange}
                    getKey={(item: ToolInfo) => item.name}
                    filter={(searchTerm, item) => {
                        return (
                            item.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
                            !!item.description?.toLowerCase().includes(searchTerm.toLowerCase())
                        );
                    }}
                />
                <Text size={400} weight="semibold">
                    {intl.formatMessage(IncidentHandlerCreateResources.customInstructionTitle)}
                </Text>
                <Textarea
                    placeholder={intl.formatMessage(IncidentHandlerCreateResources.customInstructionPlaceholder)}
                    value={customInstructions}
                    onChange={(_e, newValue) => onCustomInstructionsChange(newValue.value ?? '')}
                    rows={4}
                    className={generateHandlerStyles.textField}
                    disabled={generatingInstructions || !handlerLoaded}
                />
                <div
                    style={{
                        display: 'flex',
                        gap: 10,
                    }}
                >
                    <Button
                        appearance="primary"
                        onClick={() => generateInstructions()}
                        disabled={
                            !name || (!customInstructions && !selectedIncidentIds?.length) || generatingInstructions || !handlerLoaded
                        }
                    >
                        {intl.formatMessage(IncidentHandlerCreateResources.generate)}
                    </Button>
                    <Button
                        onClick={() => {
                            setGenerateInstructionsStepSkipped(true);
                            setCurrentStep(IncidentHandlerCreateSteps.ReviewAndEdit);
                        }}
                        disabled={!name || (!customInstructions && !incidentProcessingGuide) || generatingInstructions || !handlerLoaded}
                    >
                        {intl.formatMessage(IncidentHandlerCreateResources.skip)}
                    </Button>
                    <DirtyStateConfirmationWrapper isDirty={isDirty} onConfirm={exitToHome}>
                        <Button>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
                    </DirtyStateConfirmationWrapper>
                </div>
            </div>
        </>
    );
};

import { IColumn } from '@fluentui/react';
import { Button, Dropdown, Option, Spinner, Text, Textarea, tokens } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentDocument } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { MultipleSelectionShimmerDetailsList } from '../../../Components/MultipleSelectionShimmerDetailsList';
import { SelectedItemsList } from '../../../Components/SelectedItemsList';
import { generateHandlerStyles } from '../../../Styles/IncidentManagement.styles';
import { IncidentTableFieldNames, TimeDuration, TimeDurationKey } from '../Contracts';
import { DirtyStateConfirmationWrapper } from '../DirtyStateConfirmationDialog';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';

export const IncidentsAndGuidanceStep = () => {
    const intl = useIntl();
    const context = useContext(IncidentHandlerConsolidatedCreateContext);
    const {
        exitToHome,
        setCurrentStep,
        setGenerateInstructionsStepSkipped,
        selectedTimespan,
        onSelectedTimespanChange,
        incidents,
        selectedIncidents,
        onSelectedIncidentsChange,
        loadingIncidents,
        handlerLoaded,
        incidentsListDivRef,
        isLoadingInitialIncidents,
        hasMoreOldIncidents,
        loadMoreOldIncidents,
        generatingInstructions,
        generateInstructions,
    } = context;

    const { dirty, values, setFieldValue } = useFormikContext<IncidentHandlerCreateFormValues>();

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

    const incidentsTableHeight = useMemo(() => {
        const selectedIncidentsListHeaderHeight = 55;
        const selectedIncidentsListRowHeight = 54;
        const selectedIncidentsListHeight =
            (selectedIncidents?.length || 1) * selectedIncidentsListRowHeight + selectedIncidentsListHeaderHeight;

        const incidentsTableHeaderHeight = 42;
        const incidentsTableRowHeight = 32;
        const incidentsTableHeight = (incidents?.length || 0) * incidentsTableRowHeight + incidentsTableHeaderHeight;

        return incidentsTableHeight < selectedIncidentsListHeight ? 'fit-content' : undefined;
    }, [selectedIncidents, incidents]);

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
                    padding: '20px 20px',
                    gap: '20px',
                    height: 'calc(100% - 114px)',
                    overflowY: 'auto',
                }}
            >
                <Text size={300}>{intl.formatMessage(IncidentHandlerCreateResources.customHandlerCreateDescription)}</Text>
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
                    disabled={loadingIncidents || generatingInstructions || !handlerLoaded}
                >
                    {timespanDropdownOptions.map(option => (
                        <Option value={option.key} checkIcon={null}>
                            {option.text}
                        </Option>
                    ))}
                </Dropdown>
                <div style={{ display: 'flex', flexDirection: 'row', gap: 20, marginTop: 10, width: 'calc(100% - 3px)' }}>
                    <MultipleSelectionShimmerDetailsList
                        listContainerStyle={{
                            width: '100%',
                            height: incidentsTableHeight,
                        }}
                        ref={incidentsListDivRef}
                        data={incidents}
                        selectedKeys={values.incidentIds || []}
                        loading={loadingIncidents}
                        columns={incidentTableColumns}
                        disabled={!handlerLoaded || generatingInstructions}
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
                            onSelectedIncidentsChange(values.incidentIds?.filter(incidentId => incidentId !== removedIncident.id) || [])
                        }
                        getItemTitle={incident => incident.title}
                        getItemId={incident => incident.id}
                        title={intl.formatMessage(IncidentHandlerCreateResources.selectedIncidents)}
                        emptyText={intl.formatMessage(IncidentHandlerCreateResources.selectedIncidentsEmptyText)}
                        disabled={!handlerLoaded || generatingInstructions}
                    />
                </div>
                <Text size={400} weight="semibold">
                    {intl.formatMessage(IncidentHandlerCreateResources.addCustomInstructionTitle)}
                </Text>
                <Text size={300}>{intl.formatMessage(IncidentHandlerCreateResources.addCustomInstructionDescription)}</Text>
                <Textarea
                    placeholder={intl.formatMessage(IncidentHandlerCreateResources.customInstructionPlaceholder)}
                    value={values.customInstructions}
                    onChange={(_e, newValue) => setFieldValue('customInstructions', newValue.value ?? '')}
                    rows={4}
                    disabled={!handlerLoaded || generatingInstructions}
                    className={generateHandlerStyles.textField}
                />
            </div>
            <div
                style={{
                    display: 'flex',
                    gap: 10,
                    padding: 20,
                    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
                }}
            >
                <Button
                    onClick={() => {
                        setCurrentStep(IncidentHandlerCreateSteps.FilterStep);
                    }}
                    disabled={!handlerLoaded || generatingInstructions}
                >
                    {intl.formatMessage(IncidentHandlerCreateResources.back)}
                </Button>
                <Button
                    appearance="primary"
                    onClick={() => {
                        generateInstructions();
                    }}
                    disabled={!handlerLoaded || generatingInstructions}
                >
                    {intl.formatMessage(IncidentHandlerCreateResources.generate)}
                </Button>
                <Button
                    onClick={() => {
                        setGenerateInstructionsStepSkipped(true);
                        setCurrentStep(IncidentHandlerCreateSteps.ReviewAndTestStep);
                    }}
                    disabled={!handlerLoaded || generatingInstructions}
                >
                    {intl.formatMessage(IncidentHandlerCreateResources.skip)}
                </Button>
                <DirtyStateConfirmationWrapper isDirty={dirty} onConfirm={exitToHome}>
                    <Button disabled={!handlerLoaded || generatingInstructions}>
                        {intl.formatMessage(IncidentHandlerCreateResources.cancel)}
                    </Button>
                </DirtyStateConfirmationWrapper>
            </div>
        </>
    );
};

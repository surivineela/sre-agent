import { IColumn } from '@fluentui/react';
import { Button, Dropdown, Option, Spinner, Text, Textarea } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentDocument } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementType } from '../../../../Common/Contracts/Azure/SreAgent';
import { IncidentHandlerCreateResources, IncidentManagementResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { MultipleSelectionShimmerDetailsList } from '../../../Components/MultipleSelectionShimmerDetailsList';
import { SelectedItemsList } from '../../../Components/SelectedItemsList';
import { generateHandlerStyles, useIncidentManagementStyles } from '../../../Styles/IncidentManagement.styles';
import { IncidentTableFieldNames, TimeDuration, TimeDurationKey } from '../Contracts';
import { DirtyStateConfirmationWrapper } from '../DirtyStateConfirmationDialog';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';

export const DefineAgentLearningStep = () => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const context = useContext(IncidentHandlerConsolidatedCreateContext);
    const {
        incidentPlatformType,
        exitToHome,
        setCurrentStep,
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
        incidentTriggerWithLearningsMetadata,
    } = context;

    const { dirty, values, setFieldValue } = useFormikContext<IncidentHandlerCreateFormValues>();

    const timespanDropdownOptions = useMemo(() => {
        if (incidentPlatformType === IncidentManagementType.AzMonitor) {
            return [
                {
                    key: TimeDurationKey.Last1Day,
                    value: TimeDuration.Last1Day,
                    text: intl.formatMessage(IncidentHandlerCreateResources.last1day),
                },
                {
                    key: TimeDurationKey.Last7Days,
                    value: TimeDuration.Last7Days,
                    text: intl.formatMessage(IncidentHandlerCreateResources.last7days),
                },
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
            ];
        }

        return [
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
        ];
    }, [intl, incidentPlatformType]);

    const selectedTimespanOption = useMemo(() => {
        return timespanDropdownOptions.find(option => option.value === selectedTimespan);
    }, [selectedTimespan, timespanDropdownOptions]);

    const incidentTableColumns: IColumn[] = useMemo(() => {
        return [
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
            {
                key: IncidentTableFieldNames.Title,
                fieldName: IncidentTableFieldNames.Title,
                name: intl.formatMessage(IncidentHandlerCreateResources.title),
                minWidth: 100,
                isMultiline: true,
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
                    <Spinner size="large" aria-label={intl.formatMessage(IncidentManagementResources.generating)} />
                </div>
            )}
            <div className={styles.stepContent}>
                <div className={styles.stepContentSection}>
                    <Text size={300} weight="semibold" as="h2" style={{ margin: 0 }}>
                        {intl.formatMessage(IncidentHandlerCreateResources.chooseIncidentsTitle)}
                    </Text>
                    <Text size={300}>{intl.formatMessage(IncidentHandlerCreateResources.chooseIncidentDescription)}</Text>
                    <Dropdown
                        id="timespanDropdown"
                        style={{ maxWidth: 300 }}
                        value={selectedTimespanOption?.text}
                        onOptionSelect={(_event, data) => {
                            const selectedOption = timespanDropdownOptions.find(option => option.key === data.optionValue);
                            onSelectedTimespanChange(selectedOption?.value || TimeDuration.Last30Days);
                        }}
                        disabled={loadingIncidents || generatingInstructions || !handlerLoaded}
                        aria-label={intl.formatMessage(SreAgentResources.timeRange)}
                    >
                        {timespanDropdownOptions.map(option => (
                            <Option value={option.key} checkIcon={null}>
                                {option.text}
                            </Option>
                        ))}
                    </Dropdown>
                    <div style={{ display: 'flex', flexDirection: 'row', gap: 20, width: 'calc(100% - 3px)' }}>
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
                            getRemoveButtonAriaLabel={incident =>
                                intl.formatMessage(IncidentHandlerCreateResources.removeIncidentItem, {
                                    incidentId: incident.id,
                                    incidentTitle: incident.title,
                                })
                            }
                            title={intl.formatMessage(IncidentHandlerCreateResources.selectedIncidents)}
                            emptyText={intl.formatMessage(IncidentHandlerCreateResources.selectedIncidentsEmptyText)}
                            disabled={!handlerLoaded || generatingInstructions}
                        />
                    </div>
                </div>

                <div className={styles.stepContentSection}>
                    <Text size={300} weight="semibold" as="h2" style={{ margin: 0 }}>
                        {intl.formatMessage(IncidentHandlerCreateResources.addInstructionsTitle)}
                    </Text>
                    <Text size={300}>{intl.formatMessage(IncidentHandlerCreateResources.addInstructionsDescription)}</Text>
                    <Textarea
                        placeholder={intl.formatMessage(IncidentHandlerCreateResources.customInstructionPlaceholder)}
                        value={values.customInstructions}
                        onChange={(_e, newValue) => setFieldValue('customInstructions', newValue.value ?? '')}
                        rows={4}
                        disabled={!handlerLoaded || generatingInstructions}
                        className={generateHandlerStyles.textArea}
                        resize="vertical"
                    />
                </div>
            </div>

            <div className={styles.stepFooter}>
                <Button
                    onClick={() => {
                        setCurrentStep(
                            incidentTriggerWithLearningsMetadata
                                ? IncidentHandlerCreateSteps.IncidentTriggerStep
                                : IncidentHandlerCreateSteps.FilterStep
                        );
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
                    disabled={!handlerLoaded || generatingInstructions || (!values.customInstructions && selectedIncidents?.length === 0)}
                >
                    {intl.formatMessage(IncidentHandlerCreateResources.generateAndReview)}
                </Button>
                <Button
                    onClick={() => {
                        setCurrentStep(
                            incidentTriggerWithLearningsMetadata
                                ? IncidentHandlerCreateSteps.CreateSubagentStep
                                : IncidentHandlerCreateSteps.ReviewAndTestStep
                        );
                    }}
                    disabled={!handlerLoaded || generatingInstructions}
                >
                    {intl.formatMessage(IncidentHandlerCreateResources.next)}
                </Button>
                <DirtyStateConfirmationWrapper isDirty={dirty} onConfirm={() => exitToHome()}>
                    <Button disabled={!handlerLoaded || generatingInstructions}>
                        {intl.formatMessage(IncidentHandlerCreateResources.cancel)}
                    </Button>
                </DirtyStateConfirmationWrapper>
            </div>
        </>
    );
};

import { IColumn } from '@fluentui/react';
import { Button, Dropdown, Option, Text, tokens } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentDocument } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementType } from '../../../../Common/Contracts/Azure/SreAgent';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { MultipleSelectionShimmerDetailsList } from '../../../Components/MultipleSelectionShimmerDetailsList';
import { getPriorityOrSeverityStrings } from '../../Utilities';
import { IncidentTableFieldNames, TimeDuration, TimeDurationKey } from '../Contracts';
import { DirtyStateConfirmationWrapper } from '../DirtyStateConfirmationDialog';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';

export const PreviewIncidentsStep: FC = () => {
    const intl = useIntl();
    const { dirty } = useFormikContext<IncidentHandlerCreateFormValues>();

    const { incidentPlatformType, setCurrentStep, exitToHome, handlerLoaded, saveHandler, incidentsPreviewMetadata } = useContext(
        IncidentHandlerConsolidatedCreateContext
    );

    const {
        incidentsListDivRef,
        incidents,
        loadingIncidents,
        isLoadingInitialIncidents,
        loadMoreOldIncidents,
        hasMoreOldIncidents,
        selectedTimespan,
        onSelectedTimespanChange,
    } = incidentsPreviewMetadata;

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
        const { fieldLabel: priorityOrSeverityLabel } = getPriorityOrSeverityStrings(incidentPlatformType);
        return [
            {
                key: IncidentTableFieldNames.Priority,
                fieldName: IncidentTableFieldNames.Priority,
                name: intl.formatMessage(priorityOrSeverityLabel),
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
    }, [intl, incidentPlatformType]);

    return (
        <>
            <div
                style={{
                    display: 'flex',
                    flexDirection: 'column',
                    padding: '20px 20px 0px 20px',
                    height: 'calc(100% - 94px)',
                    overflowY: 'auto',
                    gap: 16,
                }}
            >
                <Text size={300}>{intl.formatMessage(IncidentHandlerCreateResources.previewIncidentsDescription)}</Text>
                <Dropdown
                    id="timespanDropdown"
                    style={{ maxWidth: 300 }}
                    value={selectedTimespanOption?.text}
                    onOptionSelect={(_event, data) => {
                        const selectedOption = timespanDropdownOptions.find(option => option.key === data.optionValue);
                        onSelectedTimespanChange(selectedOption?.value || TimeDuration.Last30Days);
                    }}
                    disabled={loadingIncidents || !handlerLoaded}
                >
                    {timespanDropdownOptions.map(option => (
                        <Option value={option.key} checkIcon={null}>
                            {option.text}
                        </Option>
                    ))}
                </Dropdown>
                <MultipleSelectionShimmerDetailsList
                    listContainerStyle={{
                        width: '100%',
                        minHeight: (incidents?.length || 0) < 4 ? 'fit-content' : '200px',
                        maxHeight: 'unset',
                    }}
                    ref={incidentsListDivRef}
                    data={incidents}
                    selectedKeys={[]}
                    loading={loadingIncidents}
                    columns={incidentTableColumns}
                    onChange={() => {}}
                    getKey={(item: IncidentDocument) => item.id}
                    selectionLimit={5}
                    isLoadingInitialItems={isLoadingInitialIncidents}
                    loadMoreItems={loadMoreOldIncidents}
                    hasMoreItems={hasMoreOldIncidents}
                    disallowSelection={true}
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
                >
                    {intl.formatMessage(IncidentHandlerCreateResources.back)}
                </Button>
                <Button appearance="primary" onClick={saveHandler} disabled={!dirty}>
                    {intl.formatMessage(IncidentHandlerCreateResources.save)}
                </Button>
                <DirtyStateConfirmationWrapper isDirty={dirty} onConfirm={exitToHome}>
                    <Button>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
                </DirtyStateConfirmationWrapper>
            </div>
        </>
    );
};

import { IColumn } from '@fluentui/react';
import { Button, Dropdown, Option, Text, tokens } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentDocument } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { MultipleSelectionShimmerDetailsList } from '../../../Components/MultipleSelectionShimmerDetailsList';
import { IncidentTableFieldNames, TimeDuration, TimeDurationKey } from '../Contracts';
import { DirtyStateConfirmationWrapper } from '../DirtyStateConfirmationDialog';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';

export const PreviewIncidentsStep: FC = () => {
    const intl = useIntl();
    const {
        setCurrentStep,
        exitToHome,
        incidentsListDivRef,
        incidents,
        loadingIncidents,
        isLoadingInitialIncidents,
        loadMoreOldIncidents,
        hasMoreOldIncidents,
        selectedTimespan,
        onSelectedTimespanChange,
        handlerLoaded,
        saveHandler,
    } = useContext(IncidentHandlerConsolidatedCreateContext);
    const { dirty } = useFormikContext<IncidentHandlerCreateFormValues>();

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

    return (
        <>
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
                <Text size={300}>{intl.formatMessage(IncidentHandlerCreateResources.previewIncidentsDescription)}</Text>
                <Dropdown
                    id="timespanDropdown"
                    style={{ maxWidth: 300 }}
                    value={selectedTimespanOption?.text}
                    onOptionSelect={(_event, data) => {
                        const selectedOption = timespanDropdownOptions.find(option => option.key === data.optionValue);
                        onSelectedTimespanChange(selectedOption?.value || TimeDuration.Last60Days);
                    }}
                    disabled={loadingIncidents || !handlerLoaded}
                >
                    {timespanDropdownOptions.map(option => (
                        <Option value={option.key} checkIcon={null}>
                            {option.text}
                        </Option>
                    ))}
                </Dropdown>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 16, height: 'calc(100% - 94px)' }}>
                    <MultipleSelectionShimmerDetailsList
                        listContainerStyle={{ width: '100%', minHeight: 'unset' }}
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

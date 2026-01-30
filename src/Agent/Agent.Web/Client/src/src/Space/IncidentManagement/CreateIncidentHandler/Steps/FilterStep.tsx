import {
    Button,
    Dropdown,
    Field,
    Input,
    MessageBar,
    Option,
    OptionOnSelectData,
    Skeleton,
    SkeletonItem,
    Text,
} from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentTriggerEvent } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementType } from '../../../../Common/Contracts/Azure/SreAgent';
import { IncidentHandlerCreateResources, IncidentManagementResources } from '../../../../Strings/SREAgentResources';
import { CopilotCheckbox as Checkbox } from '../../../Components/Common/CopilotCheckbox';
import { useIncidentManagementStyles } from '../../../Styles/IncidentManagement.styles';
import { IcmOwningTeamSearch } from '../../IcmOwningTeamSearch';
import { getPlatformSpecificStrings } from '../../Utilities';
import { FilterConflictWarning } from '../Common/FilterConflictWarning';
import { TriggerTypeSelector } from '../Common/TriggerTypeSelector';
import { DirtyStateConfirmationWrapper } from '../DirtyStateConfirmationDialog';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';

export const FilterStep: FC = () => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const {
        filterMode,
        exitToHome,
        setCurrentStep,
        incidentTypeOptions,
        impactedServiceOptions,
        priorityOptions,
        incidentPlatformType,
        conflictingFilters,
        filterFieldOptionsLoading,
    } = useContext(IncidentHandlerConsolidatedCreateContext);
    const { values, setFieldValue, setFieldTouched, dirty } = useFormikContext<IncidentHandlerCreateFormValues>();

    const platformSpecificStrings = useMemo(() => getPlatformSpecificStrings(incidentPlatformType), [incidentPlatformType]);

    const incidentTypeOptionsExtended = useMemo(() => {
        const options = [];
        if (incidentPlatformType !== IncidentManagementType.Icm) {
            options.push({ key: 'ALL', display: intl.formatMessage(IncidentManagementResources.allIncidentTypes) });
        }
        incidentTypeOptions.forEach(option => options.push({ key: option, display: option }));
        return options;
    }, [incidentTypeOptions, intl, incidentPlatformType]);

    const selectedIncidentTypeDisplay = useMemo(() => {
        const key = values.incidentType || (filterMode === 'edit' ? 'ALL' : '');
        const selectedOption = incidentTypeOptionsExtended.find(option => option.key === key);
        return selectedOption ? selectedOption.display : '';
    }, [incidentTypeOptionsExtended, values.incidentType, filterMode]);

    const impactedServiceOptionsExtended = useMemo(() => {
        const options = [{ key: 'ALL', display: intl.formatMessage(IncidentManagementResources.allImpactedServices) }];
        impactedServiceOptions.forEach(option => options.push({ key: option, display: option }));
        return options;
    }, [impactedServiceOptions, intl]);

    const selectedImpactedServiceDisplay = useMemo(() => {
        const key = values.impactedService || (filterMode === 'edit' ? 'ALL' : '');
        const selectedOption = impactedServiceOptionsExtended.find(option => option.key === key);
        return selectedOption ? selectedOption.display : '';
    }, [impactedServiceOptionsExtended, values.impactedService, filterMode]);

    const priorityOptionsExtended = useMemo(() => {
        const options = [{ key: 'ALL', display: intl.formatMessage(platformSpecificStrings.severityOrPriorityAllOptionLabel) }];
        priorityOptions.forEach(option => options.push({ key: option, display: option }));
        return options;
    }, [priorityOptions, intl, platformSpecificStrings]);

    const selectedPriorityDisplay = useMemo(() => {
        if (values.priorities == null) {
            return filterMode === 'edit' ? intl.formatMessage(platformSpecificStrings.severityOrPriorityAllOptionLabel) : '';
        }
        if (values.priorities.length === 0 || values.priorities.includes('ALL')) {
            return intl.formatMessage(platformSpecificStrings.severityOrPriorityAllOptionLabel);
        }
        const selectedOptionDisplayValues: string[] = [];
        values.priorities.forEach(priority => {
            const option = priorityOptionsExtended.find(option => option.key === priority);
            if (option) {
                selectedOptionDisplayValues.push(option.display);
            }
        });
        return selectedOptionDisplayValues.join(', ');
    }, [priorityOptionsExtended, values.priorities, filterMode, intl, platformSpecificStrings]);

    const onPriorityOptionSelect = useCallback(
        (data: OptionOnSelectData) => {
            const { optionValue, selectedOptions } = data;

            if (optionValue === 'ALL') {
                if (selectedOptions.includes('ALL')) {
                    // "ALL" was checked so set all priorities as selected
                    setFieldValue('priorities', ['ALL', ...priorityOptions.sort()]);
                } else {
                    // "ALL" was unchecked so clear all priorities
                    setFieldValue('priorities', []);
                }
                return;
            }

            if (!priorityOptions.some(opt => !selectedOptions.includes(opt))) {
                // All individual priorities are selected, so ensure "ALL" is also selected
                if (!selectedOptions.includes('ALL')) {
                    setFieldValue('priorities', ['ALL', ...selectedOptions.sort()]);
                }
                return;
            }

            // Some individual priorities are not selected, so ensure "ALL" is not selected
            const newPriorities = selectedOptions.filter(opt => opt !== 'ALL').sort();
            setFieldValue('priorities', newPriorities);
        },
        [setFieldValue, priorityOptions, priorityOptionsExtended]
    );

    // Get current triggers or default to IncidentCreatedOrTransferred
    const currentTriggers = useMemo(() => {
        return values.triggers || [IncidentTriggerEvent.IncidentCreatedOrTransferred];
    }, [values.triggers]);

    const handleTriggersChange = useCallback(
        (triggers: IncidentTriggerEvent[]) => {
            setFieldValue('triggers', triggers);
        },
        [setFieldValue]
    );

    const isNextDisabled = useMemo((): boolean => {
        if (filterMode === 'create') {
            return (
                !values.filterName ||
                !values.priorities ||
                (incidentPlatformType !== IncidentManagementType.AzMonitor && (!values.impactedService || !values.incidentType))
            );
        }

        if (incidentPlatformType === IncidentManagementType.Icm && (!values.owningTeamId || !values.incidentType)) {
            return true;
        }

        return false;
    }, [
        filterMode,
        incidentPlatformType,
        values.filterName,
        values.owningTeamId,
        values.impactedService,
        values.priorities,
        values.incidentType,
    ]);

    return (
        <>
            <div className={styles.stepContent}>
                <div className={styles.filterStepContentSection}>
                    {filterMode === 'edit' && (
                        <MessageBar intent="info" layout="multiline">
                            {intl.formatMessage(IncidentManagementResources.editIncidentHandlerDescription)}
                        </MessageBar>
                    )}

                    {incidentPlatformType === IncidentManagementType.Icm && <FilterConflictWarning conflicts={conflictingFilters ?? []} />}

                    <Field label={intl.formatMessage(IncidentManagementResources.incidentHandlerName)} required>
                        <Input
                            name="filterName"
                            value={values.filterName}
                            onChange={(_, data) => setFieldValue('filterName', data.value)}
                            placeholder={intl.formatMessage(IncidentManagementResources.incidentHandlerNamePlaceholder)}
                            disabled={filterMode === 'edit'}
                            className={styles.inputField}
                        />
                    </Field>
                </div>

                <div className={styles.filterStepContentSection}>
                    <Text size={300} weight="semibold" as="h2" style={{ margin: 0 }}>
                        {intl.formatMessage(IncidentHandlerCreateResources.filterParametersTitle)}
                    </Text>
                    <Text size={300}>{intl.formatMessage(IncidentHandlerCreateResources.filterParametersDescription)}</Text>

                    {incidentPlatformType === IncidentManagementType.Icm && (
                        <div className={styles.filterStepContentSection}>
                            <IcmOwningTeamSearch
                                defaultTeamId={values.owningTeamId}
                                onFieldTouched={() => setFieldTouched('owningTeamId', true)}
                                onUpdateOwningTeam={team => setFieldValue('owningTeamId', `${team.id}`)}
                                comboboxClassName={styles.inputField}
                            />

                            <Field label={intl.formatMessage(IncidentManagementResources.monitorId)}>
                                <Input
                                    name={'monitorId'}
                                    value={values.monitorId}
                                    onChange={(_, data) => setFieldValue('monitorId', data.value)}
                                    placeholder={intl.formatMessage(IncidentManagementResources.monitorIdPlaceholder)}
                                    className={styles.inputField}
                                />
                            </Field>

                            <Field label={intl.formatMessage(IncidentManagementResources.createdBy)}>
                                <Input
                                    name={'createdBy'}
                                    value={values.createdBy}
                                    onChange={(_, data) => setFieldValue('createdBy', data.value)}
                                    placeholder={intl.formatMessage(IncidentManagementResources.createdByPlaceholder)}
                                    className={styles.inputField}
                                />
                            </Field>

                            <TriggerTypeSelector
                                selectedTriggers={currentTriggers}
                                onTriggersChange={handleTriggersChange}
                                owningTeamId={values.owningTeamId}
                            />
                        </div>
                    )}

                    {incidentPlatformType !== IncidentManagementType.AzMonitor && (
                        <div className={styles.filterStepContentSection}>
                            <Field label={intl.formatMessage(IncidentManagementResources.incidentType)} required>
                                {filterFieldOptionsLoading ? (
                                    <Skeleton className={styles.inputField}>
                                        <SkeletonItem style={{ height: 32 }} />
                                    </Skeleton>
                                ) : (
                                    <Dropdown
                                        name="incidentType"
                                        selectedOptions={values.incidentType ? [values.incidentType] : []}
                                        value={selectedIncidentTypeDisplay}
                                        onOptionSelect={(_, data) => setFieldValue('incidentType', data.optionValue)}
                                        onBlur={() => setFieldTouched('incidentType', true)}
                                        placeholder={intl.formatMessage(IncidentManagementResources.chooseIncidentType)}
                                        className={styles.inputField}
                                    >
                                        {incidentTypeOptionsExtended.map(option => (
                                            <Option value={option.key} key={option.key}>
                                                {option.display}
                                            </Option>
                                        ))}
                                    </Dropdown>
                                )}
                            </Field>

                            <Field label={intl.formatMessage(IncidentManagementResources.impactedService)} required>
                                {filterFieldOptionsLoading ? (
                                    <Skeleton className={styles.inputField}>
                                        <SkeletonItem style={{ height: 32 }} />
                                    </Skeleton>
                                ) : (
                                    <Dropdown
                                        placeholder={intl.formatMessage(IncidentManagementResources.chooseImpactedService)}
                                        name={'impactedService'}
                                        value={selectedImpactedServiceDisplay}
                                        selectedOptions={values.impactedService ? [values.impactedService] : []}
                                        onOptionSelect={(_, data) => setFieldValue('impactedService', data.optionValue)}
                                        onBlur={() => {
                                            setFieldTouched('impactedService', true);
                                        }}
                                        className={styles.inputField}
                                    >
                                        {impactedServiceOptionsExtended.map(option => (
                                            <Option value={option.key} key={option.key}>
                                                {option.display}
                                            </Option>
                                        ))}
                                    </Dropdown>
                                )}
                            </Field>
                        </div>
                    )}

                    <Field label={intl.formatMessage(platformSpecificStrings.severityOrPriorityLabel)} required>
                        {filterFieldOptionsLoading ? (
                            <Skeleton className={styles.inputField}>
                                <SkeletonItem style={{ height: 32 }} />
                            </Skeleton>
                        ) : (
                            <Dropdown
                                multiselect
                                placeholder={intl.formatMessage(platformSpecificStrings.severityOrPriorityPlaceholder)}
                                name={'priorities'}
                                value={selectedPriorityDisplay}
                                selectedOptions={values.priorities || []}
                                onOptionSelect={(_, data) => onPriorityOptionSelect(data)}
                                onBlur={() => setFieldTouched('priorities', true)}
                                className={styles.inputField}
                            >
                                {priorityOptionsExtended.map(option => (
                                    <Option value={option.key} key={option.key}>
                                        {option.display}
                                    </Option>
                                ))}
                            </Dropdown>
                        )}
                    </Field>

                    <Field label={intl.formatMessage(IncidentManagementResources.titleContains)}>
                        <Input
                            name={'titleContains'}
                            value={values.titleContains}
                            onChange={(_, data) => setFieldValue('titleContains', data.value)}
                            placeholder={intl.formatMessage(IncidentManagementResources.titlePlaceholder)}
                            className={styles.inputField}
                        />
                    </Field>
                </div>

                <div className={styles.filterStepContentSection}>
                    <Text size={300} weight="semibold" as="h2" style={{ margin: 0 }}>
                        {intl.formatMessage(IncidentHandlerCreateResources.addCustomResponseGuidanceTitle)}
                    </Text>
                    <Text size={300} id="add-custom-response-guidance-description">
                        {intl.formatMessage(IncidentHandlerCreateResources.addCustomResponseGuidanceDescription)}
                    </Text>
                    <Checkbox
                        name={'useCustomHandler'}
                        checked={values.useCustomHandler}
                        onChange={(_, data) => setFieldValue('useCustomHandler', data.checked)}
                        label={intl.formatMessage(IncidentHandlerCreateResources.addCustomResponseGuidanceLabel)}
                        labelPosition="after"
                        aria-describedby="add-custom-response-guidance-description"
                    />
                </div>
            </div>
            <div className={styles.stepFooter}>
                <Button
                    appearance="primary"
                    onClick={() => {
                        setCurrentStep(
                            values.useCustomHandler
                                ? IncidentHandlerCreateSteps.DefineAgentLearningStep
                                : IncidentHandlerCreateSteps.PreviewIncidentsStep
                        );
                    }}
                    disabled={isNextDisabled}
                >
                    {intl.formatMessage(IncidentHandlerCreateResources.next)}
                </Button>
                <DirtyStateConfirmationWrapper isDirty={dirty} onConfirm={() => exitToHome()}>
                    <Button>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
                </DirtyStateConfirmationWrapper>
            </div>
        </>
    );
};

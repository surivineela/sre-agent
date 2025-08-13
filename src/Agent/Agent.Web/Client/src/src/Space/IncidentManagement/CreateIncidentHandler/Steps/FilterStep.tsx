import { Button, Checkbox, Dropdown, Field, Input, MessageBar, Option, Radio, RadioGroup, Text, tokens } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { AgentMode } from '../../../../Common/Contracts/Azure/SreAgent';
import { IncidentHandlerCreateResources, IncidentManagementResources } from '../../../../Strings/SREAgentResources';
import { DirtyStateConfirmationWrapper } from '../DirtyStateConfirmationDialog';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';

export const FilterStep: FC = () => {
    const intl = useIntl();

    const { filterMode, exitToHome, setCurrentStep, incidentTypeOptions, impactedServiceOptions, priorityOptions } = useContext(
        IncidentHandlerConsolidatedCreateContext
    );
    const { values, setFieldValue, setFieldTouched, dirty } = useFormikContext<IncidentHandlerCreateFormValues>();

    const incidentTypeOptionsExtended = useMemo(() => {
        const options = [{ key: 'ALL', display: intl.formatMessage(IncidentManagementResources.allIncidentTypes) }];
        incidentTypeOptions.forEach(option => options.push({ key: option, display: option }));
        return options;
    }, [incidentTypeOptions, intl]);

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
        const options = [{ key: 'ALL', display: intl.formatMessage(IncidentManagementResources.allPriorities) }];
        priorityOptions.forEach(option => options.push({ key: option, display: option }));
        return options;
    }, [priorityOptions, intl]);

    const selectedPriorityDisplay = useMemo(() => {
        const key = values.priority || (filterMode === 'edit' ? 'ALL' : '');
        const selectedOption = priorityOptionsExtended.find(option => option.key === key);
        return selectedOption ? selectedOption.display : '';
    }, [priorityOptionsExtended, values.priority, filterMode]);

    const isNextDisabled = useMemo((): boolean => {
        return filterMode === 'create' && (!values.filterName || !values.impactedService || !values.priority || !values.incidentType);
    }, [filterMode, values.filterName, values.impactedService, values.priority, values.incidentType]);

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
                <div style={{ paddingBottom: '10px' }}>
                    {filterMode === 'edit' ? (
                        <MessageBar intent="info">
                            {intl.formatMessage(IncidentManagementResources.editIncidentHandlerDescription)}
                        </MessageBar>
                    ) : (
                        <>{intl.formatMessage(IncidentManagementResources.createIncidentHandlerDescription)}</>
                    )}
                </div>

                <form style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                    <Field label={intl.formatMessage(IncidentManagementResources.incidentHandlerName)} required>
                        <Input
                            name="filterName"
                            value={values.filterName}
                            onChange={(_, data) => setFieldValue('filterName', data.value)}
                            placeholder={intl.formatMessage(IncidentManagementResources.incidentHandlerNamePlaceholder)}
                            disabled={filterMode === 'edit'}
                        />
                    </Field>

                    <Text size={400} weight="semibold">
                        {intl.formatMessage(IncidentHandlerCreateResources.filterParametersTitle)}
                    </Text>
                    <Text size={300}>{intl.formatMessage(IncidentHandlerCreateResources.filterParametersDescription)}</Text>

                    <Field label={intl.formatMessage(IncidentManagementResources.incidentType)} required>
                        <Dropdown
                            name="incidentType"
                            selectedOptions={values.incidentType ? [values.incidentType] : []}
                            value={selectedIncidentTypeDisplay}
                            onOptionSelect={(_, data) => setFieldValue('incidentType', data.optionValue)}
                            onBlur={() => setFieldTouched('incidentType', true)}
                            placeholder={intl.formatMessage(IncidentManagementResources.chooseIncidentType)}
                        >
                            {incidentTypeOptionsExtended.map(option => (
                                <Option value={option.key} key={option.key}>
                                    {option.display}
                                </Option>
                            ))}
                        </Dropdown>
                    </Field>

                    <Field label={intl.formatMessage(IncidentManagementResources.impactedService)} required>
                        <Dropdown
                            placeholder={intl.formatMessage(IncidentManagementResources.chooseImpactedService)}
                            name={'impactedService'}
                            value={selectedImpactedServiceDisplay}
                            selectedOptions={values.impactedService ? [values.impactedService] : []}
                            onOptionSelect={(_, data) => setFieldValue('impactedService', data.optionValue)}
                            onBlur={() => {
                                setFieldTouched('impactedService', true);
                            }}
                        >
                            {impactedServiceOptionsExtended.map(option => (
                                <Option value={option.key} key={option.key}>
                                    {option.display}
                                </Option>
                            ))}
                        </Dropdown>
                    </Field>

                    <Field label={intl.formatMessage(IncidentManagementResources.priority)} required>
                        <Dropdown
                            placeholder={intl.formatMessage(IncidentManagementResources.choosePriority)}
                            name={'priority'}
                            value={selectedPriorityDisplay}
                            onBlur={() => setFieldTouched('priority', true)}
                            selectedOptions={values.priority ? [values.priority] : []}
                            onOptionSelect={(_, data) => setFieldValue('priority', data.optionValue)}
                        >
                            {priorityOptionsExtended.map(option => (
                                <Option value={option.key} key={option.key}>
                                    {option.display}
                                </Option>
                            ))}
                        </Dropdown>
                    </Field>

                    <Field label={intl.formatMessage(IncidentManagementResources.titleContains)}>
                        <Input
                            name={'titleContains'}
                            value={values.titleContains}
                            onChange={(_, data) => setFieldValue('titleContains', data.value)}
                            placeholder={intl.formatMessage(IncidentManagementResources.titlePlaceholder)}
                        />
                    </Field>
                    <Field label={intl.formatMessage(IncidentManagementResources.agentAutonomyLevel)}>
                        <RadioGroup
                            name="agentMode"
                            value={values.agentMode}
                            onChange={(_, data) => setFieldValue('agentMode', data.value)}
                        >
                            <Radio
                                value={AgentMode.review}
                                label={
                                    <>
                                        {intl.formatMessage(IncidentManagementResources.reviewDefault)}
                                        <br />
                                        <Text size={200}>
                                            {intl.formatMessage(IncidentManagementResources.autonomyLevelReviewDescription)}
                                        </Text>
                                    </>
                                }
                            />
                            <Radio
                                value={AgentMode.autonomous}
                                label={
                                    <>
                                        {intl.formatMessage(IncidentManagementResources.autonomousWord)}
                                        <br />
                                        <Text size={200}>
                                            {intl.formatMessage(IncidentManagementResources.autonomyLevelAutonomousDescription)}
                                        </Text>
                                    </>
                                }
                            />
                        </RadioGroup>
                    </Field>
                    <Checkbox
                        name={'useCustomHandler'}
                        checked={values.useCustomHandler}
                        onChange={(_, data) => setFieldValue('useCustomHandler', data.checked)}
                        label={intl.formatMessage(IncidentHandlerCreateResources.addCustomInstructions)}
                        labelPosition="after"
                    />
                </form>
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
                    appearance="primary"
                    onClick={() => {
                        setCurrentStep(
                            values.useCustomHandler
                                ? IncidentHandlerCreateSteps.IncidentsAndGuidanceStep
                                : IncidentHandlerCreateSteps.PreviewIncidentsStep
                        );
                    }}
                    disabled={isNextDisabled}
                >
                    {intl.formatMessage(IncidentHandlerCreateResources.next)}
                </Button>
                <DirtyStateConfirmationWrapper isDirty={dirty} onConfirm={exitToHome}>
                    <Button>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
                </DirtyStateConfirmationWrapper>
            </div>
        </>
    );
};

import { Button, Dropdown, Field, Input, Option, Radio, RadioGroup, Text, tokens } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { AgentMode, IncidentManagementType } from '../../../../Common/Contracts/Azure/SreAgent';
import {
    ExtendedAgentsGraphResources,
    IncidentHandlerCreateResources,
    IncidentManagementResources,
} from '../../../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../../../Styles/IncidentManagement.styles';
import { IcmOwningTeamSearch } from '../../IcmOwningTeamSearch';
import { getPlatformSpecificStrings } from '../../Utilities';
import { DirtyStateConfirmationWrapper } from '../DirtyStateConfirmationDialog';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';

export const IncidentTriggerStep: FC = () => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();

    const {
        subAgentNames,
        filterMode,
        exitToHome,
        setCurrentStep,
        incidentTypeOptions,
        impactedServiceOptions,
        priorityOptions,
        incidentPlatformType,
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
        const key = values.priority || (filterMode === 'edit' ? 'ALL' : '');
        const selectedOption = priorityOptionsExtended.find(option => option.key === key);
        return selectedOption ? selectedOption.display : '';
    }, [priorityOptionsExtended, values.priority, filterMode]);

    const disableAllFields = useMemo(
        () => !incidentPlatformType || incidentPlatformType === IncidentManagementType.None,
        [incidentPlatformType]
    );

    const isNextDisabled = useMemo((): boolean => {
        if (filterMode === 'create') {
            const handlingAgentRequired = !values.isIncidentTriggerWithLearnings && !values.handlingAgent;
            return (
                !values.filterName ||
                !values.priority ||
                handlingAgentRequired ||
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
        values.priority,
        values.incidentType,
        values.handlingAgent,
        values.isIncidentTriggerWithLearnings,
    ]);

    return (
        <>
            <div
                style={{
                    display: 'flex',
                    flexDirection: 'column',
                    padding: '20px 20px',
                    gap: '32px',
                    height: 'calc(100% - 114px)',
                    overflowY: 'auto',
                }}
            >
                <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                    <Text size={400} weight="semibold" as="h2" style={{ margin: 0 }}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.triggerDetails)}
                    </Text>

                    <Field label={intl.formatMessage(ExtendedAgentsGraphResources.incidentTriggerName)} required>
                        <Input
                            name="filterName"
                            value={values.filterName}
                            onChange={(_, data) => setFieldValue('filterName', data.value)}
                            placeholder={intl.formatMessage(ExtendedAgentsGraphResources.incidentTriggerNamePlaceholder)}
                            disabled={disableAllFields || filterMode === 'edit'}
                            className={styles.inputField}
                        />
                    </Field>

                    {incidentPlatformType === IncidentManagementType.Icm && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                            <IcmOwningTeamSearch
                                defaultTeamId={values.owningTeamId}
                                defaultTeamName={values.owningTeamName}
                                onFieldTouched={() => setFieldTouched('owningTeamId', true)}
                                onUpdateOwningTeam={team => {
                                    setFieldValue('owningTeamId', `${team.id}`);
                                    setFieldValue('owningTeamName', team.name);
                                }}
                                disabled={disableAllFields}
                                comboboxClassName={styles.inputField}
                            />
                        </div>
                    )}

                    {incidentPlatformType !== IncidentManagementType.AzMonitor && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                            <Field label={intl.formatMessage(IncidentManagementResources.incidentType)} required>
                                <Dropdown
                                    name="incidentType"
                                    selectedOptions={values.incidentType ? [values.incidentType] : []}
                                    value={selectedIncidentTypeDisplay}
                                    onOptionSelect={(_, data) => setFieldValue('incidentType', data.optionValue)}
                                    onBlur={() => setFieldTouched('incidentType', true)}
                                    placeholder={intl.formatMessage(IncidentManagementResources.chooseIncidentType)}
                                    disabled={disableAllFields}
                                    className={styles.inputField}
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
                                    disabled={disableAllFields}
                                    className={styles.inputField}
                                >
                                    {impactedServiceOptionsExtended.map(option => (
                                        <Option value={option.key} key={option.key}>
                                            {option.display}
                                        </Option>
                                    ))}
                                </Dropdown>
                            </Field>
                        </div>
                    )}

                    <Field label={intl.formatMessage(platformSpecificStrings.severityOrPriorityLabel)} required>
                        <Dropdown
                            placeholder={intl.formatMessage(platformSpecificStrings.severityOrPriorityPlaceholder)}
                            name={'priority'}
                            value={selectedPriorityDisplay}
                            onBlur={() => setFieldTouched('priority', true)}
                            selectedOptions={values.priority ? [values.priority] : []}
                            onOptionSelect={(_, data) => setFieldValue('priority', data.optionValue)}
                            disabled={disableAllFields}
                            className={styles.inputField}
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
                            value={values.titleContains || ''}
                            onChange={(_, data) => setFieldValue('titleContains', data.value)}
                            placeholder={intl.formatMessage(IncidentManagementResources.titlePlaceholder)}
                            disabled={disableAllFields}
                            className={styles.inputField}
                        />
                    </Field>
                    {incidentPlatformType === IncidentManagementType.Icm && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                            <Field label={intl.formatMessage(IncidentManagementResources.monitorId)}>
                                <Input
                                    name={'monitorId'}
                                    value={values.monitorId}
                                    onChange={(_, data) => setFieldValue('monitorId', data.value)}
                                    placeholder={intl.formatMessage(IncidentManagementResources.monitorIdPlaceholder)}
                                    disabled={disableAllFields}
                                    className={styles.inputField}
                                />
                            </Field>

                            <Field label={intl.formatMessage(IncidentManagementResources.createdBy)}>
                                <Input
                                    name={'createdBy'}
                                    value={values.createdBy}
                                    onChange={(_, data) => setFieldValue('createdBy', data.value)}
                                    placeholder={intl.formatMessage(IncidentManagementResources.createdByPlaceholder)}
                                    disabled={disableAllFields}
                                    className={styles.inputField}
                                />
                            </Field>
                        </div>
                    )}
                </div>
                {!values.isIncidentTriggerWithLearnings && (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                        <Text size={400} weight="semibold" as="h2" style={{ margin: 0 }}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.subagent)}
                        </Text>
                        <Field label={intl.formatMessage(ExtendedAgentsGraphResources.responseSubagent)} required>
                            <Dropdown
                                name="handlingAgent"
                                selectedOptions={values.handlingAgent ? [values.handlingAgent] : []}
                                value={values.handlingAgent || ''}
                                onOptionSelect={(_, data) => {
                                    setFieldValue('handlingAgent', data.optionValue);
                                }}
                                onBlur={() => setFieldTouched('handlingAgent', true)}
                                placeholder={intl.formatMessage(ExtendedAgentsGraphResources.responseSubagentPlaceholder)}
                                disabled={disableAllFields}
                                className={styles.inputField}
                            >
                                {subAgentNames?.map(name => (
                                    <Option value={name} key={name}>
                                        {name}
                                    </Option>
                                ))}
                            </Dropdown>
                        </Field>
                        <Field label={intl.formatMessage(IncidentManagementResources.agentAutonomyLevel)}>
                            <RadioGroup
                                name="agentMode"
                                value={values.agentMode}
                                onChange={(_, data) => setFieldValue('agentMode', data.value)}
                                disabled={disableAllFields}
                            >
                                <Radio
                                    value={AgentMode.autonomous}
                                    label={
                                        <>
                                            {intl.formatMessage(IncidentManagementResources.autonomousDefault)}
                                            <br />
                                            <Text size={200}>
                                                {intl.formatMessage(IncidentManagementResources.autonomyLevelAutonomousDescription)}
                                            </Text>
                                        </>
                                    }
                                />
                                <Radio
                                    value={AgentMode.review}
                                    label={
                                        <>
                                            {intl.formatMessage(IncidentManagementResources.reviewWord)}
                                            <br />
                                            <Text size={200}>
                                                {intl.formatMessage(IncidentManagementResources.autonomyLevelReviewDescription)}
                                            </Text>
                                        </>
                                    }
                                />
                            </RadioGroup>
                        </Field>
                    </div>
                )}
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
                    onClick={() =>
                        setCurrentStep(
                            values.isIncidentTriggerWithLearnings
                                ? IncidentHandlerCreateSteps.IncidentsAndGuidanceStep
                                : IncidentHandlerCreateSteps.PreviewIncidentsStep
                        )
                    }
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

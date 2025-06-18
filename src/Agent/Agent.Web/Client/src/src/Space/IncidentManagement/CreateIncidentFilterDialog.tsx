import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Dropdown,
    Field,
    Input,
    MessageBar,
    Option,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { Formik, FormikHelpers, useFormikContext } from 'formik';
import { Dispatch, FC, useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentFilterPayload } from '../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementResources, SreAgentResources } from '../../Strings/SREAgentResources';

interface CreateIncidentFilterProps {
    isDialogOpen: boolean;
    setIsDialogOpen: Dispatch<React.SetStateAction<boolean>>;
    createIncidentFilter: (incidentFilter: IncidentFilterPayload) => Promise<void>;
    updateIncidentFilter: (incidentFilter: IncidentFilterPayload) => Promise<void>;
    priorityOptions: string[];
    impactedServiceOptions: string[];
    incidentTypeOptions: string[];
    initialValues?: IncidentFilterFormProps;
    isEditMode: boolean;
}

interface CreateOrUpdateIncidentFilterFormProps {
    isDialogOpen: boolean;
    setIsDialogOpen: Dispatch<React.SetStateAction<boolean>>;
    priorityOptions: string[];
    impactedServiceOptions: string[];
    incidentTypeOptions: string[];
    isEditMode: boolean;
}

export interface IncidentFilterFormProps {
    id: string;
    impactedService: string;
    priority: string;
    incidentType: string;
    titleContains?: string;
}

export const CreateOrUpdateIncidentFilterDialog: FC<CreateIncidentFilterProps> = ({
    isDialogOpen,
    setIsDialogOpen,
    createIncidentFilter,
    updateIncidentFilter,
    priorityOptions,
    impactedServiceOptions,
    incidentTypeOptions,
    initialValues,
    isEditMode = false,
}) => {
    const initialFormValues = useMemo((): IncidentFilterFormProps => {
        if (isEditMode && initialValues) {
            return {
                id: initialValues.id || '',
                impactedService: initialValues.impactedService || '',
                priority: initialValues.priority || '',
                incidentType: initialValues.incidentType || '',
                titleContains: initialValues.titleContains || '',
            };
        }

        return {
            id: '',
            titleContains: '',
            impactedService: '',
            priority: '',
            incidentType: '',
        };
    }, [isEditMode, initialValues]);

    const handleSubmit = useCallback(
        async (values: IncidentFilterFormProps, formikHelpers: FormikHelpers<IncidentFilterFormProps>) => {
            const incidentFilter: IncidentFilterPayload = {
                Id: values.id,
                ImpactedService: values.impactedService === 'ALL' ? undefined : values.impactedService,
                Priority: values.priority === 'ALL' ? undefined : values.priority,
                IncidentType: values.incidentType === 'ALL' ? undefined : values.incidentType,
                TitleContains: values.titleContains,
            };

            if (isEditMode) {
                await updateIncidentFilter(incidentFilter);
            } else {
                await createIncidentFilter(incidentFilter);
            }

            formikHelpers.resetForm();
            setIsDialogOpen(false);
        },
        [createIncidentFilter, isEditMode, setIsDialogOpen, updateIncidentFilter]
    );

    return (
        <Formik<IncidentFilterFormProps> initialValues={initialFormValues} enableReinitialize={true} onSubmit={handleSubmit}>
            <CreateOrUpdateFilterForm
                isDialogOpen={isDialogOpen}
                setIsDialogOpen={setIsDialogOpen}
                isEditMode={isEditMode}
                incidentTypeOptions={incidentTypeOptions}
                impactedServiceOptions={impactedServiceOptions}
                priorityOptions={priorityOptions}
            />
        </Formik>
    );
};

const CreateOrUpdateFilterForm = ({
    isDialogOpen,
    setIsDialogOpen,
    isEditMode,
    incidentTypeOptions,
    impactedServiceOptions,
    priorityOptions,
}: CreateOrUpdateIncidentFilterFormProps) => {
    const intl = useIntl();

    const { initialValues, values, setFieldValue, setFieldTouched, submitForm, resetForm } = useFormikContext<IncidentFilterFormProps>();

    const incidentTypeOptionsExtended = useMemo(() => {
        const options = [{ key: 'ALL', display: intl.formatMessage(IncidentManagementResources.allIncidentTypes) }];
        incidentTypeOptions.forEach(option => options.push({ key: option, display: option }));
        return options;
    }, [incidentTypeOptions]);
    const selectedIncidentTypeDisplay = useMemo(() => {
        const key = values.incidentType || (isEditMode ? 'ALL' : '');
        const selectedOption = incidentTypeOptionsExtended.find(option => option.key === key);
        return selectedOption ? selectedOption.display : '';
    }, [incidentTypeOptionsExtended, values.incidentType, isEditMode]);

    const impactedServiceOptionsExtended = useMemo(() => {
        const options = [{ key: 'ALL', display: intl.formatMessage(IncidentManagementResources.allImpactedServices) }];
        impactedServiceOptions.forEach(option => options.push({ key: option, display: option }));
        return options;
    }, [impactedServiceOptions]);
    const selectedImpactedServiceDisplay = useMemo(() => {
        const key = values.impactedService || (isEditMode ? 'ALL' : '');
        const selectedOption = impactedServiceOptionsExtended.find(option => option.key === key);
        return selectedOption ? selectedOption.display : '';
    }, [impactedServiceOptionsExtended, values.impactedService, isEditMode]);

    const priorityOptionsExtended = useMemo(() => {
        const options = [{ key: 'ALL', display: intl.formatMessage(IncidentManagementResources.allPriorities) }];
        priorityOptions.forEach(option => options.push({ key: option, display: option }));
        return options;
    }, [priorityOptions]);
    const selectedPriorityDisplay = useMemo(() => {
        const key = values.priority || (isEditMode ? 'ALL' : '');
        const selectedOption = priorityOptionsExtended.find(option => option.key === key);
        return selectedOption ? selectedOption.display : '';
    }, [priorityOptionsExtended, values.priority, isEditMode]);

    const isSaveDisabled = useMemo((): boolean => {
        return !isEditMode
            ? !values.id || !values.impactedService || !values.priority || !values.incidentType
            : initialValues.impactedService === values.impactedService &&
                  initialValues.priority === values.priority &&
                  initialValues.incidentType === values.incidentType &&
                  initialValues.titleContains === values.titleContains;
    }, [
        isEditMode,
        values.id,
        values.impactedService,
        values.priority,
        values.incidentType,
        values.titleContains,
        initialValues.impactedService,
        initialValues.priority,
        initialValues.incidentType,
        initialValues.titleContains,
    ]);

    return (
        <Dialog open={isDialogOpen} onOpenChange={(_, data) => setIsDialogOpen(data.open)}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle
                        action={<Button appearance="transparent" icon={<Dismiss24Regular />} onClick={() => setIsDialogOpen(false)} />}
                    >
                        {isEditMode
                            ? intl.formatMessage(IncidentManagementResources.editIncidentHandler)
                            : intl.formatMessage(IncidentManagementResources.createIncidentHandler)}
                    </DialogTitle>

                    <DialogContent>
                        <div style={{ paddingBottom: '10px' }}>
                            {isEditMode ? (
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
                                    name="id"
                                    value={values.id}
                                    onChange={(_, data) => setFieldValue('id', data.value)}
                                    placeholder={intl.formatMessage(IncidentManagementResources.incidentHandlerNamePlaceholder)}
                                    disabled={isEditMode}
                                />
                            </Field>

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
                                    name="titleContains"
                                    value={values.titleContains}
                                    onChange={(_, data) => setFieldValue('titleContains', data.value)}
                                    placeholder={intl.formatMessage(IncidentManagementResources.titlePlaceholder)}
                                />
                            </Field>
                        </form>
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="primary" type="submit" onClick={() => submitForm()} disabled={isSaveDisabled}>
                            {isEditMode ? intl.formatMessage(SreAgentResources.update) : intl.formatMessage(SreAgentResources.save)}
                        </Button>
                        <Button
                            appearance="secondary"
                            onClick={() => {
                                setIsDialogOpen(false);
                                resetForm();
                            }}
                        >
                            {intl.formatMessage(SreAgentResources.cancel)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

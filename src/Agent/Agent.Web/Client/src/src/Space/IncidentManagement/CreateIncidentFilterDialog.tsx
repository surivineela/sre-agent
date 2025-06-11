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
    Option,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { Formik, FormikHelpers } from 'formik';
import { Dispatch, FC } from 'react';
import { useIntl } from 'react-intl';
import { IncidentFilterPayload } from '../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementResources, SreAgentResources } from '../../Strings/SREAgentResources';

interface CreateIncidentFilterProps {
    isDialogOpen: boolean;
    setIsDialogOpen: Dispatch<React.SetStateAction<boolean>>;
    createIncidentFilter: (incidentFilter: IncidentFilterPayload) => Promise<void>;
    priorityOptions: string[];
    impactedServiceOptions: string[];
    incidentTypeOptions: string[];
}

interface CreateIncidentFilterFormProps {
    id: string;
    impactedService: string;
    priority: string;
    incidentType: string;
    titleContains?: string;
}

export const CreateIncidentFilterDialog: FC<CreateIncidentFilterProps> = ({
    isDialogOpen,
    setIsDialogOpen,
    createIncidentFilter,
    priorityOptions,
    impactedServiceOptions,
    incidentTypeOptions,
}) => {
    const intl = useIntl();

    return (
        <Formik<CreateIncidentFilterFormProps>
            initialValues={{
                id: '',
                titleContains: '',
                impactedService: '',
                priority: '',
                incidentType: '',
            }}
            onSubmit={async (values: CreateIncidentFilterFormProps, formikHelpers: FormikHelpers<CreateIncidentFilterFormProps>) => {
                const incidentFilter: IncidentFilterPayload = {
                    Id: values.id,
                    ImpactedService: values.impactedService,
                    Priority: values.priority,
                    IncidentType: values.incidentType,
                    TitleContains: values.titleContains,
                };
                createIncidentFilter(incidentFilter).then(() => formikHelpers.resetForm());
                setIsDialogOpen(false);
            }}
        >
            {({ values, setFieldTouched, setFieldValue, resetForm, submitForm }) => (
                <Dialog open={isDialogOpen} onOpenChange={(_, data) => setIsDialogOpen(data.open)}>
                    <DialogSurface>
                        <DialogBody>
                            <DialogTitle
                                action={
                                    <Button appearance="transparent" icon={<Dismiss24Regular />} onClick={() => setIsDialogOpen(false)} />
                                }
                            >
                                {intl.formatMessage(IncidentManagementResources.createIncidentHandler)}
                            </DialogTitle>

                            <DialogContent>
                                <form style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                                    <Field label={intl.formatMessage(IncidentManagementResources.incidentHandlerName)} required>
                                        <Input
                                            name="id"
                                            value={values.id}
                                            onChange={(_, data) => setFieldValue('id', data.value)}
                                            placeholder={intl.formatMessage(IncidentManagementResources.incidentHandlerNamePlaceholder)}
                                        />
                                    </Field>

                                    <Field label={intl.formatMessage(IncidentManagementResources.incidentType)}>
                                        <Dropdown
                                            name="incidentType"
                                            selectedOptions={values.incidentType ? [values.incidentType] : []}
                                            value={values.incidentType}
                                            onOptionSelect={(_, data) => setFieldValue('incidentType', data.optionValue)}
                                            onBlur={() => setFieldTouched('incidentType', true)}
                                            placeholder={intl.formatMessage(IncidentManagementResources.selectIncidentType)}
                                        >
                                            {incidentTypeOptions.map(option => (
                                                <Option value={option} key={option}>
                                                    {option}
                                                </Option>
                                            ))}
                                        </Dropdown>
                                    </Field>

                                    <Field label={intl.formatMessage(IncidentManagementResources.impactedService)}>
                                        <Dropdown
                                            placeholder={intl.formatMessage(IncidentManagementResources.selectImpactedService)}
                                            name={'impactedService'}
                                            value={values.impactedService}
                                            selectedOptions={values.impactedService ? [values.impactedService] : []}
                                            onOptionSelect={(_, data) => setFieldValue('impactedService', data.optionValue)}
                                            onBlur={() => {
                                                setFieldTouched('impactedService', true);
                                            }}
                                        >
                                            {impactedServiceOptions.map(option => (
                                                <Option value={option} key={option}>
                                                    {option}
                                                </Option>
                                            ))}
                                        </Dropdown>
                                    </Field>

                                    <Field label={intl.formatMessage(IncidentManagementResources.priority)}>
                                        <Dropdown
                                            placeholder={intl.formatMessage(IncidentManagementResources.selectPriority)}
                                            name={'priority'}
                                            value={values.priority}
                                            onBlur={() => setFieldTouched('priority', true)}
                                            selectedOptions={values.priority ? [values.priority] : []}
                                            onOptionSelect={(_, data) => setFieldValue('priority', data.optionValue)}
                                        >
                                            {priorityOptions.map(option => (
                                                <Option key={option} value={option}>
                                                    {option}
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
                                <Button appearance="primary" type="submit" onClick={() => submitForm()} disabled={!values.id}>
                                    {intl.formatMessage(SreAgentResources.save)}
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
            )}
        </Formik>
    );
};

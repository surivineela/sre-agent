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
import { Formik, FormikHelpers, useFormikContext } from 'formik';
import { Dispatch, FC, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { DataConnector } from '../../Common/Contracts/Azure/SreAgent';
import { DataConnectionsResources, SreAgentResources } from '../../Strings/SREAgentResources';

interface CreateDataConnectorProps {
    isDialogOpen: boolean;
    setIsDialogOpen: Dispatch<React.SetStateAction<boolean>>;
    createDataConnector: (dataConnector: DataConnector) => Promise<void>;
    updateDataConnector: (dataConnector: DataConnector) => Promise<void>;
    identities: string[];
    isEditMode: boolean;
    initialValues?: DataConnectorFormProps;
    isOperationInProgress?: boolean;
    existingDataConnectors?: DataConnector[];
}

interface CreateOrUpdateDataConnectorFormProps {
    isDialogOpen: boolean;
    setIsDialogOpen: Dispatch<React.SetStateAction<boolean>>;
    isEditMode: boolean;
    identities: string[];
    isOperationInProgress?: boolean;
    existingDataConnectors?: DataConnector[];
}

export interface DataConnectorFormProps {
    name: string;
    dataConnectorType: string;
    dataSource: string;
    identity: string;
}

export const CreateOrUpdateDataConnectorDialog: FC<CreateDataConnectorProps> = ({
    isDialogOpen,
    setIsDialogOpen,
    createDataConnector,
    updateDataConnector,
    identities,
    initialValues,
    isEditMode = false,
    isOperationInProgress = false,
    existingDataConnectors,
}) => {
    const initialFormValues = useMemo((): DataConnectorFormProps => {
        if (isEditMode && initialValues) {
            return {
                name: initialValues.name || '',
                dataConnectorType: initialValues.dataConnectorType || '',
                dataSource: initialValues.dataSource || '',
                identity: initialValues.identity || '',
            };
        }

        return {
            name: '',
            dataConnectorType: '',
            dataSource: '',
            identity: '',
        };
    }, [isEditMode, initialValues]);

    const handleSubmit = useCallback(
        async (values: DataConnectorFormProps, formikHelpers: FormikHelpers<DataConnectorFormProps>) => {
            const dataConnector: DataConnector = {
                name: values.name,
                dataConnectorType: values.dataConnectorType,
                dataSource: values.dataSource,
                identity: values.identity,
            };

            setIsDialogOpen(false);
            if (isEditMode) {
                await updateDataConnector(dataConnector);
            } else {
                await createDataConnector(dataConnector);
            }

            formikHelpers.resetForm();
        },
        [createDataConnector, isEditMode, setIsDialogOpen, updateDataConnector]
    );

    return (
        <Formik<DataConnectorFormProps> initialValues={initialFormValues} enableReinitialize={true} onSubmit={handleSubmit}>
            <CreateOrUpdateDataConnectorForm
                isDialogOpen={isDialogOpen}
                setIsDialogOpen={setIsDialogOpen}
                isEditMode={isEditMode}
                identities={identities}
                isOperationInProgress={isOperationInProgress}
                existingDataConnectors={existingDataConnectors}
            />
        </Formik>
    );
};

const CreateOrUpdateDataConnectorForm = ({
    isDialogOpen,
    setIsDialogOpen,
    isEditMode,
    identities,
    isOperationInProgress = false,
    existingDataConnectors,
}: CreateOrUpdateDataConnectorFormProps) => {
    const intl = useIntl();
    const [nameError, setNameError] = useState<string | undefined>();

    const { values, setFieldValue, submitForm, resetForm } = useFormikContext<DataConnectorFormProps>();

    const identityOptions = useMemo(() => {
        return (
            identities?.map(resourceId => {
                const parts = resourceId.split('/');
                const name = parts[parts.length - 1] || resourceId;
                return {
                    id: resourceId,
                    name: name,
                };
            }) ?? []
        );
    }, [identities]);

    useEffect(() => {
        if (!values.name || isEditMode) {
            setNameError(undefined);
            return;
        }

        const isDuplicate = existingDataConnectors?.some(connector => connector.name.toLowerCase() === values.name.toLowerCase());
        setNameError(isDuplicate ? intl.formatMessage(DataConnectionsResources.duplicateNameError) : undefined);
    }, [values.name, existingDataConnectors, isEditMode, intl]);

    useEffect(() => {
        // Auto-select the first identity if there's only one option and no current selection
        if (identityOptions.length === 1 && !values.identity) {
            setFieldValue('identity', identityOptions[0].id);
        }
    }, [identityOptions, values.identity, setFieldValue]);

    const isSaveDisabled = useMemo((): boolean => {
        return !values.name || !values.dataConnectorType || !values.dataSource || !values.identity || isOperationInProgress || !!nameError;
    }, [values.name, values.dataConnectorType, values.dataSource, values.identity, isOperationInProgress, nameError]);

    return (
        <Dialog open={isDialogOpen} onOpenChange={(_, data) => setIsDialogOpen(data.open)}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle
                        action={<Button appearance="transparent" icon={<Dismiss24Regular />} onClick={() => setIsDialogOpen(false)} />}
                    >
                        {isEditMode
                            ? intl.formatMessage(DataConnectionsResources.editDataConnection)
                            : intl.formatMessage(DataConnectionsResources.createDataConnection)}
                    </DialogTitle>
                    <DialogContent>
                        <form style={{ display: 'flex', flexDirection: 'column', gap: 16, paddingBottom: 16 }}>
                            <Field
                                label={intl.formatMessage(DataConnectionsResources.name)}
                                required
                                validationState={nameError ? 'error' : 'none'}
                                validationMessage={nameError}
                            >
                                <Input
                                    name="name"
                                    value={values.name}
                                    onChange={(_, data) => setFieldValue('name', data.value)}
                                    placeholder={intl.formatMessage(DataConnectionsResources.namePlaceholder)}
                                    disabled={isOperationInProgress}
                                />
                            </Field>

                            <Field label={intl.formatMessage(DataConnectionsResources.dataConnectionType)} required>
                                <Input
                                    name="dataConnectorType"
                                    value={values.dataConnectorType}
                                    onChange={(_, data) => setFieldValue('dataConnectorType', data.value)}
                                    placeholder={intl.formatMessage(DataConnectionsResources.typePlaceholder)}
                                    disabled={isOperationInProgress}
                                />
                            </Field>

                            <Field label={intl.formatMessage(DataConnectionsResources.dataSource)} required>
                                <Input
                                    name="dataSource"
                                    value={values.dataSource}
                                    onChange={(_, data) => setFieldValue('dataSource', data.value)}
                                    placeholder={intl.formatMessage(DataConnectionsResources.dataSourcePlaceholder)}
                                    disabled={isOperationInProgress}
                                />
                            </Field>

                            <Field label={intl.formatMessage(DataConnectionsResources.identity)} required>
                                <Dropdown
                                    name="identity"
                                    value={identityOptions.find(option => option.id === values.identity)?.name || ''}
                                    onOptionSelect={(_, data) => {
                                        const selectedOption = identityOptions.find(option => option.name === data.optionText);
                                        if (selectedOption) {
                                            setFieldValue('identity', selectedOption.id);
                                        }
                                    }}
                                    placeholder={intl.formatMessage(DataConnectionsResources.identityPlaceholder)}
                                    disabled={isOperationInProgress}
                                >
                                    {identityOptions.map(option => (
                                        <Option key={option.id} value={option.name}>
                                            {option.name}
                                        </Option>
                                    ))}
                                </Dropdown>
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
                            disabled={isOperationInProgress}
                        >
                            {intl.formatMessage(SreAgentResources.cancel)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

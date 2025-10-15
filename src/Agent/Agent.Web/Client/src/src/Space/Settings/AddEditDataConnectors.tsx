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
    Link,
    Option,
    OptionGroup,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { Formik, FormikHelpers, useFormikContext } from 'formik';
import { Dispatch, FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ApiVersions } from '../../Common/ApiVersions';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { MsiIdentity } from '../../Common/Contracts/Azure/ArmObj';
import { DataConnector } from '../../Common/Contracts/Azure/SreAgent';
import { DataConnectorsResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { IdentityKeys, IdentityType } from '../Contracts/Identity';
import { IdentityStatus } from './Identity.ReactView';

const connectorTypeOptions = [{ id: 'Kusto' }, { id: 'TsgCrawler' }, { id: 'KustoDataIndexer' }];
const kustoDataSourceExample = 'https://cluster-url/database-name';

interface CreateDataConnectorProps {
    isDialogOpen: boolean;
    setIsDialogOpen: Dispatch<React.SetStateAction<boolean>>;
    createDataConnector: (dataConnector: DataConnector) => Promise<void>;
    updateDataConnector: (dataConnector: DataConnector) => Promise<void>;
    agentIdentity?: MsiIdentity;
    isEditMode: boolean;
    initialValues?: DataConnectorFormProps;
    isOperationInProgress?: boolean;
    existingDataConnectors?: DataConnector[];
    refreshAgent: () => void;
}

interface CreateOrUpdateDataConnectorFormProps {
    isDialogOpen: boolean;
    setIsDialogOpen: Dispatch<React.SetStateAction<boolean>>;
    isEditMode: boolean;
    agentIdentity?: MsiIdentity;
    isOperationInProgress?: boolean;
    existingDataConnectors?: DataConnector[];
    refreshAgent: () => void;
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
    agentIdentity,
    initialValues,
    isEditMode = false,
    isOperationInProgress = false,
    existingDataConnectors,
    refreshAgent,
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
                agentIdentity={agentIdentity}
                isOperationInProgress={isOperationInProgress}
                existingDataConnectors={existingDataConnectors}
                refreshAgent={refreshAgent}
            />
        </Formik>
    );
};

const CreateOrUpdateDataConnectorForm = ({
    isDialogOpen,
    setIsDialogOpen,
    isEditMode,
    agentIdentity,
    isOperationInProgress = false,
    existingDataConnectors,
    refreshAgent,
}: CreateOrUpdateDataConnectorFormProps) => {
    const intl = useIntl();
    const azPortalContext = useContext(AzPortalContext);
    const { resourceId } = useContext(EnvironmentContext);

    const [nameError, setNameError] = useState<string | undefined>();
    const [dataSourceError, setDataSourceError] = useState<string | undefined>();

    const { initialValues, values, setFieldValue, submitForm, resetForm } = useFormikContext<DataConnectorFormProps>();

    const dataSourcePlaceholder = useMemo(() => {
        if (values.dataConnectorType === 'Kusto') {
            return kustoDataSourceExample;
        }

        return intl.formatMessage(DataConnectorsResources.dataSourcePlaceholder);
    }, [intl, values.dataConnectorType]);

    const isSystemAssignedIdentityEnabled = useMemo(() => {
        return agentIdentity?.type.toLowerCase().includes(IdentityType.systemAssigned.toLowerCase());
    }, [agentIdentity]);

    const userAssignedIdentityOptions = useMemo(() => {
        const userAssignedOptions: { id: string; name: string }[] = [];

        const userAssignedIdentityRscIds = agentIdentity?.userAssignedIdentities ? Object.keys(agentIdentity.userAssignedIdentities) : [];
        if (userAssignedIdentityRscIds.length > 0) {
            userAssignedIdentityRscIds.forEach(resourceId => {
                const parts = resourceId.split('/');
                const name = parts[parts.length - 1] || resourceId;
                userAssignedOptions.push({
                    id: resourceId,
                    name: name,
                });
            });
        }

        return userAssignedOptions;
    }, [agentIdentity]);

    const openIdentityBlade = useCallback(async () => {
        const bladeClosedPromise = azPortalContext.openBlade({
            extension: 'Microsoft_Azure_ManagedServiceIdentity',
            detailBlade: 'AzureResourceIdentitiesBladeV2',
            detailBladeInputs: {
                resourceId,
                apiVersion: ApiVersions.microsoftAppApiVersion20250501Preview,
                systemAssignedStatus: IdentityStatus.Supported,
                userAssignedStatus: IdentityStatus.Supported,
            },
        });

        await bladeClosedPromise;

        refreshAgent();
    }, [azPortalContext, resourceId, refreshAgent]);

    useEffect(() => {
        if (!values.name) {
            setNameError(undefined);
            return;
        }

        const isDuplicate = existingDataConnectors?.some(
            connector =>
                connector.name.toLowerCase() === values.name.toLowerCase() &&
                (!isEditMode || connector.name.toLowerCase() !== initialValues.name.toLowerCase())
        );
        setNameError(isDuplicate ? intl.formatMessage(DataConnectorsResources.duplicateNameError) : undefined);
    }, [values.name, initialValues.name, existingDataConnectors, isEditMode, intl]);

    useEffect(() => {
        if (!values.dataSource || values.dataConnectorType !== 'Kusto') {
            setDataSourceError(undefined);
            return;
        }

        let isValidUri = false;
        try {
            const url = new URL(values.dataSource);
            isValidUri = url.protocol === 'https:' && !!url.host.trim() && !!url.pathname && url.pathname.trim() !== '/';
        } catch {
            isValidUri = false;
        }

        setDataSourceError(
            !isValidUri
                ? intl.formatMessage(DataConnectorsResources.dataSourceKustoFormatError, { format: kustoDataSourceExample })
                : undefined
        );
    }, [values.dataConnectorType, values.dataSource, intl]);

    useEffect(() => {
        // Auto-select the first identity if there's only one option and no current selection
        const allSelectableOptions = [...userAssignedIdentityOptions];
        if (isSystemAssignedIdentityEnabled) {
            allSelectableOptions.unshift({
                id: IdentityKeys.system,
                name: 'filler',
            });
        }

        if (allSelectableOptions.length === 1 && !values.identity) {
            setFieldValue('identity', allSelectableOptions[0].id);
        }
    }, [isSystemAssignedIdentityEnabled, userAssignedIdentityOptions, values.identity, setFieldValue]);

    const isSaveDisabled = useMemo((): boolean => {
        return (
            !values.name ||
            !values.dataConnectorType ||
            !values.dataSource ||
            !values.identity ||
            isOperationInProgress ||
            !!nameError ||
            !!dataSourceError
        );
    }, [values.name, values.dataConnectorType, values.dataSource, values.identity, isOperationInProgress, nameError, dataSourceError]);

    return (
        <Dialog open={isDialogOpen} onOpenChange={(_, data) => setIsDialogOpen(data.open)}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle
                        action={
                            <Button
                                appearance="transparent"
                                icon={<Dismiss24Regular />}
                                onClick={() => setIsDialogOpen(false)}
                                aria-label={intl.formatMessage(SreAgentResources.close)}
                            />
                        }
                    >
                        {isEditMode
                            ? intl.formatMessage(DataConnectorsResources.editDataConnector)
                            : intl.formatMessage(DataConnectorsResources.createDataConnector)}
                    </DialogTitle>
                    <DialogContent>
                        <form style={{ display: 'flex', flexDirection: 'column', gap: 16, paddingBottom: 16 }}>
                            <Field
                                label={intl.formatMessage(DataConnectorsResources.name)}
                                required
                                validationState={nameError ? 'error' : 'none'}
                                validationMessage={nameError}
                            >
                                <Input
                                    name="name"
                                    value={values.name}
                                    onChange={(_, data) => setFieldValue('name', data.value)}
                                    placeholder={intl.formatMessage(DataConnectorsResources.namePlaceholder)}
                                    disabled={isOperationInProgress}
                                />
                            </Field>

                            <Field label={intl.formatMessage(DataConnectorsResources.dataConnectorType)} required>
                                <Dropdown
                                    name="dataConnectorType"
                                    value={connectorTypeOptions.find(option => option.id === values.dataConnectorType)?.id || ''}
                                    onOptionSelect={(_, data) => {
                                        const selectedOption = connectorTypeOptions.find(option => option.id === data.optionValue);
                                        if (selectedOption) {
                                            setFieldValue('dataConnectorType', selectedOption.id);
                                        }
                                    }}
                                    placeholder={intl.formatMessage(DataConnectorsResources.typePlaceholder)}
                                    disabled={isOperationInProgress}
                                >
                                    {connectorTypeOptions.map(option => (
                                        <Option key={option.id} value={option.id}>
                                            {option.id}
                                        </Option>
                                    ))}
                                </Dropdown>
                            </Field>

                            <Field
                                label={intl.formatMessage(DataConnectorsResources.dataSource)}
                                validationState={dataSourceError ? 'error' : 'none'}
                                validationMessage={dataSourceError}
                                required
                            >
                                <Input
                                    name="dataSource"
                                    value={values.dataSource}
                                    onChange={(_, data) => setFieldValue('dataSource', data.value)}
                                    placeholder={dataSourcePlaceholder}
                                    disabled={isOperationInProgress}
                                />
                            </Field>

                            <Field label={intl.formatMessage(DataConnectorsResources.identity)} required>
                                <Dropdown
                                    name="identity"
                                    value={
                                        values.identity === IdentityKeys.system
                                            ? intl.formatMessage(SreAgentResources.systemAssigned)
                                            : userAssignedIdentityOptions.find(option => option.id === values.identity)?.name || ''
                                    }
                                    onOptionSelect={(_, data) => {
                                        if (data.optionValue) {
                                            setFieldValue('identity', data.optionValue);
                                        }
                                    }}
                                    placeholder={intl.formatMessage(DataConnectorsResources.identityPlaceholder)}
                                    disabled={isOperationInProgress}
                                >
                                    {isSystemAssignedIdentityEnabled && (
                                        <Option key={IdentityKeys.system} value={IdentityKeys.system}>
                                            {intl.formatMessage(SreAgentResources.systemAssigned)}
                                        </Option>
                                    )}

                                    {userAssignedIdentityOptions.length > 0 && (
                                        <OptionGroup label={intl.formatMessage(SreAgentResources.userAssigned)}>
                                            {userAssignedIdentityOptions.map(option => (
                                                <Option key={option.id} value={option.id}>
                                                    {option.name}
                                                </Option>
                                            ))}
                                        </OptionGroup>
                                    )}
                                </Dropdown>
                                <Link onClick={openIdentityBlade} style={{ marginTop: '8px', display: 'block', fontSize: '14px' }}>
                                    {intl.formatMessage(SreAgentResources.addIdentity)}
                                </Link>
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

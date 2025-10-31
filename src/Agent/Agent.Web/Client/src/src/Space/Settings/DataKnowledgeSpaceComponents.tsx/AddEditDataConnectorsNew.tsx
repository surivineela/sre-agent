import {
    Button,
    Card,
    CardHeader,
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
    SearchBox,
    Text,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { Formik, FormikHelpers, useFormikContext } from 'formik';
import { Dispatch, FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ApiVersions } from '../../../Common/ApiVersions';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { MsiIdentity } from '../../../Common/Contracts/Azure/ArmObj';
import { Connector } from '../../../Common/Contracts/Azure/SreAgent';
import { resolveResourceIcon } from '../../../Common/Helpers/Resources';
import { DataConnectorsResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { IdentityKeys, IdentityType } from '../../Contracts/Identity';
import { IdentityStatus } from '../Identity.ReactView';
import { useAddEditConnectorsStyles } from '../Styles/DataKnowledgeSpace.styles';

export interface ConnectorTypeOption {
    id: string;
    name: string;
    service: string;
    description: string;
    img: string;
}

export enum ConnectorType {
    AzureDataExplorerQuery = 'Kusto',
    AzureDataExplorerIndexing = 'KustoDataIndexer',
    AzureDevOpsDocumentation = 'TsgCrawler',
}

export const getConnectorTypeOptions = (intl: any): ConnectorTypeOption[] => [
    {
        id: ConnectorType.AzureDataExplorerQuery,
        name: intl.formatMessage(DataConnectorsResources.databaseQueryConnector),
        service: 'Azure Data Explorer',
        description: intl.formatMessage(DataConnectorsResources.predefinedQueriesDescription),
        img: resolveResourceIcon('AzureDataExplorer'),
    },
    {
        id: ConnectorType.AzureDataExplorerIndexing,
        name: intl.formatMessage(DataConnectorsResources.databaseIndexingConnector),
        service: 'Azure Data Explorer',
        description: intl.formatMessage(DataConnectorsResources.queryGenerationDescription),
        img: resolveResourceIcon('AzureDataExplorer'),
    },
    {
        id: ConnectorType.AzureDevOpsDocumentation,
        name: intl.formatMessage(DataConnectorsResources.documentationConnector),
        service: 'Azure DevOps',
        description: intl.formatMessage(DataConnectorsResources.documentationDescription),
        img: resolveResourceIcon('AzureDevOps'),
    },
    /**
    {
        id: 'GitHubDocumentation',
        name: intl.formatMessage(DataConnectorsResources.documentationConnector),
        service: 'GitHub',
        description: intl.formatMessage(DataConnectorsResources.documentationDescription),
        img: resolveResourceIcon('Github'),
    },
    */
];
const kustoDataSourceExample = 'https://cluster-url/database-name';

interface CreateDataConnectorProps {
    isDialogOpen: boolean;
    setIsDialogOpen: Dispatch<React.SetStateAction<boolean>>;
    onSubmit: (dataConnector: Connector) => void;
    agentIdentity?: MsiIdentity;
    isEditMode: boolean;
    initialValues?: DataConnectorFormProps;
    isOperationInProgress?: boolean;
    existingDataConnectors?: Connector[];
    refreshAgent: () => void;
}

interface CreateOrUpdateDataConnectorFormProps {
    isDialogOpen: boolean;
    setIsDialogOpen: Dispatch<React.SetStateAction<boolean>>;
    isEditMode: boolean;
    agentIdentity?: MsiIdentity;
    isOperationInProgress?: boolean;
    existingDataConnectors?: Connector[];
    refreshAgent: () => void;
    selectedConnectorType?: ConnectorTypeOption | null;
    onBackToSelection?: () => void;
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
    onSubmit,
    agentIdentity,
    initialValues,
    isEditMode = false,
    isOperationInProgress = false,
    existingDataConnectors,
    refreshAgent,
}) => {
    const intl = useIntl();
    const styles = useAddEditConnectorsStyles();
    const [showCardSelection, setShowCardSelection] = useState(!isEditMode);
    const [selectedConnectorType, setSelectedConnectorType] = useState<ConnectorTypeOption | null>(null);
    const [searchText, setSearchText] = useState('');
    const connectorTypeOptions = useMemo(() => getConnectorTypeOptions(intl), [intl]);

    const filteredConnectorOptions = useMemo(() => {
        return connectorTypeOptions.filter((option: ConnectorTypeOption) => {
            const searchLower = searchText.toLowerCase().trim();
            const matchesSearch =
                !searchLower ||
                option.name.toLowerCase().includes(searchLower) ||
                option.service.toLowerCase().includes(searchLower) ||
                option.description.toLowerCase().includes(searchLower);

            return matchesSearch;
        });
    }, [searchText, connectorTypeOptions]);

    const initialFormValues = useMemo((): DataConnectorFormProps => {
        if (isEditMode && initialValues) {
            const convertedType = initialValues.dataConnectorType as ConnectorType;
            return {
                name: initialValues.name || '',
                dataConnectorType: convertedType || initialValues.dataConnectorType,
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
            const dataConnector: Connector = {
                name: values.name,
                dataConnectorType: values.dataConnectorType.toString(),
                dataSource: values.dataSource,
                identity: values.identity,
            };
            setIsDialogOpen(false);
            formikHelpers.resetForm();
            onSubmit(dataConnector);
        },
        [onSubmit, setIsDialogOpen]
    );

    const handleConnectorSelect = (connector: ConnectorTypeOption) => {
        setSelectedConnectorType(connector);
        setShowCardSelection(false);
    };

    const handleBackToSelection = () => {
        setShowCardSelection(true);
        setSelectedConnectorType(null);
    };

    if (showCardSelection && !isEditMode) {
        return (
            <Dialog open={isDialogOpen} onOpenChange={(_, data) => setIsDialogOpen(data.open)}>
                <DialogSurface className={styles.dialogSurface}>
                    <DialogBody className={styles.dialogBody}>
                        <DialogTitle
                            action={<Button appearance="transparent" icon={<Dismiss24Regular />} onClick={() => setIsDialogOpen(false)} />}
                        >
                            {intl.formatMessage(DataConnectorsResources.connectDataSource)}
                        </DialogTitle>
                        <DialogContent>
                            <div className={styles.searchBoxContainer}>
                                <SearchBox
                                    placeholder={intl.formatMessage(DataConnectorsResources.searchPlaceholder)}
                                    value={searchText}
                                    onChange={(_, data) => setSearchText(data.value || '')}
                                    className={styles.searchBox}
                                />
                            </div>
                            <div className={styles.cardContainer}>
                                <div className={styles.cardGrid}>
                                    {filteredConnectorOptions.map((connector: ConnectorTypeOption, index: number) => (
                                        <Card key={`${connector.id}-${index}`} onClick={() => handleConnectorSelect(connector)}>
                                            <CardHeader
                                                image={<img src={connector.img} alt={connector.name} className={styles.image} />}
                                                header={
                                                    <div>
                                                        <Text weight="semibold">{connector.name}</Text>
                                                        <Text size={200} className={styles.serviceDescription}>
                                                            {connector.service}
                                                        </Text>
                                                    </div>
                                                }
                                            />
                                            <Text size={200} className={styles.serviceMoreInfoText}>
                                                {connector.description}
                                            </Text>
                                        </Card>
                                    ))}
                                </div>
                            </div>
                        </DialogContent>
                    </DialogBody>
                    <div className={styles.formSeparator}>
                        <DialogActions className={styles.dialogActionsContainer}>
                            <Button appearance="secondary" onClick={() => setIsDialogOpen(false)}>
                                {intl.formatMessage(DataConnectorsResources.cancel)}
                            </Button>
                        </DialogActions>
                    </div>
                </DialogSurface>
            </Dialog>
        );
    }

    return (
        <Formik<DataConnectorFormProps>
            initialValues={{
                ...initialFormValues,
                dataConnectorType: selectedConnectorType?.id || initialFormValues.dataConnectorType,
            }}
            enableReinitialize={true}
            onSubmit={handleSubmit}
        >
            <CreateOrUpdateDataConnectorForm
                isDialogOpen={isDialogOpen}
                setIsDialogOpen={setIsDialogOpen}
                isEditMode={isEditMode}
                agentIdentity={agentIdentity}
                isOperationInProgress={isOperationInProgress}
                existingDataConnectors={existingDataConnectors}
                refreshAgent={refreshAgent}
                selectedConnectorType={selectedConnectorType}
                onBackToSelection={handleBackToSelection}
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
    selectedConnectorType,
    onBackToSelection,
}: CreateOrUpdateDataConnectorFormProps) => {
    const intl = useIntl();
    const styles = useAddEditConnectorsStyles();
    const azPortalContext = useContext(AzPortalContext);
    const { resourceId } = useContext(EnvironmentContext);

    // Get connector type options using the new function
    const connectorTypeOptions = useMemo(() => getConnectorTypeOptions(intl), [intl]);

    const [nameError, setNameError] = useState<string | undefined>();
    const [dataSourceError, setDataSourceError] = useState<string | undefined>();

    const { initialValues, values, setFieldValue, submitForm, resetForm } = useFormikContext<DataConnectorFormProps>();

    const dataSourcePlaceholder = useMemo(() => {
        if (values.dataConnectorType === 'AzureDataExplorerQuery') {
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
        if (!values.dataSource || values.dataConnectorType !== 'AzureDataExplorerQuery') {
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
                        {(() => {
                            const currentConnectorType =
                                isEditMode && initialValues
                                    ? connectorTypeOptions.find(
                                          (option: ConnectorTypeOption) => option.id === initialValues.dataConnectorType
                                      )
                                    : selectedConnectorType;

                            return currentConnectorType ? (
                                <div className={styles.connectorHeader}>
                                    <img src={currentConnectorType.img} alt={currentConnectorType.name} className={styles.connectorIcon} />
                                    <div>
                                        <div>{currentConnectorType.name}</div>
                                        <div className={styles.connectorTypeText}>{currentConnectorType.service}</div>
                                    </div>
                                </div>
                            ) : (
                                intl.formatMessage(DataConnectorsResources.createDataConnector)
                            );
                        })()}
                    </DialogTitle>
                    <DialogContent>
                        <form className={styles.form}>
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

                            <Field
                                label={
                                    selectedConnectorType
                                        ? `${selectedConnectorType.service} ${intl.formatMessage(DataConnectorsResources.repositoryUrl)}`
                                        : intl.formatMessage(DataConnectorsResources.repositoryUrl)
                                }
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

                            <Field label={intl.formatMessage(DataConnectorsResources.managedIdentity)} required>
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
                                <Link onClick={openIdentityBlade} className={styles.identityLink}>
                                    {intl.formatMessage(SreAgentResources.addIdentity)}
                                </Link>
                            </Field>
                        </form>
                    </DialogContent>
                </DialogBody>
                <DialogActions className={styles.dialogActionsSpaceBetween}>
                    {!isEditMode && selectedConnectorType && onBackToSelection ? (
                        <Button appearance="secondary" onClick={onBackToSelection}>
                            {intl.formatMessage(DataConnectorsResources.back)}
                        </Button>
                    ) : (
                        <div></div>
                    )}
                    <div className={styles.buttonGroup}>
                        <Button appearance="primary" type="submit" onClick={() => submitForm()} disabled={isSaveDisabled}>
                            {isEditMode
                                ? intl.formatMessage(SreAgentResources.update)
                                : intl.formatMessage(DataConnectorsResources.connectToAgent)}
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
                    </div>
                </DialogActions>
            </DialogSurface>
        </Dialog>
    );
};

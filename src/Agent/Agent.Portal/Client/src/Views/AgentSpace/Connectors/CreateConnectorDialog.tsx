import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Link,
    makeStyles,
    Option,
    OptionGroup,
    tokens,
} from '@fluentui/react-components';
import { Dismiss24Regular, Open16Regular } from '@fluentui/react-icons';
import { Formik, FormikHelpers, useFormikContext } from 'formik';
import { useCallback, useEffect, useMemo } from 'react';
import { IntlShape, useIntl } from 'react-intl';
import * as Yup from 'yup';
import { DropdownFormik } from '../../../Common/Components/Formik/DropdownFormik';
import { InputFormik } from '../../../Common/Components/Formik/InputFormik';
import { ApiVersions } from '../../../Common/Constants/ApiVersions';
import { AgentSpace, AgentSpaceConnector } from '../../../Common/Contracts/AgentSpace';
import { ArmObj } from '../../../Common/Contracts/Arm';
import { IdentityKeys, IdentityStatus, IdentityType } from '../../../Common/Contracts/Identity';
import { parseArmId } from '../../../Common/Utilities/ArmId';
import { buildBladeUrl } from '../../../Common/Utilities/Url';
import { PortalResources } from '../../../Strings/Resources';
import { ConnectorTypeOptions, isValidKustoDataSource, KustoDataSourceExample } from './ConnectorConstants';

interface ValidationContext {
    existingConnectors: AgentSpaceConnector[];
    isEditMode: boolean;
    originalName: string;
}

const getValidationSchema = (intl: IntlShape, context: ValidationContext) =>
    Yup.object({
        name: Yup.string()
            .required(intl.formatMessage(PortalResources.fieldRequired))
            .test('unique-name', intl.formatMessage(PortalResources.duplicateConnectorNameError), value => {
                if (!value) return true;
                const isDuplicate = context.existingConnectors.some(
                    connector =>
                        connector.name.toLowerCase() === value.toLowerCase() &&
                        (!context.isEditMode || connector.name.toLowerCase() !== context.originalName.toLowerCase())
                );
                return !isDuplicate;
            }),
        dataConnectorType: Yup.string().required(intl.formatMessage(PortalResources.fieldRequired)),
        dataSource: Yup.string().when('dataConnectorType', {
            is: (type: string) => type && type !== 'ICM',
            then: schema =>
                schema
                    .required(intl.formatMessage(PortalResources.fieldRequired))
                    .test(
                        'valid-kusto-format',
                        intl.formatMessage(PortalResources.dataSourceKustoFormatError, { example: KustoDataSourceExample }),
                        function (value) {
                            const { dataConnectorType } = this.parent;
                            if (dataConnectorType !== 'Kusto' || !value) return true;
                            return isValidKustoDataSource(value);
                        }
                    ),
            otherwise: schema => schema.optional(),
        }),
        identity: Yup.string().required(intl.formatMessage(PortalResources.fieldRequired)),
    });

const useStyles = makeStyles({
    form: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        paddingBottom: tokens.spacingVerticalM,
    },
    addIdentityLink: {
        marginTop: tokens.spacingVerticalXS,
        display: 'inline-flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        fontSize: tokens.fontSizeBase200,
    },
    externalIcon: {
        fontSize: '12px',
    },
});

export interface ConnectorFormValues {
    name: string;
    dataConnectorType: string;
    dataSource: string;
    identity: string;
}

interface CreateConnectorDialogProps {
    isOpen: boolean;
    onClose: () => void;
    agentSpace: ArmObj<AgentSpace> | null;
    existingConnectors: AgentSpaceConnector[];
    onSubmit: (connector: AgentSpaceConnector) => Promise<boolean>;
    /** For edit mode - pre-populate the form */
    initialValues?: ConnectorFormValues;
    isEditMode?: boolean;
}

interface InnerFormProps {
    isOpen: boolean;
    onClose: () => void;
    agentSpace: ArmObj<AgentSpace> | null;
    isEditMode: boolean;
    onRefreshAgentSpace?: () => void;
}

const InnerForm = ({ isOpen, onClose, agentSpace, isEditMode, onRefreshAgentSpace }: InnerFormProps) => {
    const intl = useIntl();
    const styles = useStyles();

    const { values, setFieldValue, submitForm, resetForm, isSubmitting, isValid } = useFormikContext<ConnectorFormValues>();

    const dataSourcePlaceholder = useMemo(() => {
        if (values.dataConnectorType === 'Kusto') {
            return KustoDataSourceExample;
        }
        return intl.formatMessage(PortalResources.dataSourcePlaceholder);
    }, [values.dataConnectorType, intl]);

    const isSystemAssignedIdentityEnabled = useMemo(() => {
        return agentSpace?.identity?.type?.toLowerCase().includes(IdentityType.systemAssigned.toLowerCase());
    }, [agentSpace]);

    const userAssignedIdentityOptions = useMemo(() => {
        const userAssignedOptions: { id: string; name: string }[] = [];
        const userAssignedIdentityRscIds = agentSpace?.identity?.userAssignedIdentities
            ? Object.keys(agentSpace.identity.userAssignedIdentities)
            : [];

        if (userAssignedIdentityRscIds.length > 0) {
            userAssignedIdentityRscIds.forEach(resourceId => {
                const parsed = parseArmId(resourceId);
                const name = parsed.resourceName || resourceId;
                userAssignedOptions.push({ id: resourceId, name });
            });
        }

        return userAssignedOptions;
    }, [agentSpace]);

    const handleOpenIdentityBlade = useCallback(() => {
        if (!agentSpace?.id) return;

        const bladeUrl = buildBladeUrl({
            extension: 'Microsoft_Azure_ManagedServiceIdentity',
            detailBlade: 'AzureResourceIdentitiesBladeV2',
            detailBladeInputs: {
                resourceId: agentSpace.id,
                apiVersion: ApiVersions.microsoftAppApiVersion20250501Preview,
                systemAssignedStatus: IdentityStatus.Supported,
                userAssignedStatus: IdentityStatus.Supported,
            },
        });
        window.open(bladeUrl, '_blank', 'noopener,noreferrer');
        onRefreshAgentSpace?.();
    }, [agentSpace?.id, onRefreshAgentSpace]);

    // Auto-select identity if only one option
    useEffect(() => {
        const allOptions = [...userAssignedIdentityOptions];
        if (isSystemAssignedIdentityEnabled) {
            allOptions.unshift({ id: IdentityKeys.system, name: intl.formatMessage(PortalResources.systemAssigned) });
        }

        if (allOptions.length === 1 && !values.identity) {
            setFieldValue('identity', allOptions[0].id);
        }
    }, [isSystemAssignedIdentityEnabled, userAssignedIdentityOptions, values.identity, setFieldValue, intl]);

    const isSaveDisabled = useMemo((): boolean => {
        return isSubmitting || !isValid;
    }, [isSubmitting, isValid]);

    const handleClose = useCallback(() => {
        resetForm();
        onClose();
    }, [resetForm, onClose]);

    const getIdentityDisplayValue = useCallback(() => {
        if (values.identity === IdentityKeys.system) {
            return intl.formatMessage(PortalResources.systemAssigned);
        }
        return userAssignedIdentityOptions.find(option => option.id === values.identity)?.name || '';
    }, [values.identity, userAssignedIdentityOptions, intl]);

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => !data.open && handleClose()}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle action={<Button appearance="transparent" icon={<Dismiss24Regular />} onClick={handleClose} />}>
                        {isEditMode
                            ? intl.formatMessage(PortalResources.editConnector)
                            : intl.formatMessage(PortalResources.createConnector)}
                    </DialogTitle>
                    <DialogContent>
                        <div className={styles.form}>
                            <InputFormik
                                name="name"
                                label={intl.formatMessage(PortalResources.name)}
                                required
                                placeholder={intl.formatMessage(PortalResources.connectorNamePlaceholder)}
                                disabled={isEditMode || isSubmitting}
                                orientation="vertical"
                                showUntouchedFieldError
                            />

                            <DropdownFormik
                                name="dataConnectorType"
                                label={intl.formatMessage(PortalResources.type)}
                                required
                                value={ConnectorTypeOptions.find(option => option.id === values.dataConnectorType)?.label || ''}
                                placeholder={intl.formatMessage(PortalResources.connectorTypePlaceholder)}
                                disabled={isSubmitting}
                                orientation="vertical"
                            >
                                {ConnectorTypeOptions.map(option => (
                                    <Option key={option.id} value={option.id}>
                                        {option.label}
                                    </Option>
                                ))}
                            </DropdownFormik>

                            {values.dataConnectorType !== 'ICM' && (
                                <InputFormik
                                    name="dataSource"
                                    label={intl.formatMessage(PortalResources.dataSource)}
                                    required
                                    placeholder={dataSourcePlaceholder}
                                    disabled={isSubmitting}
                                    orientation="vertical"
                                    showUntouchedFieldError
                                />
                            )}

                            <DropdownFormik
                                name="identity"
                                label={intl.formatMessage(PortalResources.identity)}
                                required
                                value={getIdentityDisplayValue()}
                                placeholder={intl.formatMessage(PortalResources.identityPlaceholder)}
                                disabled={isSubmitting}
                                orientation="vertical"
                                sublabel={
                                    <Link onClick={handleOpenIdentityBlade} className={styles.addIdentityLink}>
                                        {intl.formatMessage(PortalResources.addIdentity)}
                                        <Open16Regular className={styles.externalIcon} />
                                    </Link>
                                }
                            >
                                {isSystemAssignedIdentityEnabled && (
                                    <Option key={IdentityKeys.system} value={IdentityKeys.system}>
                                        {intl.formatMessage(PortalResources.systemAssigned)}
                                    </Option>
                                )}

                                {userAssignedIdentityOptions.length > 0 && (
                                    <OptionGroup label={intl.formatMessage(PortalResources.userAssigned)}>
                                        {userAssignedIdentityOptions.map(option => (
                                            <Option key={option.id} value={option.id}>
                                                {option.name}
                                            </Option>
                                        ))}
                                    </OptionGroup>
                                )}
                            </DropdownFormik>
                        </div>
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="primary" onClick={submitForm} disabled={isSaveDisabled}>
                            {isEditMode ? intl.formatMessage(PortalResources.update) : intl.formatMessage(PortalResources.save)}
                        </Button>
                        <Button appearance="secondary" onClick={handleClose} disabled={isSubmitting}>
                            {intl.formatMessage(PortalResources.cancel)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

export const CreateConnectorDialog = ({
    isOpen,
    onClose,
    agentSpace,
    existingConnectors,
    onSubmit,
    initialValues,
    isEditMode = false,
}: CreateConnectorDialogProps) => {
    const intl = useIntl();

    const defaultValues: ConnectorFormValues = useMemo(
        () =>
            initialValues || {
                name: '',
                dataConnectorType: '',
                dataSource: '',
                identity: '',
            },
        [initialValues]
    );

    const validationSchema = useMemo(
        () =>
            getValidationSchema(intl, {
                existingConnectors,
                isEditMode,
                originalName: initialValues?.name || '',
            }),
        [intl, existingConnectors, isEditMode, initialValues?.name]
    );

    const handleSubmit = useCallback(
        async (values: ConnectorFormValues, formikHelpers: FormikHelpers<ConnectorFormValues>) => {
            const connector: AgentSpaceConnector = {
                name: values.name,
                dataConnectorType: values.dataConnectorType,
                // For ICM type, fill with dummy value as API still requires it
                dataSource: values.dataConnectorType === 'ICM' ? 'placeholder' : values.dataSource,
                identity: values.identity,
            };

            const success = await onSubmit(connector);
            if (success) {
                formikHelpers.resetForm();
                onClose();
            }
        },
        [onSubmit, onClose]
    );

    return (
        <Formik<ConnectorFormValues>
            initialValues={defaultValues}
            validationSchema={validationSchema}
            onSubmit={handleSubmit}
            enableReinitialize={true}
            validateOnMount
        >
            <InnerForm isOpen={isOpen} onClose={onClose} agentSpace={agentSpace} isEditMode={isEditMode} />
        </Formik>
    );
};

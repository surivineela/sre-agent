import { Button, Field, Input, makeStyles, Switch, Text, tokens } from '@fluentui/react-components';
import { Dismiss16Regular, Save16Regular } from '@fluentui/react-icons';
import { Formik, FormikHelpers, useFormikContext } from 'formik';
import { useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import * as Yup from 'yup';
import { AgentSpaceClient } from '../../Common/Clients/AgentSpaceClient';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { useNotifications } from '../../Common/Contexts/NotificationContext';
import { AgentSpace, GenevaActionsConfiguration } from '../../Common/Contracts/AgentSpace';
import { ArmObj } from '../../Common/Contracts/Arm';
import { PortalResources, RolesAndPermissions } from '../../Strings/Resources';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
    },
    sectionHeader: {
        fontWeight: 600,
        fontSize: '16px',
    },
    sectionDescription: {
        color: tokens.colorNeutralForeground3,
        marginBottom: '8px',
    },
    switchRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
    form: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        maxWidth: '400px',
    },
    label: {
        color: tokens.colorNeutralForeground3,
    },
    readOnlyValue: {
        color: tokens.colorNeutralForeground1,
    },
    buttonRow: {
        display: 'flex',
        gap: '8px',
        marginTop: '8px',
    },
});

interface GenevaActionsBasicsFormValues {
    enableGenevaActions: boolean;
    acisEndpoint: string;
    clientId: string;
    extensionName: string;
}

interface GenevaActionPoliciesBasicsTabProps {
    agentSpace: ArmObj<AgentSpace> | null;
    refresh: () => Promise<void>;
    disabled: boolean;
}

interface BasicsFormContentProps {
    disabled: boolean;
    certificateSubjectName?: string;
}

const BasicsFormContent = ({ disabled, certificateSubjectName }: BasicsFormContentProps) => {
    const intl = useIntl();
    const styles = useStyles();
    const { values, handleChange, handleBlur, dirty, isSubmitting, submitForm, resetForm, errors, touched } =
        useFormikContext<GenevaActionsBasicsFormValues>();

    const buttonsDisabled = !dirty || isSubmitting || disabled;

    return (
        <div className={styles.form}>
            <div className={styles.switchRow}>
                <Text>{intl.formatMessage(PortalResources.genevaActionsEnabled)}:</Text>
                <Switch
                    checked={values.enableGenevaActions}
                    onChange={(_, data) => handleChange({ target: { name: 'enableGenevaActions', value: data.checked } })}
                    disabled={disabled || isSubmitting}
                />
            </div>

            <Text>
                {intl.formatMessage(PortalResources.genevaActionsCertificateName)}: {certificateSubjectName || '-'}
            </Text>

            {/* Show configuration fields only when enabled */}
            {values.enableGenevaActions && (
                <>
                    <Field
                        label={intl.formatMessage(RolesAndPermissions.acisEndpoint)}
                        validationMessage={touched.acisEndpoint && errors.acisEndpoint ? errors.acisEndpoint : undefined}
                        validationState={touched.acisEndpoint && errors.acisEndpoint ? 'error' : undefined}
                    >
                        <Input
                            name="acisEndpoint"
                            value={values.acisEndpoint}
                            onChange={handleChange}
                            onBlur={handleBlur}
                            disabled={disabled || isSubmitting}
                            placeholder="https://acis.example.com"
                        />
                    </Field>

                    <Field
                        label={intl.formatMessage(RolesAndPermissions.clientId)}
                        validationMessage={touched.clientId && errors.clientId ? errors.clientId : undefined}
                        validationState={touched.clientId && errors.clientId ? 'error' : undefined}
                    >
                        <Input
                            name="clientId"
                            value={values.clientId}
                            onChange={handleChange}
                            onBlur={handleBlur}
                            disabled={disabled || isSubmitting}
                        />
                    </Field>

                    <Field
                        label={intl.formatMessage(RolesAndPermissions.extensionName)}
                        validationMessage={touched.extensionName && errors.extensionName ? errors.extensionName : undefined}
                        validationState={touched.extensionName && errors.extensionName ? 'error' : undefined}
                    >
                        <Input
                            name="extensionName"
                            value={values.extensionName}
                            onChange={handleChange}
                            onBlur={handleBlur}
                            disabled={disabled || isSubmitting}
                        />
                    </Field>
                </>
            )}

            <div className={styles.buttonRow}>
                <Button appearance="primary" icon={<Save16Regular />} disabled={buttonsDisabled} onClick={() => submitForm()}>
                    {intl.formatMessage(PortalResources.save)}
                </Button>
                <Button appearance="secondary" icon={<Dismiss16Regular />} disabled={buttonsDisabled} onClick={() => resetForm()}>
                    {intl.formatMessage(PortalResources.discard)}
                </Button>
            </div>
        </div>
    );
};

export const GenevaActionPoliciesBasicsTab = ({ agentSpace, refresh, disabled }: GenevaActionPoliciesBasicsTabProps) => {
    const intl = useIntl();
    const styles = useStyles();
    const { start, succeed, fail } = useNotifications();

    const agentSpaceClient = useMemo(() => AgentSpaceClient.getInstance(TelemetrySource.AgentSpaceView), []);

    const genevaConfig = agentSpace?.properties?.policies?.genevaActionsConfiguration;

    const initialValues: GenevaActionsBasicsFormValues = useMemo(
        () => ({
            enableGenevaActions: !!genevaConfig?.acisEndpoint || !!genevaConfig?.clientId || !!genevaConfig?.extensionName,
            acisEndpoint: genevaConfig?.acisEndpoint ?? '',
            clientId: genevaConfig?.clientId ?? '',
            extensionName: genevaConfig?.extensionName ?? '',
        }),
        [genevaConfig]
    );

    const validationSchema = useMemo(
        () =>
            Yup.object({
                acisEndpoint: Yup.string().when('enableGenevaActions', {
                    is: true,
                    then: schema => schema.url(intl.formatMessage(PortalResources.invalidUrl)),
                }),
            }),
        [intl]
    );

    const handleSubmit = useCallback(
        async (values: GenevaActionsBasicsFormValues, formikHelpers: FormikHelpers<GenevaActionsBasicsFormValues>) => {
            if (!agentSpace) return;

            const notificationId = start(
                intl.formatMessage(PortalResources.updateAgentSpace),
                intl.formatMessage(PortalResources.updatingAgentSpace)
            );

            // Build the configuration
            const genevaActionsConfiguration: GenevaActionsConfiguration | undefined = values.enableGenevaActions
                ? {
                      acisEndpoint: values.acisEndpoint || undefined,
                      clientId: values.clientId || undefined,
                      extensionName: values.extensionName || undefined,
                      // Preserve existing values
                      allowedActions: genevaConfig?.allowedActions,
                      certificateSubjectName: genevaConfig?.certificateSubjectName,
                      certificateSubjectAlternativeName: genevaConfig?.certificateSubjectAlternativeName,
                      authenticationMode: genevaConfig?.authenticationMode,
                  }
                : undefined;

            const response = await agentSpaceClient.updateAgentSpace(agentSpace.id, {
                policies: {
                    genevaActionsConfiguration,
                },
            });

            if (response.isSuccessful) {
                succeed(
                    notificationId,
                    intl.formatMessage(PortalResources.updateAgentSpace),
                    intl.formatMessage(PortalResources.updateAgentSpaceSuccess)
                );
                await refresh();
                formikHelpers.resetForm({ values });
            } else {
                fail(
                    notificationId,
                    intl.formatMessage(PortalResources.updateAgentSpace),
                    intl.formatMessage(PortalResources.updateAgentSpaceError)
                );
            }
        },
        [agentSpace, agentSpaceClient, genevaConfig, intl, start, succeed, fail, refresh]
    );

    if (!agentSpace) {
        return null;
    }

    return (
        <div className={styles.container}>
            <Text className={styles.sectionDescription}>{intl.formatMessage(PortalResources.policiesDescription)}</Text>

            <Formik<GenevaActionsBasicsFormValues>
                initialValues={initialValues}
                validationSchema={validationSchema}
                onSubmit={handleSubmit}
                enableReinitialize
            >
                <BasicsFormContent
                    disabled={disabled}
                    certificateSubjectName={genevaConfig?.certificateSubjectAlternativeName || genevaConfig?.certificateSubjectName}
                />
            </Formik>
        </div>
    );
};

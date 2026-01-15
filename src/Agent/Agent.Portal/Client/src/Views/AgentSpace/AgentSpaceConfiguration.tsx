import { Button, makeStyles } from '@fluentui/react-components';
import { Dismiss16Regular, Save16Regular } from '@fluentui/react-icons';
import { Formik, FormikHelpers, useFormikContext } from 'formik';
import { useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import * as Yup from 'yup';
import { AgentSpaceClient } from '../../Common/Clients/AgentSpaceClient';
import { SpinButtonFormik } from '../../Common/Components/Formik/SpinButtonFormik';
import { TextareaFormik } from '../../Common/Components/Formik/TextareaFormik';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { useNotifications } from '../../Common/Contexts/NotificationContext';
import { AgentSpace } from '../../Common/Contracts/AgentSpace';
import { ArmObj } from '../../Common/Contracts/Arm';
import { PortalResources } from '../../Strings/Resources';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
        padding: '20px',
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
    },
    fieldGroup: {
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
        maxWidth: '400px',
    },
    textarea: {
        minHeight: '100px',
    },
    buttonRow: {
        display: 'flex',
        gap: '8px',
        marginTop: '8px',
    },
});

interface AgentSpaceConfigurationFormValues {
    description: string;
    maxAgentCount: number;
}

interface AgentSpaceConfigurationProps {
    agentSpace: ArmObj<AgentSpace> | null;
    refresh: () => Promise<void>;
}

export const AgentSpaceConfiguration = ({ agentSpace, refresh }: AgentSpaceConfigurationProps) => {
    const intl = useIntl();
    const { start, succeed, fail } = useNotifications();

    const client = useMemo(() => AgentSpaceClient.getInstance(TelemetrySource.AgentSpaceView), []);

    const initialValues: AgentSpaceConfigurationFormValues = useMemo(
        () => ({
            description: agentSpace?.properties?.description ?? '',
            maxAgentCount: agentSpace?.properties?.maxAgentCount ?? 10,
        }),
        [agentSpace]
    );

    const validationSchema = useMemo(
        () =>
            Yup.object({
                maxAgentCount: Yup.number()
                    .min(1, intl.formatMessage(PortalResources.minAgentCount))
                    .max(100, intl.formatMessage(PortalResources.maxAgentCountLimit))
                    .required(),
            }),
        [intl]
    );

    const handleSubmit = useCallback(
        async (values: AgentSpaceConfigurationFormValues, formikHelpers: FormikHelpers<AgentSpaceConfigurationFormValues>) => {
            if (!agentSpace) return;

            const notificationId = start(
                intl.formatMessage(PortalResources.updateAgentSpace),
                intl.formatMessage(PortalResources.updatingAgentSpace)
            );

            const response = await client.updateAgentSpace(agentSpace.id, {
                description: values.description,
                maxAgentCount: values.maxAgentCount,
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
        [agentSpace, client, intl, start, succeed, fail, refresh]
    );

    if (!agentSpace) {
        return null;
    }

    return (
        <Formik<AgentSpaceConfigurationFormValues>
            initialValues={initialValues}
            validationSchema={validationSchema}
            onSubmit={handleSubmit}
            validateOnChange={true}
            enableReinitialize={true}
        >
            <InnerAgentSpaceConfiguration />
        </Formik>
    );
};

const InnerAgentSpaceConfiguration = () => {
    const intl = useIntl();
    const styles = useStyles();
    const { dirty, isSubmitting, isValid, submitForm, resetForm } = useFormikContext<AgentSpaceConfigurationFormValues>();

    const handleDiscard = useCallback(() => {
        resetForm();
    }, [resetForm]);

    return (
        <div className={styles.container}>
            <div className={styles.section}>
                <div className={styles.fieldGroup}>
                    <TextareaFormik
                        name="description"
                        label={intl.formatMessage(PortalResources.description)}
                        placeholder={intl.formatMessage(PortalResources.noDescription)}
                        disabled={isSubmitting}
                        isLoading={isSubmitting}
                        resize="vertical"
                        className={styles.textarea}
                    />
                </div>

                <div className={styles.fieldGroup}>
                    <SpinButtonFormik
                        name="maxAgentCount"
                        label={intl.formatMessage(PortalResources.maxAgentCount)}
                        disabled={isSubmitting}
                        isLoading={isSubmitting}
                        min={1}
                        max={100}
                    />
                </div>

                <div className={styles.buttonRow}>
                    <Button icon={<Dismiss16Regular />} appearance="secondary" disabled={!dirty || isSubmitting} onClick={handleDiscard}>
                        {intl.formatMessage(PortalResources.discard)}
                    </Button>
                    <Button
                        icon={<Save16Regular />}
                        appearance="primary"
                        disabled={!dirty || isSubmitting || !isValid}
                        onClick={submitForm}
                    >
                        {intl.formatMessage(PortalResources.save)}
                    </Button>
                </div>
            </div>
        </div>
    );
};

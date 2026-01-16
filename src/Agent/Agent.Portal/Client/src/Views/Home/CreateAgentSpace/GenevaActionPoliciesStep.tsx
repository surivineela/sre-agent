import { makeStyles, Switch, Text, tokens } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useIntl } from 'react-intl';
import { AllowedActionsTable } from '../../../Common/Components/GenevaActions/AllowedActionsTable';
import { GenevaActionsWarningBanner } from '../../../Common/Components/GenevaActions/GenevaActionsWarningBanner';
import { AgentSpaceCreateFormValues } from '../../../Common/Contracts/AgentSpace';
import { useIsInternal } from '../../../Common/Hooks/useIsInternal';
import { PortalResources, RolesAndPermissions } from '../../../Strings/Resources';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
        padding: '24px',
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
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
});

interface GenevaActionPoliciesStepProps {
    isDeploying: boolean;
}

export const GenevaActionPoliciesStep = ({ isDeploying }: GenevaActionPoliciesStepProps) => {
    const intl = useIntl();
    const styles = useStyles();
    const { values, setFieldValue } = useFormikContext<AgentSpaceCreateFormValues>();
    const { isInternalTenant } = useIsInternal();

    const isDisabled = isDeploying || !isInternalTenant;

    return (
        <div className={styles.container}>
            {!isInternalTenant && <GenevaActionsWarningBanner />}

            <div className={styles.section}>
                <Text className={styles.sectionHeader}>{intl.formatMessage(PortalResources.policies)}</Text>
                <Text className={styles.sectionDescription}>{intl.formatMessage(PortalResources.policiesDescription)}</Text>

                <div className={styles.switchRow}>
                    <Switch
                        checked={values.enableGenevaActions}
                        onChange={(_, data) => setFieldValue('enableGenevaActions', data.checked)}
                        disabled={isDisabled}
                    />
                    <Text>{intl.formatMessage(PortalResources.enableGenevaActions)}</Text>
                </div>
            </div>

            <div className={styles.section}>
                <Text className={styles.sectionHeader}>{intl.formatMessage(RolesAndPermissions.allowedActions)}</Text>
                <Text className={styles.sectionDescription}>{intl.formatMessage(PortalResources.allowedActionsDescription)}</Text>

                <AllowedActionsTable
                    rows={values.allowedActions}
                    onChange={rows => setFieldValue('allowedActions', rows)}
                    disabled={isDisabled || !values.enableGenevaActions}
                />
            </div>
        </div>
    );
};

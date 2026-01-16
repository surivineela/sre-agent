import { makeStyles, Option, Text, Textarea, tokens } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { DropdownFormik } from '../../../Common/Components/Formik/DropdownFormik';
import { InputFormik } from '../../../Common/Components/Formik/InputFormik';
import { ResourceGroupDropdown } from '../../../Common/Components/ResourceGroupDropdown';
import { SubscriptionDropdown } from '../../../Common/Components/SubscriptionDropdown';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { AgentSpaceCreateFormValues } from '../../../Common/Contracts/AgentSpace';
import { Subscription } from '../../../Common/Contracts/Arm';
import { getCanonicalLocation } from '../../../Common/Utilities/Location';
import { PortalResources } from '../../../Strings/Resources';
import { useSreAgentLocations } from '../Create/useSreAgentLocations';

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
    textareaWrapper: {
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
    },
    textareaLabel: {
        fontWeight: 400,
    },
    textarea: {
        width: '100%',
        minHeight: '80px',
    },
});

interface AgentSpaceBasicsProps {
    isDeploying: boolean;
}

export const AgentSpaceBasics = ({ isDeploying }: AgentSpaceBasicsProps) => {
    const intl = useIntl();
    const styles = useStyles();
    const { values, setFieldValue, errors } = useFormikContext<AgentSpaceCreateFormValues>();

    const { locationsList, locationsLoading } = useSreAgentLocations(values.subscriptionId, TelemetrySource.AgentSpaceCreate);

    const locationDropdownOptions = useMemo(() => {
        return (locationsList ?? []).map(location => ({
            key: getCanonicalLocation(location),
            text: location,
            data: getCanonicalLocation(location),
        }));
    }, [locationsList]);

    const onSubscriptionChange = useCallback(
        (subscription?: Subscription) => {
            setFieldValue('subscriptionId', subscription?.subscriptionId ?? '');
            setFieldValue('resourceGroupId', '');
            setFieldValue('isResourceGroupNew', false);
        },
        [setFieldValue]
    );

    return (
        <div className={styles.container}>
            {/* Project Details Section */}
            <div className={styles.section}>
                <Text className={styles.sectionHeader}>{intl.formatMessage(PortalResources.projectDetails)}</Text>
                <Text className={styles.sectionDescription}>{intl.formatMessage(PortalResources.projectDetailsDescription)}</Text>

                <SubscriptionDropdown
                    label={intl.formatMessage(PortalResources.subscription)}
                    selectedSubscriptionId={values.subscriptionId}
                    onSubscriptionChange={onSubscriptionChange}
                    disabled={isDeploying}
                />

                <ResourceGroupDropdown
                    subscriptionId={values.subscriptionId}
                    selectedResourceGroupId={values.resourceGroupId}
                    onResourceGroupChange={resourceGroup => {
                        setFieldValue('resourceGroupId', resourceGroup?.id ?? '');
                        setFieldValue('isResourceGroupNew', resourceGroup?.new ?? false);
                    }}
                    disabled={isDeploying}
                    errorMessage={errors.resourceGroupId}
                    telemetrySource={TelemetrySource.AgentSpaceCreate}
                    createNew
                />
            </div>

            {/* Agent Space Details Section */}
            <div className={styles.section}>
                <Text className={styles.sectionHeader}>{intl.formatMessage(PortalResources.agentSpaceDetails)}</Text>
                <Text className={styles.sectionDescription}>{intl.formatMessage(PortalResources.agentSpaceDetailsDescription)}</Text>

                <InputFormik
                    name="name"
                    label={intl.formatMessage(PortalResources.agentSpaceName)}
                    required
                    placeholder={intl.formatMessage(PortalResources.enterName)}
                    disabled={isDeploying}
                    orientation="vertical"
                />

                <DropdownFormik
                    name="location"
                    label={intl.formatMessage(PortalResources.region)}
                    required
                    value={locationDropdownOptions.find(opt => opt.data === values.location)?.text ?? ''}
                    selectedOptions={values.location ? [values.location] : []}
                    placeholder={intl.formatMessage(PortalResources.selectRegion)}
                    disabled={locationsLoading || isDeploying}
                    isLoading={locationsLoading}
                    orientation="vertical"
                >
                    {locationDropdownOptions.map(option => (
                        <Option key={option.key} value={option.data} text={option.text}>
                            {option.text}
                        </Option>
                    ))}
                </DropdownFormik>

                <InputFormik
                    name="maxAgentCount"
                    label={intl.formatMessage(PortalResources.maxAgentCount)}
                    required
                    type="number"
                    disabled={isDeploying}
                    orientation="vertical"
                />

                <div className={styles.textareaWrapper}>
                    <Text className={styles.textareaLabel}>{intl.formatMessage(PortalResources.description)}</Text>
                    <Textarea
                        className={styles.textarea}
                        value={values.description}
                        onChange={(_, data) => setFieldValue('description', data.value)}
                        disabled={isDeploying}
                        resize="vertical"
                    />
                </div>
            </div>
        </div>
    );
};

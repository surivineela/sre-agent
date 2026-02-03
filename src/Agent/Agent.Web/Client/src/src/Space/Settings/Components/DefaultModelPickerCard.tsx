import {
    Button,
    Caption1,
    Card,
    Dropdown,
    Field,
    Link,
    MessageBar,
    MessageBarBody,
    Option,
    Skeleton,
    SkeletonItem,
} from '@fluentui/react-components';
import { Formik } from 'formik';
import { useContext } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { SreAgentFwLinks } from '../../../Common/Constants/FwLinks';
import { Model, ModelProvider } from '../../../Common/Contracts/Azure/SreAgent';
import { SettingsTabResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { useSupportedModels } from '../Hooks/useSupportedModels';
import { useSettingsStyles } from '../Styles/Settings.styles';

export interface DefaultModelPickerCardProps {
    resourceId: string;
    region: string;
    currentProvider?: string;
    isAgentLoading: boolean;
    isUpdatingUpgradeChannel: boolean;
}

export const DefaultModelPickerCard = ({
    resourceId,
    region,
    currentProvider,
    isAgentLoading,
    isUpdatingUpgradeChannel,
}: DefaultModelPickerCardProps) => {
    const intl = useIntl();
    const styles = useSettingsStyles();
    const az = useContext(AzPortalContext);

    const {
        supportedProviders,
        isSupportedModelsLoading,
        getSupportedModelsFailure,
        updateDefaultModel,
        isUpdatingDefaultModel,
        refreshSupportedModels,
        showAnthropicDisabledMessage,
    } = useSupportedModels(resourceId, region);

    return (
        <Formik<Model>
            initialValues={{ provider: currentProvider || '' }}
            enableReinitialize
            onSubmit={values => updateDefaultModel(values)}
        >
            {({ dirty, values, setFieldValue, resetForm, submitForm }) => (
                <Card style={styles.basicsCardStyle}>
                    <div style={styles.sectionTitleStyle}>{intl.formatMessage(SettingsTabResources.modelProviderLabel)}</div>

                    {getSupportedModelsFailure && (
                        <MessageBar intent="error" layout="multiline" style={{ alignItems: 'center' }}>
                            <MessageBarBody style={styles.failedToLoadMessageBarContentStyle}>
                                {getSupportedModelsFailure}
                                <Button appearance="outline" size="small" onClick={() => refreshSupportedModels()}>
                                    {intl.formatMessage(SreAgentResources.refresh)}
                                </Button>
                            </MessageBarBody>
                        </MessageBar>
                    )}

                    {showAnthropicDisabledMessage && (
                        <MessageBar layout="multiline" style={{ alignItems: 'center' }}>
                            <MessageBarBody>
                                <Caption1>{intl.formatMessage(SettingsTabResources.anthropicNotAvailable)}</Caption1>
                            </MessageBarBody>
                        </MessageBar>
                    )}

                    <Field id="providerField" label={intl.formatMessage(SettingsTabResources.providerLabel)} orientation="vertical">
                        {isAgentLoading || isSupportedModelsLoading ? (
                            <Skeleton>
                                <SkeletonItem style={styles.dropdownSkeletonStyle} />
                            </Skeleton>
                        ) : (
                            <Dropdown
                                id="provider"
                                style={styles.dropdownStyles}
                                value={supportedProviders?.find(option => option.key === values.provider)?.text || values.provider}
                                onOptionSelect={(_event, data) => {
                                    az.logAmplitudeControlEvent({
                                        targetType: 'dropdown',
                                        targetAction: 'changed',
                                        targetName: 'provider',
                                        targetFriendlyName: 'provider',
                                        valueObjectName: data?.optionValue ?? '',
                                        valueObjectFriendlyName: data?.optionText ?? '',
                                    });
                                    setFieldValue('provider', data.optionValue);
                                }}
                                disabled={isUpdatingUpgradeChannel || !!getSupportedModelsFailure || isUpdatingDefaultModel}
                            >
                                {supportedProviders?.map(option => (
                                    <Option value={option.key} checkIcon={null} disabled={option.disabled}>
                                        {option.text}
                                    </Option>
                                ))}
                            </Dropdown>
                        )}
                    </Field>

                    {values.provider === ModelProvider.Anthropic && (
                        <MessageBar layout="multiline" style={{ alignItems: 'center' }}>
                            <MessageBarBody>
                                <Caption1>{intl.formatMessage(SettingsTabResources.anthropicEuRegionInfoMessage)}</Caption1>{' '}
                                <Link href={SreAgentFwLinks.sreAgentDataHandling} target="_blank" rel="noopener noreferrer">
                                    <Caption1>{intl.formatMessage(SettingsTabResources.anthropicEuRegionLearnMore)}</Caption1>
                                </Link>
                            </MessageBarBody>
                        </MessageBar>
                    )}

                    <div style={styles.commandBarButtonContainerStyle}>
                        <Button
                            appearance="primary"
                            onClick={() => submitForm()}
                            disabled={
                                !dirty ||
                                isUpdatingUpgradeChannel ||
                                isSupportedModelsLoading ||
                                !!getSupportedModelsFailure ||
                                isUpdatingDefaultModel
                            }
                        >
                            {intl.formatMessage(SreAgentResources.save)}
                        </Button>
                        <Button
                            appearance="outline"
                            onClick={() => resetForm()}
                            disabled={
                                !dirty ||
                                isUpdatingUpgradeChannel ||
                                isSupportedModelsLoading ||
                                !!getSupportedModelsFailure ||
                                isUpdatingDefaultModel
                            }
                        >
                            {intl.formatMessage(SreAgentResources.cancel)}
                        </Button>
                    </div>
                </Card>
            )}
        </Formik>
    );
};

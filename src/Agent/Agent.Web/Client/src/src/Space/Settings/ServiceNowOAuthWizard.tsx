import {
    Button,
    Card,
    Field,
    Input,
    MessageBar,
    MessageBarBody,
    MessageBarTitle,
    ProgressBar,
    Spinner,
    Text,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import { CheckmarkCircle24Regular } from '@fluentui/react-icons';
import { FC, useState } from 'react';
import { useIntl } from 'react-intl';
import { ServiceNowResources } from '../../Strings/SREAgentResources';

const useStyles = makeStyles({
    wizard: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        padding: tokens.spacingVerticalXL,
    },
    stepIndicator: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: tokens.spacingVerticalL,
    },
    step: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: tokens.spacingVerticalS,
        flex: 1,
    },
    stepNumber: {
        width: '32px',
        height: '32px',
        borderRadius: '50%',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: tokens.colorNeutralBackground3,
        color: tokens.colorNeutralForeground2,
        fontWeight: tokens.fontWeightSemibold,
    },
    stepNumberActive: {
        backgroundColor: tokens.colorBrandBackground,
        color: tokens.colorNeutralForegroundOnBrand,
    },
    stepNumberComplete: {
        backgroundColor: tokens.colorPaletteGreenBackground3,
        color: tokens.colorNeutralForegroundOnBrand,
    },
    stepLine: {
        height: '2px',
        flex: 1,
        backgroundColor: tokens.colorNeutralBackground3,
        marginTop: '16px',
    },
    stepLineComplete: {
        backgroundColor: tokens.colorPaletteGreenBorder2,
    },
    formFields: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    actions: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        justifyContent: 'flex-end',
        marginTop: tokens.spacingVerticalL,
    },
    authorizingCard: {
        padding: tokens.spacingVerticalXL,
        textAlign: 'center',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: tokens.spacingVerticalL,
    },
});

export interface ServiceNowOAuthWizardProps {
    endpoint?: string;
    clientId?: string;
    clientSecret?: string;
    onComplete: (endpoint: string, clientId: string, clientSecret: string) => Promise<{ success: boolean; errorMessage?: string }>;
    onCancel: () => void;
}

type WizardStep = 'credentials' | 'authorizing' | 'complete';

const ServiceNowOAuthWizard: FC<ServiceNowOAuthWizardProps> = ({
    endpoint: initialEndpoint,
    clientId: initialClientId,
    clientSecret: initialClientSecret,
    onComplete,
    onCancel,
}) => {
    const styles = useStyles();
    const intl = useIntl();

    const [currentStep, setCurrentStep] = useState<WizardStep>('credentials');
    const [endpoint, setEndpoint] = useState(initialEndpoint || '');
    const [clientId, setClientId] = useState(initialClientId || '');
    const [clientSecret, setClientSecret] = useState(initialClientSecret || '');
    const [error, setError] = useState<string | undefined>();
    const [isProcessing, setIsProcessing] = useState(false);

    const handleSubmit = async () => {
        setError(undefined);
        setIsProcessing(true);
        setCurrentStep('authorizing');

        try {
            const result = await onComplete(endpoint, clientId, clientSecret);

            if (result.success) {
                setCurrentStep('complete');
            } else {
                setError(result.errorMessage || 'OAuth setup failed');
                setCurrentStep('credentials');
            }
        } catch (err) {
            setError('An unexpected error occurred');
            setCurrentStep('credentials');
        } finally {
            setIsProcessing(false);
        }
    };

    const isCredentialsValid = endpoint && clientId && clientSecret;

    const getStepNumber = (step: WizardStep): number => {
        switch (step) {
            case 'credentials':
                return 1;
            case 'authorizing':
                return 2;
            case 'complete':
                return 3;
        }
    };

    const currentStepNumber = getStepNumber(currentStep);

    return (
        <div className={styles.wizard}>
            {/* Step Indicator */}
            <div className={styles.stepIndicator}>
                <div className={styles.step}>
                    <div
                        className={`${styles.stepNumber} ${
                            currentStepNumber >= 1 ? (currentStepNumber > 1 ? styles.stepNumberComplete : styles.stepNumberActive) : ''
                        }`}
                    >
                        {currentStepNumber > 1 ? <CheckmarkCircle24Regular /> : '1'}
                    </div>
                    <Text size={200}>{intl.formatMessage(ServiceNowResources.oauthStepEnterCredentials)}</Text>
                </div>
                <div className={`${styles.stepLine} ${currentStepNumber >= 2 ? styles.stepLineComplete : ''}`} />
                <div className={styles.step}>
                    <div
                        className={`${styles.stepNumber} ${
                            currentStepNumber >= 2 ? (currentStepNumber > 2 ? styles.stepNumberComplete : styles.stepNumberActive) : ''
                        }`}
                    >
                        {currentStepNumber > 2 ? <CheckmarkCircle24Regular /> : '2'}
                    </div>
                    <Text size={200}>{intl.formatMessage(ServiceNowResources.oauthStepAuthorize)}</Text>
                </div>
                <div className={`${styles.stepLine} ${currentStepNumber >= 3 ? styles.stepLineComplete : ''}`} />
                <div className={styles.step}>
                    <div className={`${styles.stepNumber} ${currentStepNumber >= 3 ? styles.stepNumberActive : ''}`}>3</div>
                    <Text size={200}>{intl.formatMessage(ServiceNowResources.oauthStepComplete)}</Text>
                </div>
            </div>

            {/* Step Content */}
            {currentStep === 'credentials' && (
                <>
                    {error && (
                        <MessageBar intent="error" shape="square">
                            <MessageBarBody>
                                <MessageBarTitle>{intl.formatMessage(ServiceNowResources.oauthSetupFailed)}</MessageBarTitle>
                                {error}
                            </MessageBarBody>
                        </MessageBar>
                    )}

                    <MessageBar intent="info" shape="square">
                        <MessageBarBody>
                            <MessageBarTitle>{intl.formatMessage(ServiceNowResources.oauthStep1Title)}</MessageBarTitle>
                            {intl.formatMessage(ServiceNowResources.oauthStep1Description)}
                        </MessageBarBody>
                    </MessageBar>

                    <div className={styles.formFields}>
                        <Field label="ServiceNow Instance URL" required>
                            <Input
                                placeholder="https://your-instance.service-now.com"
                                value={endpoint}
                                onChange={(_, data) => setEndpoint(data.value)}
                                disabled={isProcessing}
                            />
                        </Field>

                        <Field label="OAuth Client ID" required>
                            <Input
                                placeholder="Enter your OAuth Client ID"
                                value={clientId}
                                onChange={(_, data) => setClientId(data.value)}
                                disabled={isProcessing}
                            />
                        </Field>

                        <Field label="OAuth Client Secret" required>
                            <Input
                                type="password"
                                placeholder="Enter your OAuth Client Secret"
                                value={clientSecret}
                                onChange={(_, data) => setClientSecret(data.value)}
                                disabled={isProcessing}
                            />
                        </Field>
                    </div>

                    <div className={styles.actions}>
                        <Button appearance="secondary" onClick={onCancel} disabled={isProcessing}>
                            {intl.formatMessage(ServiceNowResources.oauthCancel)}
                        </Button>
                        <Button appearance="primary" onClick={handleSubmit} disabled={!isCredentialsValid || isProcessing}>
                            {intl.formatMessage(ServiceNowResources.oauthNextAuthorize)}
                        </Button>
                    </div>
                </>
            )}

            {currentStep === 'authorizing' && (
                <Card className={styles.authorizingCard}>
                    <Spinner size="extra-large" />
                    <Text weight="semibold" size={400}>
                        {intl.formatMessage(ServiceNowResources.oauthAuthorizingTitle)}
                    </Text>
                    <Text size={300}>{intl.formatMessage(ServiceNowResources.oauthAuthorizingPopupMessage)}</Text>
                    <ProgressBar />
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                        {intl.formatMessage(ServiceNowResources.oauthAuthorizingWaitMessage)}
                    </Text>
                </Card>
            )}

            {currentStep === 'complete' && (
                <>
                    <Card className={styles.authorizingCard}>
                        <CheckmarkCircle24Regular style={{ fontSize: '64px', color: tokens.colorPaletteGreenForeground1 }} />
                        <Text weight="semibold" size={500}>
                            {intl.formatMessage(ServiceNowResources.oauthConnectionAuthorized)}
                        </Text>
                        <Text size={300}>{intl.formatMessage(ServiceNowResources.oauthConnectionSuccessMessage)}</Text>
                        <MessageBar intent="success" shape="square">
                            <MessageBarBody>{intl.formatMessage(ServiceNowResources.oauthConnectionReadyMessage)}</MessageBarBody>
                        </MessageBar>
                    </Card>

                    <div className={styles.actions}>
                        <Button appearance="primary" onClick={onCancel}>
                            {intl.formatMessage(ServiceNowResources.oauthDone)}
                        </Button>
                    </div>
                </>
            )}
        </div>
    );
};

export default ServiceNowOAuthWizard;

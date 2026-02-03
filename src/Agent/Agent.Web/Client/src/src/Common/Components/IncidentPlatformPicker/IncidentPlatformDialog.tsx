import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { FC, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { OnboardingWizardResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { IncidentManagementType } from '../../Contracts/Azure/SreAgent';
import { IncidentPlatformPicker, isIncidentPlatformFormValid } from './IncidentPlatformPicker';

export interface IncidentPlatformConfig {
    type: IncidentManagementType;
    pagerDutyApiKey?: string;
    serviceNowEndpoint?: string;
    serviceNowUsername?: string;
    serviceNowPassword?: string;
}

interface IncidentPlatformDialogProps {
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
    onSave: (config: IncidentPlatformConfig) => Promise<boolean>;
    initialConfig?: Partial<IncidentPlatformConfig>;
}

const useIncidentPlatformDialogStyles = makeStyles({
    dialogSurface: {
        maxWidth: '600px',
        width: '100%',
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        paddingBottom: tokens.spacingVerticalXXL,
    },
});

export const IncidentPlatformDialog: FC<IncidentPlatformDialogProps> = ({ isOpen, onOpenChange, onSave, initialConfig }) => {
    const intl = useIntl();
    const styles = useIncidentPlatformDialogStyles();

    const [selectedType, setSelectedType] = useState<IncidentManagementType | undefined>(initialConfig?.type);
    const [pagerDutyApiKey, setPagerDutyApiKey] = useState(initialConfig?.pagerDutyApiKey ?? '');
    const [serviceNowEndpoint, setServiceNowEndpoint] = useState(initialConfig?.serviceNowEndpoint ?? '');
    const [serviceNowUsername, setServiceNowUsername] = useState(initialConfig?.serviceNowUsername ?? '');
    const [serviceNowPassword, setServiceNowPassword] = useState(initialConfig?.serviceNowPassword ?? '');
    const [isSaving, setIsSaving] = useState(false);

    useEffect(() => {
        setSelectedType(initialConfig?.type);
        setPagerDutyApiKey(initialConfig?.pagerDutyApiKey ?? '');
        setServiceNowEndpoint(initialConfig?.serviceNowEndpoint ?? '');
        setServiceNowUsername(initialConfig?.serviceNowUsername ?? '');
        setServiceNowPassword(initialConfig?.serviceNowPassword ?? '');
    }, [initialConfig]);

    const pickerValues: IncidentPlatformConfig = useMemo(
        () => ({
            type: selectedType || IncidentManagementType.None,
            pagerDutyApiKey,
            serviceNowEndpoint,
            serviceNowUsername,
            serviceNowPassword,
        }),
        [selectedType, pagerDutyApiKey, serviceNowEndpoint, serviceNowUsername, serviceNowPassword]
    );

    const isFormValid = useMemo(() => isIncidentPlatformFormValid(pickerValues), [pickerValues]);

    const handleCancel = useCallback(() => {
        onOpenChange(false);
    }, [onOpenChange]);

    const handleSave = useCallback(async () => {
        if (!selectedType || !isFormValid) return;

        setIsSaving(true);
        const config: IncidentPlatformConfig = {
            type: selectedType,
            ...(selectedType === IncidentManagementType.PagerDuty && { pagerDutyApiKey }),
            ...(selectedType === IncidentManagementType.ServiceNow && {
                serviceNowEndpoint,
                serviceNowUsername,
                serviceNowPassword,
            }),
        };

        const success = await onSave(config);
        setIsSaving(false);

        if (success) {
            onOpenChange(false);
        }
    }, [selectedType, isFormValid, pagerDutyApiKey, serviceNowEndpoint, serviceNowUsername, serviceNowPassword, onSave, onOpenChange]);

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody>
                    <DialogTitle
                        action={
                            <Button
                                appearance="transparent"
                                icon={<Dismiss24Regular />}
                                onClick={handleCancel}
                                aria-label={intl.formatMessage(SreAgentResources.close)}
                            />
                        }
                    >
                        {intl.formatMessage(OnboardingWizardResources.incidentPlatform)}
                    </DialogTitle>
                    <DialogContent className={styles.dialogContent}>
                        <IncidentPlatformPicker
                            values={pickerValues}
                            onPlatformSelect={setSelectedType}
                            onPagerDutyApiKeyChange={setPagerDutyApiKey}
                            onServiceNowEndpointChange={setServiceNowEndpoint}
                            onServiceNowUsernameChange={setServiceNowUsername}
                            onServiceNowPasswordChange={setServiceNowPassword}
                            showDescription={true}
                        />
                    </DialogContent>
                </DialogBody>
                <DialogActions>
                    <Button appearance="primary" onClick={handleSave} disabled={!isFormValid || isSaving}>
                        {isSaving ? intl.formatMessage(OnboardingWizardResources.saving) : intl.formatMessage(SreAgentResources.save)}
                    </Button>
                    <Button appearance="secondary" onClick={handleCancel}>
                        {intl.formatMessage(SreAgentResources.cancel)}
                    </Button>
                </DialogActions>
            </DialogSurface>
        </Dialog>
    );
};

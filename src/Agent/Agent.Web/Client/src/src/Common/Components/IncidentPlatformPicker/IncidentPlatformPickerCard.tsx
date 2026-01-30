import { FC, useCallback, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentContext } from '../../../Space/Contracts/Context';
import { OnboardingWizardResources } from '../../../Strings/SREAgentResources';
import { useAzPortalContext } from '../../AzPortalProxy/Providers/AzPortalProxyContext';
import { IncidentManagementType } from '../../Contracts/Azure/SreAgent';
import { SelectableCard } from '../SelectableCard/SelectableCard';
import { IncidentPlatformConfig, IncidentPlatformDialog } from './IncidentPlatformDialog';

interface IncidentPlatformPickerCardProps {
    onSaveSuccess?: () => void;
}

export const IncidentPlatformPickerCard: FC<IncidentPlatformPickerCardProps> = ({ onSaveSuccess }) => {
    const intl = useIntl();
    const azPortalContext = useAzPortalContext();
    const { agentObj, patchAgent } = useContext(SreAgentContext);

    const [isDialogOpen, setIsDialogOpen] = useState(false);

    const existingConfig = agentObj?.properties?.incidentManagementConfiguration;

    const initialConfig: Partial<IncidentPlatformConfig> = {
        type: existingConfig?.type,
        serviceNowEndpoint: existingConfig?.connectionUrl,
    };

    const handleSave = useCallback(
        async (config: IncidentPlatformConfig): Promise<boolean> => {
            let incidentManagementConfig = null;

            if (config.type !== IncidentManagementType.None) {
                incidentManagementConfig = {
                    type: config.type,
                    connectionName: config.type.toLowerCase(),
                    ...(config.type === IncidentManagementType.PagerDuty && {
                        connectionKey: config.pagerDutyApiKey,
                    }),
                    ...(config.type === IncidentManagementType.ServiceNow && {
                        connectionUrl: config.serviceNowEndpoint,
                        connectionKey: JSON.stringify({
                            username: config.serviceNowUsername,
                            password: config.serviceNowPassword,
                        }),
                    }),
                };
            }

            const response = await patchAgent({
                properties: {
                    incidentManagementConfiguration: incidentManagementConfig,
                },
            });

            if (response.metadata.success) {
                azPortalContext.log({
                    action: 'incident-platform-config',
                    actionModifier: 'saved',
                    logLevel: 'info',
                    data: { type: config.type },
                });
                onSaveSuccess?.();
                return true;
            }

            azPortalContext.log({
                action: 'incident-platform-config',
                actionModifier: 'save-failed',
                logLevel: 'error',
                data: { type: config.type },
            });
            return false;
        },
        [patchAgent, azPortalContext, onSaveSuccess]
    );

    return (
        <>
            <SelectableCard
                onSelect={() => setIsDialogOpen(true)}
                icon={<img src="./AzMonitor.svg" alt="" aria-hidden="true" width={20} height={20} />}
                title={intl.formatMessage(OnboardingWizardResources.incidentPlatform)}
            />
            <IncidentPlatformDialog
                isOpen={isDialogOpen}
                onOpenChange={setIsDialogOpen}
                onSave={handleSave}
                initialConfig={initialConfig}
            />
        </>
    );
};

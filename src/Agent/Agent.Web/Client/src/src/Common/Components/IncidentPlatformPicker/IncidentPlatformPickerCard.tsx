import { FC } from 'react';
import { useIntl } from 'react-intl';
import { OnboardingWizardResources } from '../../../Strings/SREAgentResources';
import { SelectableCard } from '../SelectableCard/SelectableCard';
import { IncidentPlatformDialog } from './IncidentPlatformDialog';
import { UseIncidentPlatformPickerResult } from './useIncidentPlatformPicker';

export const IncidentPlatformPickerCard: FC<{ picker: UseIncidentPlatformPickerResult }> = ({ picker }) => {
    const intl = useIntl();

    return (
        <>
            <SelectableCard
                onSelect={() => picker.setIsDialogOpen(true)}
                icon={<img src="./AzMonitor.svg" alt="" aria-hidden="true" width={20} height={20} />}
                title={intl.formatMessage(OnboardingWizardResources.incidentPlatform)}
            />
            <IncidentPlatformDialog
                isOpen={picker.isDialogOpen}
                onOpenChange={picker.setIsDialogOpen}
                onSave={picker.saveIncidentPlatform}
                initialConfig={picker.initialConfig}
            />
        </>
    );
};

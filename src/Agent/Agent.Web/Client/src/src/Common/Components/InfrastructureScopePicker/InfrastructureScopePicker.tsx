import { FC } from 'react';
import { useIntl } from 'react-intl';
import ResourceGroupIcon from '../../../../assets/ResourceGroup.svg';
import SubscriptionIcon from '../../../../assets/Subscription.svg';
import { ResourceGroupPickerDialog as BaseResourceGroupPickerDialog } from '../../../Space/Onboarding/Steps/ResourceGroupPickerDialog';
import { SubscriptionPickerDialog as BaseSubscriptionPickerDialog } from '../../../Space/Onboarding/Steps/SubscriptionPickerDialog';
import { OnboardingWizardResources } from '../../../Strings/SREAgentResources';
import { SelectableCard } from '../SelectableCard/SelectableCard';
import { useInfrastructureScopePicker, UseInfrastructureScopePickerResult } from './useInfrastructureScopePicker';

interface InfrastructureScopePickerProps {
    picker: UseInfrastructureScopePickerResult;
}

export const SubscriptionPickerCard: FC<InfrastructureScopePickerProps> = ({ picker }) => {
    const intl = useIntl();

    return (
        <SelectableCard
            onSelect={() => picker.setIsSubscriptionPickerOpen(true)}
            disabled={picker.subscriptionsLoading}
            icon={<img src={SubscriptionIcon} alt="" aria-hidden="true" />}
            title={intl.formatMessage(OnboardingWizardResources.addSubscription)}
        />
    );
};

export const ResourceGroupPickerCard: FC<InfrastructureScopePickerProps> = ({ picker }) => {
    const intl = useIntl();

    return (
        <SelectableCard
            onSelect={() => picker.setIsResourceGroupPickerOpen(true)}
            icon={<img src={ResourceGroupIcon} alt="" aria-hidden="true" />}
            title={intl.formatMessage(OnboardingWizardResources.addResourceGroup)}
        />
    );
};

interface InfrastructureScopeDialogsProps {
    picker: UseInfrastructureScopePickerResult;
}

export const InfrastructureScopeDialogs: FC<InfrastructureScopeDialogsProps> = ({ picker }) => {
    return (
        <>
            <BaseSubscriptionPickerDialog
                isOpen={picker.isSubscriptionPickerOpen}
                onDismiss={() => picker.setIsSubscriptionPickerOpen(false)}
                onApply={picker.handleSubscriptionsApplied}
                initialSelectedIds={picker.selectedSubscriptionIds}
                selectableSubscriptions={picker.selectableSubscriptions}
                disabledSubscriptions={picker.disabledSubscriptions}
                isLoading={picker.subscriptionsWithRolesLoading}
            />

            <BaseResourceGroupPickerDialog
                isOpen={picker.isResourceGroupPickerOpen}
                onDismiss={() => picker.setIsResourceGroupPickerOpen(false)}
                onApply={picker.handleResourceGroupsApplied}
                initialSelectedIds={picker.selectedResourceGroupIds}
                allSubscriptions={picker.allSubscriptions}
                defaultSubscriptionId={picker.defaultSubscriptionId}
            />
        </>
    );
};

export { useInfrastructureScopePicker };
export type { UseInfrastructureScopePickerResult };

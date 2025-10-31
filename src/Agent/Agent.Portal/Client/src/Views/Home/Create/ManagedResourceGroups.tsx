import { useFormikContext } from 'formik';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ResourceGroupPicker } from '../../../Common/Components/ResourceGroupPicker/ResourceGroupPicker';
import { TextWithLink } from '../../../Common/Components/TextWithLink';
import { LearnMoreLinks } from '../../../Common/Constants/Links';
import { useSubscriptions } from '../../../Common/Contexts/SubscriptionsContext';
import { ResourceGroup } from '../../../Common/Contracts/Arm';
import { PortalResources } from '../../../Strings/Resources';
import { SreAgentCreateFormProps } from './CreateAgentDialog';

export const ManagedResourceGroups = () => {
    const intl = useIntl();
    const { values, setFieldValue } = useFormikContext<SreAgentCreateFormProps>();
    const { subscriptions } = useSubscriptions();

    // Map subscriptions to the format expected by ResourceGroupPicker
    const subscriptionOptions = useMemo(
        () =>
            subscriptions.map(sub => ({
                key: sub.subscriptionId,
                text: sub.displayName,
                data: sub,
            })),
        [subscriptions]
    );

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
            <TextWithLink
                text={intl.formatMessage(PortalResources.managedResourceGroupDescription)}
                linkUrl={LearnMoreLinks.sreAgentManagedRgPermissions}
                linkText={intl.formatMessage(PortalResources.learnMoreAboutManagedRgPermissions)}
                dontShowLearnMoreLinkIcon
            />

            <ResourceGroupPicker
                subscriptionId={values.subscriptionId}
                existingResourceGroupIds={values.managedResourceGroups?.map(rg => rg.id) || []}
                onChangeSelection={(selectedResourceGroups: ResourceGroup[]) => {
                    setFieldValue('managedResourceGroups', selectedResourceGroups);
                }}
                subscriptionOptions={subscriptionOptions}
            />
        </div>
    );
};

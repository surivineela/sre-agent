import { MessageBar, MessageBarBody } from '@fluentui/react-components';
import { Warning16Filled } from '@fluentui/react-icons';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';
import { LearnMoreLinks } from '../../Constants/Links';
import { TextWithLink } from '../TextWithLink';

/**
 * Warning banner displayed when Geneva Actions is not available for the current tenant.
 * Geneva Actions is only supported for AME, PME, and Torus tenants.
 */
export const GenevaActionsWarningBanner = () => {
    const intl = useIntl();

    return (
        <MessageBar intent="warning" icon={<Warning16Filled />}>
            <MessageBarBody>
                <TextWithLink text={intl.formatMessage(PortalResources.genevaActionsWarning)} linkUrl={LearnMoreLinks.genevaActions} />
            </MessageBarBody>
        </MessageBar>
    );
};

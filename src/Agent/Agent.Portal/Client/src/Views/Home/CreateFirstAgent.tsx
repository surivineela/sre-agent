import { Button } from '@fluentui/react-components';
import { Add16Regular, Library16Regular } from '@fluentui/react-icons';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';

export const CreateFirstAgent = () => {
    const intl = useIntl();

    return (
        <div>
            <div>Illustration</div>

            <div>{intl.formatMessage(PortalResources.createYourFirstAgent)}</div>

            <div>{intl.formatMessage(PortalResources.createYourFirstAgentSubtext)}</div>

            <div>
                <Button appearance="primary" icon={<Add16Regular />}>
                    {intl.formatMessage(PortalResources.createAgent)}
                </Button>
                <Button icon={<Library16Regular />}>{intl.formatMessage(PortalResources.viewPopularSkills)}</Button>
            </div>
        </div>
    );
};

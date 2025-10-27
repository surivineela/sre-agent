import { Button, Card, Image, Text } from '@fluentui/react-components';
import { Add16Regular, Library16Regular } from '@fluentui/react-icons';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';

export const CreateFirstAgent = () => {
    const intl = useIntl();

    return (
        <Card style={{ width: 1000, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 32 }}>
            <Image src="./SreAgent.svg" width={192} height={192} alt={intl.formatMessage(PortalResources.azureSreAgent)} />

            <Text weight="semibold">{intl.formatMessage(PortalResources.createYourFirstAgent)}</Text>

            <Text>{intl.formatMessage(PortalResources.createYourFirstAgentSubtext)}</Text>

            <div style={{ display: 'flex', gap: 8 }}>
                <Button appearance="primary" icon={<Add16Regular />}>
                    {intl.formatMessage(PortalResources.createAgent)}
                </Button>
                <Button icon={<Library16Regular />}>{intl.formatMessage(PortalResources.viewPopularSkills)}</Button>
            </div>
        </Card>
    );
};

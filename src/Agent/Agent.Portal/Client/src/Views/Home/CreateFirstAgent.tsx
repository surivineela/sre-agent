import { Body2, Button, Card, Image, Title1 } from '@fluentui/react-components';
import { Add16Regular, Library16Regular } from '@fluentui/react-icons';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';

export const CreateFirstAgent = () => {
    const intl = useIntl();

    return (
        <Card style={{ height: 575, width: 1000, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 32 }}>
            <Image src='SreAgent.svg' width={192} height={192} alt={intl.formatMessage(PortalResources.azureSreAgent)} />

            <Title1>{intl.formatMessage(PortalResources.createYourFirstAgent)}</Title1>

            <Body2 style={{ maxWidth: 550, textAlign: 'center' }}>{intl.formatMessage(PortalResources.createYourFirstAgentSubtext)}</Body2>

            <div style={{ display: 'flex', gap: 8 }}>
                <Button appearance="primary" icon={<Add16Regular />}>
                    {intl.formatMessage(PortalResources.createAgent)}
                </Button>
                <Button icon={<Library16Regular />}>{intl.formatMessage(PortalResources.viewPopularSkills)}</Button>
            </div>
        </Card>
    );
};

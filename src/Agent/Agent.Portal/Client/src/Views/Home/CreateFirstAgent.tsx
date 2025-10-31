import { Body2, Button, Card, Image, makeStyles, Title1 } from '@fluentui/react-components';
import { Add16Regular, Library16Regular } from '@fluentui/react-icons';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';

const useStyles = makeStyles({
    card: {
        height: '575px',
        width: '1000px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: '32px',
    },
    subtitle: {
        maxWidth: '550px',
        textAlign: 'center',
    },
    buttonContainer: {
        display: 'flex',
        gap: '8px',
    },
});

interface CreateFirstAgentProps {
    onClickCreate: () => void;
}

export const CreateFirstAgent = ({ onClickCreate }: CreateFirstAgentProps) => {
    const intl = useIntl();
    const styles = useStyles();

    return (
        <Card className={styles.card}>
            <Image src="SreAgent.svg" width={192} height={192} alt={intl.formatMessage(PortalResources.azureSreAgent)} />

            <Title1>{intl.formatMessage(PortalResources.createYourFirstAgent)}</Title1>

            <Body2 className={styles.subtitle}>{intl.formatMessage(PortalResources.createYourFirstAgentSubtext)}</Body2>

            <div className={styles.buttonContainer}>
                <Button appearance="primary" icon={<Add16Regular />} onClick={onClickCreate}>
                    {intl.formatMessage(PortalResources.createAgent)}
                </Button>
                <Button icon={<Library16Regular />}>{intl.formatMessage(PortalResources.viewPopularSkills)}</Button>
            </div>
        </Card>
    );
};

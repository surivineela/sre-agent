import { Body2, Button, Image, makeStyles, Title1 } from '@fluentui/react-components';
import { Add16Regular } from '@fluentui/react-icons';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';

const useStyles = makeStyles({
    container: {
        height: '575px',
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

interface NoAgentSpacesProps {
    onClickCreate: () => void;
}

export const NoAgentSpaces = ({ onClickCreate }: NoAgentSpacesProps) => {
    const intl = useIntl();
    const styles = useStyles();

    return (
        <div className={styles.container}>
            <Image src="SreAgentSpace.svg" width={144} height={144} alt={intl.formatMessage(PortalResources.agentSpace)} />

            <Title1>{intl.formatMessage(PortalResources.noAgentSpacesFound)}</Title1>

            <Body2 className={styles.subtitle}>{intl.formatMessage(PortalResources.noAgentSpacesFoundDescription)}</Body2>

            <div className={styles.buttonContainer}>
                <Button appearance="primary" icon={<Add16Regular />} onClick={onClickCreate}>
                    {intl.formatMessage(PortalResources.createAgentSpace)}
                </Button>
            </div>
        </div>
    );
};

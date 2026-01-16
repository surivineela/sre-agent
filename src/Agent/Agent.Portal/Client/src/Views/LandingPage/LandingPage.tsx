import { Button, Image, makeStyles, tokens } from '@fluentui/react-components';
import { useIntl } from 'react-intl';
import { useAuth } from '../../Common/Contexts/AuthContext';
import { PortalResources } from '../../Strings/Resources';

const useStyles = makeStyles({
    container: {
        height: '100%',
        background: `radial-gradient(63.13% 56.83% at 36.87% 69.97%, ${tokens.colorPaletteMarigoldBackground2} 0%, ${tokens.colorPalettePlatinumBackground2} 100%)`,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-evenly',
        padding: tokens.spacingHorizontalL,
        boxSizing: 'border-box',
        '@media (max-width: 768px)': {
            flexDirection: 'column',
            justifyContent: 'center',
            gap: tokens.spacingVerticalXXL,
        },
    },
    hero: {
        padding: `${tokens.spacingVerticalXXXL} ${tokens.spacingHorizontalXXL}`,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXL,
        '@media (max-width: 768px)': {
            padding: tokens.spacingHorizontalM,
            alignItems: 'center',
            textAlign: 'center',
        },
    },
    heading: {
        fontSize: '46px',
        fontWeight: tokens.fontWeightSemibold,
        lineHeight: '58px',
        color: tokens.colorNeutralForeground1,
        margin: 0,
        maxWidth: '600px',
        '@media (max-width: 768px)': {
            fontSize: '32px',
            lineHeight: '40px',
        },
    },
    description: {
        fontSize: tokens.fontSizeBase400,
        lineHeight: '22px',
        color: tokens.colorNeutralForeground2,
        margin: 0,
        maxWidth: '600px',
    },
    buttonContainer: {
        display: 'flex',
        flexDirection: 'row',
        gap: tokens.spacingHorizontalM,
        alignItems: 'center',
    },
    primaryButton: {
        paddingLeft: tokens.spacingHorizontalL,
        paddingRight: tokens.spacingHorizontalL,
    },
    secondaryButton: {
        paddingLeft: tokens.spacingHorizontalL,
        paddingRight: tokens.spacingHorizontalL,
    },
    heroImage: {
        '@media (max-width: 768px)': {
            width: '180px',
            height: '180px',
        },
    },
});

export const LandingPage = () => {
    const intl = useIntl();
    const { signIn } = useAuth();
    const styles = useStyles();

    return (
        <div className={styles.container}>
            <div className={styles.hero}>
                <h1 className={styles.heading}>{intl.formatMessage(PortalResources.reduceSiteReliabilityExpensesPreview)}</h1>
                <p className={styles.description}>{intl.formatMessage(PortalResources.reduceSiteReliabilityExpensesDescription)}</p>
                <div className={styles.buttonContainer}>
                    <Button appearance="primary" size="large" className={styles.primaryButton} onClick={() => signIn()}>
                        {intl.formatMessage(PortalResources.signIn)}
                    </Button>
                </div>
            </div>

            <Image
                src="SreAgent.svg"
                width={256}
                height={256}
                alt={intl.formatMessage(PortalResources.azureSreAgent)}
                className={styles.heroImage}
            />
        </div>
    );
};

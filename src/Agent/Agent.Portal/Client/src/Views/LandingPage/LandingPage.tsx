import { Button, Image, makeStyles, tokens } from '@fluentui/react-components';
import { useIntl } from 'react-intl';
import { LearnMoreLinks, TryAzureForFreeLink } from '../../Common/Constants/Links';
import { PortalResources } from '../../Strings/Resources';

const useStyles = makeStyles({
    container: {
        height: '100%',
        background: `radial-gradient(63.13% 56.83% at 36.87% 69.97%, ${tokens.colorPaletteMarigoldBackground2} 0%, ${tokens.colorPalettePlatinumBackground2} 100%)`,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-evenly',
    },
    hero: {
        padding: `${tokens.spacingVerticalXXXL} ${tokens.spacingHorizontalXXL}`,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXL,
    },
    heading: {
        fontSize: '46px',
        fontWeight: tokens.fontWeightSemibold,
        lineHeight: '58px',
        color: tokens.colorNeutralForeground1,
        margin: 0,
        maxWidth: '600px',
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
});

export const LandingPage = () => {
    const intl = useIntl();
    const styles = useStyles();

    return (
        <div className={styles.container}>
            <div className={styles.hero}>
                <h1 className={styles.heading}>{intl.formatMessage(PortalResources.reduceSiteReliabilityExpenses)}</h1>
                <p className={styles.description}>{intl.formatMessage(PortalResources.reduceSiteReliabilityExpensesDescription)}</p>
                <div className={styles.buttonContainer}>
                    <Button
                        appearance="primary"
                        size="large"
                        className={styles.primaryButton}
                        onClick={() => window.open(TryAzureForFreeLink, '_blank', 'noopener,noreferrer')}
                    >
                        {intl.formatMessage(PortalResources.tryAzureForFree)}
                    </Button>
                    <Button
                        size="large"
                        className={styles.secondaryButton}
                        onClick={() => window.open(LearnMoreLinks.sreAgentOverview, '_blank', 'noopener,noreferrer')}
                    >
                        {intl.formatMessage(PortalResources.exploreAgentSkills)}
                    </Button>
                </div>
            </div>

            <Image src="SreAgent.svg" width={256} height={256} alt={intl.formatMessage(PortalResources.azureSreAgent)} />
        </div>
    );
};

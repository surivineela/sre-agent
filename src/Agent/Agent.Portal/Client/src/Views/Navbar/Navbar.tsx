import { Image, makeStyles, Text, tokens, Tooltip } from '@fluentui/react-components';
import { useIntl } from 'react-intl';
import { useNavigate } from 'react-router-dom';
import { PortalResources } from '../../Strings/Resources';
import { NotificationButton } from './NotificationButton';
import { SettingsContent } from './SettingsContent';
import { UserAuthContent } from './UserAuthContent';

const useStyles = makeStyles({
    navbar: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'space-between',
        gap: '20px',
        height: '44px',
        paddingLeft: tokens.spacingHorizontalXL,
        paddingRight: tokens.spacingHorizontalXL,
        alignItems: 'center',
        backgroundColor: tokens.colorNeutralBackground4,
    },
    section: {
        display: 'flex',
        flexDirection: 'row',
        alignContent: 'center',
        gap: '8px',
    },
    logoSection: {
        display: 'flex',
        flexDirection: 'row',
        alignContent: 'center',
        gap: '8px',
        cursor: 'pointer',
        paddingTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalS,
        paddingLeft: tokens.spacingHorizontalS,
        paddingRight: tokens.spacingHorizontalS,
        borderRadius: tokens.borderRadiusMedium,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground4Hover,
        },
        ':active': {
            backgroundColor: tokens.colorNeutralBackground4Pressed,
        },
    },
});

export const Navbar = () => {
    const intl = useIntl();
    const navigate = useNavigate();
    const styles = useStyles();

    return (
        <div className={styles.navbar}>
            <Tooltip content={intl.formatMessage(PortalResources.azureSreAgents)} relationship="label">
                <div className={styles.logoSection} onClick={() => navigate('/')}>
                    <Image src='SreAgent.svg' width={18} height={18} alt={intl.formatMessage(PortalResources.azureSreAgents)} />
                    <Text weight="semibold">{intl.formatMessage(PortalResources.azureSreAgents)}</Text>
                </div>
            </Tooltip>

            <div className={styles.section}>
                <NotificationButton />

                <SettingsContent />

                <UserAuthContent />
            </div>
        </div>
    );
};

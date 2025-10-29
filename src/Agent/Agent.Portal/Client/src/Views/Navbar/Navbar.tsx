import { Image, makeStyles, Text, tokens, Tooltip } from '@fluentui/react-components';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate } from 'react-router-dom';
import { PortalResources } from '../../Strings/Resources';
import { NotificationButton } from './NotificationButton';
import { SettingsContent } from './SettingsContent';
import { UserAuthContent } from './UserAuthContent';
import { useMemo } from 'react';

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
    breadcrumbSeparator: {
        color: tokens.colorNeutralForeground3,
    },
    agentName: {
        color: tokens.colorNeutralForeground1,
    },
});

export const Navbar = () => {
    const intl = useIntl();
    const navigate = useNavigate();
    const location = useLocation();
    const styles = useStyles();

    // TODO: Use ArmId.parse for this?
    // Parse agent name from the route if we're on an agent page
    const agentName = useMemo(() => decodeURIComponent(location.pathname.match(/^\/agents\/(.+)/)?.[1] ?? '').split('/')?.pop()?.split('/')?.pop(), [location.pathname]);

    return (
        <div className={styles.navbar}>
            <Tooltip content={intl.formatMessage(PortalResources.azureSreAgents)} relationship="label">
                <div className={styles.logoSection} onClick={() => navigate('/')}>
                    <Image src='SreAgent.svg' width={18} height={18} alt={intl.formatMessage(PortalResources.azureSreAgents)} />
                    <Text weight="semibold">{intl.formatMessage(PortalResources.azureSreAgents)}</Text>
                    {agentName && (
                        <>
                            {/* TODO: Fancy selector thingy */}
                            <Text className={styles.breadcrumbSeparator}>/</Text>
                            <Text weight="semibold" className={styles.agentName}>{decodeURIComponent(agentName)}</Text>
                        </>
                    )}
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

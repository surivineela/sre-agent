import { Button, Image, makeStyles, Text, tokens, Tooltip } from '@fluentui/react-components';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useParams } from 'react-router-dom';
import { PreviewBadge } from '../../Common/Components/PreviewBadge';
import { LearnMoreLinks } from '../../Common/Constants/Links';
import { useAuth } from '../../Common/Contexts/AuthContext';
import { usePersistentNavigate } from '../../Common/Hooks/usePersistentNavigate';
import { parseArmId } from '../../Common/Utilities/ArmId';
import { parseResourceRoute } from '../../Common/Utilities/ResourceRouting';
import { PortalResources } from '../../Strings/Resources';
import { FeedbackButton } from './FeedbackButton';
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
        minHeight: '44px',
        maxHeight: '44px',
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
        flexShrink: 0,
    },
    logoSection: {
        display: 'flex',
        flexDirection: 'row',
        alignContent: 'center',
        alignItems: 'center',
        gap: '8px',
        cursor: 'pointer',
        paddingTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalS,
        paddingLeft: tokens.spacingHorizontalS,
        paddingRight: tokens.spacingHorizontalS,
        borderRadius: tokens.borderRadiusMedium,
        minWidth: 0,
        overflow: 'hidden',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground4Hover,
        },
        ':active': {
            backgroundColor: tokens.colorNeutralBackground4Pressed,
        },
    },
    logoText: {
        flexShrink: 0,
    },
    breadcrumbSeparator: {
        color: tokens.colorNeutralForeground3,
        flexShrink: 0,
    },
    agentName: {
        color: tokens.colorNeutralForeground1,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        minWidth: 0,
    },
    navbarButton: {
        minWidth: '50px',
    },
});

export const Navbar = () => {
    const intl = useIntl();
    const navigate = usePersistentNavigate();
    const styles = useStyles();
    const location = useLocation();
    const { agentName: encodedExternalAgentName } = useParams<{ agentName: string }>();
    const { isAuthenticated } = useAuth();

    // Parse agent name from the route if we're on an agent page
    const agentName = useMemo(() => {
        // External agent name from route parameter
        if (encodedExternalAgentName) {
            return decodeURIComponent(encodedExternalAgentName);
        }

        // ARM-based agent name from resource ID (path-based routing)
        const agentRoute = parseResourceRoute(location.pathname, '/agents');
        if (agentRoute) {
            return parseArmId(agentRoute.resourceId).resourceName;
        }

        return undefined;
    }, [location.pathname, encodedExternalAgentName]);

    // Parse space name from the route if we're on an agent space page
    const spaceName = useMemo(() => {
        const spaceRoute = parseResourceRoute(location.pathname, '/spaces');
        if (spaceRoute) {
            return parseArmId(spaceRoute.resourceId).resourceName;
        }
        return undefined;
    }, [location.pathname]);

    return (
        <div className={styles.navbar}>
            <Tooltip content={intl.formatMessage(PortalResources.azureSreAgents)} relationship="label">
                <div className={styles.logoSection} onClick={() => navigate('/')}>
                    <Image src="SreAgent.svg" width={18} height={18} alt={intl.formatMessage(PortalResources.azureSreAgents)} />
                    <Text weight="semibold" className={styles.logoText}>
                        {intl.formatMessage(PortalResources.azureSreAgents)}
                    </Text>
                    <PreviewBadge />
                    {agentName && (
                        <>
                            {/* TODO: Fancy selector thingy */}
                            <Text className={styles.breadcrumbSeparator}>/</Text>
                            <Text weight="semibold" className={styles.agentName} title={agentName}>
                                {agentName}
                            </Text>
                        </>
                    )}
                    {spaceName && (
                        <>
                            <Text className={styles.breadcrumbSeparator}>/</Text>
                            <Text>{intl.formatMessage(PortalResources.agentSpaces)}</Text>
                            <Text className={styles.breadcrumbSeparator}>/</Text>
                            <Text weight="semibold" className={styles.agentName} title={spaceName}>
                                {spaceName}
                            </Text>
                        </>
                    )}
                </div>
            </Tooltip>

            <div className={styles.section}>
                <Button
                    className={styles.navbarButton}
                    appearance="subtle"
                    onClick={() => window.open(LearnMoreLinks.sreAgentOverview, '_blank', 'noopener,noreferrer')}
                >
                    {intl.formatMessage(PortalResources.docs)}
                </Button>

                {isAuthenticated && <NotificationButton />}

                {isAuthenticated && <FeedbackButton />}

                <SettingsContent />

                <UserAuthContent />
            </div>
        </div>
    );
};

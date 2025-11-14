import { Button, Image, makeStyles, Text, tokens, Tooltip } from '@fluentui/react-components';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { useParams } from 'react-router-dom';
import { LearnMoreLinks } from '../../Common/Constants/Links';
import { usePersistentNavigate } from '../../Common/Hooks/usePersistentNavigate';
import { parseArmId } from '../../Common/Utilities/ArmId';
import { PortalResources } from '../../Strings/Resources';
import { FeedbackButton } from './FeedbackButton';
import { NotificationButton } from './NotificationButton';
import { SettingsContent } from './SettingsContent';
import { UserAuthContent } from './UserAuthContent';
import { useAuth } from '../../Common/Contexts/AuthContext';

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
    navbarButton: {
        minWidth: '50px',
    },
});

export const Navbar = () => {
    const intl = useIntl();
    const navigate = usePersistentNavigate();
    const styles = useStyles();
    const { agentId: encodedAgentId } = useParams<{ agentId: string }>();
    const { isAuthenticated } = useAuth();

    // Parse agent name from the route if we're on an agent page
    const agentName = useMemo(() => {
        if (!encodedAgentId) return undefined;

        const agentRscId = decodeURIComponent(encodedAgentId);
        return parseArmId(agentRscId).resourceName;
    }, [encodedAgentId]);

    return (
        <div className={styles.navbar}>
            <Tooltip content={intl.formatMessage(PortalResources.azureSreAgents)} relationship="label">
                <div className={styles.logoSection} onClick={() => navigate('/')}>
                    <Image src="SreAgent.svg" width={18} height={18} alt={intl.formatMessage(PortalResources.azureSreAgents)} />
                    <Text weight="semibold">{intl.formatMessage(PortalResources.azureSreAgents)}</Text>
                    {agentName && (
                        <>
                            {/* TODO: Fancy selector thingy */}
                            <Text className={styles.breadcrumbSeparator}>/</Text>
                            <Text weight="semibold" className={styles.agentName}>
                                {decodeURIComponent(agentName)}
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

import {
    Button,
    Image,
    makeStyles,
    Persona,
    Popover,
    PopoverSurface,
    PopoverTrigger,
    Text,
    tokens,
    Tooltip,
} from '@fluentui/react-components';
import { Settings32Regular } from '@fluentui/react-icons';
import { useIntl } from 'react-intl';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../Common/Contexts/AuthContext';
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
    popoverSurface: {
        minWidth: '320px',
        padding: tokens.spacingVerticalL,
    },
});

export const Navbar = () => {
    const intl = useIntl();
    const navigate = useNavigate();
    const { status, user } = useAuth();
    const styles = useStyles();

    const isPending = status === 'pending';
    const personaName = user?.name;
    const personaSecondaryText = user?.username;

    return (
        <div className={styles.navbar}>
            <Tooltip content={intl.formatMessage(PortalResources.azureSreAgents)} relationship="label">
                <div className={styles.logoSection} onClick={() => navigate('/')}>
                    <Image src="./SreAgent.svg" width={18} height={18} alt={intl.formatMessage(PortalResources.azureSreAgents)} />
                    <Text weight="semibold">{intl.formatMessage(PortalResources.azureSreAgents)}</Text>
                </div>
            </Tooltip>

            <div className={styles.section}>
                <NotificationButton />

                <Popover>
                    <PopoverTrigger>
                        <Tooltip content={intl.formatMessage(PortalResources.settings)} relationship="label">
                            <Button
                                icon={<Settings32Regular />}
                                appearance="subtle"
                                disabled={isPending}
                                aria-label={intl.formatMessage(PortalResources.settings)}
                            />
                        </Tooltip>
                    </PopoverTrigger>

                    <PopoverSurface className={styles.popoverSurface}>
                        <SettingsContent />
                    </PopoverSurface>
                </Popover>

                <Popover>
                    <PopoverTrigger>
                        <Persona avatar={{ image: { src: '' } }} name={personaName} secondaryText={personaSecondaryText} />
                    </PopoverTrigger>

                    <PopoverSurface>
                        <UserAuthContent />
                    </PopoverSurface>
                </Popover>
            </div>
        </div>
    );
};

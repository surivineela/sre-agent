import { Button, Image, Persona, Popover, PopoverSurface, PopoverTrigger, Text, tokens } from '@fluentui/react-components';
import { Settings32Regular } from '@fluentui/react-icons';
import { useIntl } from 'react-intl';
import { useAuth } from '../../Common/Contexts/AuthContext';
import { PortalResources } from '../../Strings/Resources';
import { SettingsContent } from './SettingsContent';
import { UserAuthContent } from './UserAuthContent';

export const Navbar = () => {
    const intl = useIntl();
    const { status, user } = useAuth();

    const isPending = status === 'pending';
    const personaName = user?.name;
    const personaSecondaryText = user?.username;

    return (
        <div
            style={{
                display: 'flex',
                flexDirection: 'row',
                justifyContent: 'space-between',
                gap: 20,
                height: 44,
                paddingLeft: tokens.spacingHorizontalXL,
                paddingRight: tokens.spacingHorizontalXL,
                alignItems: 'center',
                backgroundColor: tokens.colorNeutralBackground4,
            }}
        >
            <div style={{ display: 'flex', flexDirection: 'row', alignContent: 'center', gap: 8 }}>
                <Image src="./SreAgent.svg" width={18} height={18} alt={intl.formatMessage(PortalResources.azureSreAgents)} />
                <Text weight="semibold">{intl.formatMessage(PortalResources.azureSreAgents)}</Text>
            </div>

            <div style={{ display: 'flex', flexDirection: 'row', alignContent: 'center', gap: 8 }}>
                <Popover>
                    <PopoverTrigger>
                        <Button
                            icon={<Settings32Regular />}
                            appearance="subtle"
                            disabled={isPending}
                            aria-label={intl.formatMessage(PortalResources.settings)}
                        />
                    </PopoverTrigger>

                    <PopoverSurface style={{ minWidth: '320px', padding: tokens.spacingVerticalL }}>
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

import { Button, Divider, Image, Persona, Popover, PopoverSurface, PopoverTrigger, Text, tokens } from '@fluentui/react-components';
import { useIntl } from 'react-intl';
import { useAuth } from '../Common/Contexts/AuthContext';
import { PortalResources } from '../Strings/Resources';

export const Navbar = () => {
    const intl = useIntl();
    const { signOut } = useAuth();

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

            <div>
                <Popover>
                    <PopoverTrigger>
                        <Persona avatar={{ image: { src: '' } }} />
                    </PopoverTrigger>

                    <PopoverSurface>
                        <div>
                            <Persona avatar={{ image: { src: '' } }} name="Full Name" secondaryText="email@microsoft.com" />
                            <div>Directory dropdown</div>

                            <Divider />

                            <div>
                                <Button>{intl.formatMessage(PortalResources.signInWithDifferentAccount)}</Button>
                                <Button onClick={() => signOut()}>{intl.formatMessage(PortalResources.signOut)}</Button>
                            </div>
                        </div>
                    </PopoverSurface>
                </Popover>
            </div>
        </div>
    );
};

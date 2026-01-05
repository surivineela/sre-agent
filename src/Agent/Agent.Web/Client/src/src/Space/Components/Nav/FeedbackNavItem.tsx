import { SplitCopilotNavItem } from '@fluentui-copilot/react-copilot';
import { Menu, MenuButtonProps, MenuItem, MenuList, MenuPopover, MenuTrigger } from '@fluentui/react-components';
import { bundleIcon, Open20Filled, Open20Regular, PersonFeedback20Filled, PersonFeedback20Regular } from '@fluentui/react-icons';
import { memo, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import {
    FeedbackResources,
    GithubIssueResources,
    SreAgentResources,
    SreAgentTabResources,
    SupportResources,
} from '../../../Strings/SREAgentResources';
import { openSupportBlade } from '../../Settings/AzureSettings.ReactView';
import Fade from '../Fade';
import { FeedbackDialog } from '../FeedbackDialog';
import GithubIssueDialog from '../GithubIssueDialog';

const FeedbackIcon = bundleIcon(PersonFeedback20Filled, PersonFeedback20Regular);
const OpenSupportTicketIcon = bundleIcon(Open20Filled, Open20Regular);

// Directly use SVG for more control over colors
export const GithubIssueIcon = () => {
    return (
        <svg width="18" height="18" viewBox="0 0 16 16" xmlns="http://www.w3.org/2000/svg" fill="currentColor">
            <path
                fillRule="evenodd"
                clipRule="evenodd"
                d="M7.976 0A7.977 7.977 0 0 0 0 7.976c0 3.522 2.3 6.507 5.431 7.584.392.049.538-.196.538-.392v-1.37c-2.201.49-2.69-1.076-2.69-1.076-.343-.93-.881-1.175-.881-1.175-.734-.489.048-.489.048-.489.783.049 1.224.832 1.224.832.734 1.223 1.859.88 2.3.685.048-.538.293-.88.489-1.076-1.762-.196-3.621-.881-3.621-3.964 0-.88.293-1.566.832-2.153-.05-.147-.343-.978.098-2.055 0 0 .685-.196 2.201.832.636-.196 1.322-.245 2.007-.245s1.37.098 2.006.245c1.517-1.027 2.202-.832 2.202-.832.44 1.077.146 1.908.097 2.104a3.16 3.16 0 0 1 .832 2.153c0 3.083-1.86 3.719-3.62 3.915.293.244.538.733.538 1.467v2.202c0 .196.146.44.538.392A7.984 7.984 0 0 0 16 7.976C15.951 3.572 12.38 0 7.976 0z"
            />
        </svg>
    );
};

const FeedbackNavItem = ({ isNavOpen }: { isNavOpen: boolean }) => {
    const intl = useIntl();

    const azPortalProxy = useContext(AzPortalContext);
    const { resourceId } = useContext(EnvironmentContext);

    const [isFeedbackDialogOpen, setIsFeedbackDialogOpen] = useState(false);
    const [isGithubIssueDialogOpen, setIsGithubIssueDialogOpen] = useState(false);
    const [isOpen, setIsOpen] = useState(false);

    const navItems = useMemo(() => {
        return [
            {
                icon: <FeedbackIcon />,
                value: 'agentFeedback',
                onClick: () => setIsFeedbackDialogOpen(true),
                label: intl.formatMessage(FeedbackResources.provideAgentFeedback),
            },
            {
                icon: <GithubIssueIcon />,
                value: 'githubIssue',
                onClick: () => setIsGithubIssueDialogOpen(true),
                label: intl.formatMessage(GithubIssueResources.createGithubIssueTitle),
            },
            {
                icon: <OpenSupportTicketIcon />,
                value: 'openSupportTicket',
                onClick: () => openSupportBlade(azPortalProxy, resourceId),
                label: intl.formatMessage(SupportResources.buttonText),
            },
        ];
    }, [intl, azPortalProxy, resourceId]);

    return (
        <>
            <Fade visible={isNavOpen} unmountOnExit>
                <div>
                    <Menu open={isOpen} onOpenChange={(_, data) => setIsOpen(data.open)}>
                        <MenuTrigger>
                            {(triggerProps: MenuButtonProps) => (
                                <SplitCopilotNavItem
                                    navItem={{
                                        level: 1,
                                        value: 'feedback',
                                        children: intl.formatMessage(SreAgentTabResources.feedback),
                                        icon: <FeedbackIcon />,
                                        onContextMenu: (e: React.MouseEvent) => {
                                            setIsOpen(true);
                                            e.preventDefault();
                                        },
                                    }}
                                    menuButton={{
                                        ...triggerProps,
                                        'aria-label': intl.formatMessage(SreAgentResources.moreOptions),
                                    }}
                                    menuButtonTooltip={{
                                        content: intl.formatMessage(SreAgentResources.moreOptions),
                                        relationship: 'label',
                                    }}
                                />
                            )}
                        </MenuTrigger>
                        <MenuPopover>
                            <MenuList>
                                {navItems.map((item, index) => (
                                    <MenuItem key={index} icon={item.icon} onClick={item.onClick}>
                                        {item.label}
                                    </MenuItem>
                                ))}
                            </MenuList>
                        </MenuPopover>
                    </Menu>
                </div>
            </Fade>
            <FeedbackDialog isOpen={isFeedbackDialogOpen} setIsOpen={setIsFeedbackDialogOpen} />
            <GithubIssueDialog isOpen={isGithubIssueDialogOpen} setIsOpen={setIsGithubIssueDialogOpen} />
        </>
    );
};

export default memo(FeedbackNavItem);

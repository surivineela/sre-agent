import { Image, makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import { Settings20Regular } from '@fluentui/react-icons';
import { FC, useCallback } from 'react';
import { useIntl } from 'react-intl';
import { ApiVersions } from '../../../Common/Constants/ApiVersions';
import { IdentityStatus } from '../../../Common/Contracts/Identity';
import { buildBladeUrl } from '../../../Common/Utilities/Url';
import { PortalResources } from '../../../Strings/Resources';
import { AgentSpaceNavItem as AgentSpaceNavItemEnum } from '../Hooks/useAgentSpaceNav';
import { AgentSpaceNavItem } from './AgentSpaceNavItem';
import { NavCollapseButton } from './NavCollapseButton';

const useStyles = makeStyles({
    nav: {
        display: 'flex',
        flexDirection: 'column',
        backgroundColor: tokens.colorNeutralBackground1,
        borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
        transitionProperty: 'width',
        transitionDuration: '0.25s',
        transitionTimingFunction: 'ease',
        overflowX: 'hidden',
        overflowY: 'auto',
        flexShrink: 0,
    },
    navExpanded: {
        width: '250px',
    },
    navCollapsed: {
        width: '56px',
    },
    header: {
        display: 'flex',
        justifyContent: 'flex-end',
        padding: tokens.spacingVerticalM,
        paddingRight: tokens.spacingHorizontalM,
    },
    headerCollapsed: {
        justifyContent: 'center',
        paddingRight: tokens.spacingVerticalM,
    },
    body: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        paddingLeft: tokens.spacingHorizontalS,
        paddingRight: tokens.spacingHorizontalS,
        flex: 1,
        overflowX: 'hidden',
        overflowY: 'auto',
    },
});

interface AgentSpaceNavProps {
    isNavOpen: boolean;
    selectedView: AgentSpaceNavItemEnum;
    onSelectView: (view: AgentSpaceNavItemEnum) => void;
    onToggle: () => void;
    showInternalTabs: boolean;
    resourceId: string;
}

export const AgentSpaceNav: FC<AgentSpaceNavProps> = ({
    isNavOpen,
    selectedView,
    onSelectView,
    onToggle,
    showInternalTabs,
    resourceId,
}) => {
    const intl = useIntl();
    const styles = useStyles();

    const handleIdentityClick = useCallback(() => {
        const bladeUrl = buildBladeUrl({
            extension: 'Microsoft_Azure_ManagedServiceIdentity',
            detailBlade: 'AzureResourceIdentitiesBladeV2',
            detailBladeInputs: {
                resourceId,
                apiVersion: ApiVersions.microsoftAppApiVersion20250501Preview,
                systemAssignedStatus: IdentityStatus.Supported,
                userAssignedStatus: IdentityStatus.Supported,
            },
        });
        window.open(bladeUrl, '_blank', 'noopener,noreferrer');
    }, [resourceId]);

    return (
        <nav className={mergeClasses(styles.nav, isNavOpen ? styles.navExpanded : styles.navCollapsed)}>
            <div className={mergeClasses(styles.header, !isNavOpen && styles.headerCollapsed)}>
                <NavCollapseButton isNavOpen={isNavOpen} onToggle={onToggle} />
            </div>

            <div className={styles.body}>
                <AgentSpaceNavItem
                    icon={<Image src="/SreAgentSpace.svg" width={20} height={20} alt={intl.formatMessage(PortalResources.overview)} />}
                    label={intl.formatMessage(PortalResources.overview)}
                    isSelected={selectedView === AgentSpaceNavItemEnum.Overview}
                    isNavOpen={isNavOpen}
                    onClick={() => onSelectView(AgentSpaceNavItemEnum.Overview)}
                />

                <AgentSpaceNavItem
                    icon={<Settings20Regular />}
                    label={intl.formatMessage(PortalResources.configuration)}
                    isSelected={selectedView === AgentSpaceNavItemEnum.Configuration}
                    isNavOpen={isNavOpen}
                    onClick={() => onSelectView(AgentSpaceNavItemEnum.Configuration)}
                />

                {showInternalTabs && (
                    <AgentSpaceNavItem
                        icon={
                            <Image
                                src="/GenevaAction.svg"
                                width={20}
                                height={20}
                                alt={intl.formatMessage(PortalResources.genevaActionPolicies)}
                            />
                        }
                        label={intl.formatMessage(PortalResources.genevaActionPolicies)}
                        isSelected={selectedView === AgentSpaceNavItemEnum.GenevaActionPolicies}
                        isNavOpen={isNavOpen}
                        onClick={() => onSelectView(AgentSpaceNavItemEnum.GenevaActionPolicies)}
                    />
                )}

                <AgentSpaceNavItem
                    icon={<Image src="/Connectors.svg" width={20} height={20} alt={intl.formatMessage(PortalResources.connectors)} />}
                    label={intl.formatMessage(PortalResources.connectors)}
                    isSelected={selectedView === AgentSpaceNavItemEnum.Connectors}
                    isNavOpen={isNavOpen}
                    onClick={() => onSelectView(AgentSpaceNavItemEnum.Connectors)}
                />

                <AgentSpaceNavItem
                    icon={<Image src="/Identity.svg" width={20} height={20} alt={intl.formatMessage(PortalResources.identity)} />}
                    label={intl.formatMessage(PortalResources.identity)}
                    isSelected={false}
                    isNavOpen={isNavOpen}
                    onClick={handleIdentityClick}
                    isExternal
                />
            </div>
        </nav>
    );
};

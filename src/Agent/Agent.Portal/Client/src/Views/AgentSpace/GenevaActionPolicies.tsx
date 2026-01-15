import { makeStyles, SelectTabData, SelectTabEvent, Tab, TabList, Text, tokens } from '@fluentui/react-components';
import { useState } from 'react';
import { useIntl } from 'react-intl';
import { GenevaActionsWarningBanner } from '../../Common/Components/GenevaActions/GenevaActionsWarningBanner';
import { AgentSpace } from '../../Common/Contracts/AgentSpace';
import { ArmObj } from '../../Common/Contracts/Arm';
import { useIsInternal } from '../../Common/Hooks/useIsInternal';
import { PortalResources, RolesAndPermissions } from '../../Strings/Resources';
import { GenevaActionPoliciesAllowedActionsTab } from './GenevaActionPoliciesAllowedActionsTab';
import { GenevaActionPoliciesBasicsTab } from './GenevaActionPoliciesBasicsTab';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        padding: '20px',
    },
    headerBlock: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    title: {
        fontWeight: 600,
        fontSize: '18px',
    },
    tabContent: {
        paddingTop: tokens.spacingVerticalM,
    },
});

enum GenevaActionPoliciesTabKey {
    Basics = 'basics',
    AllowedActions = 'allowedActions',
}

interface GenevaActionPoliciesProps {
    agentSpace: ArmObj<AgentSpace> | null;
    refresh: () => Promise<void>;
}

export const GenevaActionPolicies = ({ agentSpace, refresh }: GenevaActionPoliciesProps) => {
    const intl = useIntl();
    const styles = useStyles();
    const { isInternalTenant } = useIsInternal();

    const [selectedTab, setSelectedTab] = useState<GenevaActionPoliciesTabKey>(GenevaActionPoliciesTabKey.Basics);

    const handleTabSelect = (_: SelectTabEvent, data: SelectTabData) => {
        setSelectedTab(data.value as GenevaActionPoliciesTabKey);
    };

    // Disable editing for non-internal tenants
    const isDisabled = !isInternalTenant;

    if (!agentSpace) {
        return null;
    }

    return (
        <div className={styles.container}>
            <Text className={styles.title}>{intl.formatMessage(PortalResources.genevaActionPolicies)}</Text>

            {!isInternalTenant && <GenevaActionsWarningBanner />}

            <div className={styles.headerBlock}>
                <TabList selectedValue={selectedTab} onTabSelect={handleTabSelect}>
                    <Tab value={GenevaActionPoliciesTabKey.Basics}>{intl.formatMessage(PortalResources.basics)}</Tab>
                    <Tab value={GenevaActionPoliciesTabKey.AllowedActions}>{intl.formatMessage(RolesAndPermissions.allowedActions)}</Tab>
                </TabList>
            </div>

            <div className={styles.tabContent}>
                {selectedTab === GenevaActionPoliciesTabKey.Basics && (
                    <GenevaActionPoliciesBasicsTab agentSpace={agentSpace} refresh={refresh} disabled={isDisabled} />
                )}
                {selectedTab === GenevaActionPoliciesTabKey.AllowedActions && (
                    <GenevaActionPoliciesAllowedActionsTab agentSpace={agentSpace} refresh={refresh} disabled={isDisabled} />
                )}
            </div>
        </div>
    );
};

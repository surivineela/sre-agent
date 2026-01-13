import { FC, memo } from 'react';
import { Thread, ThreadSource } from '../../../Common/Contracts/DataPlane/Thread';
import { PrimaryNavItemValues, SecondaryNavItemValues, ThreadCategoryKey } from '../../Contracts/SreAgentSpace';
import ActivitiesCategoryNavItem from './ActivitiesCategoryNavItem';
import BuilderCategoryNavItem from './BuilderCategoryNavItem';
import MonitoringCategoryNavItem from './MonitoringCategoryNavItem';
import NewChatItem from './NewChatItem';
import SettingsCategoryNavItem from './SettingsCategoryNavItem';

interface INonThreadCategoryNavItemsProps {
    isNavOpen: boolean;
    openedCategoryNavItems: (PrimaryNavItemValues | ThreadCategoryKey)[];
    threads: Thread[];
    selectThread: (threadId: string | null) => void;
    excludedSources?: ThreadSource[];
    controlPlaneTabsVisible: boolean;
    logsTabDisabled: boolean;
    onLogsClick: () => void;
    incidentVisible: boolean;
    incidentDisabled: boolean;
    onClickCategoryNavItem: (tabValue: PrimaryNavItemValues | ThreadCategoryKey) => void;
    onClickSubNavItem: (tabValue: PrimaryNavItemValues, secondaryNavItem: SecondaryNavItemValues) => void;
}

const NonThreadCategoryNavItems: FC<INonThreadCategoryNavItemsProps> = ({
    isNavOpen,
    openedCategoryNavItems,
    threads,
    selectThread,
    excludedSources,
    controlPlaneTabsVisible,
    logsTabDisabled,
    onLogsClick,
    incidentVisible,
    incidentDisabled,
    onClickCategoryNavItem,
    onClickSubNavItem,
}) => {
    return (
        <>
            <NewChatItem isNavOpen={isNavOpen} threads={threads} selectThread={selectThread} excludedSources={excludedSources} />
            <ActivitiesCategoryNavItem
                isNavOpen={isNavOpen}
                openedCategoryNavItems={openedCategoryNavItems}
                incidentVisible={incidentVisible}
                incidentDisabled={incidentDisabled}
                onClickCategoryNavItem={onClickCategoryNavItem}
                onClickSubNavItem={onClickSubNavItem}
            />
            <BuilderCategoryNavItem
                isNavOpen={isNavOpen}
                incidentVisible={incidentVisible}
                incidentDisabled={incidentDisabled}
                onClickCategoryNavItem={onClickCategoryNavItem}
                onClickSubNavItem={onClickSubNavItem}
            />
            <MonitoringCategoryNavItem
                isNavOpen={isNavOpen}
                controlPlaneTabsVisible={controlPlaneTabsVisible}
                onLogsClick={onLogsClick}
                logsTabDisabled={logsTabDisabled}
                incidentVisible={incidentVisible}
                incidentDisabled={incidentDisabled}
                onClickCategoryNavItem={onClickCategoryNavItem}
                onClickSubNavItem={onClickSubNavItem}
            />
            <SettingsCategoryNavItem
                isNavOpen={isNavOpen}
                controlPlaneTabsVisible={controlPlaneTabsVisible}
                incidentVisible={incidentVisible}
                incidentDisabled={incidentDisabled}
                onClickCategoryNavItem={onClickCategoryNavItem}
                onClickSubNavItem={onClickSubNavItem}
            />
        </>
    );
};

export default memo(NonThreadCategoryNavItems);

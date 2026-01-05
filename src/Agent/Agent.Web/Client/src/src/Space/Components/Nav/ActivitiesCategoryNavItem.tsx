import {
    PositioningShorthand,
    TeachingPopover,
    TeachingPopoverBody,
    TeachingPopoverFooter,
    TeachingPopoverSurface,
    TeachingPopoverTitle,
} from '@fluentui/react-components';
import {
    bundleIcon,
    ChatEmpty20Filled,
    ChatEmpty20Regular,
    DocumentText20Filled,
    DocumentText20Regular,
    Warning20Filled,
    Warning20Regular,
} from '@fluentui/react-icons';
import { FC, memo, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { LocalStorageFlags, useLocalStorage } from '../../../Common/Hooks/useLocalStorage';
import { IncidentManagementResources, SreAgentResources, SreAgentTabResources } from '../../../Strings/SREAgentResources';
import {
    CategoryNavItemInput,
    PrimaryNavItemValues,
    SecondaryNavItemValues,
    SubNavItemInput,
    ThreadCategoryKey,
} from '../../Contracts/SreAgentSpace';
import CategoryNavItem from './CategoryNavItem';

interface IActivitiesCategoryNavItemProps {
    isNavOpen: boolean;
    openedCategoryNavItems: (PrimaryNavItemValues | ThreadCategoryKey)[];
    incidentVisible: boolean;
    incidentDisabled: boolean;
    onClickCategoryNavItem: (tabValue: PrimaryNavItemValues) => void;
    onClickSubNavItem: (tabValue: PrimaryNavItemValues, secondaryNavItem: SecondaryNavItemValues) => void;
}

const ActivitiesCategoryNavItem: FC<IActivitiesCategoryNavItemProps> = ({
    isNavOpen,
    openedCategoryNavItems,
    incidentVisible,
    incidentDisabled,
    onClickCategoryNavItem,
    onClickSubNavItem,
}) => {
    const intl = useIntl();

    const activitiesRef = useRef<HTMLButtonElement>(null);
    const incidentsRef = useRef<HTMLDivElement>(null);

    const [teachingPopoverPositioning, setTeachingPopoverPositioning] = useState<PositioningShorthand>();

    const { item: isIncidentManagementTeachingPopoverDismissed, setItem: setIsIncidentManagementTeachingPopoverDismissed } =
        useLocalStorage(LocalStorageFlags.IncidentManagementPopoverDismissed);

    const showIncidentManagementTeachingPopover = useMemo(() => {
        return isIncidentManagementTeachingPopoverDismissed !== 'true';
    }, [isIncidentManagementTeachingPopoverDismissed]);

    const categoryItem = useMemo(
        (): CategoryNavItemInput => ({
            value: PrimaryNavItemValues.Activities,
            label: intl.formatMessage(SreAgentTabResources.activities),
            icon: bundleIcon(ChatEmpty20Filled, ChatEmpty20Regular),
            isVisible: isNavOpen,
            disabled: false,
            ref: activitiesRef,
        }),
        [isNavOpen, intl]
    );

    const subItems = useMemo((): SubNavItemInput[] => {
        return [
            {
                value: SecondaryNavItemValues.IncidentOverview,
                isVisible: incidentVisible,
                label: intl.formatMessage(SreAgentResources.incidents),
                disabled: incidentDisabled,
                icon: bundleIcon(Warning20Filled, Warning20Regular),
                ref: incidentsRef,
            },
            {
                value: SecondaryNavItemValues.DailyReports,
                isVisible: true,
                disabled: false,
                icon: bundleIcon(DocumentText20Filled, DocumentText20Regular),
                label: intl.formatMessage(SreAgentTabResources.dailyReports),
            },
        ];
    }, [intl, incidentDisabled, incidentVisible]);

    const isActivitiesCategoryOpened = openedCategoryNavItems.includes(PrimaryNavItemValues.Activities);

    useEffect(() => {
        setTimeout(() => {
            setTeachingPopoverPositioning({
                position: 'after',
                align: 'center',
                target: isActivitiesCategoryOpened ? incidentsRef.current : activitiesRef.current,
            });
        }, 300);
    }, [isActivitiesCategoryOpened]);

    return (
        <>
            <CategoryNavItem
                categoryItem={categoryItem}
                subItems={subItems}
                onClickCategoryNavItem={onClickCategoryNavItem}
                onClickSubNavItem={onClickSubNavItem}
            />
            <TeachingPopover
                appearance="brand"
                open={showIncidentManagementTeachingPopover && !!teachingPopoverPositioning}
                withArrow={true}
                positioning={teachingPopoverPositioning}
            >
                <TeachingPopoverSurface>
                    <TeachingPopoverBody>
                        <TeachingPopoverTitle>
                            {intl.formatMessage(IncidentManagementResources.incidentThreadsMovedTitle)}
                        </TeachingPopoverTitle>
                        <div style={{ maxWidth: '280px', wordWrap: 'break-word' }}>
                            {intl.formatMessage(IncidentManagementResources.incidentThreadsMovedDescription)}
                        </div>
                    </TeachingPopoverBody>
                    <TeachingPopoverFooter
                        primary={{
                            onClick: () => setIsIncidentManagementTeachingPopoverDismissed('true'),
                            children: intl.formatMessage(SreAgentResources.gotIt),
                        }}
                    />
                </TeachingPopoverSurface>
            </TeachingPopover>
        </>
    );
};

export default memo(ActivitiesCategoryNavItem);

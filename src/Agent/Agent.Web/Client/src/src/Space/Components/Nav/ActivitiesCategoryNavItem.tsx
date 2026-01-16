import {
    bundleIcon,
    ChatEmpty20Filled,
    ChatEmpty20Regular,
    DocumentText20Filled,
    DocumentText20Regular,
    Warning20Filled,
    Warning20Regular,
} from '@fluentui/react-icons';
import { FC, memo, useMemo, useRef } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources, SreAgentTabResources } from '../../../Strings/SREAgentResources';
import { CategoryNavItemInput, PrimaryNavItemValues, SecondaryNavItemValues, SubNavItemInput } from '../../Contracts/SreAgentSpace';
import CategoryNavItem from './CategoryNavItem';

interface IActivitiesCategoryNavItemProps {
    isNavOpen: boolean;
    incidentVisible: boolean;
    incidentDisabled: boolean;
    onClickCategoryNavItem: (tabValue: PrimaryNavItemValues) => void;
    onClickSubNavItem: (tabValue: PrimaryNavItemValues, secondaryNavItem: SecondaryNavItemValues) => void;
}

const ActivitiesCategoryNavItem: FC<IActivitiesCategoryNavItemProps> = ({
    isNavOpen,
    incidentVisible,
    incidentDisabled,
    onClickCategoryNavItem,
    onClickSubNavItem,
}) => {
    const intl = useIntl();

    const activitiesRef = useRef<HTMLButtonElement>(null);
    const incidentsRef = useRef<HTMLDivElement>(null);

    const categoryItem = useMemo(
        (): CategoryNavItemInput => ({
            value: PrimaryNavItemValues.Activities,
            label: intl.formatMessage(SreAgentTabResources.activities),
            icon: bundleIcon(ChatEmpty20Filled, ChatEmpty20Regular),
            filledIcon: ChatEmpty20Filled,
            isCollapsed: !isNavOpen,
            isVisible: true,
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

    return (
        <>
            <CategoryNavItem
                categoryItem={categoryItem}
                subItems={subItems}
                onClickCategoryNavItem={onClickCategoryNavItem}
                onClickSubNavItem={onClickSubNavItem}
            />
        </>
    );
};

export default memo(ActivitiesCategoryNavItem);

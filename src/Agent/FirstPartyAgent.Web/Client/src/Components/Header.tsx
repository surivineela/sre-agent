import { mergeStyles, Pivot, PivotItem } from '@fluentui/react';
import { useQueryParams } from '../Hooks/UseQueryParams';

export enum TabItem {
    IncidentManager = "incidentManager",
    Config = "config",
}

interface HeaderProps {
    onViewChange: (curItem: TabItem) => void;
}

const Header = (props: HeaderProps) => {
    const { isPlayground, isDebug } = useQueryParams();  // Get URL parameters
    const headerStyles = mergeStyles({
        backgroundColor: "f8f9fa",
        boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)",
        padding: "0.75rem, 0",
    });

     const onLinkClick = (item?: PivotItem) => {
        if (item?.props.itemKey) {
            const curItem = item.props.itemKey as TabItem;
            props.onViewChange(curItem);
        }
    };

    return (
        <Pivot className={headerStyles} onLinkClick={onLinkClick} defaultSelectedKey="incidentManager">
            <PivotItem headerText='Incident Manager' itemKey={TabItem.IncidentManager} />
            {isPlayground && isDebug && (
                <PivotItem headerText='CosmosDB(Debug)' itemKey={TabItem.Config} />
            )}
        </Pivot>
    );
}

export default Header;
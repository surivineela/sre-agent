import { Breadcrumb, BreadcrumbButton, BreadcrumbDivider, BreadcrumbItem } from '@fluentui/react-components';
import { FC } from 'react';
import { useIncidentManagementStyles } from '../../Styles/IncidentManagement.styles';
import { DirtyStateConfirmationWrapper } from '../CreateIncidentHandler/DirtyStateConfirmationDialog';
import { DirtyStateNavigationConfirmDialog } from '../CreateIncidentHandler/NavigationConfirmDialog';

interface BreadcrumbNavigationProps {
    title: string;
    parentTitle: string;
    onParentClick: () => void;
    isDirty?: boolean;
    children?: React.ReactNode;
}

export const BreadcrumbNavigation: FC<BreadcrumbNavigationProps> = ({ title, parentTitle, onParentClick, isDirty = false, children }) => {
    const styles = useIncidentManagementStyles();

    return (
        <div className={styles.breadCrumbAndPanelWrapper}>
            <Breadcrumb className={styles.breadcrumb}>
                <BreadcrumbItem>
                    <DirtyStateConfirmationWrapper isDirty={isDirty} onConfirm={onParentClick}>
                        <BreadcrumbButton>{parentTitle}</BreadcrumbButton>
                    </DirtyStateConfirmationWrapper>
                </BreadcrumbItem>
                <BreadcrumbDivider />
                <BreadcrumbItem style={{ marginLeft: 6 }}>{title}</BreadcrumbItem>
            </Breadcrumb>
            <div className={styles.navPanelWrapper}>
                <DirtyStateNavigationConfirmDialog isDirty={isDirty} />
                {children}
            </div>
        </div>
    );
};

export default BreadcrumbNavigation;

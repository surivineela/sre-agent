import { Button, tokens } from '@fluentui/react-components';
import { ArrowLeft16Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIncidentManagementStyles } from '../../Styles/IncidentManagement.styles';
import { DirtyStateConfirmationWrapper } from '../CreateIncidentHandler/DirtyStateConfirmationDialog';
import { DirtyStateNavigationConfirmDialog } from '../CreateIncidentHandler/NavigationConfirmDialog';

interface TitleBarNavigationProps {
    title: string;
    onBackClick: () => void;
    children: React.ReactNode;
    isDirty?: boolean;
}

const TitleBarNavigation: FC<TitleBarNavigationProps> = ({ title, onBackClick, children, isDirty = false }) => {
    const styles = useIncidentManagementStyles();

    return (
        <div className={styles.navPanelWrapper}>
            <div
                style={{
                    padding: '0px 20px',
                    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
                    display: 'flex',
                    flexDirection: 'row',
                    alignItems: 'center',
                    gap: 8,
                }}
            >
                <DirtyStateConfirmationWrapper isDirty={isDirty} onConfirm={onBackClick}>
                    <Button appearance="transparent" icon={<ArrowLeft16Regular />} />
                </DirtyStateConfirmationWrapper>
                <h2
                    style={{
                        fontWeight: 600,
                        fontSize: '16px',
                        lineHeight: '22px',
                        whiteSpace: 'nowrap',
                        textOverflow: 'ellipsis',
                        overflow: 'hidden',
                    }}
                >
                    {title}
                </h2>
            </div>
            <div className={styles.navPanelContent}>
                <DirtyStateNavigationConfirmDialog isDirty={isDirty} />
                {children}
            </div>
        </div>
    );
};

export default TitleBarNavigation;

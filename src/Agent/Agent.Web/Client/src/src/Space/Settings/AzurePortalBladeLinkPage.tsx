import { Button } from '@fluentui/react-components';
import { OpenRegular } from '@fluentui/react-icons';
import { memo } from 'react';
import { useSettingsStyles } from './Styles/Settings.styles';

interface IAzurePortalBladeLinkPageProps {
    title: string;
    description: string;
    buttonText: string;
    onClickButton: () => void;
}

const AzurePortalBladeLinkPage = ({ title, description, buttonText, onClickButton }: IAzurePortalBladeLinkPageProps) => {
    const styles = useSettingsStyles();

    return (
        <>
            <div style={styles.generalSettingsHeader}>{title}</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '20px', alignItems: 'flex-start' }}>
                {description}
                <Button icon={<OpenRegular />} onClick={onClickButton}>
                    {buttonText}
                </Button>
            </div>
        </>
    );
};

export default memo(AzurePortalBladeLinkPage);

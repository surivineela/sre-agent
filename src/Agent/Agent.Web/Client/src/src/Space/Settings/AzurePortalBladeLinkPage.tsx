import { EntityCard, EntityTitle } from '@fluentui-copilot/react-copilot';
import { Button } from '@fluentui/react-components';
import { OpenRegular } from '@fluentui/react-icons';
import { memo } from 'react';

interface IAzurePortalBladeLinkPageProps {
    title: string;
    description: string;
    buttonText: string;
    onClickButton: () => void;
}

const AzurePortalBladeLinkPage = ({ title, description, buttonText, onClickButton }: IAzurePortalBladeLinkPageProps) => {
    return (
        <EntityCard
            entityTitle={<EntityTitle primaryText={title} secondaryText={description} />}
            style={{ height: '180px', justifyContent: 'space-between' }}
            actions={
                <>
                    <Button icon={<OpenRegular />} onClick={onClickButton}>
                        {buttonText}
                    </Button>
                </>
            }
        ></EntityCard>
    );
};

export default memo(AzurePortalBladeLinkPage);

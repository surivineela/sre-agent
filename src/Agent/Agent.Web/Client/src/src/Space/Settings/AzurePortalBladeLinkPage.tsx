import { EntityCard, EntityTitle } from '@fluentui-copilot/react-copilot';
import { Button, makeStyles } from '@fluentui/react-components';
import { OpenRegular } from '@fluentui/react-icons';
import { memo } from 'react';

interface IAzurePortalBladeLinkPageProps {
    title: string;
    description: string;
    buttonText: string;
    onClickButton: () => void;
}

const useStyles = makeStyles({
    root: {
        ':hover': {
            cursor: 'pointer',
        },
    },
});

const AzurePortalBladeLinkPage = ({ title, description, buttonText, onClickButton }: IAzurePortalBladeLinkPageProps) => {
    const styles = useStyles();

    return (
        <EntityCard
            className={styles.root}
            entityTitle={<EntityTitle primaryText={title} secondaryText={description} />}
            style={{ height: '180px', justifyContent: 'space-between' }}
            actions={
                <>
                    <Button icon={<OpenRegular />} onClick={onClickButton}>
                        {buttonText}
                    </Button>
                </>
            }
            onClick={onClickButton}
        ></EntityCard>
    );
};

export default memo(AzurePortalBladeLinkPage);

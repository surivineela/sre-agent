import { Button, makeStyles, mergeClasses } from '@fluentui/react-components';
import { ArrowDownRegular } from '@fluentui/react-icons';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { ActivitiesResources } from '../../Strings/SREAgentResources';

const useNewMessageButtonStyles = makeStyles({
    root: {
        opacity: '1',
        transition: 'opacity 0.3s ease',
        pointerEvents: 'auto',
        position: 'absolute',
        right: '50px',
        bottom: '180px',
    },
    hidden: {
        opacity: '0',
        pointerEvents: 'none',
    },
});

const NewMessageButton = ({ isVisible, onClick }: { isVisible?: boolean; onClick: () => void }) => {
    const { root, hidden } = useNewMessageButtonStyles();
    const buttonStyles = mergeClasses(root, isVisible ? undefined : hidden);

    const intl = useIntl();

    return (
        <Button appearance="primary" icon={<ArrowDownRegular />} onClick={onClick} className={buttonStyles}>
            {intl.formatMessage(ActivitiesResources.newMessagesButtonText)}
        </Button>
    );
};

export default memo(NewMessageButton);

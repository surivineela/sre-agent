import { Body1 } from '@fluentui-copilot/react-copilot';
import { makeStyles } from '@fluentui/react-components';
import { FC, memo } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';

const useStyles = makeStyles({
    emptyBodyContainer: {
        position: 'absolute',
        top: '50%',
        left: '50%',
        transform: 'translate(-50%, -50%)',
    },
    emptyBodyImage: {
        width: '150px',
        height: '150px',
        display: 'block',
    },
});

interface EmptyBodyProps {
    imageSrc?: string;
    message: string;
}

const EmptyBody: FC<EmptyBodyProps> = ({ imageSrc, message }) => {
    const intl = useIntl();
    const styles = useStyles();

    return (
        <div className={styles.emptyBodyContainer}>
            <img
                src={imageSrc || 'AiSearchWarningSpotIllustration.svg'}
                alt={intl.formatMessage(SreAgentResources.warning)}
                className={styles.emptyBodyImage}
            />
            <Body1 block={true} align={'center'}>
                {message}
            </Body1>
        </div>
    );
};

export default memo(EmptyBody);

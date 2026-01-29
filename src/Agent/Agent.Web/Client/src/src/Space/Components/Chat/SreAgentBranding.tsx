import { Title3 } from '@fluentui-copilot/react-copilot';
import { Image, makeStyles, mergeClasses } from '@fluentui/react-components';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../../Strings/SREAgentResources';

const useStyles = makeStyles({
    brandContainer: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'center',
        alignItems: 'center',
        gap: '8px',
    },
    leftAligned: {
        justifyContent: 'flex-start',
    },
});

interface SreAgentBrandingProps {
    alignLeft?: boolean;
}

export const SreAgentBranding: FC<SreAgentBrandingProps> = ({ alignLeft = false }) => {
    const styles = useStyles();
    const intl = useIntl();

    return (
        <div className={mergeClasses(styles.brandContainer, alignLeft && styles.leftAligned)}>
            <Image src="./SreAgent.svg" width={32} height={32} alt={intl.formatMessage(SreAgentResources.azureSreAgent)} />
            <Title3 as={'h2'}>{intl.formatMessage(SreAgentResources.azureSreAgent)}</Title3>
        </div>
    );
};

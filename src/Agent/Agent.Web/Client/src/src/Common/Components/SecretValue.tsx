import { Button } from '@fluentui/react-components';
import { EyeOffRegular, EyeRegular } from '@fluentui/react-icons';
import { useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import CopyButton from './CopyButton';

interface SecretValueProps {
    value: string;
}

export const SecretValue = (props: SecretValueProps) => {
    const { value } = props;
    const intl = useIntl();

    const [hidden, setHidden] = useState(true);

    const onShowHideButtonClick = useCallback(() => {
        setHidden(!hidden);
    }, [hidden]);

    return (
        <div style={{ display: 'flex', alignItems: 'center', gap: '2px' }}>
            <Button
                onClick={() => {
                    onShowHideButtonClick();
                }}
                icon={hidden ? <EyeRegular /> : <EyeOffRegular />}
                appearance="transparent"
                size="small"
                style={{ padding: 0 }}
            >
                {hidden ? intl.formatMessage(SreAgentResources.clickToShowValue) : value}
            </Button>

            <CopyButton textToCopy={value} />
        </div>
    );
};

import { Button, Tooltip } from '@fluentui/react-components';
import { CheckmarkRegular, CopyRegular } from '@fluentui/react-icons';
import { useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';

interface CopyButtonProps {
    textToCopy: string;
}

export const CopyButton = (props: CopyButtonProps) => {
    const { textToCopy } = props;

    const intl = useIntl();

    const [copied, setCopied] = useState(false);

    const handleCopy = async (event: React.MouseEvent) => {
        event.stopPropagation();

        await navigator.clipboard.writeText(textToCopy);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
    };

    return (
        <Tooltip
            content={copied ? intl.formatMessage(SreAgentResources.copied) : intl.formatMessage(SreAgentResources.copyToClipboard)}
            relationship="label"
        >
            <Button icon={copied ? <CheckmarkRegular /> : <CopyRegular />} onClick={handleCopy} appearance="subtle" />
        </Tooltip>
    );
};

export default CopyButton;

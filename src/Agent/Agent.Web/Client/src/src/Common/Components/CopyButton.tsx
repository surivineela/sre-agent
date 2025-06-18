import { Button, Tooltip } from '@fluentui/react-components';
import { CheckmarkRegular, CopyRegular } from '@fluentui/react-icons';
import { useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';

interface CopyButtonProps {
    textToCopy: string;
    /** 'subtle' by default */
    buttonAppearance?: 'primary' | 'secondary' | 'transparent';
    /** Icon button only by default */
    showCopyText?: true;
}

export const CopyButton = (props: CopyButtonProps) => {
    const { textToCopy, buttonAppearance, showCopyText } = props;

    const intl = useIntl();

    const [copied, setCopied] = useState(false);

    const handleCopy = async (event: React.MouseEvent) => {
        event.stopPropagation();

        try {
            // This seems to be blocked in portal iframes
            await navigator.clipboard.writeText(textToCopy);
        } catch (error) {
            // Fallback method
            const textArea = document.createElement('textarea');
            textArea.value = textToCopy;
            document.body.appendChild(textArea);
            textArea.focus();
            textArea.select();
            document.execCommand('copy');
            document.body.removeChild(textArea);
        }

        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
    };

    return (
        <Tooltip
            content={copied ? intl.formatMessage(SreAgentResources.copied) : intl.formatMessage(SreAgentResources.copyToClipboard)}
            relationship="label"
        >
            <Button icon={copied ? <CheckmarkRegular /> : <CopyRegular />} onClick={handleCopy} appearance={buttonAppearance ?? 'subtle'}>
                {showCopyText ? intl.formatMessage(SreAgentResources.copy) : null}
            </Button>
        </Tooltip>
    );
};

export default CopyButton;

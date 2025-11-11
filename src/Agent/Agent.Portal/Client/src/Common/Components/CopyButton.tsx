import { Button, Tooltip } from '@fluentui/react-components';
import { CheckmarkRegular, CopyRegular } from '@fluentui/react-icons';
import { memo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';
import { copyToClipboard } from '../Utilities/Clipboard';

interface CopyButtonProps {
    textToCopy: string;
    /** 'subtle' by default */
    buttonAppearance?: 'primary' | 'secondary' | 'transparent' | 'subtle';
    /** Icon button only by default */
    showCopyText?: true;
}

export const CopyButton = (props: CopyButtonProps) => {
    const { textToCopy, buttonAppearance, showCopyText } = props;

    const intl = useIntl();

    const [copied, setCopied] = useState(false);

    const handleCopy = async (event: React.MouseEvent) => {
        event.stopPropagation();

        copyToClipboard(textToCopy);

        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
    };

    return (
        <Tooltip
            content={copied ? intl.formatMessage(PortalResources.copied) : intl.formatMessage(PortalResources.copyToClipboard)}
            relationship="label"
        >
            <Button icon={copied ? <CheckmarkRegular /> : <CopyRegular />} onClick={handleCopy} appearance={buttonAppearance ?? 'subtle'}>
                {showCopyText ? intl.formatMessage(PortalResources.copy) : null}
            </Button>
        </Tooltip>
    );
};

export default memo(CopyButton);

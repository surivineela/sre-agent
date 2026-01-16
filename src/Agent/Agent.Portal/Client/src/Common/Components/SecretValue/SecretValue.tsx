import { Button, makeStyles, tokens } from '@fluentui/react-components';
import { EyeOffRegular, EyeRegular } from '@fluentui/react-icons';
import { useState } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';
import { CopyButton } from '../CopyButton';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
    valueText: {
        maxWidth: '200px',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
});

interface SecretValueProps {
    /** The secret value to display */
    value: string;
    /** When true, forces the value to be revealed (overrides local hidden state) */
    forceRevealed?: boolean;
    /** Optional: hide the copy button */
    hideCopyButton?: boolean;
}

export const SecretValue = ({ value, forceRevealed = false, hideCopyButton = false }: SecretValueProps) => {
    const intl = useIntl();
    const styles = useStyles();
    const [localHidden, setLocalHidden] = useState(true);

    // forceRevealed overrides local state
    const isHidden = forceRevealed ? false : localHidden;

    const handleToggle = () => {
        setLocalHidden(prev => !prev);
    };

    if (!value) {
        return <span>-</span>;
    }

    return (
        <div className={styles.container}>
            <Button
                onClick={handleToggle}
                icon={isHidden ? <EyeRegular /> : <EyeOffRegular />}
                appearance="transparent"
                size="small"
                title={isHidden ? intl.formatMessage(PortalResources.showValue) : intl.formatMessage(PortalResources.hideValue)}
            >
                <span className={styles.valueText}>{isHidden ? intl.formatMessage(PortalResources.clickToReveal) : value}</span>
            </Button>
            {!hideCopyButton && <CopyButton textToCopy={value} />}
        </div>
    );
};

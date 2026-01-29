import { Link, makeStyles, tokens, Tooltip } from '@fluentui/react-components';
import { BrainCircuit20Regular } from '@fluentui/react-icons';
import { memo, useMemo } from 'react';

interface MemoryCitationProps {
    text: string;
    previewLength?: number;
    onClick?: () => void;
}

const useStyles = makeStyles({
    container: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        padding: `${tokens.spacingVerticalXXS} 0`,
        minWidth: 0,
    },
    icon: {
        color: tokens.colorNeutralForeground3,
        flexShrink: 0,
    },
    text: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        color: tokens.colorNeutralForeground2,
        fontStyle: 'italic',
    },
});

const MemoryCitation = ({ text, previewLength = 50, onClick }: MemoryCitationProps) => {
    const styles = useStyles();

    const preview = useMemo(() => {
        if (text.length <= previewLength) {
            return `"${text}"`;
        }
        return `"${text.substring(0, previewLength)}..."`;
    }, [text, previewLength]);

    const needsTooltip = text.length > previewLength;

    const content = (
        <div className={styles.container} role="listitem">
            <BrainCircuit20Regular className={styles.icon} aria-hidden="true" />
            {onClick ? (
                <Link as="button" className={styles.text} onClick={onClick} aria-label={text}>
                    {preview}
                </Link>
            ) : (
                <span className={styles.text} aria-label={text}>
                    {preview}
                </span>
            )}
        </div>
    );

    if (needsTooltip) {
        return (
            <Tooltip content={text} relationship="label" positioning="above">
                {content}
            </Tooltip>
        );
    }

    return content;
};

export default memo(MemoryCitation);

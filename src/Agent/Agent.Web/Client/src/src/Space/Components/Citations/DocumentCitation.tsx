import { Link, makeStyles, tokens } from '@fluentui/react-components';
import { Document20Regular, Open16Regular } from '@fluentui/react-icons';
import { memo } from 'react';

interface DocumentCitationProps {
    title: string;
    url?: string | null;
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
    title: {
        display: 'inline-flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXXS,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    externalIcon: {
        flexShrink: 0,
        verticalAlign: 'middle',
    },
});

const DocumentCitation = ({ title, url, onClick }: DocumentCitationProps) => {
    const styles = useStyles();

    return (
        <div className={styles.container} role="listitem">
            <Document20Regular className={styles.icon} aria-hidden="true" />
            {url || onClick ? (
                <Link as="button" className={styles.title} onClick={onClick}>
                    {title}
                    {url && <Open16Regular className={styles.externalIcon} aria-hidden="true" />}
                </Link>
            ) : (
                <span className={styles.title}>{title}</span>
            )}
        </div>
    );
};

export default memo(DocumentCitation);

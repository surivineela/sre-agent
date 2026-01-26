import { Link, makeStyles, tokens } from '@fluentui/react-components';
import { ClipboardTask20Regular } from '@fluentui/react-icons';
import { memo } from 'react';

interface TrajectoryInsightCitationProps {
    title: string;
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
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
});

const TrajectoryInsightCitation = ({ title, onClick }: TrajectoryInsightCitationProps) => {
    const styles = useStyles();

    return (
        <div className={styles.container} role="listitem">
            <ClipboardTask20Regular className={styles.icon} aria-hidden="true" />
            <Link as="button" className={styles.title} onClick={onClick}>
                {title}
            </Link>
        </div>
    );
};

export default memo(TrajectoryInsightCitation);

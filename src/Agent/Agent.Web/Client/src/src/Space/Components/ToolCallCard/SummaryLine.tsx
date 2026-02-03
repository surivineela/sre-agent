import { makeStyles, mergeClasses, Spinner, tokens } from '@fluentui/react-components';
import { ChevronDown16Regular, ChevronRight16Regular } from '@fluentui/react-icons';
import { memo } from 'react';
import { SummaryLineProps } from './types';

const useStyles = makeStyles({
    root: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        padding: '2px 0',
        cursor: 'pointer',
        color: tokens.colorNeutralForeground3,
        fontSize: '13px',
        userSelect: 'none',
        ':hover': {
            color: tokens.colorNeutralForeground2,
        },
    },
    rootDisabled: {
        cursor: 'default',
        ':hover': {
            color: tokens.colorNeutralForeground3,
        },
    },
    chevron: {
        color: tokens.colorNeutralForeground4,
        flexShrink: 0,
        fontSize: '14px',
    },
    chevronHidden: {
        visibility: 'hidden',
    },
    icon: {
        color: tokens.colorNeutralForeground4,
        flexShrink: 0,
        display: 'flex',
        alignItems: 'center',
        fontSize: '14px',
    },
    actionText: {
        color: tokens.colorNeutralForeground4,
        fontSize: '12px',
    },
    keyParam: {
        fontFamily: 'Consolas, Monaco, monospace',
        color: tokens.colorNeutralForeground2,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        flexShrink: 1,
        minWidth: 0,
        fontSize: '12px',
    },
    separator: {
        flexShrink: 0,
        color: tokens.colorNeutralForeground4,
    },
    resultInfo: {
        color: tokens.colorNeutralForeground4,
        flexShrink: 0,
        whiteSpace: 'nowrap',
        fontSize: '12px',
    },
    resultInfoError: {
        color: tokens.colorPaletteRedForeground1,
    },
    spinner: {
        marginLeft: '-2px',
    },
});

/**
 * Reusable summary line component for tool call cards.
 * Displays: [chevron] [icon] [action] [keyParam] · [resultInfo]
 *
 * Examples:
 * - ▸ 🔍 Searched for "error" · 5 matches
 * - ▸ 📄 Read App.tsx · lines 1-50
 * - ▸ ⚙️ Ran npm install · exit 0
 */
const SummaryLine = ({ summary, isExpanded, isLoading, hasContent, onClick }: SummaryLineProps) => {
    const classes = useStyles();

    const handleClick = () => {
        if (hasContent && !isLoading) {
            onClick();
        }
    };

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if ((e.key === 'Enter' || e.key === ' ') && hasContent && !isLoading) {
            e.preventDefault();
            onClick();
        }
    };

    return (
        <div
            className={mergeClasses(classes.root, (!hasContent || isLoading) && classes.rootDisabled)}
            onClick={handleClick}
            onKeyDown={handleKeyDown}
            role="button"
            tabIndex={hasContent && !isLoading ? 0 : -1}
            aria-expanded={hasContent ? isExpanded : undefined}
        >
            {/* Loading spinner or chevron */}
            {isLoading ? (
                <Spinner size="extra-tiny" className={classes.spinner} />
            ) : hasContent ? (
                isExpanded ? (
                    <ChevronDown16Regular className={classes.chevron} />
                ) : (
                    <ChevronRight16Regular className={classes.chevron} />
                )
            ) : (
                <span className={mergeClasses(classes.chevron, classes.chevronHidden)}>
                    <ChevronRight16Regular />
                </span>
            )}

            {/* Tool icon */}
            <span className={classes.icon}>{summary.icon}</span>

            {/* Action text */}
            <span className={classes.actionText}>{summary.actionText}</span>

            {/* Key parameter */}
            <span className={classes.keyParam}>{summary.keyParam}</span>

            {/* Separator and result info */}
            <span className={classes.separator}>·</span>
            <span className={mergeClasses(classes.resultInfo, summary.isError && classes.resultInfoError)}>{summary.resultInfo}</span>
        </div>
    );
};

export default memo(SummaryLine);

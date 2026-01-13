import { Badge, Button, Link, makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import { DismissRegular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../Strings/SREAgentResources';

export interface PillSetItem {
    key: string;
    label: string;
};

export interface PillSetProps {
    items: PillSetItem[];
    onRemoveItem: (key: string) => void;
    onClearAll: () => void;
    disabled?: boolean;
    className?: string;
};

export const PillSet: FC<PillSetProps> = ({ items, onRemoveItem, onClearAll, disabled, className }) => {
    const intl = useIntl();
    const styles = usePillSetStyles();

    if (items.length === 0) {
        return null;
    }
    return (
        <div className={mergeClasses(styles.root, className)}>
            {items.map(item => (
                <Pill key={item.key} label={item.label} onRemove={() => onRemoveItem(item.key)} disabled={disabled} />
            ))}
            <Link onClick={onClearAll} className={styles.clearAllLink} disabled={disabled}>
                {intl.formatMessage(ExtendedAgentsGraphResources.clearAll)}
            </Link>
        </div>
    );
};

interface PillProps {
    label: string;
    onRemove: () => void;
    disabled?: boolean;
}

const Pill: FC<PillProps> = ({ label, onRemove, disabled }) => {
    const intl = useIntl();
    const styles = usePillSetStyles();
    return (
        <Badge size="medium" className={styles.pill}>
            {label}
            <Button
                appearance="transparent"
                aria-label={intl.formatMessage(SreAgentResources.removeItemWithName, { name: label })}
                onClick={onRemove}
                className={styles.pillRemoveButton}
                icon={<DismissRegular className={styles.pillRemoveButtonIcon} />}
                disabled={disabled}
            />
        </Badge>
    );
};

const usePillSetStyles = makeStyles({
    root: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: '8px',
    },
    clearAllLink: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
    },
    pill: {
        display: 'flex',
        alignItems: 'center',
        gap: '2px',
        backgroundColor: tokens.colorNeutralBackground2,
        color: tokens.colorNeutralForeground2,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        paddingRight: '0px',
    },
    pillRemoveButton: {
        width: '18px',
        height: '18px',
        minWidth: '18px',
        padding: '1px',
        marginRight: '2px',
        zIndex: 1,
        borderRadius: `${tokens.borderRadiusCircular} !important`,
    },
    pillRemoveButtonIcon: {
        height: '12px',
        width: '12px',
    },
});

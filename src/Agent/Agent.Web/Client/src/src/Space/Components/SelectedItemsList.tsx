import { Button, makeStyles, Text, tokens } from '@fluentui/react-components';
import { Dismiss12Regular } from '@fluentui/react-icons';
import { Resizable } from '../Activities/Resizable';

const useSelectedIncidentsListStyles = makeStyles({
    listWrapper: {
        minWidth: '400px',
    },
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
        paddingBottom: '12px',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: '4px',
        width: '100%',
    },
    header: {
        display: 'flex',
        fontWeight: 600,
        fontSize: '14px',
        lineHeight: '20px',
        padding: '12px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    incidentItem: {
        display: 'flex',
        flexDirection: 'row',
        padding: '4px 12px',
        width: 'calc(100% - 22px)',
    },
    incidentDetails: {
        display: 'flex',
        flexDirection: 'column',
        width: 'calc(100% - 32px)',
    },
    incidentTitle: {
        lineHeight: '18px',
        fontSize: '13px',
        textOverflow: 'ellipsis',
        overflow: 'hidden',
        whiteSpace: 'nowrap',
        color: tokens.colorNeutralForeground1,
    },
    incidentId: {
        lineHeight: '18px',
        fontSize: '13px',
        color: tokens.colorNeutralForeground2,
    },
    emptyText: {
        fontSize: '13px',
        padding: '0px 12px',
    },
    iconButton: {
        margin: '0px 0px 0px auto',
        padding: '0',
        border: 'none',
        minWidth: 'auto',
        minHeight: 'auto',
        '&:hover': {
            backgroundColor: 'transparent',
        },
        '&:active': {
            backgroundColor: 'transparent',
        },
    },
});

interface SelectedItemsListProps<T> {
    title: string;
    emtpyText: string;
    items: T[];
    getItemTitle: (item: T) => string;
    getItemId: (item: T) => string;
    onRemove: (item: T) => void;
}

export const SelectedItemsList = <T extends object>({
    title,
    emtpyText,
    items,
    getItemTitle: getTitle,
    getItemId: getId,
    onRemove,
}: SelectedItemsListProps<T>) => {
    const styles = useSelectedIncidentsListStyles();
    return (
        <Resizable
            position="right"
            initialWidth="400px"
            minWidthPixels={400}
            maxWidthPercent={50}
            collapsedWidthPixels={0}
            collapsed={false}
            setCollapsed={() => {}}
            handleStyle={{ top: 2, bottom: 2 }}
        >
            <div className={styles.root}>
                <Text className={styles.header}>{title}</Text>
                {!items?.length && <Text className={styles.emptyText}>{emtpyText}</Text>}
                {items?.map(item => (
                    <div key={getId(item)} className={styles.incidentItem}>
                        <div className={styles.incidentDetails}>
                            <Text className={styles.incidentTitle}>{getTitle(item)}</Text>
                            <Text className={styles.incidentId}>{getId(item)}</Text>
                        </div>
                        <Button
                            className={styles.iconButton}
                            icon={<Dismiss12Regular />}
                            appearance="subtle"
                            onClick={() => onRemove(item)}
                        />
                    </div>
                ))}
            </div>
        </Resizable>
    );
};

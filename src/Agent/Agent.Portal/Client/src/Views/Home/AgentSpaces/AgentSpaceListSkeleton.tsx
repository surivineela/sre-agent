import { makeStyles, SkeletonItem, TableCellLayout } from '@fluentui/react-components';
import { FC } from 'react';

const useSkeletonStyles = makeStyles({
    row: {
        display: 'flex',
        gap: '12px',
        padding: '8px 0',
        alignItems: 'center',
    },
    iconSkeleton: {
        width: '16px',
        height: '16px',
    },
    nameSkeleton: {
        width: '200px',
        flex: '1',
    },
    subscriptionSkeleton: {
        width: '250px',
        flex: '1',
    },
    resourceGroupSkeleton: {
        width: '150px',
        flex: '1',
    },
    regionSkeleton: {
        width: '100px',
        flex: '1',
    },
});

interface AgentSpaceListSkeletonProps {
    rowCount?: number;
}

export const AgentSpaceListSkeleton: FC<AgentSpaceListSkeletonProps> = ({ rowCount = 6 }) => {
    const styles = useSkeletonStyles();

    return (
        <>
            {[...Array(rowCount)].map((_, index) => (
                <div key={index} className={styles.row}>
                    <TableCellLayout media={<SkeletonItem size={16} className={styles.iconSkeleton} />}>
                        <SkeletonItem size={16} className={styles.nameSkeleton} />
                    </TableCellLayout>
                    <TableCellLayout>
                        <SkeletonItem size={16} className={styles.subscriptionSkeleton} />
                    </TableCellLayout>
                    <TableCellLayout>
                        <SkeletonItem size={16} className={styles.resourceGroupSkeleton} />
                    </TableCellLayout>
                    <TableCellLayout>
                        <SkeletonItem size={16} className={styles.regionSkeleton} />
                    </TableCellLayout>
                </div>
            ))}
        </>
    );
};

import { makeStyles, SkeletonItem } from '@fluentui/react-components';
import { FC } from 'react';

const useSkeletonStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
    },
    row: {
        display: 'flex',
        gap: '10px',
        marginBottom: '8px',
        padding: '8px',
    },
    checkboxSkeleton: {
        width: '30px',
    },
    nameSkeleton: {
        width: '300px',
        flex: '1',
    },
    subscriptionSkeleton: {
        width: '200px',
    },
    locationSkeleton: {
        width: '150px',
    },
});

export const ResourceGroupPickerSkeleton: FC = () => {
    const styles = useSkeletonStyles();

    return (
        <div className={styles.container}>
            {[...Array(5)].map((_, index) => (
                <div key={index} className={styles.row}>
                    <SkeletonItem size={16} className={styles.checkboxSkeleton} />
                    <SkeletonItem size={16} className={styles.nameSkeleton} />
                    <SkeletonItem size={16} className={styles.subscriptionSkeleton} />
                    <SkeletonItem size={16} className={styles.locationSkeleton} />
                </div>
            ))}
        </div>
    );
};

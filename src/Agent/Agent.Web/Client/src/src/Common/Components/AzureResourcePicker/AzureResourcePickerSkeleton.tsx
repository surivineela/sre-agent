import { makeStyles, SkeletonItem, tokens } from '@fluentui/react-components';
import { FC } from 'react';

const useSkeletonStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
    },
    row: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        marginBottom: tokens.spacingVerticalS,
        padding: tokens.spacingHorizontalS,
        alignItems: 'center',
    },
    checkboxSkeleton: {
        width: '24px',
    },
    nameSkeleton: {
        width: '350px',
        flex: '1',
    },
    roleSkeleton: {
        width: '140px',
    },
});

export const AzureResourcePickerSkeleton: FC = () => {
    const styles = useSkeletonStyles();

    return (
        <div className={styles.container}>
            {/* Render 8 skeleton rows */}
            {[...Array(8)].map((_, index) => (
                <div key={index} className={styles.row}>
                    <SkeletonItem size={16} className={styles.checkboxSkeleton} />
                    <SkeletonItem size={16} className={styles.nameSkeleton} />
                    <SkeletonItem size={16} className={styles.roleSkeleton} />
                </div>
            ))}
        </div>
    );
};

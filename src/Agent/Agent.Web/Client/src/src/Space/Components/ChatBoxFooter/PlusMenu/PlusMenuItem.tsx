import { MenuItem, Spinner } from '@fluentui/react-components';
import { Checkmark16Regular } from '@fluentui/react-icons';
import { FC, memo, ReactElement, useCallback } from 'react';
import { usePlusMenuStyles } from '../styles';

export interface PlusMenuItemProps {
    icon: ReactElement; // Use ReactElement instead of ReactNode to exclude booleans
    label: string;
    disabled?: boolean;
    // Toggle variant
    isChecked?: boolean;
    onToggle?: () => void;
    // Action variant
    onClick?: () => void;
    // Loading state
    isLoading?: boolean;
    loadingIcon?: ReactElement; // Custom loading icon (defaults to Spinner)
}

export const PlusMenuItem: FC<PlusMenuItemProps> = memo(
    ({ icon, label, disabled, isChecked, onToggle, onClick, isLoading, loadingIcon }) => {
        const styles = usePlusMenuStyles();

        const handleClick = useCallback(() => {
            if (onToggle) {
                onToggle();
            } else if (onClick) {
                onClick();
            }
        }, [onToggle, onClick]);

        const displayIcon = isLoading ? (loadingIcon ?? <Spinner size="tiny" />) : icon;

        const secondaryContentElement = isChecked ? <Checkmark16Regular className={styles.checkmarkIcon} /> : undefined;

        return (
            <MenuItem icon={displayIcon} onClick={handleClick} disabled={disabled || isLoading} secondaryContent={secondaryContentElement}>
                {label}
            </MenuItem>
        );
    }
);

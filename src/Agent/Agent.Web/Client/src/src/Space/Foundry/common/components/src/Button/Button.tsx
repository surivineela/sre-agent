import { mergeStyles } from '@fluentui/react';
import type { ButtonProps as FluentButtonProps } from '@fluentui/react-components';
import { Button as FluentButton, Spinner } from '@fluentui/react-components';
import type { JSX, MouseEvent as ReactMouseEvent } from 'react';
import { forwardRef, useCallback, useId } from 'react';
import { useButtonStyles } from './Button.Styles';

/**
 * The appearance of the button.
 * - `primary`: The primary button style. Corresponds to the Fluent UI primary button.
 * - `secondary`: The secondary button style. Corresponds to the Fluent UI secondary button.
 * - `subtle`: The subtle button style. Corresponds to the Fluent UI subtle button.
 * - `danger`: The danger button style. Looks like a primary button but with a red color.
 * - `danger-subtle`: The danger button style. Looks like a subtle button but with a red color.
 * - `unstyled`: An unstyled button. This is a plain button with no styling, used to plainly wrap a
 *               non-interactive component to make it clickable. No hover/click/disabled styles are
 *               provided, and icons are not rendered.
 */
export type ButtonAppearance = 'primary' | 'secondary' | 'subtle' | 'danger' | 'danger-subtle' | 'unstyled' | 'outline';

export type ButtonProps = Omit<JSX.IntrinsicElements['button'], 'type'> &
    Pick<FluentButtonProps, 'icon' | 'iconPosition' | 'size'> & {
        showLoadingSpinner?: boolean;
        type?: JSX.IntrinsicElements['button']['type'];
        appearance?: ButtonAppearance;
    };

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
    ({ onClick, type = 'button', showLoadingSpinner, icon, className, appearance = 'secondary', id, ...rest }, ref): JSX.Element => {
        const styles = useButtonStyles();

        const handleClick = useCallback(
            (event: ReactMouseEvent<HTMLButtonElement>) => {
                onClick?.(event);
            },
            [onClick]
        );

        const generatedId = useId();
        const buttonId = id ?? generatedId;

        if (appearance === 'unstyled') {
            return <button ref={ref} className={mergeStyles(styles.unstyled, className)} onClick={handleClick} type={type} {...rest} />;
        }

        const fluentAppearance = appearance === 'danger' ? 'primary' : appearance === 'danger-subtle' ? 'subtle' : appearance;

        return (
            <FluentButton
                {...rest}
                ref={ref}
                appearance={fluentAppearance}
                as="button"
                className={mergeStyles(
                    appearance === 'secondary' && styles.secondary,
                    appearance === 'danger' && styles.danger,
                    appearance === 'danger-subtle' && styles.dangerSubtle,
                    className
                )}
                icon={showLoadingSpinner ? <Spinner aria-labelledby={buttonId} size="tiny" /> : icon}
                id={buttonId}
                onClick={handleClick}
                shape="circular"
                type={type}
            />
        );
    }
);
Button.displayName = 'Button';

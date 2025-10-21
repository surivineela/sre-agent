import { makeStyles, tokens } from '@fluentui/react-components';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'row',
        gap: tokens.spacingHorizontalM,
    },
    option: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: tokens.spacingVerticalXS,
        cursor: 'pointer',
        border: 'none',
        background: 'transparent',
        padding: 0,
    },
    imageWrapper: {
        display: 'flex',
        borderRadius: tokens.borderRadiusXLarge,
    },
    image: {
        display: 'block',
    },
    label: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightRegular,
        color: tokens.colorNeutralForeground1,
    },
});

export interface ImageRadioOption<T extends string = string> {
    value: T;
    image: string;
    label: string;
    imageWidth?: string;
    imageHeight?: string;
}

interface ImageRadioGroupProps<T extends string = string> {
    options: ImageRadioOption<T>[];
    value: T;
    onChange: (value: T) => void;
    ariaLabel?: string;
    name?: string;
}

export const ImageRadioGroup = <T extends string = string>({ options, value, onChange, ariaLabel, name }: ImageRadioGroupProps<T>) => {
    const styles = useStyles();

    return (
        <div className={styles.container} role="radiogroup" aria-label={ariaLabel} data-name={name}>
            {options.map(option => {
                const isSelected = value === option.value;
                return (
                    <button
                        key={option.value}
                        type="button"
                        role="radio"
                        aria-checked={isSelected}
                        className={styles.option}
                        onClick={() => onChange(option.value)}
                    >
                        <div
                            className={styles.imageWrapper}
                            style={{
                                border: isSelected ? `3px solid ${tokens.colorBrandStroke1}` : undefined,
                                borderRadius: tokens.borderRadiusXLarge,
                                transition: 'border-color 0.2s ease',
                            }}
                        >
                            <img
                                src={option.image}
                                alt=""
                                className={styles.image}
                                style={{
                                    width: option.imageWidth || 'auto',
                                    height: option.imageHeight || 'auto',
                                }}
                            />
                        </div>
                        <span className={styles.label}>{option.label}</span>
                    </button>
                );
            })}
        </div>
    );
};

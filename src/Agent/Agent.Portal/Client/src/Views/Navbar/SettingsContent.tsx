import { Combobox, Label, makeStyles, Option, tokens } from '@fluentui/react-components';
import { useIntl } from 'react-intl';
import { ImageRadioGroup, ImageRadioOption } from '../../Common/Components/ImageRadioGroup';
import { useUserPreferences } from '../../Common/Contexts/UserPreferencesContext';
import { PortalResources } from '../../Strings/Resources';

const useStyles = makeStyles({
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        marginBottom: tokens.spacingVerticalL,
    },
    sectionTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        marginBottom: tokens.spacingVerticalS,
    },
    combobox: {
        width: '100%',
    },
});

interface LanguageOption {
    value: string;
    label: string;
}

export const SettingsContent = () => {
    const intl = useIntl();
    const styles = useStyles();
    const { theme, locale, setTheme, setLocale } = useUserPreferences();

    const languageOptions: LanguageOption[] = [
        { value: 'en', label: 'English' },
        { value: 'cs', label: 'Čeština' },
        { value: 'de', label: 'Deutsch' },
        { value: 'es', label: 'Español' },
        { value: 'fr', label: 'Français' },
        { value: 'hu', label: 'Magyar' },
        { value: 'id', label: 'bahasa Indonesia' },
        { value: 'it', label: 'Italiano' },
        { value: 'ja', label: '日本語' },
        { value: 'ko', label: '한국어' },
        { value: 'nl', label: 'Nederlands' },
        { value: 'pl', label: 'Polski' },
        { value: 'pt-BR', label: 'Português (Brasil)' },
        { value: 'pt-PT', label: 'Português (Portugal)' },
        { value: 'ru', label: 'Русский' },
        { value: 'sv', label: 'Svenska' },
        { value: 'tr', label: 'Türkçe' },
        { value: 'zh-Hans', label: '中文(简体)' },
        { value: 'zh-Hant', label: '中文(繁體)' },
    ];

    const getSelectedLanguage = () => {
        const selected = languageOptions.find(opt => opt.value === locale || locale.startsWith(opt.value));
        return selected?.label || languageOptions[0].label;
    };

    const handleLanguageChange = (value: string) => {
        const option = languageOptions.find(opt => opt.label === value);
        if (option) {
            setLocale(option.value);
        }
    };

    const themeOptions: ImageRadioOption<'system' | 'light' | 'dark'>[] = [
        {
            value: 'system',
            image: '/SystemTheme.svg',
            label: intl.formatMessage(PortalResources.system),
            imageWidth: '79px',
            imageHeight: '44px',
        },
        {
            value: 'light',
            image: '/LightTheme.svg',
            label: intl.formatMessage(PortalResources.light),
            imageWidth: '79px',
            imageHeight: '44px',
        },
        {
            value: 'dark',
            image: '/DarkTheme.svg',
            label: intl.formatMessage(PortalResources.dark),
            imageWidth: '79px',
            imageHeight: '44px',
        },
    ];

    return (
        <div>
            <div className={styles.section}>
                <Label className={styles.sectionTitle}>{intl.formatMessage(PortalResources.themes)}</Label>
                <ImageRadioGroup
                    options={themeOptions}
                    value={theme}
                    onChange={setTheme}
                    ariaLabel={intl.formatMessage(PortalResources.themes)}
                />
            </div>

            <div className={styles.section}>
                <Label className={styles.sectionTitle}>{intl.formatMessage(PortalResources.language)}</Label>
                <Combobox
                    className={styles.combobox}
                    value={getSelectedLanguage()}
                    onOptionSelect={(_, data) => data.optionText && handleLanguageChange(data.optionText)}
                    aria-label={intl.formatMessage(PortalResources.language)}
                >
                    {languageOptions.map(option => (
                        <Option key={option.value} text={option.label}>
                            {option.label}
                        </Option>
                    ))}
                </Combobox>
            </div>
        </div>
    );
};

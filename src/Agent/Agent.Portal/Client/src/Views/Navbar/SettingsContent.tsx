import {
    Button,
    Combobox,
    Dropdown,
    Label,
    Link,
    makeStyles,
    Option,
    Popover,
    PopoverSurface,
    PopoverTrigger,
    tokens,
    Tooltip,
} from '@fluentui/react-components';
import { Settings32Regular } from '@fluentui/react-icons';
import { useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ImageRadioGroup, ImageRadioOption } from '../../Common/Components/ImageRadioGroup';
import { LearnMoreLinks } from '../../Common/Constants/Links';
import { useAuth } from '../../Common/Contexts/AuthContext';
import { useSubscriptions } from '../../Common/Contexts/SubscriptionsContext';
import { useUserPreferences } from '../../Common/Contexts/UserPreferencesContext';
import { PortalResources } from '../../Strings/Resources';

// Intentionally not localized so users can easily identify their language
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

const useStyles = makeStyles({
    popoverSurface: {
        minWidth: '320px',
        padding: tokens.spacingVerticalL,
    },
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
    footer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        marginTop: tokens.spacingVerticalL,
        paddingTop: tokens.spacingVerticalL,
        borderTopWidth: '1px',
        borderTopStyle: 'solid',
        borderTopColor: tokens.colorNeutralStroke2,
    },
    footerLinks: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalM,
        alignItems: 'center',
    },
    footerLink: {
        fontSize: tokens.fontSizeBase200,
    },
});

interface LanguageOption {
    value: string;
    label: string;
}

export const SettingsContent = () => {
    const intl = useIntl();
    const styles = useStyles();
    const { isAuthenticated } = useAuth();
    const { theme, locale, setTheme, setLocale } = useUserPreferences();
    const { subscriptions, selectedSubscriptions, setSelectedSubscriptions, isLoading: isLoadingSubscriptions } = useSubscriptions();

    const selectedLanguage = useMemo(() => {
        const selected = languageOptions.find(opt => opt.value === locale || locale.startsWith(opt.value));
        return selected?.label || languageOptions[0].label;
    }, [locale]);

    const handleLanguageChange = useCallback(
        (value: string) => {
            const option = languageOptions.find(opt => opt.label === value);
            if (option) {
                setLocale(option.value);
            }
        },
        [setLocale]
    );

    const subscriptionDisplayValue = useMemo(() => {
        const count = selectedSubscriptions.length;

        if (count === 0) return intl.formatMessage(PortalResources.allSubscriptions);
        if (count === 1) return selectedSubscriptions[0].displayName;

        return intl.formatMessage(PortalResources.subscriptionsSelected, { count });
    }, [selectedSubscriptions, intl]);

    const themeOptions = useMemo<ImageRadioOption<'system' | 'light' | 'dark'>[]>(
        () => [
            {
                value: 'system',
                image: 'SystemTheme.svg',
                label: intl.formatMessage(PortalResources.system),
                imageWidth: '79px',
                imageHeight: '44px',
            },
            {
                value: 'light',
                image: 'LightTheme.svg',
                label: intl.formatMessage(PortalResources.light),
                imageWidth: '79px',
                imageHeight: '44px',
            },
            {
                value: 'dark',
                image: 'DarkTheme.svg',
                label: intl.formatMessage(PortalResources.dark),
                imageWidth: '79px',
                imageHeight: '44px',
            },
        ],
        [intl]
    );

    return (
        <Popover>
            <PopoverTrigger>
                <Tooltip content={intl.formatMessage(PortalResources.settings)} relationship="label">
                    <Button
                        icon={<Settings32Regular />}
                        appearance="subtle"
                        disabled={!isAuthenticated}
                        aria-label={intl.formatMessage(PortalResources.settings)}
                    />
                </Tooltip>
            </PopoverTrigger>

            <PopoverSurface className={styles.popoverSurface}>
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
                            value={selectedLanguage}
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

                    {isAuthenticated && (
                        <div className={styles.section}>
                            <Label className={styles.sectionTitle}>
                                {intl.formatMessage(PortalResources.defaultSelectedSubscriptions)}
                            </Label>
                            <Dropdown
                                className={styles.combobox}
                                placeholder={intl.formatMessage(PortalResources.selectSubscriptions)}
                                selectedOptions={selectedSubscriptions.map(s => s.subscriptionId)}
                                onOptionSelect={(_, data) => {
                                    setSelectedSubscriptions(data.selectedOptions);
                                }}
                                value={subscriptionDisplayValue}
                                disabled={isLoadingSubscriptions}
                                aria-label={intl.formatMessage(PortalResources.defaultSelectedSubscriptions)}
                                multiselect
                            >
                                {subscriptions.map(sub => (
                                    <Option key={sub.subscriptionId} value={sub.subscriptionId}>
                                        {sub.displayName}
                                    </Option>
                                ))}
                            </Dropdown>
                        </div>
                    )}

                    <div className={styles.footer}>
                        <div className={styles.footerLinks}>
                            <Link
                                className={styles.footerLink}
                                href={LearnMoreLinks.privacyAndCookies}
                                target="_blank"
                                rel="noopener noreferrer"
                            >
                                {intl.formatMessage(PortalResources.privacyAndCookies)}
                            </Link>
                            <Link
                                className={styles.footerLink}
                                href={LearnMoreLinks.termsAndConditions}
                                target="_blank"
                                rel="noopener noreferrer"
                            >
                                {intl.formatMessage(PortalResources.termsAndConditions)}
                            </Link>
                            <Link className={styles.footerLink} href={LearnMoreLinks.trademarks} target="_blank" rel="noopener noreferrer">
                                {intl.formatMessage(PortalResources.trademarks)}
                            </Link>
                        </div>
                    </div>
                </div>
            </PopoverSurface>
        </Popover>
    );
};

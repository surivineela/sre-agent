const enum ThemeMode {
    Light = 0,
    Dark = 1
}

export interface ITheme {
    /**
     * Theme color code.
     */
    colorCode: string;
    /**
     * Theme sample image uri.
     */
    imageUri: string;
    /**
     * Theme name.
     */
    name: string;
    /**
     * Theme title.
     */
    title: string;
    /**
     * Theme mode.
     */
    mode?: ThemeMode;
}
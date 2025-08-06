import js from '@eslint/js';
import formatjs from 'eslint-plugin-formatjs';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
    { ignores: ['dist', 'node_modules', 'coverage'] },
    {
        extends: [js.configs.recommended, ...tseslint.configs.recommended],
        files: ['**/*.{ts,tsx}'],
        languageOptions: {
            ecmaVersion: 2020,
            globals: globals.browser,
        },
        plugins: {
            'react-hooks': reactHooks,
            'react-refresh': reactRefresh,
            formatjs: formatjs,
        },
        rules: {
            ...reactHooks.configs.recommended.rules,
            'react-refresh/only-export-components': 'off',
            '@typescript-eslint/no-unused-vars': 'off',
            '@typescript-eslint/no-explicit-any': 'off',
            'formatjs/enforce-id': [
                'error',
                {
                    idInterpolationPattern: '[sha512:contenthash:base64:6]',
                },
            ],
            'formatjs/enforce-placeholders': [
                'error',
                {
                    ignoreList: ['foo'],
                },
            ],
            'formatjs/enforce-default-message': ['error', 'literal'],
            'formatjs/no-multiple-whitespaces': [1],
            'formatjs/no-multiple-plurals': ['error'],
            'formatjs/no-complex-selectors': [
                'error',
                {
                    limit: 4,
                },
            ],
            'formatjs/no-invalid-icu': ['error'],
        },
    }
);

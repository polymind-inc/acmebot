import js from '@eslint/js';
import stylistic from '@stylistic/eslint-plugin';
import { defineConfig, globalIgnores } from 'eslint/config';
import vue from 'eslint-plugin-vue';
import globals from 'globals';
import tseslint from 'typescript-eslint';

const sourceFiles = ['**/*.{js,ts,vue}'];

export default defineConfig([
  globalIgnores(['dist/', '../wwwroot/dashboard-vnext/']),
  {
    name: 'acmebot-dashboard/linter-options',
    linterOptions: {
      reportUnusedDisableDirectives: 'error',
      reportUnusedInlineConfigs: 'error',
    },
  },
  {
    name: 'acmebot-dashboard/browser-source',
    files: sourceFiles,
    languageOptions: {
      ecmaVersion: 'latest',
      sourceType: 'module',
      globals: {
        ...globals.browser,
        __ACMEBOT_COMMIT_HASH__: 'readonly',
        __ACMEBOT_VERSION__: 'readonly',
      },
    },
  },
  {
    name: 'acmebot-dashboard/node-config',
    files: ['eslint.config.js', 'vite.config.ts'],
    languageOptions: {
      globals: globals.node,
    },
  },
  {
    name: 'acmebot-dashboard/recommended-rules',
    files: sourceFiles,
    extends: [
      js.configs.recommended,
      ...tseslint.configs.strict,
      ...tseslint.configs.stylistic,
      ...vue.configs['flat/recommended'],
      stylistic.configs.customize({
        indent: 2,
        quotes: 'single',
        semi: true,
        arrowParens: true,
        commaDangle: 'always-multiline',
        braceStyle: '1tbs',
        quoteProps: 'as-needed',
        jsx: false,
      }),
    ],
    rules: {
      '@stylistic/member-delimiter-style': [
        'error',
        {
          multiline: {
            delimiter: 'semi',
            requireLast: true,
          },
          singleline: {
            delimiter: 'semi',
            requireLast: false,
          },
        },
      ],
      '@stylistic/operator-linebreak': [
        'error',
        'after',
        {
          overrides: {
            '?': 'before',
            ':': 'before',
          },
        },
      ],
      '@typescript-eslint/no-unused-vars': [
        'error',
        {
          argsIgnorePattern: '^_',
          caughtErrorsIgnorePattern: '^_',
          destructuredArrayIgnorePattern: '^_',
          ignoreRestSiblings: true,
        },
      ],
      'vue/multi-word-component-names': [
        'error',
        {
          ignores: ['App'],
        },
      ],
    },
  },
  {
    name: 'acmebot-dashboard/vue-typescript-parser',
    files: ['**/*.vue'],
    languageOptions: {
      parserOptions: {
        parser: tseslint.parser,
      },
    },
  },
]);

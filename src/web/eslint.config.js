// @ts-check
import js from "@eslint/js";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import i18next from "eslint-plugin-i18next";
import globals from "globals";
import tseslint from "typescript-eslint";

export default tseslint.config(
  { ignores: ["dist", "coverage", "node_modules", "playwright-report", "test-results", "src/api"] },
  js.configs.recommended,
  ...tseslint.configs.strictTypeChecked,
  {
    languageOptions: {
      ecmaVersion: 2023,
      globals: { ...globals.browser },
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      "@typescript-eslint/no-explicit-any": "error",
      "@typescript-eslint/consistent-type-imports": "error",
      "@typescript-eslint/restrict-template-expressions": ["error", { allowNumber: true }],
    },
  },
  {
    files: ["src/**/*.{ts,tsx}"],
    plugins: { "react-hooks": reactHooks, "react-refresh": reactRefresh },
    rules: {
      ...reactHooks.configs.recommended.rules,
      "react-refresh/only-export-components": ["error", { allowConstantExport: true }],
    },
  },
  {
    // No hard-coded UI text in JSX: everything goes through i18n (CONTRIBUTING.md, invariant 8).
    files: ["src/**/*.tsx"],
    ignores: ["src/**/*.test.tsx", "src/test/**", "src/dev/**"],
    plugins: { i18next },
    rules: {
      "i18next/no-literal-string": [
        "error",
        {
          mode: "jsx-only",
          "jsx-attributes": {
            exclude: [
              "className",
              "style",
              "type",
              "key",
              "id",
              "htmlFor",
              "name",
              "value",
              "role",
              "lang",
              "href",
              "to",
              "rel",
              "target",
              "src",
              "autoComplete",
              "data-testid",
            ],
          },
        },
      ],
    },
  },
  {
    // Playwright: wait for a state, never for a duration (docs/testing.md §9.2).
    files: ["e2e/**/*.ts"],
    languageOptions: { globals: { ...globals.node } },
    rules: {
      "no-restricted-syntax": [
        "error",
        {
          selector: "CallExpression[callee.property.name='waitForTimeout']",
          message: "Use web-first assertions (expect(...).toBeVisible()) instead of fixed waits.",
        },
      ],
    },
  },
  {
    files: ["*.config.{js,ts}", "scripts/**/*.{js,mjs,ts}", "e2e/support/**/*.mjs"],
    languageOptions: { globals: { ...globals.node } },
  },
  {
    files: ["**/*.js", "**/*.mjs"],
    ...tseslint.configs.disableTypeChecked,
  },
);

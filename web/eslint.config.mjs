import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  // The app is the same code in every environment (ADR-0033): it neither asks
  // which one it is in nor carries a stand-in for anything. Stories and their
  // mocks aren't in the app.
  {
    files: ["src/**/*.{ts,tsx}", "next.config.ts"],
    ignores: ["**/*.stories.tsx", "**/__mocks__/**"],
    rules: {
      "no-restricted-imports": [
        "error",
        {
          patterns: [
            {
              regex: "(^|/)(msw|openapi-msw|mock-api|mocks)(/|$)",
              message: "Stand-ins are separate services, reached by address (ADR-0033).",
            },
          ],
        },
      ],
      "no-restricted-syntax": [
        "error",
        {
          selector: "ImportExpression[source.value=/(^|\\/)(msw|openapi-msw|mock-api|mocks)(\\/|$)/]",
          message: "Stand-ins are separate services, reached by address (ADR-0033).",
        },
        {
          selector:
            "MemberExpression[object.object.name='process'][object.property.name='env']:matches([property.name=/^(NODE_ENV|MOCK_API)$/], [property.value=/^(NODE_ENV|MOCK_API)$/])",
          message: "The app is the same in every environment (ADR-0033): configure what differs by its own setting.",
        },
        {
          selector:
            "VariableDeclarator[init.object.name='process'][init.property.name='env'] Property[key.name=/^(NODE_ENV|MOCK_API)$/]",
          message: "The app is the same in every environment (ADR-0033): configure what differs by its own setting.",
        },
      ],
    },
  },
  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    ".next/**",
    "out/**",
    "build/**",
    "next-env.d.ts",
    // Playwright output (generated, not source):
    "playwright-report/**",
    "test-results/**",
    // Storybook build output:
    "storybook-static/**",
  ]),
]);

export default eslintConfig;

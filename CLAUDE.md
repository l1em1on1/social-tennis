# Project: Tennis League and Social Games

Tennis League and Social Games — an application for organizing and planning league and social tennis games.

Easy to use mobile first web application to manage social leagues, gather match stats and to allow users join leagues, track scores, and find best suitable dates for the game.

- **License**: GPLv3
- **Repository**: `git@github.com:l1em1on1/social-tennis.git`

## Development Environment

- Language: TypeScript
- Framework: Next.js 16 (App Router)
- Component Library: Radix UI
- Styling: Tailwind CSS
- Testing: Jest + React Testing Library
- Linting: ESLint with @typescript-eslint
- Formatting: Prettier
- Mocking: Mock Service Worker - https://mswjs.io/

## Testing Practices

- Testing Library: @testing-library/react
- Mocking: msw, vi.mock()
- Organize tests in /tests or co-located with components

## Component Guidelines

- Style components with Tailwind utility classes
- Co-locate CSS modules or component-specific styling in the same directory

## Code Style Standards

- TypeScript strict mode, no `any` types
- Use named exports, not default exports
- Prefer arrow functions
- Annotate return types
- Always destructure props
- Avoid any type, use unknown or strict generics
- Group imports: react → next → libraries → local

## Project strucutre

- UI separate from logic
- data fetching services with abstraction to allow an easy use of mock service

## Security

- Validate all server-side inputs (API routes)
- Use HTTPS-only cookies and CSRF tokens when applicable
- Protect sensitive routes with middleware or session logic

## Additional Info

- @package.json for available npm commands.
- @docs/authentication.md for auth flow details
- @docs/data.md for data storage solution
- @docs/models.md for data structure
- @docs/features.md for summary of functionallity
- @docs/sitemap.md for pages list

### Custom Slash Commands

Stored in .claude/commands/:

- /generate-hook: Scaffold a React hook with proper types and test
- /wrap-client-component: Convert server to client-side with hydration-safe boundary
- /update-tailwind-theme: Modify Tailwind config and regenerate tokens
- /mock-react-query: Set up MSW mocking for all useQuery keys

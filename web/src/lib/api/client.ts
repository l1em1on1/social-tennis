import createClient from "openapi-fetch";
import type { paths } from "./schema";

// Server-side only. API_URL points at the api service inside the compose
// network; the browser never calls the .NET API directly (ADR-0001) — pages
// and route handlers proxy through this client instead.
export const api = createClient<paths>({ baseUrl: process.env.API_URL });

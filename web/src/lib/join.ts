/**
 * A Club join link's code (#222, ADR-0032): 22 base64url characters, as the
 * API makes them. A code from the URL or a form is checked before it goes
 * into a link, a redirect or an API path, so nothing else rides along (#53).
 */
const JOIN_CODE = /^[A-Za-z0-9_-]{22}$/;

export const isJoinCode = (value: unknown): value is string => typeof value === "string" && JOIN_CODE.test(value);

/** The join link's page, which every link a Manager shares opens. */
export const joinPath = (code: string) => `/join/${code}` as const;

/** Where a name form is, so a refused name is reported there: Home, Profiles, or a join link's page. */
export type NameFormPage = "/" | "/profiles" | `/join/${string}`;

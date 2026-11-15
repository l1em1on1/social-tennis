const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * True when a value is an id the API could have issued (a UUID). Ids taken
 * from form data or the URL go into API paths; anything else, such as `..`,
 * would change which API route is called (#53), so it is rejected first.
 */
export const isId = (value: unknown): value is string => typeof value === "string" && UUID.test(value);
